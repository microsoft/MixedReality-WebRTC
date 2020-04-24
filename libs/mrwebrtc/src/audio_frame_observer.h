// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include <mutex>

#include "api/media_stream_interface.h"

#include "audio_frame.h"
#include "callback.h"

namespace Microsoft {
namespace MixedReality {
namespace WebRTC {

/// Callback fired on newly available audio frame.
using AudioFrameReadyCallback = Callback<const AudioFrame&>;

/// Audio frame observer to get notified of newly available audio frames.
class AudioFrameObserver : public webrtc::AudioTrackSinkInterface {
 public:
  void SetCallback(AudioFrameReadyCallback callback) noexcept;

 protected:
  // AudioTrackSinkInterface interface
  void OnData(const void* audio_data,
              int bits_per_sample,
              int sample_rate,
              size_t number_of_channels,
              size_t number_of_frames) noexcept override;

 private:
  AudioFrameReadyCallback callback_ RTC_GUARDED_BY(mutex_);
  std::mutex mutex_;
};

}  // namespace WebRTC
}  // namespace MixedReality
}  // namespace Microsoft
