// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#include "pch.h"

#include "audio_frame_observer.h"

namespace Microsoft::MixedReality::WebRTC {

void AudioFrameObserver::SetCallback(
    AudioFrameReadyCallback callback) noexcept {
  auto lock = std::lock_guard{mutex_};
  callback_ = std::move(callback);
}

void AudioFrameObserver::OnData(const void* audio_data,
                                int bits_per_sample,
                                int sample_rate,
                                size_t number_of_channels,
                                size_t number_of_frames) noexcept {
  auto lock = std::lock_guard{mutex_};
  if (!callback_)
    return;
  callback_(audio_data, static_cast<uint32_t>(bits_per_sample),
            static_cast<uint32_t>(sample_rate),
            static_cast<uint32_t>(number_of_channels),
            static_cast<uint32_t>(number_of_frames));
}

}  // namespace Microsoft::MixedReality::WebRTC
