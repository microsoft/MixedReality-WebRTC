// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include "interop/global_factory.h"
#include "media/device_video_track_source.h"
#include "video_track_source_interop.h"

namespace {

using namespace Microsoft::MixedReality::WebRTC;
/// Video track source producing video frames using a local video capture device
/// accessed via the built-in video capture module implementation.
class BuiltinVideoCaptureDeviceTrackSource
    : public webrtc::VideoTrackSource,
      public rtc::VideoSinkInterface<webrtc::VideoFrame> {
 public:
  static mrsResult Create(
      const mrsLocalVideoDeviceInitConfig& config,
      rtc::scoped_refptr<BuiltinVideoCaptureDeviceTrackSource>& source_out) {
    std::unique_ptr<webrtc::VideoCaptureModule::DeviceInfo> info(
        webrtc::VideoCaptureFactory::CreateDeviceInfo());
    if (!info) {
      return Result::kUnknownError;
    }

    // List all available video capture devices, filtering by unique ID if
    // the user provided a non-empty unique device ID.
    std::vector<std::string> filtered_device_ids;
    {
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
              filtered_device_ids.push_back(id);
              break;
            }
          }
        }
        if (filtered_device_ids.empty()) {
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
            filtered_device_ids.push_back(id);
          }
        }
        if (filtered_device_ids.empty()) {
          RTC_LOG(LS_ERROR) << "Could not find any video catpure device.";
          return Result::kNotFound;
        }
      }
    }

    // Further filter devices based on capabilities, if any was requested
    rtc::scoped_refptr<webrtc::VideoCaptureModule> vcm;
    webrtc::VideoCaptureCapability capability;
    if ((config.width > 0) || (config.height > 0) || (config.framerate > 0.0)) {
      for (const auto& device_id_utf8 : filtered_device_ids) {
        const int num_capabilities =
            info->NumberOfCapabilities(device_id_utf8.c_str());
        for (int icap = 0; icap < num_capabilities; ++icap) {
          if (info->GetCapability(device_id_utf8.c_str(), icap, capability) !=
              0) {
            continue;
          }
          if ((config.width > 0) && (capability.width != (int)config.width)) {
            continue;
          }
          if ((config.height > 0) &&
              (capability.height != (int)config.height)) {
            continue;
          }
          const int config_fps = static_cast<int>(config.framerate + 0.5);
          if ((config.framerate > 0.0) && (capability.maxFPS != config_fps)) {
            continue;
          }

          // Found matching device with capability, try to open it
          vcm = webrtc::VideoCaptureFactory::Create(device_id_utf8.c_str());
          if (vcm) {
            break;
          }
        }
      }
    } else {
      // Otherwise if no capability was requested open the first available
      // capture device.
      for (const auto& device_id_utf8 : filtered_device_ids) {
        vcm = webrtc::VideoCaptureFactory::Create(device_id_utf8.c_str());
        if (!vcm) {
          continue;
        }

        // Get the first capability, since none was requested
        info->GetCapability(device_id_utf8.c_str(), 0, capability);
        break;
      }
    }

    if (vcm) {
      // Create the video track source wrapping the capture module
      source_out =
          new rtc::RefCountedObject<BuiltinVideoCaptureDeviceTrackSource>(
              std::move(vcm));
      if (!source_out) {
        return Result::kUnknownError;
      }

      // Start capturing. All WebRTC track sources start in the capturing state
      // by convention.
      auto res = source_out->Initialize(std::move(capability));
      if (res != Result::kSuccess) {
        source_out = nullptr;
        return res;
      }
      return Result::kSuccess;
    }

    RTC_LOG(LS_ERROR) << "Failed to open any video capture device (tried "
                      << filtered_device_ids.size() << " devices).";
    return Result::kInvalidOperation;
  }

  ~BuiltinVideoCaptureDeviceTrackSource() override { Destroy(); }

  void AddOrUpdateSink(rtc::VideoSinkInterface<webrtc::VideoFrame>* sink,
                       const rtc::VideoSinkWants& wants) override {
    broadcaster_.AddOrUpdateSink(sink, wants);
  }

  void RemoveSink(rtc::VideoSinkInterface<webrtc::VideoFrame>* sink) override {
    broadcaster_.RemoveSink(sink);
  }

  void OnFrame(const webrtc::VideoFrame& frame) override {
    broadcaster_.OnFrame(frame);
  }

 protected:
  explicit BuiltinVideoCaptureDeviceTrackSource(
      rtc::scoped_refptr<webrtc::VideoCaptureModule> vcm)
      : webrtc::VideoTrackSource(/*remote=*/false), vcm_(std::move(vcm)) {
    vcm_->RegisterCaptureDataCallback(this);
  }

  mrsResult Initialize(webrtc::VideoCaptureCapability capability) {
    if (vcm_->StartCapture(capability) != 0) {
      Destroy();
      return Result::kUnknownError;
    }
    capability_ = std::move(capability);
    return Result::kSuccess;
  }

  void Destroy() {
    if (!vcm_) {
      return;
    }
    vcm_->StopCapture();
    vcm_->DeRegisterCaptureDataCallback();
    vcm_ = nullptr;
  }

 private:
  rtc::VideoSourceInterface<webrtc::VideoFrame>* source() override {
    return this;
  }
  rtc::scoped_refptr<webrtc::VideoCaptureModule> vcm_;
  webrtc::VideoCaptureCapability capability_;
  rtc::VideoBroadcaster broadcaster_;
};

}  // namespace

namespace Microsoft {
namespace MixedReality {
namespace WebRTC {

ErrorOr<RefPtr<DeviceVideoTrackSource>> DeviceVideoTrackSource::Create(
    const mrsLocalVideoDeviceInitConfig& init_config) noexcept {
  RefPtr<GlobalFactory> global_factory(GlobalFactory::InstancePtr());
  auto pc_factory = global_factory->GetPeerConnectionFactory();
  if (!pc_factory) {
    return Error(Result::kInvalidOperation);
  }

  // Create the video track source
  rtc::scoped_refptr<BuiltinVideoCaptureDeviceTrackSource> video_source;
  {
    // Ensure this call runs on the signaling thread because e.g. the DirectShow
    // capture will start capture from the calling thread and expects it to be
    // the signaling thread.
    rtc::Thread* const signaling_thread = global_factory->GetSignalingThread();
    mrsResult res = signaling_thread->Invoke<mrsResult>(
        RTC_FROM_HERE, [&init_config, &video_source] {
          return BuiltinVideoCaptureDeviceTrackSource::Create(init_config,
                                                              video_source);
        });
    if (res != Result::kSuccess) {
      return Error(res);
    }
  }
  if (!video_source) {
    return Error(Result::kUnknownError);
  }

  // Create the wrapper
  RefPtr<DeviceVideoTrackSource> wrapper =
      new DeviceVideoTrackSource(global_factory, std::move(video_source));
  if (!wrapper) {
    RTC_LOG(LS_ERROR) << "Failed to create device video track source.";
    return Error(Result::kUnknownError);
  }
  return wrapper;
}

DeviceVideoTrackSource::DeviceVideoTrackSource(
    RefPtr<GlobalFactory> global_factory,
    rtc::scoped_refptr<webrtc::VideoTrackSourceInterface> source) noexcept
    : VideoTrackSource(std::move(global_factory),
                       ObjectType::kDeviceVideoTrackSource,
                       std::move(source)) {}

}  // namespace WebRTC
}  // namespace MixedReality
}  // namespace Microsoft
