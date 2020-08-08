// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include "device_video_track_source_interop.h"
#include "mrs_errors.h"
#include "refptr.h"
#include "tracked_object.h"
#include "video_frame.h"
#include "video_track_source.h"

#include "api/mediastreaminterface.h"

namespace Microsoft {
namespace MixedReality {
namespace WebRTC {

/// Video track source generating frames from a local video capture device
/// (webcam).
class DeviceVideoTrackSource : public VideoTrackSource {
 public:
  static ErrorOr<RefPtr<DeviceVideoTrackSource>> Create(
      const mrsLocalVideoDeviceInitConfig& init_config) noexcept;

  static Error GetVideoCaptureDevices(
      Callback<const mrsVideoCaptureDeviceInfo*> enum_callback,
      Callback<mrsResult> end_callback) noexcept;
  static Error GetVideoProfiles(
      absl::string_view device_id,
      mrsVideoProfileKind profile_kind,
      Callback<const mrsVideoProfileInfo*> enum_callback,
      Callback<mrsResult> end_callback) noexcept;
  static Error GetVideoCaptureFormats(
      absl::string_view device_id,
      absl::string_view profile_id,
      mrsVideoProfileKind profile_kind,
      Callback<const mrsVideoCaptureFormatInfo*> enum_callback,
      Callback<mrsResult> end_callback) noexcept;

 protected:
#if defined(MR_SHARING_ANDROID)

  DeviceVideoTrackSource(
      RefPtr<GlobalFactory> global_factory,
      rtc::scoped_refptr<webrtc::VideoTrackSourceInterface> source,
      jobject java_video_capturer) noexcept;
  ~DeviceVideoTrackSource();

  /// Global reference to the Java video capturer (org.webrtc.VideoCapturer)
  /// object.
  jobject java_video_capturer_{nullptr};

#else  // defined(MR_SHARING_ANDROID)

  DeviceVideoTrackSource(
      RefPtr<GlobalFactory> global_factory,
      rtc::scoped_refptr<webrtc::VideoTrackSourceInterface> source) noexcept;

#endif  // defined(MR_SHARING_ANDROID)
};

}  // namespace WebRTC
}  // namespace MixedReality
}  // namespace Microsoft
