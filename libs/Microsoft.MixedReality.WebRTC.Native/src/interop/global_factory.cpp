// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// This is a precompiled header, it must be on its own, followed by a blank
// line, to prevent clang-format from reordering it with other headers.
#include "pch.h"

#include "interop/global_factory.h"
#include "local_video_track.h"
#include "peer_connection.h"

namespace {

using namespace Microsoft::MixedReality::WebRTC;

/// Global factory of all global objects, including the peer connection factory
/// itself, with added thread safety. This keeps a track of all objects alive,
/// to determine when it is safe to release the WebRTC threads, thereby allowing
/// a DLL linking this code to be unloaded.
std::unique_ptr<GlobalFactory> g_factory = std::make_unique<GlobalFactory>();

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

const std::unique_ptr<GlobalFactory>& GlobalFactory::Instance() {
  return g_factory;
}

GlobalFactory::~GlobalFactory() {
  std::scoped_lock lock(mutex_);
  if (!alive_objects_.empty()) {
    // WebRTC object destructors are also dispatched to the signaling thread,
    // like all method calls, but the threads are stopped by the GlobalFactory
    // shutdown, so dispatching will never complete.
    RTC_LOG(LS_ERROR) << "Shutting down the global factory while "
                      << alive_objects_.size()
                      << " objects are still alive. This will likely deadlock.";
    for (auto&& pair : alive_objects_) {
      RTC_LOG(LS_ERROR) << "- " << ObjectToString(pair.second, pair.first)
                        << " [" << pair.first->GetApproxRefCount()
                        << " ref(s)]";
    }
  }
  ShutdownNoLock();
}

rtc::scoped_refptr<webrtc::PeerConnectionFactoryInterface>
GlobalFactory::GetOrCreate() {
  std::scoped_lock lock(mutex_);
  if (!factory_) {
    if (Initialize() != Result::kSuccess) {
      return nullptr;
    }
  }
  return factory_;
}

mrsResult GlobalFactory::GetOrCreate(
    rtc::scoped_refptr<webrtc::PeerConnectionFactoryInterface>& factory) {
  factory = nullptr;
  std::scoped_lock lock(mutex_);
  if (!factory_) {
    mrsResult res = Initialize();
    if (res != Result::kSuccess) {
      return res;
    }
  }
  factory = factory_;
  return (factory ? Result::kSuccess : Result::kUnknownError);
}

rtc::scoped_refptr<webrtc::PeerConnectionFactoryInterface>
GlobalFactory::GetExisting() noexcept {
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
      if (alive_objects_.empty()) {
        ShutdownNoLock();
      }
    }
  } catch (...) {
  }
}

#if defined(WINUWP)

using WebRtcFactoryPtr =
    std::shared_ptr<wrapper::impl::org::webRtc::WebRtcFactory>;

WebRtcFactoryPtr GlobalFactory::get() {
  std::scoped_lock lock(mutex_);
  if (!impl_) {
    if (Initialize() != Result::kSuccess) {
      return nullptr;
    }
  }
  return impl_;
}

mrsResult GlobalFactory::GetOrCreateWebRtcFactory(WebRtcFactoryPtr& factory) {
  factory.reset();
  std::scoped_lock lock(mutex_);
  if (!impl_) {
    mrsResult res = Initialize();
    if (res != Result::kSuccess) {
      return res;
    }
  }
  factory = impl_;
  return (factory ? Result::kSuccess : Result::kUnknownError);
}

#endif  // defined(WINUWP)

mrsResult GlobalFactory::Initialize() {
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

void GlobalFactory::ShutdownNoLock() {
  factory_ = nullptr;
#if defined(WINUWP)
  impl_ = nullptr;
#else   // defined(WINUWP)
  network_thread_.reset();
  worker_thread_.reset();
  signaling_thread_.reset();
#endif  // defined(WINUWP)
}

}  // namespace Microsoft::MixedReality::WebRTC
