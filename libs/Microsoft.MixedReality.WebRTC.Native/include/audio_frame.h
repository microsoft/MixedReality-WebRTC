// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include <cstdint>

namespace Microsoft::MixedReality::WebRTC {

/// View over an existing buffer representing an audio frame, in the sense
/// of a single group of contiguous audio data.
struct AudioFrame {
  /// Pointer to the raw contiguous memory block holding the audio data in
  /// channel interleaved format. The length of the buffer is at least
  /// (|bits_per_sample_| / 8 * |channel_count_| * |sample_count_|) bytes.
  const void* data_;

  /// Number of bits per sample, often 8 or 16, for a single channel.
  std::uint32_t bits_per_sample_;

  /// Sampling rate, in Hertz (number of samples per second).
  std::uint32_t sampling_rate_hz_;

  /// Number of interleaved channels in a single audio sample.
  std::uint32_t channel_count_;

  /// Number of consecutive samples. The frame duration is given by the ratio
  /// |sample_count_| / |sampling_rate_hz_|.
  std::uint32_t sample_count_;
};

}  // namespace Microsoft::MixedReality::WebRTC
