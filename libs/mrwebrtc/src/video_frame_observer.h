// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include <mutex>

#include "api/video/video_frame.h"
#include "api/video/video_sink_interface.h"

#include "callback.h"
#include "video_frame.h"

#include "rtc_base/memory/aligned_malloc.h"

namespace Microsoft {
namespace MixedReality {
namespace WebRTC {

/// Callback fired on newly available video frame, encoded as I420.
using I420AFrameReadyCallback = Callback<const I420AVideoFrame&>;

/// Callback fired on newly available video frame, encoded as ARGB.
using Argb32FrameReadyCallback = Callback<const Argb32VideoFrame&>;

/// Helper function to calculate the minimum size of an ARGB32 frame given its
/// dimensions in pixels.
constexpr inline size_t Argb32FrameSize(int width, int height) {
  return (static_cast<size_t>(height) * width) * 4;
}

/// Plain 32-bit ARGB buffer in standard memory.
class ArgbBuffer : public webrtc::VideoFrameBuffer {
 public:
  /// Create a new buffer with enough storage for a frame with the given width
  /// and height in pixels.
  static inline rtc::scoped_refptr<ArgbBuffer> Create(int width, int height) {
    return new rtc::RefCountedObject<ArgbBuffer>(width, height, width * 4);
  }

  /// Create a new buffer with enough storage for a frame with the given width
  /// and height in pixels, with explicit stride.
  static inline rtc::scoped_refptr<ArgbBuffer> Create(int width,
                                                      int height,
                                                      int stride) {
    RTC_CHECK_GE(stride, width * 4);
    return new rtc::RefCountedObject<ArgbBuffer>(width, height, stride);
  }

  /// Recycle the current buffer for a frame which fits in it (frame size less
  /// than or equal to buffer storage size) but has different dimensions. This
  /// recalculate the strides without performing any allocation.
  void Recycle(int width, int height) noexcept {
    Recycle(width, height, width * 4);
  }

  /// Recycle the current buffer for a frame which fits in it (frame size less
  /// than or equal to buffer storage size) but has different dimensions. This
  /// recalculate the strides without performing any allocation.
  void Recycle(int width, int height, int stride) noexcept {
    RTC_CHECK_GE(stride, width * 4);
    RTC_CHECK(static_cast<size_t>(height) * stride <= Size());
    width_ = width;
    height_ = height;
    stride_ = stride;
  }

  //// Create a new buffer and copy the pixel data.
  // static rtc::scoped_refptr<ArgbBuffer> Copy(const I010BufferInterface&
  // buffer);

  //// Convert and put I420 buffer into a new buffer.
  // static rtc::scoped_refptr<ArgbBuffer> Copy(const I420BufferInterface&
  // buffer);

  //// Return a rotated copy of |src|.
  // static rtc::scoped_refptr<ArgbBuffer> Rotate(const I010BufferInterface&
  // src,
  //                                             VideoRotation rotation);

  // VideoFrameBuffer implementation.

  inline Type type() const override { return VideoFrameBuffer::Type::kNative; }
  inline int width() const override { return width_; }
  inline int height() const override { return height_; }
  rtc::scoped_refptr<webrtc::I420BufferInterface> ToI420() override;

  inline uint8_t* Data() { return data_.get(); }
  inline const uint8_t* Data() const { return data_.get(); }

  /// Row stride, in bytes.
  inline constexpr int Stride() const { return stride_; }

  /// Total buffer size, in bytes.
  inline constexpr size_t Size() const {
    return static_cast<size_t>(height_) * stride_;
  }

 protected:
  ArgbBuffer(int width, int height, int stride) noexcept;
  ~ArgbBuffer() override = default;

 private:
  /// Frame width, in pixels.
  int width_;

  /// Frame height, in pixels.
  int height_;

  /// Row stride, in pixels. This is always >= (4 * width_).
  int stride_;

  /// Raw buffer of ARGB32 data for the frame.
  const std::unique_ptr<uint8_t, webrtc::AlignedFreeDeleter> data_;
};

/// Video frame observer to get notified of newly available video frames.
class VideoFrameObserver : public rtc::VideoSinkInterface<webrtc::VideoFrame> {
 public:
  /// Register a callback to get notified on frame available,
  /// and received that frame as a I420-encoded buffer.
  /// This is not exclusive and can be used along another ARGB callback.
  void SetCallback(I420AFrameReadyCallback callback) noexcept;

  /// Register a callback to get notified on frame available,
  /// and received that frame as a raw decoded ARGB buffer.
  /// This is not exclusive and can be used along another I420 callback.
  void SetCallback(Argb32FrameReadyCallback callback) noexcept;

  bool HasAnyCallbacks() const { return i420a_callback_ || argb_callback_; }

 protected:
  /// Get a temporary scratch buffer for an ARGB32 frame of the given
  /// dimensions. The returned buffer does not need to be deallocated, but can
  /// be reused by a later call so concurrent access is not supported.
  /// Before calling this method the caller needs to acquire |mutex_|, and keep
  /// it for the duration of its access.
  ArgbBuffer* GetArgbScratchBuffer(int width, int height);

  // VideoSinkInterface interface
  void OnFrame(const webrtc::VideoFrame& frame) noexcept override;

 private:
  /// Registered callback for receiving I420-encoded frame.
  I420AFrameReadyCallback i420a_callback_ RTC_GUARDED_BY(mutex_);

  /// Registered callback for receiving raw decoded ARGB frame.
  Argb32FrameReadyCallback argb_callback_ RTC_GUARDED_BY(mutex_);

  /// Mutex protecting all callbacks as well as the ARGB32 scratch buffer.
  std::mutex mutex_;

  /// Reusable ARGB scratch buffer to avoid per-frame allocation.
  rtc::scoped_refptr<ArgbBuffer> argb_scratch_buffer_ RTC_GUARDED_BY(mutex_);
};

}  // namespace WebRTC
}  // namespace MixedReality
}  // namespace Microsoft
