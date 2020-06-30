// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include "mrs_errors.h"
#include "refptr.h"
#include "tracked_object.h"
#include "video_frame.h"
#include "video_track_source.h"
#include "device_video_track_source_interop.h"

#include "api/media_stream_interface.h"

namespace Microsoft {
namespace MixedReality {
namespace WebRTC {

/// Video track source generating frames from a local video capture device
/// (webcam).
class DeviceVideoTrackSource : public VideoTrackSource {
 public:
  static ErrorOr<RefPtr<DeviceVideoTrackSource>> Create(
      const mrsLocalVideoDeviceInitConfig& init_config) noexcept;

 protected:
  DeviceVideoTrackSource(
      RefPtr<GlobalFactory> global_factory,
      rtc::scoped_refptr<webrtc::VideoTrackSourceInterface> source) noexcept;
};

}  // namespace WebRTC
}  // namespace MixedReality
}  // namespace Microsoft
