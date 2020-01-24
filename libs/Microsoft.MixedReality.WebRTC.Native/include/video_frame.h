// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include <cstdint>

namespace Microsoft::MixedReality::WebRTC {

/// View over an existing buffer representing a video frame encoded in I420
/// format with an extra Alpha plane for opacity.
struct I420AVideoFrame {
  /// Width of the video frame, in pixels.
  std::uint32_t width_;

  /// Height of the video frame, in pixels.
  std::uint32_t height_;

  /// Pointer to the raw contiguous memory block holding the Y plane data.
  /// The size of the buffer is at least (|ystride_| * |height_|) bytes.
  const void* ydata_;

  /// Pointer to the raw contiguous memory block holding the U plane data.
  /// The size of the buffer is at least (|ustride_| * (|height_| + 1) / 2)
  /// bytes, due to chroma downsampling compared to the Y plane.
  const void* udata_;

  /// Pointer to the raw contiguous memory block holding the V plane data.
  /// The size of the buffer is at least (|vstride_| * (|height_| + 1) / 2)
  /// bytes, due to chroma downsampling compared to the Y plane.
  const void* vdata_;

  /// Pointer to the raw contiguous memory block holding the alpha plane data,
  /// if any. This can optionally be NULL if the frame doesn't have an Alpha
  /// component.
  /// The size of the buffer is at least (|astride_| * |height_|) bytes.
  const void* adata_;

  /// Stride in bytes between two consecutive rows in the Y plane buffer.
  /// This is always greater than or equal to |width_|.
  std::int32_t ystride_;

  /// Stride in bytes between two consecutive rows in the U plane buffer.
  /// This is always greater than or equal to ((|width_| + 1) / 2).
  std::int32_t ustride_;

  /// Stride in bytes between two consecutive rows in the V plane buffer.
  /// This is always greater than or equal to ((|width_| + 1) / 2).
  std::int32_t vstride_;

  /// Stride in bytes between two consecutive rows in the A plane buffer.
  /// This is ignored if there is no A plane (|adata_| is NULL).
  /// Otherwise, this is always greater than or equal to |width_|.
  std::int32_t astride_;
};

/// View over an existing buffer representing a video frame encoded in ARGB
/// 32-bit-per-pixel format, in little endian order (B first, A last).
struct Argb32VideoFrame {
  /// Width of the video frame, in pixels.
  std::uint32_t width_;

  /// Height of the video frame, in pixels.
  std::uint32_t height_;

  /// Pointer to the raw contiguous memory block holding the video frame data.
  /// The size of the buffer is at least (|stride_| * |height_|) bytes.
  const void* argb32_data_;

  /// Stride in bytes between two consecutive rows in the ARGB buffer.
  /// This is always greater than or equal to |width_|.
  std::int32_t stride_;
};

}  // namespace Microsoft::MixedReality::WebRTC
