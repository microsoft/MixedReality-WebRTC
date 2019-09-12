// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

// This is a precompiled header, it must be on its own, followed by a blank
// line, to prevent clang-format from reordering it with other headers.
#include "pch.h"

#include "api.h"
#include "peer_connection.h"
#include "sdp_utils.h"

using namespace Microsoft::MixedReality::WebRTC;

struct mrsEnumerator {
  virtual ~mrsEnumerator() = default;
  virtual void dispose() = 0;
};

namespace {

/// Global factory for PeerConnection objects.
rtc::scoped_refptr<webrtc::PeerConnectionFactoryInterface>
    g_peer_connection_factory;

#if defined(WINUWP)

/// Winuwp relies on a global-scoped factory wrapper
std::shared_ptr<wrapper::impl::org::webRtc::WebRtcFactory> g_winuwp_factory;

/// Initialize the global factory for UWP.
void InitUWPFactory() noexcept(false) {
  RTC_CHECK(!g_winuwp_factory);
  auto mw = winrt::Windows::ApplicationModel::Core::CoreApplication::MainView();
  auto cw = mw.CoreWindow();
  auto dispatcher = cw.Dispatcher();
  if (dispatcher.HasThreadAccess()) {
    // WebRtcFactory::setup() will deadlock if called from main UI thread
    // See https://github.com/webrtc-uwp/webrtc-uwp-sdk/issues/143
    throw winrt::hresult_wrong_thread(winrt::to_hstring(
        L"Cannot setup the UWP factory from the UI thread on UWP."));
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
    factoryConfig->enableAudioBufferEvents = true;
    g_winuwp_factory =
        std::make_shared<wrapper::impl::org::webRtc::WebRtcFactory>();
    g_winuwp_factory->thisWeak_ = g_winuwp_factory;  // mimic wrapper_create()
    g_winuwp_factory->wrapper_init_org_webRtc_WebRtcFactory(factoryConfig);
  }
  g_winuwp_factory->internalSetup();
}

#else  // defined(WINUWP)

/// WebRTC network thread.
std::unique_ptr<rtc::Thread> g_network_thread;

/// WebRTC worker thread.
std::unique_ptr<rtc::Thread> g_worker_thread;

/// WebRTC signaling thread.
std::unique_ptr<rtc::Thread> g_signaling_thread;

#endif  // defined(WINUWP)

/// Collection of all peer connection objects alive.
std::unordered_map<
    PeerConnectionHandle,
    rtc::scoped_refptr<Microsoft::MixedReality::WebRTC::PeerConnection>>
    g_peer_connection_map;

/// Predefined name of the local video track.
const std::string kLocalVideoLabel("local_video");

/// Predefined name of the local audio track.
const std::string kLocalAudioLabel("local_audio");

/// Helper to open a video capture device.
std::unique_ptr<cricket::VideoCapturer> OpenVideoCaptureDevice(
    VideoDeviceConfiguration config) noexcept(kNoExceptFalseOnUWP) {
#if defined(WINUWP)
  if (!g_winuwp_factory) {
    InitUWPFactory();
    if (!g_winuwp_factory) {
      RTC_LOG(LS_ERROR) << "Failed to initialize the UWP factory.";
      return {};
    }
  }

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
  if ((config.video_device_id != nullptr) &&
      (config.video_device_id[0] != '\0')) {
    video_device_id_str =
        rtc::ToUtf16(config.video_device_id, strlen(config.video_device_id));
  }

  for (auto&& vdi : *deviceList) {
    auto devInfo =
        wrapper::impl::org::webRtc::VideoDeviceInfo::toNative_winrt(vdi);
    const winrt::hstring& id = devInfo.Id();
    if (!video_device_id_str.empty() && (video_device_id_str != id)) {
      continue;
    }

    auto createParams = std::make_shared<
        wrapper::impl::org::webRtc::VideoCapturerCreationParameters>();
    createParams->factory = g_winuwp_factory;
    createParams->name = devInfo.Name().c_str();
    createParams->id = id.c_str();
    if (config.video_profile_id) {
      createParams->videoProfileId = config.video_profile_id;
    }
    createParams->videoProfileKind =
        (wrapper::org::webRtc::VideoProfileKind)config.video_profile_kind;
    createParams->enableMrc = config.enable_mrc;
    createParams->width = config.width;
    createParams->height = config.height;
    createParams->framerate = config.framerate;

    auto vcd = wrapper::impl::org::webRtc::VideoCapturer::create(createParams);

    if (vcd != nullptr) {
      auto nativeVcd = wrapper::impl::org::webRtc::VideoCapturer::toNative(vcd);

      RTC_LOG(LS_INFO) << "Using video capture device '"
                       << createParams->name.c_str()
                       << "' (id=" << createParams->id.c_str() << ")";

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
    if ((config.video_device_id != nullptr) &&
        (config.video_device_id[0] != '\0')) {
      video_device_id_str.assign(config.video_device_id);
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

webrtc::PeerConnectionInterface::IceTransportsType ICETransportTypeToNative(
    IceTransportType mrsValue) {
  using Native = webrtc::PeerConnectionInterface::IceTransportsType;
  using Impl = IceTransportType;
  static_assert((int)Native::kNone == (int)Impl::kNone);
  static_assert((int)Native::kNoHost == (int)Impl::kNoHost);
  static_assert((int)Native::kRelay == (int)Impl::kRelay);
  static_assert((int)Native::kAll == (int)Impl::kAll);
  return static_cast<Native>(mrsValue);
}

webrtc::PeerConnectionInterface::BundlePolicy BundlePolicyToNative(
    BundlePolicy mrsValue) {
  using Native = webrtc::PeerConnectionInterface::BundlePolicy;
  using Impl = BundlePolicy;
  static_assert((int)Native::kBundlePolicyBalanced == (int)Impl::kBalanced);
  static_assert((int)Native::kBundlePolicyMaxBundle == (int)Impl::kMaxBundle);
  static_assert((int)Native::kBundlePolicyMaxCompat == (int)Impl::kMaxCompat);
  return static_cast<Native>(mrsValue);
}

//< TODO - Unit test / check if RTC has already a utility like this
std::vector<std::string> SplitString(const std::string& str, char sep) {
  std::vector<std::string> ret;
  size_t offset = 0;
  for (size_t idx = str.find_first_of(sep); idx < std::string::npos;
       idx = str.find_first_of(sep, offset)) {
    if (idx > offset) {
      ret.push_back(str.substr(offset, idx - offset));
    }
    offset = idx + 1;
  }
  if (offset < str.size()) {
    ret.push_back(str.substr(offset));
  }
  return ret;
}

}  // namespace

#if defined(WINUWP)
rtc::Thread* UnsafeGetWorkerThread() {
  if (auto* ptr = g_winuwp_factory.get()) {
    return ptr->workerThread.get();
  }
  return nullptr;
}
#endif  // defined(WINUWP)

void MRS_CALL mrsCloseEnum(mrsEnumHandle* handleRef) noexcept {
  if (handleRef) {
    if (auto& handle = *handleRef) {
      handle->dispose();
      delete handle;
      handle = nullptr;
    }
  }
}

void MRS_CALL mrsEnumVideoCaptureDevicesAsync(
    mrsVideoCaptureDeviceEnumCallback callback,
    void* userData,
    mrsVideoCaptureDeviceEnumCompletedCallback completedCallback,
    void* completedCallbackUserData) noexcept {
  if (!callback) {
    return;
  }
#if defined(WINUWP)
  // The UWP factory needs to be initialized for getDevices() to work.
  if (!g_winuwp_factory) {
    InitUWPFactory();
    if (!g_winuwp_factory) {
      RTC_LOG(LS_ERROR) << "Failed to initialize the UWP factory.";
      return;
    }
  }

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

mrsResult MRS_CALL mrsPeerConnectionCreate(
    PeerConnectionConfiguration config,
    PeerConnectionHandle* peerHandleOut) noexcept(kNoExceptFalseOnUWP) {
  if (!peerHandleOut) {
    return MRS_E_INVALID_PARAMETER;
  }
  *peerHandleOut = nullptr;

  // Ensure the factory exists
  if (g_peer_connection_factory == nullptr) {
#if defined(WINUWP)
    if (!g_winuwp_factory) {
      InitUWPFactory();
      if (!g_winuwp_factory) {
        RTC_LOG(LS_ERROR) << "Failed to initialize the UWP factory.";
        return MRS_E_UNKNOWN;
      }
    }

    g_peer_connection_factory = g_winuwp_factory->peerConnectionFactory();
#else
    g_network_thread = rtc::Thread::CreateWithSocketServer();
    g_network_thread->SetName("WebRTC network thread", g_network_thread.get());
    g_network_thread->Start();
    g_worker_thread = rtc::Thread::Create();
    g_worker_thread->SetName("WebRTC worker thread", g_worker_thread.get());
    g_worker_thread->Start();
    g_signaling_thread = rtc::Thread::Create();
    g_signaling_thread->SetName("WebRTC signaling thread",
                                g_signaling_thread.get());
    g_signaling_thread->Start();

    g_peer_connection_factory = webrtc::CreatePeerConnectionFactory(
        g_network_thread.get(), g_worker_thread.get(), g_signaling_thread.get(),
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
    return MRS_E_UNKNOWN;
  }

  // Setup the connection configuration
  webrtc::PeerConnectionInterface::RTCConfiguration rtc_config;
  if (config.encoded_ice_servers != nullptr) {
    std::string encoded_ice_servers{config.encoded_ice_servers};
    rtc_config.servers = DecodeIceServers(encoded_ice_servers);
  }
  rtc_config.enable_rtp_data_channel = false;  // Always false for security
  rtc_config.enable_dtls_srtp = true;          // Always true for security
  rtc_config.type = ICETransportTypeToNative(config.ice_transport_type);
  rtc_config.bundle_policy = BundlePolicyToNative(config.bundle_policy);

  // Create the new peer connection
  rtc::scoped_refptr<PeerConnection> peer =
      PeerConnection::create(*g_peer_connection_factory, rtc_config);
  const PeerConnectionHandle handle{peer.get()};
  g_peer_connection_map.insert({handle, std::move(peer)});
  *peerHandleOut = handle;
  return MRS_SUCCESS;
}

void MRS_CALL mrsPeerConnectionRegisterConnectedCallback(
    PeerConnectionHandle peerHandle,
    PeerConnectionConnectedCallback callback,
    void* user_data) noexcept {
  if (auto peer = static_cast<PeerConnection*>(peerHandle)) {
    peer->RegisterConnectedCallback(Callback<>{callback, user_data});
  }
}

void MRS_CALL mrsPeerConnectionRegisterLocalSdpReadytoSendCallback(
    PeerConnectionHandle peerHandle,
    PeerConnectionLocalSdpReadytoSendCallback callback,
    void* user_data) noexcept {
  if (auto peer = static_cast<PeerConnection*>(peerHandle)) {
    peer->RegisterLocalSdpReadytoSendCallback(
        Callback<const char*, const char*>{callback, user_data});
  }
}

void MRS_CALL mrsPeerConnectionRegisterIceCandidateReadytoSendCallback(
    PeerConnectionHandle peerHandle,
    PeerConnectionIceCandidateReadytoSendCallback callback,
    void* user_data) noexcept {
  if (auto peer = static_cast<PeerConnection*>(peerHandle)) {
    peer->RegisterIceCandidateReadytoSendCallback(
        Callback<const char*, int, const char*>{callback, user_data});
  }
}

void MRS_CALL mrsPeerConnectionRegisterRenegotiationNeededCallback(
    PeerConnectionHandle peerHandle,
    PeerConnectionRenegotiationNeededCallback callback,
    void* user_data) noexcept {
  if (auto peer = static_cast<PeerConnection*>(peerHandle)) {
    peer->RegisterRenegotiationNeededCallback(Callback<>{callback, user_data});
  }
}

void MRS_CALL mrsPeerConnectionRegisterTrackAddedCallback(
    PeerConnectionHandle peerHandle,
    PeerConnectionTrackAddedCallback callback,
    void* user_data) noexcept {
  if (auto peer = static_cast<PeerConnection*>(peerHandle)) {
    peer->RegisterTrackAddedCallback(Callback<TrackKind>{callback, user_data});
  }
}

void MRS_CALL mrsPeerConnectionRegisterTrackRemovedCallback(
    PeerConnectionHandle peerHandle,
    PeerConnectionTrackRemovedCallback callback,
    void* user_data) noexcept {
  if (auto peer = static_cast<PeerConnection*>(peerHandle)) {
    peer->RegisterTrackRemovedCallback(
        Callback<TrackKind>{callback, user_data});
  }
}

void MRS_CALL mrsPeerConnectionRegisterI420LocalVideoFrameCallback(
    PeerConnectionHandle peerHandle,
    PeerConnectionI420VideoFrameCallback callback,
    void* user_data) noexcept {
  if (auto peer = static_cast<PeerConnection*>(peerHandle)) {
    peer->RegisterLocalVideoFrameCallback(
        I420FrameReadyCallback{callback, user_data});
  }
}

void MRS_CALL mrsPeerConnectionRegisterARGBLocalVideoFrameCallback(
    PeerConnectionHandle peerHandle,
    PeerConnectionARGBVideoFrameCallback callback,
    void* user_data) noexcept {
  if (auto peer = static_cast<PeerConnection*>(peerHandle)) {
    peer->RegisterLocalVideoFrameCallback(
        ARGBFrameReadyCallback{callback, user_data});
  }
}

void MRS_CALL mrsPeerConnectionRegisterI420RemoteVideoFrameCallback(
    PeerConnectionHandle peerHandle,
    PeerConnectionI420VideoFrameCallback callback,
    void* user_data) noexcept {
  if (auto peer = static_cast<PeerConnection*>(peerHandle)) {
    peer->RegisterRemoteVideoFrameCallback(
        I420FrameReadyCallback{callback, user_data});
  }
}

void MRS_CALL mrsPeerConnectionRegisterARGBRemoteVideoFrameCallback(
    PeerConnectionHandle peerHandle,
    PeerConnectionARGBVideoFrameCallback callback,
    void* user_data) noexcept {
  if (auto peer = static_cast<PeerConnection*>(peerHandle)) {
    peer->RegisterRemoteVideoFrameCallback(
        ARGBFrameReadyCallback{callback, user_data});
  }
}

MRS_API void MRS_CALL mrsPeerConnectionRegisterLocalAudioFrameCallback(
    PeerConnectionHandle peerHandle,
    PeerConnectionAudioFrameCallback callback,
    void* user_data) noexcept {
  if (auto peer = static_cast<PeerConnection*>(peerHandle)) {
    peer->RegisterLocalAudioFrameCallback(
        AudioFrameReadyCallback{callback, user_data});
  }
}

MRS_API void MRS_CALL mrsPeerConnectionRegisterRemoteAudioFrameCallback(
    PeerConnectionHandle peerHandle,
    PeerConnectionAudioFrameCallback callback,
    void* user_data) noexcept {
  if (auto peer = static_cast<PeerConnection*>(peerHandle)) {
    peer->RegisterRemoteAudioFrameCallback(
        AudioFrameReadyCallback{callback, user_data});
  }
}

mrsResult MRS_CALL
mrsPeerConnectionAddLocalVideoTrack(PeerConnectionHandle peerHandle,
                                    VideoDeviceConfiguration config)
#if defined(WINUWP)
    noexcept(false)
#else
    noexcept(true)
#endif
{
  if (auto peer = static_cast<PeerConnection*>(peerHandle)) {
    if (!g_peer_connection_factory) {
      return MRS_E_INVALID_OPERATION;
    }
    std::unique_ptr<cricket::VideoCapturer> video_capturer =
        OpenVideoCaptureDevice(config);
    if (!video_capturer) {
      return MRS_E_UNKNOWN;
    }

    //// HACK - Force max size to prevent high-res HoloLens 2 camera, which also
    /// disables MRC /
    /// https://docs.microsoft.com/en-us/windows/mixed-reality/mixed-reality-capture-for-developers#enabling-mrc-in-your-app
    //// "MRC on HoloLens 2 supports videos up to 1080p and photos up to 4K
    /// resolution"
    // cricket::VideoFormat max_format{};
    // max_format.width = 1920;
    // max_format.height = 1080;
    // max_format.interval = cricket::VideoFormat::FpsToInterval(30);
    // video_capturer->set_enable_camera_list(
    //    true);  //< needed to enable filtering
    // video_capturer->ConstrainSupportedFormats(max_format);

    //#if defined(WINUWP)
    //    //MediaConstraints videoConstraints = new MediaConstraints();
    //    //new wrapper::impl::org::webRtc::MediaConstraints(mandatory,
    //    optional); auto ptr =
    //    wrapper::org::webRtc::MediaConstraints::wrapper_create();
    //    ptr->get_mandatory()->emplace_back(new
    //    wrapper::org::webRtc::Constraint());
    //#endif

    rtc::scoped_refptr<webrtc::VideoTrackSourceInterface> video_source =
        g_peer_connection_factory->CreateVideoSource(std::move(video_capturer));
    if (!video_source) {
      return MRS_E_UNKNOWN;
    }
    rtc::scoped_refptr<webrtc::VideoTrackInterface> video_track =
        g_peer_connection_factory->CreateVideoTrack(kLocalVideoLabel,
                                                    video_source);
    if (!video_track) {
      return MRS_E_UNKNOWN;
    }
    return (peer->AddLocalVideoTrack(std::move(video_track)) ? MRS_SUCCESS
                                                             : MRS_E_UNKNOWN);
  }
  return MRS_E_UNKNOWN;
}

mrsResult MRS_CALL
mrsPeerConnectionAddLocalAudioTrack(PeerConnectionHandle peerHandle) noexcept {
  if (auto peer = static_cast<PeerConnection*>(peerHandle)) {
    if (!g_peer_connection_factory) {
      return MRS_E_INVALID_OPERATION;
    }
    rtc::scoped_refptr<webrtc::AudioSourceInterface> audio_source =
        g_peer_connection_factory->CreateAudioSource(cricket::AudioOptions());
    if (!audio_source) {
      return MRS_E_UNKNOWN;
    }
    rtc::scoped_refptr<webrtc::AudioTrackInterface> audio_track =
        g_peer_connection_factory->CreateAudioTrack(kLocalAudioLabel,
                                                    audio_source);
    if (!audio_track) {
      return MRS_E_UNKNOWN;
    }
    return (peer->AddLocalAudioTrack(std::move(audio_track)) ? MRS_SUCCESS
                                                             : MRS_E_UNKNOWN);
  }
  return MRS_E_UNKNOWN;
}

mrsResult MRS_CALL mrsPeerConnectionAddDataChannel(
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
  return MRS_E_INVALID_PEER_HANDLE;
}

void MRS_CALL mrsPeerConnectionRemoveLocalVideoTrack(
    PeerConnectionHandle peerHandle) noexcept {
  if (auto peer = static_cast<PeerConnection*>(peerHandle)) {
    peer->RemoveLocalVideoTrack();
  }
}

void MRS_CALL mrsPeerConnectionRemoveLocalAudioTrack(
    PeerConnectionHandle peerHandle) noexcept {
  if (auto peer = static_cast<PeerConnection*>(peerHandle)) {
    peer->RemoveLocalAudioTrack();
  }
}

mrsResult MRS_CALL
mrsPeerConnectionRemoveDataChannelById(PeerConnectionHandle peerHandle,
                                       int id) noexcept {
  if (auto peer = static_cast<PeerConnection*>(peerHandle)) {
    return (peer->RemoveDataChannel(id) ? MRS_SUCCESS : MRS_E_UNKNOWN);
  }
  return MRS_E_INVALID_PEER_HANDLE;
}

mrsResult MRS_CALL
mrsPeerConnectionRemoveDataChannelByLabel(PeerConnectionHandle peerHandle,
                                          const char* label) noexcept {
  if (auto peer = static_cast<PeerConnection*>(peerHandle)) {
    return (peer->RemoveDataChannel(label) ? MRS_SUCCESS : MRS_E_UNKNOWN);
  }
  return MRS_E_INVALID_PEER_HANDLE;
}

mrsResult MRS_CALL
mrsPeerConnectionSendDataChannelMessage(PeerConnectionHandle peerHandle,
                                        int id,
                                        const void* data,
                                        uint64_t size) noexcept {
  if (auto peer = static_cast<PeerConnection*>(peerHandle)) {
    return (peer->SendDataChannelMessage(id, data, size) ? MRS_SUCCESS
                                                         : MRS_E_UNKNOWN);
  }
  return MRS_E_INVALID_PEER_HANDLE;
}

mrsResult MRS_CALL mrsPeerConnectionAddIceCandidate(PeerConnectionHandle peerHandle,
                                               const char* sdp,
                                               const int sdp_mline_index,
                                               const char* sdp_mid) noexcept {
  if (auto peer = static_cast<PeerConnection*>(peerHandle)) {
    return (peer->AddIceCandidate(sdp, sdp_mline_index, sdp_mid)
                ? MRS_SUCCESS
                : MRS_E_UNKNOWN);
  }
  return MRS_E_INVALID_PEER_HANDLE;
}

mrsResult MRS_CALL
mrsPeerConnectionCreateOffer(PeerConnectionHandle peerHandle) noexcept {
  if (auto peer = static_cast<PeerConnection*>(peerHandle)) {
    return (peer->CreateOffer() ? MRS_SUCCESS : MRS_E_UNKNOWN);
  }
  return MRS_E_INVALID_PEER_HANDLE;
}

mrsResult MRS_CALL
mrsPeerConnectionCreateAnswer(PeerConnectionHandle peerHandle) noexcept {
  if (auto peer = static_cast<PeerConnection*>(peerHandle)) {
    return (peer->CreateAnswer() ? MRS_SUCCESS : MRS_E_UNKNOWN);
  }
  return MRS_E_INVALID_PEER_HANDLE;
}

mrsResult MRS_CALL
mrsPeerConnectionSetRemoteDescription(PeerConnectionHandle peerHandle,
                                      const char* type,
                                      const char* sdp) noexcept {
  if (auto peer = static_cast<PeerConnection*>(peerHandle)) {
    return (peer->SetRemoteDescription(type, sdp) ? MRS_SUCCESS
                                                  : MRS_E_UNKNOWN);
  }
  return MRS_E_INVALID_PEER_HANDLE;
}

void MRS_CALL
mrsPeerConnectionClose(PeerConnectionHandle* peerHandlePtr) noexcept {
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
          g_network_thread.reset();
          g_signaling_thread.reset();
          g_worker_thread.reset();
#endif
        }
      }
    }
    *peerHandlePtr = nullptr;
  }
}

mrsResult MRS_CALL mrsSdpForceCodecs(const char* message,
                                     SdpFilter audio_filter,
                                     SdpFilter video_filter,
                                     char* buffer,
                                     uint64_t* buffer_size) {
  RTC_CHECK(message);
  RTC_CHECK(buffer);
  RTC_CHECK(buffer_size);
  std::string message_str(message);
  std::string audio_codec_name_str;
  std::string video_codec_name_str;
  std::map<std::string, std::string> extra_audio_params;
  std::map<std::string, std::string> extra_video_params;
  if (audio_filter.codec_name) {
    audio_codec_name_str.assign(audio_filter.codec_name);
  }
  if (video_filter.codec_name) {
    video_codec_name_str.assign(video_filter.codec_name);
  }
  // Only assign extra parameters if codec name is not empty
  if (!audio_codec_name_str.empty() && audio_filter.params) {
    SdpParseCodecParameters(audio_filter.params, extra_audio_params);
  }
  if (!video_codec_name_str.empty() && video_filter.params) {
    SdpParseCodecParameters(video_filter.params, extra_video_params);
  }
  std::string out_message =
      SdpForceCodecs(message_str, audio_codec_name_str, extra_audio_params,
                     video_codec_name_str, extra_video_params);
  const size_t capacity = static_cast<size_t>(*buffer_size);
  const size_t size = out_message.size();
  *buffer_size = size + 1;
  if (capacity < size + 1) {
    return MRS_E_INVALID_PARAMETER;
  }
  memcpy(buffer, out_message.c_str(), size);
  buffer[size] = '\0';
  return MRS_SUCCESS;
}

void MRS_CALL mrsMemCpy(void* dst, const void* src, uint64_t size) {
  memcpy(dst, src, static_cast<size_t>(size));
}

void MRS_CALL mrsMemCpyStride(void* dst,
                              int32_t dst_stride,
                              const void* src,
                              int32_t src_stride,
                              int32_t elem_size,
                              int32_t elem_count) {
  RTC_CHECK(dst);
  RTC_CHECK(dst_stride >= elem_size);
  RTC_CHECK(src);
  RTC_CHECK(src_stride >= elem_size);
  if ((dst_stride == elem_size) && (src_stride == elem_size)) {
    // If tightly packed, do a single memcpy() for performance
    const size_t total_size = (size_t)elem_size * elem_count;
    memcpy(dst, src, total_size);
  } else {
    // Otherwise, copy row by row
    for (int i = 0; i < elem_count; ++i) {
      memcpy(dst, src, elem_size);
      dst = (char*)dst + dst_stride;
      src = (const char*)src + src_stride;
    }
  }
}
