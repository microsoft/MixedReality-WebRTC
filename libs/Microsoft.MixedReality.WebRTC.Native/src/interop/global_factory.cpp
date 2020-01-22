// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// This is a precompiled header, it must be on its own, followed by a blank
// line, to prevent clang-format from reordering it with other headers.
#include "pch.h"

#include "interop/global_factory.h"
#include "media/local_video_track.h"
#include "peer_connection.h"

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
std::string ObjectToString(ObjectType type, TrackedObject* obj) {
  // rtc::StringBuilder doesn't support std::string_view, nor Append(). And
  // asbl::string_view is not constexpr-friendly on MSVC due to strlen().
  // rtc::SimpleStringBuilder supports Append(), but cannot dynamically resize.
  // Assume that the object name will not be too long, and use that one.
  char buffer[512];
  rtc::SimpleStringBuilder builder(buffer);
  builder << "(";
  std::string_view sv = ObjectTypeToString(type);
  builder.Append(sv.data(), sv.length());
  if (obj) {
    builder << ") " << obj->GetName();
  } else {
    builder << ") NULL";
  }
  return builder.str();
}

}  // namespace

namespace Microsoft::MixedReality::WebRTC {

inline mrsShutdownOptions operator|(mrsShutdownOptions a,
                                    mrsShutdownOptions b) noexcept {
  return (mrsShutdownOptions)((uint32_t)a | (uint32_t)b);
}

inline mrsShutdownOptions operator&(mrsShutdownOptions a,
                                    mrsShutdownOptions b) noexcept {
  return (mrsShutdownOptions)((uint32_t)a & (uint32_t)b);
}

inline bool operator==(mrsShutdownOptions a, uint32_t b) noexcept {
  return ((uint32_t)a == b);
}

inline bool operator!=(mrsShutdownOptions a, uint32_t b) noexcept {
  return ((uint32_t)a != b);
}

void GlobalFactory::ForceShutdown() noexcept {
  std::unique_ptr<GlobalFactory>& factory = MutableInstance(false);
  if (factory) {
    factory = nullptr;  // fixme: outside lock...
  }
}

RefPtr<GlobalFactory> GlobalFactory::InstancePtr() {
  auto& factory = MutableInstance();
  return RefPtr<GlobalFactory>(factory.get());
}

std::unique_ptr<GlobalFactory>& GlobalFactory::MutableInstance(
    bool createIfNotExist) {
  /// Global factory of all global objects, including the peer connection
  /// factory itself, with added thread safety. This keeps a track of all
  /// objects alive, to determine when it is safe to release the WebRTC threads,
  /// thereby allowing a DLL linking this code to be unloaded.
  static std::unique_ptr<GlobalFactory> g_factory;
  static std::mutex g_mutex;
  // Ensure (in a thread-safe way) the factory is initialized
  std::scoped_lock lock(g_mutex);
  if (createIfNotExist && !g_factory) {
    g_factory = std::make_unique<GlobalFactory>();
    mrsResult result = g_factory->InitializeImplNoLock();
    if (result != Result::kSuccess) {
      RTC_LOG(LS_ERROR) << "Failed to initialize global MixedReality-WebRTC "
                           "factory: error code #"
                        << (int)result;
      g_factory = nullptr;
    }
  }
  return g_factory;
}

GlobalFactory::~GlobalFactory() {
  ForceShutdownImpl();
}

rtc::scoped_refptr<webrtc::PeerConnectionFactoryInterface>
GlobalFactory::GetPeerConnectionFactory() noexcept {
  std::scoped_lock lock(mutex_);
  return factory_;
}

rtc::Thread* GlobalFactory::GetWorkerThread() noexcept {
  std::scoped_lock lock(mutex_);
#if defined(WINUWP)
  return impl_->workerThread.get();
#else   // defined(WINUWP)
  return worker_thread_.get();
#endif  // defined(WINUWP)
}

void GlobalFactory::AddObject(ObjectType type, TrackedObject* obj) noexcept {
  try {
    std::scoped_lock lock(mutex_);
    alive_objects_.emplace(obj, type);
  } catch (...) {
  }
}

void GlobalFactory::RemoveObject(ObjectType type, TrackedObject* obj) noexcept {
  try {
    std::scoped_lock lock(mutex_);
    auto it = alive_objects_.find(obj);
    if (it != alive_objects_.end()) {
      RTC_CHECK(it->second == type);
      alive_objects_.erase(it);
    }
  } catch (...) {
  }
}

bool GlobalFactory::TryShutdown() noexcept {
  std::unique_ptr<GlobalFactory>& factory = GlobalFactory::MutableInstance();
  if (!factory) {
    return false;  // not shutdown by this call
  }
  try {
    if (factory->TryShutdownImpl()) {
      factory = nullptr;  // fixme : outside lock...
      return true;
    }
  } catch (...) {
    // fall through...
  }
  return false;
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
  std::scoped_lock lock(mutex_);
  if (!impl_) {
    if (InitializeImplNoLock() != Result::kSuccess) {
      return nullptr;
    }
  }
  return impl_;
}

mrsResult GlobalFactory::GetOrCreateWebRtcFactory(WebRtcFactoryPtr& factory) {
  factory.reset();
  std::scoped_lock lock(mutex_);
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
  RTC_CHECK(!factory_);

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
    factoryConfig->audioRenderingEnabled = true;
    factoryConfig->enableAudioBufferEvents = false;
    impl_ = std::make_shared<wrapper::impl::org::webRtc::WebRtcFactory>();
    impl_->thisWeak_ = impl_;  // mimic wrapper_create()
    impl_->wrapper_init_org_webRtc_WebRtcFactory(factoryConfig);
  }
  impl_->internalSetup();

  // Cache the peer connection factory
  factory_ = impl_->peerConnectionFactory();
#else   // defined(WINUWP)
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

  factory_ = webrtc::CreatePeerConnectionFactory(
      network_thread_.get(), worker_thread_.get(), signaling_thread_.get(),
      nullptr, webrtc::CreateBuiltinAudioEncoderFactory(),
      webrtc::CreateBuiltinAudioDecoderFactory(),
      std::unique_ptr<webrtc::VideoEncoderFactory>(
          new webrtc::MultiplexEncoderFactory(
              absl::make_unique<webrtc::InternalEncoderFactory>())),
      std::unique_ptr<webrtc::VideoDecoderFactory>(
          new webrtc::MultiplexDecoderFactory(
              absl::make_unique<webrtc::InternalDecoderFactory>())),
      nullptr, nullptr);
#endif  // defined(WINUWP)
  return (factory_.get() != nullptr ? Result::kSuccess : Result::kUnknownError);
}

void GlobalFactory::ForceShutdownImpl() {
  std::scoped_lock lock(mutex_);
  if (!factory_) {
    return;
  }

  // WebRTC object destructors are also dispatched to the signaling thread,
  // like all method calls, but the threads are stopped by the GlobalFactory
  // shutdown, so dispatching will never complete.
  if (!alive_objects_.empty()) {
    RTC_LOG(LS_ERROR)
        << "Shutting down the global MixedReality-WebRTC factory while "
        << alive_objects_.size()
        << " objects are still alive. This will likely deadlock.";
    if ((shutdown_options_ & mrsShutdownOptions::kLogLiveObjects) !=
        (uint32_t)0) {
      ReportLiveObjectsNoLock();
    }
    if ((shutdown_options_ & mrsShutdownOptions::kFailOnLiveObjects) !=
        (uint32_t)0) {
      return;
    }
#if defined(MR_SHARING_WIN)
    DebugBreak();
#endif
  }

  // Shutdown
  factory_ = nullptr;
#if defined(WINUWP)
  impl_ = nullptr;
#else   // defined(WINUWP)
  network_thread_.reset();
  worker_thread_.reset();
  signaling_thread_.reset();
#endif  // defined(WINUWP)
}

bool GlobalFactory::TryShutdownImpl() {
  std::scoped_lock lock(mutex_);
  if (!factory_) {
    return false;
  }
  if (!alive_objects_.empty()) {
    return false;
  }
  factory_ = nullptr;
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
  for (auto&& pair : alive_objects_) {
    RTC_LOG(LS_INFO) << "[" << i << "] "
                     << ObjectToString(pair.second, pair.first) << " [~"
                     << pair.first->GetApproxRefCount() << " ref(s)]";
    ++i;
  }
}

}  // namespace Microsoft::MixedReality::WebRTC
