// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#pragma once

#include <mutex>

#include "api/mediastreaminterface.h"

#include "callback.h"

namespace Microsoft::MixedReality::WebRTC {

/// Callback fired on newly available audio frame.
using AudioFrameReadyCallback =
    Callback<const void*, const int, const int, const int, const int>;

/// Video frame observer to get notified of newly available video frames.
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
  AudioFrameReadyCallback callback_;
  std::mutex mutex_;
};

}  // namespace Microsoft::MixedReality::WebRTC
