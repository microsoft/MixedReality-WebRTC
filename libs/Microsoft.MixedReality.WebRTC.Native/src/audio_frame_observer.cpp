// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#include "pch.h"

#include "audio_frame_observer.h"

namespace Microsoft::MixedReality::WebRTC {

void AudioFrameObserver::SetCallback(
    AudioFrameReadyCallback callback) noexcept {
  auto lock = std::scoped_lock{mutex_};
  callback_ = std::move(callback);
}

void AudioFrameObserver::OnData(const void* audio_data,
                                int bits_per_sample,
                                int sample_rate,
                                size_t number_of_channels,
                                size_t number_of_frames) noexcept {
  auto lock = std::scoped_lock{mutex_};
  if (!callback_) {
    return;
  }
  AudioFrame frame;
  frame.data_ = audio_data;
  frame.bits_per_sample_ = static_cast<uint32_t>(bits_per_sample);
  frame.sampling_rate_hz_ = static_cast<uint32_t>(sample_rate);
  frame.channel_count_ = static_cast<uint32_t>(number_of_channels);
  frame.sample_count_ = static_cast<uint32_t>(number_of_frames);
  callback_(frame);
}

}  // namespace Microsoft::MixedReality::WebRTC
