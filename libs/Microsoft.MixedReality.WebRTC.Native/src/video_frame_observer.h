// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#pragma once

#include <mutex>

#include "api/video/video_frame.h"
#include "api/video/video_sink_interface.h"

#include "callback.h"

namespace mrtk {
namespace net {
namespace webrtc_impl {

/// Callback fired on newly available video frame, encoded as I420.
/// The first 4 pointers are buffers for the YUVA planes (in that order),
/// followed by the 4 byte strides, and the width and height of the frame.
using I420FrameReadyCallback = Callback<const void*,
                                        const void*,
                                        const void*,
                                        const void*,
                                        int,
                                        int,
                                        int,
                                        int,
                                        int,
                                        int>;

/// Callback fired on newly available video frame, encoded as ARGB.
/// The first parameters are the buffer pointer and byte stride of the
/// packed ARGB data, followed by the width and height of the frame.
using ARGBFrameReadyCallback = Callback<const void*, int, int, int>;

constexpr inline size_t ArgbDataSize(int height, int stride) {
  return static_cast<size_t>(height) * stride * 4;
}

// Plain 32-bit ARGB buffer in standard memory.
class ArgbBuffer : public webrtc::VideoFrameBuffer {
 public:
  // Create a new buffer.
  static inline rtc::scoped_refptr<ArgbBuffer> Create(int width, int height) {
    return new rtc::RefCountedObject<ArgbBuffer>(width, height, width * 4);
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
  inline int Stride() const { return stride_; }
  inline constexpr size_t Size() const {
    return ArgbDataSize(height_, stride_);
  }

 protected:
  ArgbBuffer(int width, int height, int stride) noexcept;
  ~ArgbBuffer() override = default;

 private:
  const int width_;
  const int height_;
  const int stride_;
  const std::unique_ptr<uint8_t, webrtc::AlignedFreeDeleter> data_;
};

/// Video frame observer to get notified of newly available video frames.
class VideoFrameObserver : public rtc::VideoSinkInterface<webrtc::VideoFrame> {
 public:
  /// Register a callback to get notified on frame available,
  /// and received that frame as a I420-encoded buffer.
  /// This is not exclusive and can be used along another ARGB callback.
  void SetCallback(I420FrameReadyCallback callback) noexcept;

  /// Register a callback to get notified on frame available,
  /// and received that frame as a raw decoded ARGB buffer.
  /// This is not exclusive and can be used along another I420 callback.
  void SetCallback(ARGBFrameReadyCallback callback) noexcept;

 protected:
  ArgbBuffer* GetArgbScratchBuffer(int width, int height);

  // VideoSinkInterface interface
  void OnFrame(const webrtc::VideoFrame& frame) noexcept override;

 private:
  /// Registered callback for receiving I420-encoded frame.
  I420FrameReadyCallback i420_callback_;

  /// Registered callback for receiving raw decoded ARGB frame.
  ARGBFrameReadyCallback argb_callback_;

  /// Mutex protecting all callbacks.
  std::mutex mutex_;

  /// Reusable ARGB scratch buffer to avoid per-frame allocation.
  rtc::scoped_refptr<ArgbBuffer> argb_scratch_buffer_;
};

}  // namespace webrtc_impl
}  // namespace net
}  // namespace mrtk
