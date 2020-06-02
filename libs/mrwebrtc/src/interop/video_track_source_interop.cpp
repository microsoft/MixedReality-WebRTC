// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// This is a precompiled header, it must be on its own, followed by a blank
// line, to prevent clang-format from reordering it with other headers.
#include "pch.h"

#include "callback.h"
#include "interop/global_factory.h"
#include "media/video_track_source.h"
#include "utils.h"
#include "video_track_source_interop.h"

using namespace Microsoft::MixedReality::WebRTC;

namespace {

class SimpleMediaConstraints : public webrtc::MediaConstraintsInterface {
 public:
  using webrtc::MediaConstraintsInterface::Constraint;
  using webrtc::MediaConstraintsInterface::Constraints;
  static Constraint MinWidth(uint32_t min_width) {
    return Constraint(webrtc::MediaConstraintsInterface::kMinWidth,
                      std::to_string(min_width));
  }
  static Constraint MaxWidth(uint32_t max_width) {
    return Constraint(webrtc::MediaConstraintsInterface::kMaxWidth,
                      std::to_string(max_width));
  }
  static Constraint MinHeight(uint32_t min_height) {
    return Constraint(webrtc::MediaConstraintsInterface::kMinHeight,
                      std::to_string(min_height));
  }
  static Constraint MaxHeight(uint32_t max_height) {
    return Constraint(webrtc::MediaConstraintsInterface::kMaxHeight,
                      std::to_string(max_height));
  }
  static Constraint MinFrameRate(double min_framerate) {
    // Note: kMinFrameRate is read back as an int
    const int min_int = (int)std::floor(min_framerate);
    return Constraint(webrtc::MediaConstraintsInterface::kMinFrameRate,
                      std::to_string(min_int));
  }
  static Constraint MaxFrameRate(double max_framerate) {
    // Note: kMinFrameRate is read back as an int
    const int max_int = (int)std::ceil(max_framerate);
    return Constraint(webrtc::MediaConstraintsInterface::kMaxFrameRate,
                      std::to_string(max_int));
  }
  const Constraints& GetMandatory() const override { return mandatory_; }
  const Constraints& GetOptional() const override { return optional_; }
  Constraints mandatory_;
  Constraints optional_;
};

#if defined(WINUWP)
using WebRtcFactoryPtr =
    std::shared_ptr<wrapper::impl::org::webRtc::WebRtcFactory>;
#endif  // defined(WINUWP)

/// Helper to open a video capture device.
mrsResult OpenVideoCaptureDevice(
    const mrsLocalVideoDeviceInitConfig& config,
    std::unique_ptr<cricket::VideoCapturer>& capturer_out) noexcept {
  capturer_out.reset();
#if defined(WINUWP)
  RefPtr<GlobalFactory> global_factory(GlobalFactory::InstancePtr());
  WebRtcFactoryPtr uwp_factory;
  {
    mrsResult res = global_factory->GetOrCreateWebRtcFactory(uwp_factory);
    if (res != Result::kSuccess) {
      RTC_LOG(LS_ERROR) << "Failed to initialize the UWP factory.";
      return res;
    }
  }

  // Check for calls from main UI thread; this is not supported (will deadlock)
  auto mw = winrt::Windows::ApplicationModel::Core::CoreApplication::MainView();
  auto cw = mw.CoreWindow();
  auto dispatcher = cw.Dispatcher();
  if (dispatcher.HasThreadAccess()) {
    return Result::kWrongThread;
  }

  // Get devices synchronously (wait for UI thread to retrieve them for us)
  rtc::Event blockOnDevicesEvent(true, false);
  auto vci = wrapper::impl::org::webRtc::VideoCapturer::getDevices();
  vci->thenClosure([&blockOnDevicesEvent] { blockOnDevicesEvent.Set(); });
  blockOnDevicesEvent.Wait(rtc::Event::kForever);
  auto deviceList = vci->value();

  std::wstring video_device_id_str;
  if (!IsStringNullOrEmpty(config.video_device_id)) {
    video_device_id_str =
        rtc::ToUtf16(config.video_device_id, strlen(config.video_device_id));
  }

  for (auto&& vdi : *deviceList) {
    auto devInfo =
        wrapper::impl::org::webRtc::VideoDeviceInfo::toNative_winrt(vdi);
    const winrt::hstring& id = devInfo.Id();
    if (!video_device_id_str.empty() && (video_device_id_str != id)) {
      RTC_LOG(LS_VERBOSE) << "Skipping device ID " << rtc::ToUtf8(id.c_str())
                          << " not matching requested device.";
      continue;
    }

    auto createParams =
        wrapper::org::webRtc::VideoCapturerCreationParameters::wrapper_create();
    createParams->factory = uwp_factory;
    createParams->name = devInfo.Name().c_str();
    createParams->id = id.c_str();
    if (config.video_profile_id) {
      createParams->videoProfileId = config.video_profile_id;
    }
    createParams->videoProfileKind =
        (wrapper::org::webRtc::VideoProfileKind)config.video_profile_kind;
    createParams->enableMrc = (config.enable_mrc != mrsBool::kFalse);
    createParams->enableMrcRecordingIndicator =
        (config.enable_mrc_recording_indicator != mrsBool::kFalse);
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

      capturer_out = std::move(nativeVcd);
      return Result::kSuccess;
    }
  }
  RTC_LOG(LS_ERROR) << "Failed to find a local video capture device matching "
                       "the capture format constraints. None of the "
                    << deviceList->size()
                    << " devices tested had a compatible capture format.";
  return Result::kNotFound;
#else
  // List all available video capture devices, or match by ID if specified.
  std::vector<std::string> device_names;
  {
    std::unique_ptr<webrtc::VideoCaptureModule::DeviceInfo> info(
        webrtc::VideoCaptureFactory::CreateDeviceInfo());
    if (!info) {
      return Result::kUnknownError;
    }

    const int num_devices = info->NumberOfDevices();
    constexpr uint32_t kSize = 256;
    if (!IsStringNullOrEmpty(config.video_device_id)) {
      // Look for the one specific device the user asked for
      std::string video_device_id_str = config.video_device_id;
      for (int i = 0; i < num_devices; ++i) {
        char name[kSize] = {};
        char id[kSize] = {};
        if (info->GetDeviceName(i, name, kSize, id, kSize) != -1) {
          if (video_device_id_str == id) {
            // Keep only the device the user selected
            device_names.push_back(name);
            break;
          }
        }
      }
      if (device_names.empty()) {
        RTC_LOG(LS_ERROR)
            << "Could not find video capture device by unique ID: "
            << config.video_device_id;
        return Result::kNotFound;
      }
    } else {
      // List all available devices
      for (int i = 0; i < num_devices; ++i) {
        char name[kSize] = {};
        char id[kSize] = {};
        if (info->GetDeviceName(i, name, kSize, id, kSize) != -1) {
          device_names.push_back(name);
        }
      }
      if (device_names.empty()) {
        RTC_LOG(LS_ERROR) << "Could not find any video catpure device.";
        return Result::kNotFound;
      }
    }
  }

  // Open the specified capture device, or the first one available if none
  // specified.
  cricket::WebRtcVideoDeviceCapturerFactory factory;
  for (const auto& name : device_names) {
    // cricket::Device identifies devices by (friendly) name, not unique ID
    capturer_out = factory.Create(cricket::Device(name, 0));
    if (capturer_out) {
      return Result::kSuccess;
    }
  }
  RTC_LOG(LS_ERROR) << "Failed to open any video capture device (tried "
                    << device_names.size() << " devices).";
  return Result::kUnknownError;
#endif
}

}  // namespace

void MRS_CALL
mrsVideoTrackSourceAddRef(mrsVideoTrackSourceHandle handle) noexcept {
  if (auto source = static_cast<VideoTrackSource*>(handle)) {
    source->AddRef();
  } else {
    RTC_LOG(LS_WARNING)
        << "Trying to add reference to NULL VideoTrackSource object.";
  }
}

void MRS_CALL
mrsVideoTrackSourceRemoveRef(mrsVideoTrackSourceHandle handle) noexcept {
  if (auto source = static_cast<VideoTrackSource*>(handle)) {
    source->RemoveRef();
  } else {
    RTC_LOG(LS_WARNING) << "Trying to remove reference from NULL "
                           "VideoTrackSource object.";
  }
}

void MRS_CALL mrsVideoTrackSourceSetName(mrsVideoTrackSourceHandle handle,
                                         const char* name) noexcept {
  if (auto source = static_cast<VideoTrackSource*>(handle)) {
    source->SetName(name);
  }
}

mrsResult MRS_CALL mrsVideoTrackSourceGetName(mrsVideoTrackSourceHandle handle,
                                              char* buffer,
                                              uint64_t* buffer_size) noexcept {
  auto source = static_cast<VideoTrackSource*>(handle);
  if (!source) {
    RTC_LOG(LS_ERROR) << "Invalid handle to audio track source.";
    return mrsResult::kInvalidNativeHandle;
  }
  if (!buffer) {
    RTC_LOG(LS_ERROR) << "Invalid NULL string buffer.";
    return mrsResult::kInvalidParameter;
  }
  if (!buffer_size) {
    RTC_LOG(LS_ERROR) << "Invalid NULL string buffer size reference.";
    return mrsResult::kInvalidParameter;
  }
  const std::string name = source->GetName();
  const size_t capacity = *buffer_size;
  const size_t size_with_terminator = name.size() + 1;
  // Always assign size, even if buffer too small
  *buffer_size = size_with_terminator;
  if (size_with_terminator <= capacity) {
    memcpy(buffer, name.c_str(), size_with_terminator);
    return mrsResult::kSuccess;
  }
  return mrsResult::kBufferTooSmall;
}

MRS_API void MRS_CALL
mrsVideoTrackSourceSetUserData(mrsVideoTrackSourceHandle handle,
                               void* user_data) noexcept {
  if (auto source = static_cast<VideoTrackSource*>(handle)) {
    source->SetUserData(user_data);
  }
}

MRS_API void* MRS_CALL
mrsVideoTrackSourceGetUserData(mrsVideoTrackSourceHandle handle) noexcept {
  if (auto source = static_cast<VideoTrackSource*>(handle)) {
    return source->GetUserData();
  }
  return nullptr;
}

mrsResult MRS_CALL mrsVideoTrackSourceCreateFromDevice(
    const mrsLocalVideoDeviceInitConfig* init_config,
    mrsVideoTrackSourceHandle* source_handle_out) noexcept {
  if (!source_handle_out) {
    RTC_LOG(LS_ERROR) << "Invalid NULL video track source handle.";
    return Result::kInvalidParameter;
  }
  *source_handle_out = nullptr;

  RefPtr<GlobalFactory> global_factory(GlobalFactory::InstancePtr());
  auto pc_factory = global_factory->GetPeerConnectionFactory();
  if (!pc_factory) {
    return Result::kInvalidOperation;
  }

  // Open the video capture device
  std::unique_ptr<cricket::VideoCapturer> video_capturer;
  auto res = OpenVideoCaptureDevice(*init_config, video_capturer);
  if (res != Result::kSuccess) {
    RTC_LOG(LS_ERROR) << "Failed to open video capture device.";
    return res;
  }
  RTC_CHECK(video_capturer.get());

  // Apply the same constraints used for opening the video capturer
  auto videoConstraints = std::make_unique<SimpleMediaConstraints>();
  if (init_config->width > 0) {
    videoConstraints->mandatory_.push_back(
        SimpleMediaConstraints::MinWidth(init_config->width));
    videoConstraints->mandatory_.push_back(
        SimpleMediaConstraints::MaxWidth(init_config->width));
  }
  if (init_config->height > 0) {
    videoConstraints->mandatory_.push_back(
        SimpleMediaConstraints::MinHeight(init_config->height));
    videoConstraints->mandatory_.push_back(
        SimpleMediaConstraints::MaxHeight(init_config->height));
  }
  if (init_config->framerate > 0) {
    videoConstraints->mandatory_.push_back(
        SimpleMediaConstraints::MinFrameRate(init_config->framerate));
    videoConstraints->mandatory_.push_back(
        SimpleMediaConstraints::MaxFrameRate(init_config->framerate));
  }

  // Create the video track source
  rtc::scoped_refptr<webrtc::VideoTrackSourceInterface> video_source =
      pc_factory->CreateVideoSource(std::move(video_capturer),
                                    videoConstraints.get());
  if (!video_source) {
    return Result::kUnknownError;
  }

  // Create the wrapper
  RefPtr<VideoTrackSource> wrapper =
      new VideoTrackSource(global_factory, std::move(video_source));
  if (!wrapper) {
    RTC_LOG(LS_ERROR) << "Failed to create audio track source.";
    return Result::kUnknownError;
  }
  *source_handle_out = wrapper.release();
  return Result::kSuccess;
}

void MRS_CALL mrsVideoTrackSourceRegisterFrameCallback(
    mrsVideoTrackSourceHandle source_handle,
    mrsI420AVideoFrameCallback callback,
    void* user_data) noexcept {
  if (auto source = static_cast<VideoTrackSource*>(source_handle)) {
    source->SetCallback(I420AFrameReadyCallback{callback, user_data});
  }
}
