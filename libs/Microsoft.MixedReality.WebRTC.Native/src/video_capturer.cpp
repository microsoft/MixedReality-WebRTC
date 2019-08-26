// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

// This is a precompiled header, it must be on its own, followed by a blank
// line, to prevent clang-format from reordering it with other headers.
#include "pch.h"

#include "api.h"
#include "video_capturer.h"

namespace {

// Similar to webrtc::MethodCall0<> but with free function (see proxy.h)
class AsyncCaller : public rtc::MessageHandler {
 public:
  AsyncCaller(std::function<void()>&& func);
  ~AsyncCaller() override;
  void InvokeAndWait(const rtc::Location& posted_from, rtc::Thread* t);

 private:
  void OnMessage(rtc::Message*) override;
  rtc::Event ev_;
  std::function<void()> func_;
};

AsyncCaller::AsyncCaller(std::function<void()>&& func)
    : func_(std::forward<std::function<void()>>(func)) {}

AsyncCaller::~AsyncCaller() = default;

void AsyncCaller::InvokeAndWait(const rtc::Location& posted_from,
                                rtc::Thread* t) {
  if (t->IsCurrent()) {
    func_();
  } else {
    t->Post(posted_from, this, 0);
    ev_.Wait(rtc::Event::kForever);
  }
}

void AsyncCaller::OnMessage(rtc::Message*) {
  func_();
  ev_.Set();
}

}  // namespace

namespace Microsoft::MixedReality::WebRTC {

rtc::scoped_refptr<webrtc::VideoTrackSourceInterface> OpenVideoCaptureDevice(
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

  std::wstring video_device_id_str;
  if ((video_device_id != nullptr) && (video_device_id[0] != '\0')) {
    video_device_id_str =
        rtc::ToUtf16(video_device_id, strlen(video_device_id));
  }

  // Get devices synchronously (wait for UI thread to retrieve them for us)
  auto deviceList =
      winrt::Windows::Devices::Enumeration::DeviceInformation::FindAllAsync(
          winrt::Windows::Devices::Enumeration::DeviceClass::VideoCapture)
          .get();  // blocking call

  for (auto&& devInfo : deviceList) {
    auto name = devInfo.Name().c_str();
    if (!video_device_id_str.empty() && (video_device_id_str != name)) {
      continue;
    }
    auto id = devInfo.Id().c_str();

    //< TODO - select supported resolution!
    auto format = wrapper::org::webRtc::VideoFormat::wrapper_create();
    format->wrapper_init_org_webRtc_VideoFormat();
    format->set_width(640);
    format->set_height(480);
    format->set_interval(
        std::chrono::nanoseconds{(long long)(1000 * 1000 / 30.0)});
    format->set_fourcc(FOURCC('N', 'V', '1', '2'));

    auto createParams = std::make_shared<
        wrapper::impl::org::webRtc::VideoCapturerCreationParameters>();
    createParams->factory = GetOrCreateUWPFactory();
    createParams->name = name;
    createParams->id = id;
    createParams->enableMrc = enable_mrc;
    createParams->format = format;

    auto vcd = wrapper::impl::org::webRtc::VideoCapturer::create(createParams);
    if (vcd != nullptr) {
      rtc::scoped_refptr<rtc::AdaptedVideoTrackSource> nativeVcd =
          wrapper::impl::org::webRtc::VideoCapturer::toNative(vcd);

      RTC_LOG(LS_INFO) << "Using video capture device '"
                       << rtc::ToUtf8(devInfo.Name().c_str()).c_str()
                       << "' (id=" << rtc::ToUtf8(id).c_str() << ")";

      // if (auto supportedFormats = nativeVcd->GetSupportedFormats()) {
      //  RTC_LOG(LS_INFO) << "Supported video formats:";
      //  for (auto&& format : *supportedFormats) {
      //    auto str = format.ToString();
      //    RTC_LOG(LS_INFO) << "- " << str.c_str();
      //  }
      //}

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

  // Create the video capture module (VCM) from the first available device.
  // This needs to be on the WebRTC signaling thread, as the destruction code
  // will check that it is called on the same thread so we don't want to be
  // called from any random thread we don't control.
  rtc::scoped_refptr<webrtc::VideoCaptureModule> video_capture_module;
  {
    const std::unique_ptr<rtc::Thread>& signalingThread = GetSignalingThread();
    AsyncCaller handler([&video_capture_module, &device_ids]() {
      for (const auto& id : device_ids) {
        video_capture_module = webrtc::VideoCaptureFactory::Create(id.c_str());
        if (video_capture_module) {
          break;
        }
      }
    });
    handler.InvokeAndWait(RTC_FROM_HERE, signalingThread.get());
  }
  if (!video_capture_module) {
    return {};
  }

  auto video_capturer =
      std::make_unique<VideoCapturer>(std::move(video_capture_module));
  if (!video_capturer) {
    return {};
  }

  rtc::scoped_refptr<CapturerTrackSource> video_source =
      CapturerTrackSource::Create(std::move(video_capturer));
  if (!video_source) {
    return {};
  }

  return video_source;
#endif
}

#if !defined(WINUWP)

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
  if (it == sinks_.end()) {
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

#endif  // !defined(WINUWP)

}  // namespace Microsoft::MixedReality::WebRTC
