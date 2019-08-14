// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

// This is a precompiled header, it must be on its own, followed by a blank
// line, to prevent clang-format from reordering it with other headers.
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
  callback_(audio_data, bits_per_sample, sample_rate,
            static_cast<int>(number_of_channels),
            static_cast<int>(number_of_frames));
}

}  // namespace Microsoft::MixedReality::WebRTC
