// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

// This is a precompiled header, it must be on its own, followed by a blank
// line, to prevent clang-format from reordering it with other headers.
#include "pch.h"

#include "api.h"
#include "peer_connection.h"

using namespace mrtk::net::webrtc_impl;

struct mrsEnumerator {
  virtual ~mrsEnumerator() = default;
  virtual void dispose() = 0;
};

namespace {

std::unique_ptr<cricket::VideoCapturer> OpenVideoCaptureDevice(
    const char* video_device_id, bool enable_mrc = false) noexcept(kNoExceptFalseOnUWP) {
#if defined(WINUWP)
  // Check for calls from main UI thread; this is not supported (will deadlock)
  auto mw = winrt::Windows::ApplicationModel::Core::CoreApplication::MainView();
  auto cw = mw.CoreWindow();
  auto dispatcher = cw.Dispatcher();
  if (dispatcher.HasThreadAccess()) {
    throw winrt::hresult_wrong_thread(
        winrt::to_hstring(L"Cannot open the WebRTC video capture device from "
                          L"the UI thread on UWP."));
  }

  // Get devices synchronously (wait for UI thread to retrieve them for us)
  rtc::Event blockOnDevicesEvent(true, false);
  auto vci = wrapper::impl::org::webRtc::VideoCapturer::getDevices();
  vci->thenClosure([&blockOnDevicesEvent] { blockOnDevicesEvent.Set(); });
  blockOnDevicesEvent.Wait(rtc::Event::kForever);
  auto deviceList = vci->value();

  std::wstring video_device_id_str;
  if ((video_device_id != nullptr) && (video_device_id[0] != '\0')) {
    video_device_id_str =
        rtc::ToUtf16(video_device_id, strlen(video_device_id));
  }

  for (auto&& vdi : *deviceList) {
    auto devInfo =
        wrapper::impl::org::webRtc::VideoDeviceInfo::toNative_winrt(vdi);
    auto name = devInfo.Name().c_str();
    if (!video_device_id_str.empty() && (video_device_id_str != name)) {
      continue;
    }
    auto id = devInfo.Id().c_str();

    auto vcd =
        wrapper::impl::org::webRtc::VideoCapturer::create(name, id, enable_mrc);

    if (vcd != nullptr) {
      auto nativeVcd = wrapper::impl::org::webRtc::VideoCapturer::toNative(vcd);

      RTC_LOG(LS_INFO) << "Using video capture device '" << rtc::ToUtf8(name).c_str()
                       << "' (id=" << rtc::ToUtf8(id).c_str() << ")";

	  //// HACK - Force max size to prevent high-res HoloLens 2 camera, which also disables MRC
	  //// https://docs.microsoft.com/en-us/windows/mixed-reality/mixed-reality-capture-for-developers#enabling-mrc-in-your-app
	  //// "MRC on HoloLens 2 supports videos up to 1080p and photos up to 4K resolution"
	  //cricket::VideoFormat max_format{};
   //   max_format.width = 1920;
   //   max_format.height = 1080;
   //   max_format.interval = cricket::VideoFormat::FpsToInterval(30);
   //   nativeVcd->set_enable_camera_list(true); //< needed to enable filtering
	  //nativeVcd->ConstrainSupportedFormats(max_format);

      if (auto supportedFormats = nativeVcd->GetSupportedFormats()) {
        RTC_LOG(LS_INFO) << "Supported video formats:";
        for (auto&& format : *supportedFormats) {
          auto str = format.ToString();
          RTC_LOG(LS_INFO) << "- " << str.c_str();
        }
      }

      return nativeVcd;
    }
  }
  return nullptr;
#else
  std::vector<std::string> device_names;
  {
    std::unique_ptr<webrtc::VideoCaptureModule::DeviceInfo> info(
        webrtc::VideoCaptureFactory::CreateDeviceInfo());
    if (!info) {
      return nullptr;
    }

    std::string video_device_id_str;
    if ((video_device_id != nullptr) && (video_device_id[0] != '\0')) {
      video_device_id_str.assign(video_device_id);
    }

    int num_devices = info->NumberOfDevices();
    for (int i = 0; i < num_devices; ++i) {
      constexpr uint32_t kSize = 256;
      char name[kSize] = {0};
      char id[kSize] = {0};
      if (info->GetDeviceName(i, name, kSize, id, kSize) != -1) {
        device_names.push_back(name);
        if (video_device_id_str == name) {
          break;
        }
      }
    }
  }

  cricket::WebRtcVideoDeviceCapturerFactory factory;
  std::unique_ptr<cricket::VideoCapturer> capturer;
  for (const auto& name : device_names) {
    capturer = factory.Create(cricket::Device(name, 0));
    if (capturer) {
      break;
    }
  }
  return capturer;
#endif
}

/// Global factory for PeerConnection objects.
rtc::scoped_refptr<webrtc::PeerConnectionFactoryInterface>
    g_peer_connection_factory;

#if defined(WINUWP)

/// Winuwp relies on a global-scoped factory wrapper
std::unique_ptr<wrapper::impl::org::webRtc::WebRtcFactory> g_winuwp_factory;

#else

/// WebRTC worker thread.
std::unique_ptr<rtc::Thread> g_worker_thread;

/// WebRTC signaling thread.
std::unique_ptr<rtc::Thread> g_signaling_thread;

#endif

/// Collection of all peer connection objects alive.
std::unordered_map<PeerConnectionHandle,
                   rtc::scoped_refptr<mrtk::net::webrtc_impl::PeerConnection>>
    g_peer_connection_map;

/// Predefined name of the local video track.
const std::string kLocalVideoLabel("local_video");

/// Predefined name of the local audio track.
const std::string kLocalAudioLabel("local_audio");

}  // namespace

#if defined(WINUWP)
rtc::Thread* UnsafeGetWorkerThread() {
  if (auto* ptr = g_winuwp_factory.get()) {
    return ptr->workerThread.get();
  }
  return nullptr;
}
#endif

void mrsCloseEnum(mrsEnumHandle* handleRef) noexcept {
  if (handleRef) {
    if (auto& handle = *handleRef) {
      handle->dispose();
      delete handle;
      handle = nullptr;
    }
  }
}

void mrsEnumVideoCaptureDevicesAsync(
    mrsVideoCaptureDeviceEnumCallback callback,
    void* userData,
    mrsVideoCaptureDeviceEnumCompletedCallback completedCallback,
    void* completedCallbackUserData) noexcept {
  if (!callback) {
    return;
  }
#if defined(WINUWP)
  auto vci = wrapper::impl::org::webRtc::VideoCapturer::getDevices();
  vci->thenClosure(
      [vci, callback, completedCallback, userData, completedCallbackUserData] {
        auto deviceList = vci->value();
        for (auto&& vdi : *deviceList) {
          auto devInfo =
              wrapper::impl::org::webRtc::VideoDeviceInfo::toNative_winrt(vdi);
          auto id = winrt::to_string(devInfo.Id());
          id.push_back('\0');  // API must ensure null-terminated
          auto name = winrt::to_string(devInfo.Name());
          name.push_back('\0');  // API must ensure null-terminated
          (*callback)(id.c_str(), name.c_str(), userData);
        }
        if (completedCallback) {
          (*completedCallback)(completedCallbackUserData);
        }
      });
#else
  std::unique_ptr<webrtc::VideoCaptureModule::DeviceInfo> info(
      webrtc::VideoCaptureFactory::CreateDeviceInfo());
  if (!info) {
    if (completedCallback) {
      (*completedCallback)(completedCallbackUserData);
    }
  }
  int num_devices = info->NumberOfDevices();
  for (int i = 0; i < num_devices; ++i) {
    constexpr uint32_t kSize = 256;
    char name[kSize] = {0};
    char id[kSize] = {0};
    if (info->GetDeviceName(i, name, kSize, id, kSize) != -1) {
      (*callback)(id, name, userData);
    }
  }
  if (completedCallback) {
    (*completedCallback)(completedCallbackUserData);
  }
#endif
}

PeerConnectionHandle mrsPeerConnectionCreate(
    const char** turn_urls,
    const int no_of_urls,
    const char* username,
    const char* credential,
    bool /*mandatory_receive_video*/) noexcept(kNoExceptFalseOnUWP) {
  // Ensure the factory exists
  if (g_peer_connection_factory == nullptr) {
#if defined(WINUWP)
    auto mw =
        winrt::Windows::ApplicationModel::Core::CoreApplication::MainView();
    auto cw = mw.CoreWindow();
    auto dispatcher = cw.Dispatcher();
    if (dispatcher.HasThreadAccess()) {
      // WebRtcFactory::setup() will deadlock if called from main UI thread
      // See https://github.com/webrtc-uwp/webrtc-uwp-sdk/issues/143
      throw winrt::hresult_wrong_thread(winrt::to_hstring(
          L"Cannot setup the WebRTC factory from the UI thread on UWP."));
    }
    auto dispatcherQueue =
        wrapper::impl::org::webRtc::EventQueue::toWrapper(dispatcher);

    auto libConfig =
        std::make_shared<wrapper::impl::org::webRtc::WebRtcLibConfiguration>();
    libConfig->queue = dispatcherQueue;
    wrapper::impl::org::webRtc::WebRtcLib::setup(libConfig);

    auto factoryConfig = std::make_shared<
        wrapper::impl::org::webRtc::WebRtcFactoryConfiguration>();
    factoryConfig->audioCapturingEnabled = true;
    factoryConfig->audioRenderingEnabled = true;
    factoryConfig->enableAudioBufferEvents = true;
    g_winuwp_factory =
        std::make_unique<wrapper::impl::org::webRtc::WebRtcFactory>();
    g_winuwp_factory->wrapper_init_org_webRtc_WebRtcFactory(factoryConfig);
    g_winuwp_factory->setup();

    g_peer_connection_factory = g_winuwp_factory->peerConnectionFactory();
#else
    g_worker_thread.reset(new rtc::Thread());
    g_worker_thread->Start();
    g_signaling_thread.reset(new rtc::Thread());
    g_signaling_thread->Start();

    g_peer_connection_factory = webrtc::CreatePeerConnectionFactory(
        g_worker_thread.get(), g_worker_thread.get(), g_signaling_thread.get(),
        nullptr, webrtc::CreateBuiltinAudioEncoderFactory(),
        webrtc::CreateBuiltinAudioDecoderFactory(),
        std::unique_ptr<webrtc::VideoEncoderFactory>(
            new webrtc::MultiplexEncoderFactory(
                absl::make_unique<webrtc::InternalEncoderFactory>())),
        std::unique_ptr<webrtc::VideoDecoderFactory>(
            new webrtc::MultiplexDecoderFactory(
                absl::make_unique<webrtc::InternalDecoderFactory>())),
        nullptr, nullptr);
#endif
  }
  if (!g_peer_connection_factory.get()) {
    return {};
  }

  // Setup the connection configuration
  webrtc::PeerConnectionInterface::RTCConfiguration config;
  if (turn_urls != nullptr) {
    if (no_of_urls > 0) {
      config.servers.reserve(no_of_urls);
      webrtc::PeerConnectionInterface::IceServer server;
      for (int i = 0; i < no_of_urls; ++i) {
        std::string url(turn_urls[i]);
        if (url.length() > 0)
          server.urls.push_back(turn_urls[i]);
      }
      if (username) {
        std::string user_name(username);
        if (user_name.length() > 0)
          server.username = std::move(username);
      }
      if (credential) {
        std::string password(credential);
        if (password.length() > 0)
          server.password = std::move(password);
      }
      config.servers.push_back(server);
    }
  }
  config.enable_rtp_data_channel = false;
  config.enable_dtls_srtp = true;  //< TODO - Should be true/unset for security

  // Create the new peer connection
  rtc::scoped_refptr<PeerConnection> peer =
      new rtc::RefCountedObject<PeerConnection>();
  webrtc::PeerConnectionDependencies dependencies(peer);
  rtc::scoped_refptr<webrtc::PeerConnectionInterface> peer_connection =
      g_peer_connection_factory->CreatePeerConnection(config,
                                                      std::move(dependencies));
  if (peer_connection.get() == nullptr)
    return {};
  peer->SetPeerImpl(peer_connection);
  const PeerConnectionHandle handle{peer.get()};
  g_peer_connection_map.insert({handle, std::move(peer)});
  return handle;
}

void mrsPeerConnectionRegisterConnectedCallback(
    PeerConnectionHandle peerHandle,
    PeerConnectionConnectedCallback callback,
    void* user_data) noexcept {
  if (auto peer = static_cast<PeerConnection*>(peerHandle)) {
    peer->RegisterConnectedCallback(Callback<>{callback, user_data});
  }
}

void mrsPeerConnectionRegisterLocalSdpReadytoSendCallback(
    PeerConnectionHandle peerHandle,
    PeerConnectionLocalSdpReadytoSendCallback callback,
    void* user_data) noexcept {
  if (auto peer = static_cast<PeerConnection*>(peerHandle)) {
    peer->RegisterLocalSdpReadytoSendCallback(
        Callback<const char*, const char*>{callback, user_data});
  }
}

void mrsPeerConnectionRegisterIceCandidateReadytoSendCallback(
    PeerConnectionHandle peerHandle,
    PeerConnectionIceCandidateReadytoSendCallback callback,
    void* user_data) noexcept {
  if (auto peer = static_cast<PeerConnection*>(peerHandle)) {
    peer->RegisterIceCandidateReadytoSendCallback(
        Callback<const char*, int, const char*>{callback, user_data});
  }
}

void mrsPeerConnectionRegisterRenegotiationNeededCallback(
    PeerConnectionHandle peerHandle,
    PeerConnectionRenegotiationNeededCallback callback,
    void* user_data) noexcept {
  if (auto peer = static_cast<PeerConnection*>(peerHandle)) {
    peer->RegisterRenegotiationNeededCallback(Callback<>{callback, user_data});
  }
}

void mrsPeerConnectionRegisterI420LocalVideoFrameCallback(
    PeerConnectionHandle peerHandle,
    PeerConnectionI420VideoFrameCallback callback,
    void* user_data) noexcept {
  if (auto peer = static_cast<PeerConnection*>(peerHandle)) {
    peer->RegisterLocalVideoFrameCallback(
        I420FrameReadyCallback{callback, user_data});
  }
}

void mrsPeerConnectionRegisterARGBLocalVideoFrameCallback(
    PeerConnectionHandle peerHandle,
    PeerConnectionARGBVideoFrameCallback callback,
    void* user_data) noexcept {
  if (auto peer = static_cast<PeerConnection*>(peerHandle)) {
    peer->RegisterLocalVideoFrameCallback(
        ARGBFrameReadyCallback{callback, user_data});
  }
}

void mrsPeerConnectionRegisterI420RemoteVideoFrameCallback(
    PeerConnectionHandle peerHandle,
    PeerConnectionI420VideoFrameCallback callback,
    void* user_data) noexcept {
  if (auto peer = static_cast<PeerConnection*>(peerHandle)) {
    peer->RegisterRemoteVideoFrameCallback(
        I420FrameReadyCallback{callback, user_data});
  }
}

void mrsPeerConnectionRegisterARGBRemoteVideoFrameCallback(
    PeerConnectionHandle peerHandle,
    PeerConnectionARGBVideoFrameCallback callback,
    void* user_data) noexcept {
  if (auto peer = static_cast<PeerConnection*>(peerHandle)) {
    peer->RegisterRemoteVideoFrameCallback(
        ARGBFrameReadyCallback{callback, user_data});
  }
}

bool mrsPeerConnectionAddLocalVideoTrack(PeerConnectionHandle peerHandle,
                                         const char* video_device_id,
                                         bool enable_mrc)
#if defined(WINUWP)
    noexcept(false)
#else
    noexcept(true)
#endif
{
  if (auto peer = static_cast<PeerConnection*>(peerHandle)) {
    if (!g_peer_connection_factory) {
      return false;
    }
    std::unique_ptr<cricket::VideoCapturer> video_capturer =
        OpenVideoCaptureDevice(video_device_id, enable_mrc);
    if (!video_capturer) {
      return false;
    }
    rtc::scoped_refptr<webrtc::VideoTrackSourceInterface> video_source =
        g_peer_connection_factory->CreateVideoSource(std::move(video_capturer));
    if (!video_source) {
      return false;
    }
    rtc::scoped_refptr<webrtc::VideoTrackInterface> video_track =
        g_peer_connection_factory->CreateVideoTrack(kLocalVideoLabel,
                                                    video_source);
    if (!video_track) {
      return false;
    }
    return peer->AddLocalVideoTrack(std::move(video_track));
  }
  return false;
}

bool mrsPeerConnectionAddLocalAudioTrack(
    PeerConnectionHandle peerHandle) noexcept {
  if (auto peer = static_cast<PeerConnection*>(peerHandle)) {
    if (!g_peer_connection_factory) {
      return false;
    }
    rtc::scoped_refptr<webrtc::AudioSourceInterface> audio_source =
        g_peer_connection_factory->CreateAudioSource(cricket::AudioOptions());
    if (!audio_source) {
      return false;
    }
    rtc::scoped_refptr<webrtc::AudioTrackInterface> audio_track =
        g_peer_connection_factory->CreateAudioTrack(kLocalAudioLabel,
                                                    audio_source);
    if (!audio_track) {
      return false;
    }
    return peer->AddLocalAudioTrack(std::move(audio_track));
  }
  return false;
}

bool mrsPeerConnectionAddDataChannel(
    PeerConnectionHandle peerHandle,
    int id,
    const char* label,
    bool ordered,
    bool reliable,
    PeerConnectionDataChannelMessageCallback message_callback,
    void* message_user_data,
    PeerConnectionDataChannelBufferingCallback buffering_callback,
    void* buffering_user_data,
    PeerConnectionDataChannelStateCallback state_callback,
    void* state_user_data) noexcept

{
  if (auto peer = static_cast<PeerConnection*>(peerHandle)) {
    return peer->AddDataChannel(
        id, label, ordered, reliable,
        DataChannelMessageCallback{message_callback, message_user_data},
        DataChannelBufferingCallback{buffering_callback, buffering_user_data},
        DataChannelStateCallback{state_callback, state_user_data});
  }
  return false;
}

void mrsPeerConnectionRemoveLocalVideoTrack(
    PeerConnectionHandle peerHandle) noexcept {
  if (auto peer = static_cast<PeerConnection*>(peerHandle)) {
    peer->RemoveLocalVideoTrack();
  }
}

void mrsPeerConnectionRemoveLocalAudioTrack(
    PeerConnectionHandle peerHandle) noexcept {
  if (auto peer = static_cast<PeerConnection*>(peerHandle)) {
    peer->RemoveLocalAudioTrack();
  }
}

bool mrsPeerConnectionRemoveDataChannelById(PeerConnectionHandle peerHandle,
                                            int id) noexcept {
  if (auto peer = static_cast<PeerConnection*>(peerHandle)) {
    return peer->RemoveDataChannel(id);
  }
  return false;
}

bool mrsPeerConnectionRemoveDataChannelByLabel(PeerConnectionHandle peerHandle,
                                               const char* label) noexcept {
  if (auto peer = static_cast<PeerConnection*>(peerHandle)) {
    return peer->RemoveDataChannel(label);
  }
  return false;
}

bool mrsPeerConnectionSendDataChannelMessage(PeerConnectionHandle peerHandle,
                                             int id,
                                             const void* data,
                                             uint64_t size) noexcept {
  if (auto peer = static_cast<PeerConnection*>(peerHandle)) {
    return peer->SendDataChannelMessage(id, data, size);
  }
  return false;
}

bool mrsPeerConnectionAddIceCandidate(PeerConnectionHandle peerHandle,
                                      const char* sdp,
                                      const int sdp_mline_index,
                                      const char* sdp_mid) noexcept {
  if (auto peer = static_cast<PeerConnection*>(peerHandle)) {
    return peer->AddIceCandidate(sdp, sdp_mline_index, sdp_mid);
  }
  return false;
}

bool mrsPeerConnectionCreateOffer(PeerConnectionHandle peerHandle) noexcept {
  if (auto peer = static_cast<PeerConnection*>(peerHandle)) {
    return peer->CreateOffer();
  }
  return false;
}

bool mrsPeerConnectionCreateAnswer(PeerConnectionHandle peerHandle) noexcept {
  if (auto peer = static_cast<PeerConnection*>(peerHandle)) {
    return peer->CreateAnswer();
  }
  return false;
}

bool mrsPeerConnectionSetRemoteDescription(PeerConnectionHandle peerHandle,
                                           const char* type,
                                           const char* sdp) noexcept {
  if (auto peer = static_cast<PeerConnection*>(peerHandle)) {
    return peer->SetRemoteDescription(type, sdp);
  }
  return false;
}

void mrsPeerConnectionClose(PeerConnectionHandle* peerHandlePtr) noexcept {
  if (peerHandlePtr) {
    if (auto peer = static_cast<PeerConnection*>(*peerHandlePtr)) {
      auto it = g_peer_connection_map.find(peer);
      if (it != g_peer_connection_map.end()) {
        // This generally removes the last reference to the PeerConnection and
        // leads to its destruction, unless some background running task is
        // still using the connection.
        g_peer_connection_map.erase(it);
        if (g_peer_connection_map.empty()) {
          // Release the factory so that the threads are stopped and the DLL can
          // be unloaded. This is mandatory to be able to unload/reload in the
          // Unity Editor and be able to Play/Stop multiple times per Editor
          // process run.
          g_peer_connection_factory = nullptr;
#if defined(WINUWP)
          g_winuwp_factory = nullptr;
#else
          g_signaling_thread.reset();
          g_worker_thread.reset();
#endif
        }
      }
    }
    *peerHandlePtr = nullptr;
  }
}
