// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#include "pch.h"

#include "video_frame_observer.h"

namespace {

// Aligning pointer to 64 bytes for improved performance, e.g. use SIMD.
constexpr int kBufferAlignment = 64;

}  // namespace

namespace Microsoft {
namespace MixedReality {
namespace WebRTC {

ArgbBuffer::ArgbBuffer(int width, int height, int stride) noexcept
    : width_(width),
      height_(height),
      stride_(stride),
      data_(static_cast<uint8_t*>(
          webrtc::AlignedMalloc(static_cast<size_t>(height) * stride,
                                kBufferAlignment))) {
  RTC_DCHECK_GT(width, 0);
  RTC_DCHECK_GT(height, 0);
  RTC_DCHECK_GE(stride, 4 * width);
}

rtc::scoped_refptr<webrtc::I420BufferInterface> ArgbBuffer::ToI420() {
  rtc::scoped_refptr<webrtc::I420Buffer> i420_buffer =
      webrtc::I420Buffer::Create(width_, height_, stride_, stride_ / 2,
                                 stride_ / 2);
  libyuv::ARGBToI420(Data(), Stride(), i420_buffer->MutableDataY(),
                     i420_buffer->StrideY(), i420_buffer->MutableDataU(),
                     i420_buffer->StrideU(), i420_buffer->MutableDataV(),
                     i420_buffer->StrideV(), width_, height_);
  return i420_buffer;
}

void VideoFrameObserver::SetCallback(
    I420AFrameReadyCallback callback) noexcept {
  std::lock_guard<std::mutex> lock(mutex_);
  i420a_callback_ = std::move(callback);
}

void VideoFrameObserver::SetCallback(
    Argb32FrameReadyCallback callback) noexcept {
  std::lock_guard<std::mutex> lock(mutex_);
  argb_callback_ = std::move(callback);
}

ArgbBuffer* VideoFrameObserver::GetArgbScratchBuffer(int width, int height) {
  const size_t needed_size = Argb32FrameSize(width, height);
  if (auto* buffer = argb_scratch_buffer_.get()) {
    if (buffer->Size() >= needed_size) {
      buffer->Recycle(width, height);  // Update stride
      return buffer;
    }
  }
  argb_scratch_buffer_ = ArgbBuffer::Create(width, height);
  return argb_scratch_buffer_.get();
}

void VideoFrameObserver::OnFrame(const webrtc::VideoFrame& frame) noexcept {
  std::lock_guard<std::mutex> lock(mutex_);
  if (!i420a_callback_ && !argb_callback_) {
    return;
  }

  rtc::scoped_refptr<webrtc::VideoFrameBuffer> buffer(
      frame.video_frame_buffer());

  const int width = frame.width();
  const int height = frame.height();

  if (buffer->type() != webrtc::VideoFrameBuffer::Type::kI420A) {
    // The buffer is not encoded in I420 with alpha channel; use I420 without
    // alpha channel as interchange format for the callback, and convert the
    // buffer to that (or do nothing if already in I420).
    rtc::scoped_refptr<webrtc::I420BufferInterface> i420_buffer =
        buffer->ToI420();
    const uint8_t* const yptr = i420_buffer->DataY();
    const uint8_t* const uptr = i420_buffer->DataU();
    const uint8_t* const vptr = i420_buffer->DataV();

    if (i420a_callback_) {
      I420AVideoFrame i420a_frame;
      i420a_frame.ydata_ = yptr;
      i420a_frame.udata_ = uptr;
      i420a_frame.vdata_ = vptr;
      i420a_frame.adata_ = nullptr;
      i420a_frame.ystride_ = i420_buffer->StrideY();
      i420a_frame.ustride_ = i420_buffer->StrideU();
      i420a_frame.vstride_ = i420_buffer->StrideV();
      i420a_frame.astride_ = 0;
      i420a_frame.width_ = width;
      i420a_frame.height_ = height;
      i420a_callback_(i420a_frame);
    }

    if (argb_callback_) {
      ArgbBuffer* const argb_buffer = GetArgbScratchBuffer(width, height);
      libyuv::I420ToARGB(yptr, i420_buffer->StrideY(), uptr,
                         i420_buffer->StrideU(), vptr, i420_buffer->StrideV(),
                         argb_buffer->Data(), argb_buffer->Stride(), width,
                         height);
      Argb32VideoFrame argb32_frame;
      argb32_frame.argb32_data_ = argb_buffer->Data();
      argb32_frame.stride_ = argb_buffer->Stride();
      argb32_frame.width_ = width;
      argb32_frame.height_ = height;
      argb_callback_(argb32_frame);
    }

  } else {
    // The buffer is encoded in I420 with alpha channel, use it directly.
    webrtc::I420ABufferInterface* i420a_buffer = buffer->GetI420A();
    const uint8_t* const yptr = i420a_buffer->DataY();
    const uint8_t* const uptr = i420a_buffer->DataU();
    const uint8_t* const vptr = i420a_buffer->DataV();
    const uint8_t* const aptr = i420a_buffer->DataA();

    if (i420a_callback_) {
      I420AVideoFrame i420a_frame;
      i420a_frame.ydata_ = yptr;
      i420a_frame.udata_ = uptr;
      i420a_frame.vdata_ = vptr;
      i420a_frame.adata_ = aptr;
      i420a_frame.ystride_ = i420a_buffer->StrideY();
      i420a_frame.ustride_ = i420a_buffer->StrideU();
      i420a_frame.vstride_ = i420a_buffer->StrideV();
      i420a_frame.astride_ = i420a_buffer->StrideA();
      i420a_frame.width_ = width;
      i420a_frame.height_ = height;
      i420a_callback_(i420a_frame);
    }

    if (argb_callback_) {
      ArgbBuffer* const argb_buffer = GetArgbScratchBuffer(width, height);
      libyuv::I420AlphaToARGB(
          yptr, i420a_buffer->StrideY(), uptr, i420a_buffer->StrideU(), vptr,
          i420a_buffer->StrideV(), aptr, i420a_buffer->StrideA(),
          argb_buffer->Data(), argb_buffer->Stride(), width, height, 0);
      Argb32VideoFrame argb32_frame;
      argb32_frame.argb32_data_ = argb_buffer->Data();
      argb32_frame.stride_ = argb_buffer->Stride();
      argb32_frame.width_ = width;
      argb32_frame.height_ = height;
      argb_callback_(argb32_frame);
    }
  }
}

}  // namespace WebRTC
}  // namespace MixedReality
}  // namespace Microsoft
