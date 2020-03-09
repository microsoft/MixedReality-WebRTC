// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// This is a precompiled header, it must be on its own, followed by a blank
// line, to prevent clang-format from reordering it with other headers.
#include "pch.h"

#include "interop/global_factory.h"
#include "media/local_video_track.h"
#include "peer_connection.h"
#include "rtc_base/refcountedobject.h"
#include "utils.h"

#include <exception>

namespace {

using namespace Microsoft::MixedReality::WebRTC;

/// Utility to convert an ObjectType to a string, for debugging purpose. This
/// returns a view over a global constant buffer (static storage), which is
/// always valid, never deallocated.
std::string_view ObjectTypeToString(ObjectType type) {
  static_assert((int)ObjectType::kPeerConnection == 0, "");
  static_assert((int)ObjectType::kLocalVideoTrack == 1, "");
  static_assert((int)ObjectType::kExternalVideoTrackSource == 2, "");
  constexpr const std::string_view s_types[] = {
      "PeerConnection", "LocalVideoTrack", "ExternalVideoTrackSource"};
  return s_types[(int)type];
}

/// Utility to format a tracked object into a string, for debugging purpose.
std::string ObjectToString(TrackedObject* obj) {
  // rtc::StringBuilder doesn't support std::string_view, nor Append(). And
  // asbl::string_view is not constexpr-friendly on MSVC due to strlen().
  // rtc::SimpleStringBuilder supports Append(), but cannot dynamically resize.
  // Assume that the object name will not be too long, and use that one.
  char buffer[512];
  rtc::SimpleStringBuilder builder(buffer);
  if (obj) {
    builder << "(";
    std::string_view sv = ObjectTypeToString(obj->GetObjectType());
    builder.Append(sv.data(), sv.length());
    builder << ") " << obj->GetName();
  } else {
    builder << "NULL";
  }
  return builder.str();
}

}  // namespace

namespace Microsoft::MixedReality::WebRTC {

uint32_t GlobalFactory::StaticReportLiveObjects() noexcept {
  // Lock the instance to prevent shutdown if it already exists, while
  // enumerating live objects.
  RefPtr<GlobalFactory> factory(InstancePtrIfExist());
  if (factory) {
    return factory->ReportLiveObjects();
  }
  return 0;
}

mrsShutdownOptions GlobalFactory::GetShutdownOptions() noexcept {
  GlobalFactory* const factory = GetInstance();
  std::scoped_lock lock(factory->mutex_);
  return factory->shutdown_options_;
}

void GlobalFactory::SetShutdownOptions(mrsShutdownOptions options) noexcept {
  // Unconditionally set shutdown options whether or not the instance is
  // initialized. So no need to acquire the init lock.
  GlobalFactory* const factory = GetInstance();
  // However, acquire the thread-safety mutex protecting concurrent accesses.
  std::scoped_lock lock(factory->mutex_);
  factory->shutdown_options_ = options;
}

void GlobalFactory::ForceShutdown() noexcept {
  GlobalFactory* const factory = GetInstance();
  std::scoped_lock lock(factory->init_mutex_);
  if (!factory->peer_factory_) {
    return;
  }
  try {
    factory->ShutdownImplNoLock(ShutdownAction::kForceShutdown);
  } catch (std::exception& e) {
    RTC_LOG(LS_ERROR) << "Failed to shutdown library with exception: "
                      << e.what();
  } catch (...) {
    RTC_LOG(LS_ERROR) << "Failed to shutdown library due to unknown exception.";
  }
}

bool GlobalFactory::TryShutdown() noexcept {
  GlobalFactory* const factory = GetInstance();
  std::scoped_lock lock(factory->init_mutex_);
  if (!factory->peer_factory_) {
    return true;
  }
  try {
    return factory->ShutdownImplNoLock(ShutdownAction::kTryShutdownIfSafe);
  } catch (std::exception& e) {
    RTC_LOG(LS_ERROR)
        << "Failed to attempt to shutdown library with exception: " << e.what();
  } catch (...) {
    RTC_LOG(LS_ERROR)
        << "Failed to attempt to shutdown library due to unknown exception.";
  }
  return false;  // failed to shutdown
}

GlobalFactory* GlobalFactory::GetInstance() {
  // Use C++11 thread-safety guarantee to ensure a single instance is created.
  // It will be destroyed automatically on module unload. The "library
  // initialized" concept refers to this instance being initialized or not.
  static std::unique_ptr<GlobalFactory> s_factory(new GlobalFactory());
  return s_factory.get();
}

RefPtr<GlobalFactory> GlobalFactory::GetInstancePtrImpl(
    bool ensureInitialized) {
  GlobalFactory* const factory = GetInstance();
  std::scoped_lock lock(factory->init_mutex_);
  if (factory->peer_factory_) {
    RefPtr<GlobalFactory> ptr(factory);
    return ptr;  // moved
  }
  if (!ensureInitialized) {
    return nullptr;
  }
  mrsResult result = factory->InitializeImplNoLock();
  if (result != Result::kSuccess) {
    RTC_LOG(LS_ERROR) << "Failed to initialize global MixedReality-WebRTC "
                         "factory: error code #"
                      << (int)result;
    return nullptr;
  }
  RefPtr<GlobalFactory> ptr(factory);
  return ptr;  // moved
}

GlobalFactory::~GlobalFactory() {
  std::scoped_lock lock(init_mutex_);
  ShutdownImplNoLock(ShutdownAction::kFromObjectDestructor);
}

rtc::scoped_refptr<webrtc::PeerConnectionFactoryInterface>
GlobalFactory::GetPeerConnectionFactory() noexcept {
  // This only requires init_mutex_ read lock, which must be acquired to access
  // the singleton instance.
  return peer_factory_;
}

rtc::Thread* GlobalFactory::GetWorkerThread() const noexcept {
  // This only requires init_mutex_ read lock, which must be acquired to access
  // the singleton instance.
#if defined(WINUWP)
  return impl_->workerThread.get();
#else   // defined(WINUWP)
  return worker_thread_.get();
#endif  // defined(WINUWP)
}

void GlobalFactory::AddObject(TrackedObject* obj) noexcept {
  try {
    std::scoped_lock lock(mutex_);
    RTC_DCHECK(std::find(alive_objects_.begin(), alive_objects_.end(), obj) ==
               alive_objects_.end());
    alive_objects_.push_back(obj);
  } catch (...) {
  }
}

void GlobalFactory::RemoveObject(TrackedObject* obj) noexcept {
  try {
    std::scoped_lock lock(mutex_);
    auto it = std::find(alive_objects_.begin(), alive_objects_.end(), obj);
    if (it != alive_objects_.end()) {
      alive_objects_.erase(it);
    }
  } catch (...) {
  }
}

uint32_t GlobalFactory::ReportLiveObjects() {
  std::scoped_lock lock(mutex_);
  ReportLiveObjectsNoLock();
  return static_cast<uint32_t>(alive_objects_.size());
}

#if defined(WINUWP)

using WebRtcFactoryPtr =
    std::shared_ptr<wrapper::impl::org::webRtc::WebRtcFactory>;

WebRtcFactoryPtr GlobalFactory::get() {
  std::scoped_lock lock(init_mutex_);
  if (!impl_) {
    if (InitializeImplNoLock() != Result::kSuccess) {
      return nullptr;
    }
  }
  return impl_;
}

mrsResult GlobalFactory::GetOrCreateWebRtcFactory(WebRtcFactoryPtr& factory) {
  factory.reset();
  std::scoped_lock lock(init_mutex_);
  if (!impl_) {
    mrsResult res = InitializeImplNoLock();
    if (res != Result::kSuccess) {
      return res;
    }
  }
  factory = impl_;
  return (factory ? Result::kSuccess : Result::kUnknownError);
}

#endif  // defined(WINUWP)

mrsResult GlobalFactory::InitializeImplNoLock() {
  if (peer_factory_) {
    return Result::kSuccess;
  }

#if defined(WINUWP)
  RTC_CHECK(!impl_);
  auto mw = winrt::Windows::ApplicationModel::Core::CoreApplication::MainView();
  auto cw = mw.CoreWindow();
  auto dispatcher = cw.Dispatcher();
  if (dispatcher.HasThreadAccess()) {
    // WebRtcFactory::setup() will deadlock if called from main UI thread
    // See https://github.com/webrtc-uwp/webrtc-uwp-sdk/issues/143
    return Result::kWrongThread;
  }
  auto dispatcherQueue =
      wrapper::impl::org::webRtc::EventQueue::toWrapper(dispatcher);

  // Setup the WebRTC library
  {
    auto libConfig =
        std::make_shared<wrapper::impl::org::webRtc::WebRtcLibConfiguration>();
    libConfig->thisWeak_ = libConfig;  // mimic wrapper_create()
    libConfig->queue = dispatcherQueue;
    wrapper::impl::org::webRtc::WebRtcLib::setup(libConfig);
  }

  // Create the UWP factory
  {
    auto factoryConfig = std::make_shared<
        wrapper::impl::org::webRtc::WebRtcFactoryConfiguration>();
    factoryConfig->thisWeak_ = factoryConfig;  // mimic wrapper_create()
    factoryConfig->audioCapturingEnabled = true;
    factoryConfig->audioRenderingEnabled = true; //TODO change to runtime switch
    factoryConfig->enableAudioBufferEvents = false;
    impl_ = std::make_shared<wrapper::impl::org::webRtc::WebRtcFactory>();
    impl_->thisWeak_ = impl_;  // mimic wrapper_create()
    impl_->wrapper_init_org_webRtc_WebRtcFactory(factoryConfig);
  }
  impl_->internalSetup();

  // Cache the peer connection factory
  peer_factory_ = impl_->peerConnectionFactory();
#else  // defined(WINUWP)
  network_thread_ = rtc::Thread::CreateWithSocketServer();
  RTC_CHECK(network_thread_.get());
  network_thread_->SetName("WebRTC network thread", network_thread_.get());
  network_thread_->Start();
  worker_thread_ = rtc::Thread::Create();
  RTC_CHECK(worker_thread_.get());
  worker_thread_->SetName("WebRTC worker thread", worker_thread_.get());
  worker_thread_->Start();
  signaling_thread_ = rtc::Thread::Create();
  RTC_CHECK(signaling_thread_.get());
  signaling_thread_->SetName("WebRTC signaling thread",
                             signaling_thread_.get());
  signaling_thread_->Start();

#if 1
  static const int16_t zerobuf[200];

  // UWP has factoryConfig->audioRenderingEnabled but non-UWP doesn't have that flag.
  // The mixer has a dual role 1) to pull audio from the network 2) send it to the platforms
  // audio device. Without a mixer to pull the audio, there will be no audio frame received callbacks.
  // TODO: we have the opportunity here to select certain sources for mixing and others
  // for callbacks (e.g. spatial audio). Provide an API to manage this.
  struct PumpSourcesAndDiscardMixer : webrtc::AudioMixer {
    rtc::CriticalSection crit_;
    std::vector<Source*> audio_source_list_;

    bool AddSource(Source* audio_source) override {
      RTC_DCHECK(audio_source);
      rtc::CritScope lock(&crit_);
      RTC_DCHECK(find(audio_source_list_.begin(), audio_source_list_.end(),
                      audio_source) == audio_source_list_.end())
          << "Source already added to mixer";
      audio_source_list_.emplace_back(audio_source);
      return true;
    }
    void RemoveSource(Source* audio_source) override {
      RTC_DCHECK(audio_source);
      rtc::CritScope lock(&crit_);
      const auto iter = find(audio_source_list_.begin(),
                             audio_source_list_.end(), audio_source);
      RTC_DCHECK(iter != audio_source_list_.end())
          << "Source not present in mixer";
      audio_source_list_.erase(iter);
    }
    void Mix(size_t number_of_channels,
             webrtc::AudioFrame* audio_frame_for_mixing) override {
      for (auto& source : audio_source_list_) {
        // This pumps the source and fires the frame observer callbacks
        // which in turn fill the AudioReadStream buffers
        const auto audio_frame_info = source->GetAudioFrameWithInfo(
            source->PreferredSampleRate(), audio_frame_for_mixing);

        if (audio_frame_info == Source::AudioFrameInfo::kError) {
          RTC_LOG_F(LS_WARNING)
              << "failed to GetAudioFrameWithInfo() from source";
          continue;
        }
      }
      // We don't actually want these tracks to add to the mix.
      // So we return an empty frame.
      // TODO: it would be nice for tracks which are connected to a spatial
      // audio source to be intercepted earlier. Currently toggling between
      // local audio rendering and spatial audio is a global switch (not per
      // track nor connection).
      audio_frame_for_mixing->UpdateFrame(
          0, zerobuf, 80, 8000, webrtc::AudioFrame::kNormalSpeech,
          webrtc::AudioFrame::kVadUnknown, number_of_channels);
    }
  };

  auto mixer = new rtc::RefCountedObject<PumpSourcesAndDiscardMixer>();
#else
  auto mixer = (webrtc::AudioMixer*)nullptr;
#endif

  peer_factory_ = webrtc::CreatePeerConnectionFactory(
      network_thread_.get(), worker_thread_.get(), signaling_thread_.get(),
      nullptr, webrtc::CreateBuiltinAudioEncoderFactory(),
      webrtc::CreateBuiltinAudioDecoderFactory(),
      std::unique_ptr<webrtc::VideoEncoderFactory>(
          new webrtc::MultiplexEncoderFactory(
              absl::make_unique<webrtc::InternalEncoderFactory>())),
      std::unique_ptr<webrtc::VideoDecoderFactory>(
          new webrtc::MultiplexDecoderFactory(
              absl::make_unique<webrtc::InternalDecoderFactory>())),
      mixer, nullptr);
#endif  // defined(WINUWP)
  return (peer_factory_.get() != nullptr ? Result::kSuccess
                                         : Result::kUnknownError);
}

bool GlobalFactory::ShutdownImplNoLock(ShutdownAction shutdown_action) {
  if (!peer_factory_) {
    return true;  // already shut down
  }

  // This is read under the init mutex lock so can be relaxed, as it cannot
  // decrease during that time. However we should test the value before it's
  // cleared below, so use acquire semantic.
  const int num_refs = ref_count_.load(std::memory_order_acquire);
  if (num_refs > 0) {
    if (shutdown_action == ShutdownAction::kTryShutdownIfSafe) {
      return false;  // cannot shut down safely, staying initialized
    }
    bool fromDtor = (shutdown_action == ShutdownAction::kFromObjectDestructor);
    RTC_LOG(LS_ERROR)
        << "Force-shutting down the global MixedReality-WebRTC "
           "factory while it still has "
        << num_refs << " references."
        << (fromDtor ? " This will likely deadlock when dispatching the peer "
                       "connection factory destructor to the signaling thread."
                     : "");
    if ((shutdown_options_ & mrsShutdownOptions::kLogLiveObjects) != 0) {
      ReportLiveObjectsNoLock();
    }
    if ((shutdown_options_ & mrsShutdownOptions::kDebugBreakOnForceShutdown) != 0) {
#if defined(MR_SHARING_WIN)
      DebugBreak();
#endif
    }

    // Clear debug infos and references. This leaks objects, but at least won't
    // interact with future uses.
    alive_objects_.clear();
    ref_count_.store(0, std::memory_order_release);  // see "load acquire" above
  }

  // Shutdown
  peer_factory_ = nullptr;
#if defined(WINUWP)
  impl_ = nullptr;
#else   // defined(WINUWP)
  network_thread_.reset();
  worker_thread_.reset();
  signaling_thread_.reset();
#endif  // defined(WINUWP)
  return true;
}

void GlobalFactory::ReportLiveObjectsNoLock() {
  RTC_LOG(LS_INFO) << "mr-webrtc alive objects report for "
                   << alive_objects_.size() << " objects:";
  int i = 0;
  for (auto&& obj : alive_objects_) {
    RTC_LOG(LS_INFO) << "[" << i << "] " << ObjectToString(obj) << " [~"
                     << obj->GetApproxRefCount() << " ref(s)]";
    ++i;
  }
}

}  // namespace Microsoft::MixedReality::WebRTC
