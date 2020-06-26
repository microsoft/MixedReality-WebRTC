// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include "mrs_errors.h"
#include "refptr.h"
#include "tracked_object.h"
#include "video_frame.h"
#include "video_track_source.h"
#include "device_video_track_source_interop.h"

#include "api/mediastreaminterface.h"

namespace Microsoft {
namespace MixedReality {
namespace WebRTC {

/// Video capture device info.
struct VideoCaptureDeviceInfo {
  std::string id;
  std::string name;
};

/// Video capture format info.
struct VideoCaptureFormatInfo {
  int width;
  int height;
  int framerate;
  uint32_t fourcc;
};

/// Video track source generating frames from a local video capture device
/// (webcam).
class DeviceVideoTrackSource : public VideoTrackSource {
 public:
  static ErrorOr<RefPtr<DeviceVideoTrackSource>> Create(
      const mrsLocalVideoDeviceInitConfig& init_config) noexcept;

  static std::vector<VideoCaptureDeviceInfo> GetVideoCaptureDevices() noexcept;
  static std::vector<VideoCaptureFormatInfo> GetVideoCaptureFormats(absl::string_view device_id) noexcept;

 protected:
  DeviceVideoTrackSource(
      RefPtr<GlobalFactory> global_factory,
      rtc::scoped_refptr<webrtc::VideoTrackSourceInterface> source
#if defined(MR_SHARING_ANDROID)
      ,
      jobject java_video_capturer
#endif  // defined(MR_SHARING_ANDROID)
      ) noexcept;
  ~DeviceVideoTrackSource();

#if defined(MR_SHARING_ANDROID)

  /// Global reference to the Java video capturer (org.webrtc.VideoCapturer)
  /// object.
  jobject java_video_capturer_{nullptr};

#endif  // defined(MR_SHARING_ANDROID)
};

}  // namespace WebRTC
}  // namespace MixedReality
}  // namespace Microsoft
