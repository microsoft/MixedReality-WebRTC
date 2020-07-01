// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include "interop/global_factory.h"
#include "media/device_audio_track_source.h"
#include "audio_track_source_interop.h"

namespace Microsoft {
namespace MixedReality {
namespace WebRTC {

ErrorOr<RefPtr<DeviceAudioTrackSource>> DeviceAudioTrackSource::Create(
    const mrsLocalAudioDeviceInitConfig& init_config) noexcept {
  RefPtr<GlobalFactory> global_factory(GlobalFactory::InstancePtr());
  auto pc_factory = global_factory->GetPeerConnectionFactory();
  if (!pc_factory) {
    return Error(Result::kInvalidOperation);
  }

  // Create the audio track source
  cricket::AudioOptions options{};
  options.auto_gain_control = ToOptional(init_config.auto_gain_control_);
  rtc::scoped_refptr<webrtc::AudioSourceInterface> audio_source =
      pc_factory->CreateAudioSource(options);
  if (!audio_source) {
    RTC_LOG(LS_ERROR)
        << "Failed to create audio source from local audio capture device.";
    return Error(Result::kUnknownError);
  }

  // Create the wrapper
  RefPtr<DeviceAudioTrackSource> wrapper =
      new DeviceAudioTrackSource(global_factory, std::move(audio_source));
  if (!wrapper) {
    RTC_LOG(LS_ERROR) << "Failed to create device audio track source.";
    return Error(Result::kUnknownError);
  }
  return wrapper;
}

DeviceAudioTrackSource::DeviceAudioTrackSource(
    RefPtr<GlobalFactory> global_factory,
    rtc::scoped_refptr<webrtc::AudioSourceInterface> source) noexcept
    : AudioTrackSource(std::move(global_factory),
                       ObjectType::kDeviceAudioTrackSource,
                       std::move(source)) {}

}  // namespace WebRTC
}  // namespace MixedReality
}  // namespace Microsoft
