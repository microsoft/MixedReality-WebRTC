// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#include "pch.h"

#include "video_frame_observer.h"

namespace {

// Aligning pointer to 64 bytes for improved performance, e.g. use SIMD.
constexpr int kBufferAlignment = 64;

}  // namespace

namespace mrtk {
namespace net {
namespace webrtc_impl {

ArgbBuffer::ArgbBuffer(int width, int height, int stride) noexcept
    : width_(width),
      height_(height),
      stride_(stride),
      data_(static_cast<uint8_t*>(
          webrtc::AlignedMalloc(ArgbDataSize(height, stride),
                                kBufferAlignment))) {
  RTC_DCHECK_GT(width, 0);
  RTC_DCHECK_GT(height, 0);
  RTC_DCHECK_GE(stride, 4 * width);
}

rtc::scoped_refptr<webrtc::I420BufferInterface>
ArgbBuffer::ToI420() {
  rtc::scoped_refptr<webrtc::I420Buffer> i420_buffer =
      webrtc::I420Buffer::Create(width_, height_, stride_, stride_ / 2,
                                 stride_ / 2);
  libyuv::ARGBToI420(Data(), Stride(), i420_buffer->MutableDataY(),
                     i420_buffer->StrideY(), i420_buffer->MutableDataU(),
                     i420_buffer->StrideU(), i420_buffer->MutableDataV(),
                     i420_buffer->StrideV(), width_, height_);
  return i420_buffer;
}

void VideoFrameObserver::setCallback(I420FrameReadyCallback callback) noexcept {
  auto lock = std::lock_guard{mutex_};
  i420_callback_ = callback;
}

void VideoFrameObserver::setCallback(ARGBFrameReadyCallback callback) noexcept {
  auto lock = std::lock_guard{mutex_};
  argb_callback_ = callback;
}

ArgbBuffer* VideoFrameObserver::GetArgbScratchBuffer(
    int width,
    int height) {
  const size_t needed_size = ArgbDataSize(width, height);
  if (auto* buffer = argb_scratch_buffer_.get()) {
    if (buffer->Size() >= needed_size) {
      return buffer;
    }
  }
  argb_scratch_buffer_ = ArgbBuffer::Create(width, height);
  return argb_scratch_buffer_.get();
}

void VideoFrameObserver::OnFrame(const webrtc::VideoFrame& frame) noexcept {
  auto lock = std::lock_guard{mutex_};
  if (!i420_callback_ && !argb_callback_)
    return;

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
    const uint8_t* yptr = i420_buffer->DataY();
    const uint8_t* uptr = i420_buffer->DataU();
    const uint8_t* vptr = i420_buffer->DataV();
    const uint8_t* aptr = nullptr;

    if (i420_callback_) {
      i420_callback_(yptr, uptr, vptr, aptr, i420_buffer->StrideY(),
                     i420_buffer->StrideU(), i420_buffer->StrideV(), 0, width,
                     height);
    }

    if (argb_callback_) {
      ArgbBuffer* const argb_buffer = GetArgbScratchBuffer(width, height);
      libyuv::I420ToARGB(yptr, i420_buffer->StrideY(), uptr,
                         i420_buffer->StrideU(), vptr, i420_buffer->StrideV(),
                         argb_buffer->Data(), argb_buffer->Stride(), width,
                         height);
      argb_callback_(argb_buffer->Data(), argb_buffer->Stride(), width, height);
    }

  } else {
    // The buffer is encoded in I420 with alpha channel, use it directly.
    webrtc::I420ABufferInterface* i420a_buffer = buffer->GetI420A();
    const uint8_t* yptr = i420a_buffer->DataY();
    const uint8_t* uptr = i420a_buffer->DataU();
    const uint8_t* vptr = i420a_buffer->DataV();
    const uint8_t* aptr = i420a_buffer->DataA();

    if (i420_callback_) {
      i420_callback_(yptr, uptr, vptr, aptr, i420a_buffer->StrideY(),
                     i420a_buffer->StrideU(), i420a_buffer->StrideV(),
                     i420a_buffer->StrideA(), width, height);
    }

    if (argb_callback_) {
      ArgbBuffer* const argb_buffer = GetArgbScratchBuffer(width, height);
      libyuv::I420AlphaToARGB(
          yptr, i420a_buffer->StrideY(), uptr, i420a_buffer->StrideU(), vptr,
          i420a_buffer->StrideV(), aptr, i420a_buffer->StrideA(),
          argb_buffer->Data(), argb_buffer->Stride(), width, height, 0);
      argb_callback_(argb_buffer->Data(), argb_buffer->Stride(), width, height);
    }
  }
}

}  // namespace webrtc_impl
}  // namespace net
}  // namespace mrtk
