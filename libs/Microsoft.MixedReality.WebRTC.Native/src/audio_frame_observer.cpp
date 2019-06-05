// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#include "pch.h"

#include "audio_frame_observer.h"

namespace mrtk {
namespace net {
namespace webrtc_impl {

void AudioFrameObserver::setCallback(
    AudioFrameReadyCallback callback) noexcept {
  auto lock = std::lock_guard{mutex_};
  callback_ = callback;
}

void AudioFrameObserver::OnData(const void* audio_data,
                                int bits_per_sample,
                                int sample_rate,
                                size_t number_of_channels,
                                size_t number_of_frames) noexcept {
  auto lock = std::lock_guard{mutex_};
  if (!callback_)
    return;
  callback_(audio_data, bits_per_sample, sample_rate, number_of_frames,
            number_of_frames);
}

}  // namespace webrtc_impl
}  // namespace net
}  // namespace mrtk
