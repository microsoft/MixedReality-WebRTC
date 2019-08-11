// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#include "pch.h"

#include "video_capturer.h"

namespace Microsoft::MixedReality::WebRTC {

rtc::scoped_refptr<webrtc::VideoCaptureModule> OpenVideoCaptureDevice(
    const char* video_device_id,
    bool enable_mrc) noexcept {
#if defined(WINUWP)
  // Check for calls from main UI thread; this is not supported (will deadlock)
  auto mw = winrt::Windows::ApplicationModel::Core::CoreApplication::MainView();
  auto cw = mw.CoreWindow();
  auto dispatcher = cw.Dispatcher();
  if (dispatcher.HasThreadAccess()) {
    RTC_LOG(LS_ERROR) << "Cannot open the WebRTC video capture device from the "
                         "UI thread on UWP.";
    return {};
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

    auto createParams = std::make_shared<
        wrapper::impl::org::webRtc::VideoCapturerCreationParameters>();
    createParams->factory = g_winuwp_factory;
    createParams->name = name;
    createParams->id = id;
    createParams->enableMrc = enable_mrc;

    auto vcd = wrapper::impl::org::webRtc::VideoCapturer::create(createParams);

    if (vcd != nullptr) {
      auto nativeVcd = wrapper::impl::org::webRtc::VideoCapturer::toNative(vcd);

      RTC_LOG(LS_INFO) << "Using video capture device '"
                       << rtc::ToUtf8(devInfo.Name().c_str()).c_str()
                       << "' (id=" << rtc::ToUtf8(id).c_str() << ")";

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
  return {};
#else
  (void)enable_mrc;  // No MRC on non-UWP

  // List unique identifiers for all available devices if none requested, or
  // find the one requested.
  std::vector<std::string> device_ids;
  {
    std::unique_ptr<webrtc::VideoCaptureModule::DeviceInfo> info(
        webrtc::VideoCaptureFactory::CreateDeviceInfo());
    if (!info) {
      return {};
    }

    std::string video_device_id_str;
    if ((video_device_id != nullptr) && (video_device_id[0] != '\0')) {
      video_device_id_str.assign(video_device_id);
    }

    const int num_devices = info->NumberOfDevices();
    for (int i = 0; i < num_devices; ++i) {
      constexpr uint32_t kNameSize = webrtc::kVideoCaptureDeviceNameLength;
      constexpr uint32_t kIdSize = webrtc::kVideoCaptureUniqueNameLength;
      char name[kNameSize] = {};
      char id[kIdSize] = {};
      if (info->GetDeviceName(i, name, kNameSize, id, kIdSize) != -1) {
        if (video_device_id_str.empty()) {
          device_ids.push_back(id);
        } else if (video_device_id_str == id) {
          // Add only the device requested, which will fail the creation of the
          // video capture module if it cannot be used.
          device_ids.push_back(id);
          break;
        }
      }
    }
  }

  // Create the video capture module (VCM) from the first available device
  rtc::scoped_refptr<webrtc::VideoCaptureModule> capturer;
  for (const auto& id : device_ids) {
    capturer = webrtc::VideoCaptureFactory::Create(id.c_str());
    if (capturer) {
      break;
    }
  }
  return capturer;
#endif
}

VideoCapturer::VideoCapturer(rtc::scoped_refptr<webrtc::VideoCaptureModule> vcm)
    : vcm_(std::forward<rtc::scoped_refptr<webrtc::VideoCaptureModule>>(vcm)) {
  vcm_->RegisterCaptureDataCallback(this);
}

void VideoCapturer::Destroy() {
  if (!vcm_) {
    return;
  }
  vcm_->StopCapture();
  vcm_->DeRegisterCaptureDataCallback();
  vcm_ = nullptr;  // release
}

VideoCapturer::~VideoCapturer() {
  Destroy();
}

void VideoCapturer::AddOrUpdateSink(
    rtc::VideoSinkInterface<webrtc::VideoFrame>* sink,
    const rtc::VideoSinkWants& /*wants*/) {
  std::scoped_lock lock(sinks_mutex_);
  auto it = std::find(sinks_.begin(), sinks_.end(), sink);
  if (it != sinks_.end()) {
    sinks_.push_back(sink);
  }
}

void VideoCapturer::RemoveSink(
    rtc::VideoSinkInterface<webrtc::VideoFrame>* sink) {
  std::scoped_lock lock(sinks_mutex_);
  auto it = std::find(sinks_.begin(), sinks_.end(), sink);
  if (it != sinks_.end()) {
    sinks_.erase(it);
  }
}

void VideoCapturer::OnFrame(const webrtc::VideoFrame& frame) {
  std::scoped_lock lock(sinks_mutex_);
  for (auto&& sink : sinks_) {
    sink->OnFrame(frame);
  }
}

}  // namespace Microsoft::MixedReality::WebRTC
