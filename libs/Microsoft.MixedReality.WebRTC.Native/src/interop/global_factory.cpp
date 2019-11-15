// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// This is a precompiled header, it must be on its own, followed by a blank
// line, to prevent clang-format from reordering it with other headers.
#include "pch.h"

#include "interop/global_factory.h"

namespace {

using namespace Microsoft::MixedReality::WebRTC;

/// Global factory of all global objects, including the peer connection factory
/// itself, with added thread safety.
std::unique_ptr<GlobalFactory> g_factory = std::make_unique<GlobalFactory>();

}  // namespace

namespace Microsoft::MixedReality::WebRTC {

const std::unique_ptr<GlobalFactory>& GlobalFactory::Instance() {
  return g_factory;
}

GlobalFactory::~GlobalFactory() {
  std::scoped_lock lock(mutex_);
  if (!alive_objects_.empty() || !peer_connection_map_.empty()) {
    // WebRTC object destructors are also dispatched to the signaling thread,
    // but the threads are stopped by the shutdown, so dispatching will never
    // complete.
    RTC_LOG(LS_ERROR) << "Shutting down the global factory while some objects "
                         "are alive. This will likely deadlock.";
  }
  ShutdownNoLock();
}

rtc::scoped_refptr<webrtc::PeerConnectionFactoryInterface>
GlobalFactory::GetOrCreate() {
  std::scoped_lock lock(mutex_);
  if (!factory_) {
    if (Initialize() != MRS_SUCCESS) {
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
    if (res != MRS_SUCCESS) {
      return res;
    }
  }
  factory = factory_;
  return (factory ? MRS_SUCCESS : MRS_E_UNKNOWN);
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

PeerConnectionHandle GlobalFactory::AddPeerConnection(
    rtc::scoped_refptr<PeerConnection> peer) {
  RTC_CHECK(peer);
  const PeerConnectionHandle handle{peer.get()};
  {
    std::scoped_lock lock(mutex_);
    peer_connection_map_.emplace(handle, std::move(peer));
  }
  return handle;
}

void GlobalFactory::RemovePeerConnection(PeerConnectionHandle* handle) {
  if (auto peer = static_cast<PeerConnection*>(*handle)) {
    std::scoped_lock lock(mutex_);
    auto it = peer_connection_map_.find(*handle);
    if (it == peer_connection_map_.end()) {
      RTC_LOG(LS_WARNING) << "Trying to remove unknown PeerConnection object "
                             "from global map has no effect.";
      return;
    }
    peer_connection_map_.erase(it);
    *handle = nullptr;
  }
}

void GlobalFactory::NotifyPeerConnectionDestroyed() {
  CheckForShutdown();
}

void GlobalFactory::AddObject(void* ptr) {
  std::scoped_lock lock(mutex_);
  alive_objects_.insert(ptr);
}

void GlobalFactory::RemoveObject(void* ptr) {
  std::scoped_lock lock(mutex_);
  alive_objects_.erase(ptr);
  CheckForShutdown();
}

#if defined(WINUWP)

using WebRtcFactoryPtr =
    std::shared_ptr<wrapper::impl::org::webRtc::WebRtcFactory>;

WebRtcFactoryPtr GlobalFactory::get() {
  std::scoped_lock lock(mutex_);
  if (!impl_) {
    if (Initialize() != MRS_SUCCESS) {
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
    if (res != MRS_SUCCESS) {
      return res;
    }
  }
  factory = impl_;
  return (factory ? MRS_SUCCESS : MRS_E_UNKNOWN);
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
    return MRS_E_WRONG_THREAD;
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
  return (factory_.get() != nullptr ? MRS_SUCCESS : MRS_E_UNKNOWN);
}

void GlobalFactory::CheckForShutdown() {
  std::scoped_lock lock(mutex_);
  if (alive_objects_.empty() && peer_connection_map_.empty()) {
    ShutdownNoLock();
  }
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
