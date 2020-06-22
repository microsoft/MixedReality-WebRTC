// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include "audio_frame.h"
#include "audio_track_source.h"
#include "device_audio_track_source_interop.h"
#include "mrs_errors.h"
#include "refptr.h"
#include "tracked_object.h"

#include "api/mediastreaminterface.h"

namespace Microsoft {
namespace MixedReality {
namespace WebRTC {

/// Audio track source generating frames from a local audio capture device
/// (microphone).
class DeviceAudioTrackSource : public AudioTrackSource {
 public:
  static ErrorOr<RefPtr<DeviceAudioTrackSource>> Create(
      const mrsLocalAudioDeviceInitConfig& init_config) noexcept;

 protected:
  DeviceAudioTrackSource(
      RefPtr<GlobalFactory> global_factory,
      rtc::scoped_refptr<webrtc::AudioSourceInterface> source) noexcept;
};

}  // namespace WebRTC
}  // namespace MixedReality
}  // namespace Microsoft
