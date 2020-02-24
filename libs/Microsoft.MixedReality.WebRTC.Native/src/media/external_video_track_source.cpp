// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include "interop/global_factory.h"
#include "media/external_video_track_source_impl.h"

namespace {

using namespace Microsoft::MixedReality::WebRTC;

enum {
  /// Request a new video frame from the source.
  MSG_REQUEST_FRAME
};

/// Buffer adapter for an I420 video frame.
class I420ABufferAdapter : public detail::BufferAdapter {
 public:
  I420ABufferAdapter(RefPtr<I420AExternalVideoSource> video_source)
      : video_source_(std::move(video_source)) {}
  Result RequestFrame(ExternalVideoTrackSource& track_source,
                      std::uint32_t request_id,
                      std::int64_t timestamp_ms) noexcept override {
    // Request a single I420 frame
    I420AVideoFrameRequest request{track_source, timestamp_ms, request_id};
    return video_source_->FrameRequested(request);
  }
  rtc::scoped_refptr<webrtc::VideoFrameBuffer> FillBuffer(
      const I420AVideoFrame& frame_view) override {
    return webrtc::I420Buffer::Copy(
        (int)frame_view.width_, (int)frame_view.height_,
        (const uint8_t*)frame_view.ydata_, frame_view.ystride_,
        (const uint8_t*)frame_view.udata_, frame_view.ustride_,
        (const uint8_t*)frame_view.vdata_, frame_view.vstride_);
  }
  rtc::scoped_refptr<webrtc::VideoFrameBuffer> FillBuffer(
      const Argb32VideoFrame& /*frame_view*/) override {
    RTC_CHECK(false);
  }

 private:
  RefPtr<I420AExternalVideoSource> video_source_;
};

/// Buffer adapter for a 32-bit ARGB video frame.
class Argb32BufferAdapter : public detail::BufferAdapter {
 public:
  Argb32BufferAdapter(RefPtr<Argb32ExternalVideoSource> video_source)
      : video_source_(std::move(video_source)) {}
  Result RequestFrame(ExternalVideoTrackSource& track_source,
                      std::uint32_t request_id,
                      std::int64_t timestamp_ms) noexcept override {
    // Request a single ARGB32 frame
    Argb32VideoFrameRequest request{track_source, timestamp_ms, request_id};
    return video_source_->FrameRequested(request);
  }
  rtc::scoped_refptr<webrtc::VideoFrameBuffer> FillBuffer(
      const I420AVideoFrame& /*frame_view*/) override {
    RTC_CHECK(false);
  }
  rtc::scoped_refptr<webrtc::VideoFrameBuffer> FillBuffer(
      const Argb32VideoFrame& frame_view) override {
    // Check that the input frame fits within the constraints of chroma
    // downsampling (width and height multiple of 2).
    uint32_t width = frame_view.width_;
    if (width & 0x1) {
      if (!has_warned_) {
        RTC_LOG(LS_WARNING) << "ARGB32 video frame has width " << width
                            << " which is not a multiple of 2, so cannot be "
                               "chroma-downsampled. "
                               "Truncating to "
                            << (width - 1) << " before I420 conversion.";
        has_warned_ = true;
      }
      --width;
    }
    uint32_t height = frame_view.height_;
    if (height & 0x1) {
      if (!has_warned_) {
        RTC_LOG(LS_WARNING) << "ARGB32 video frame has height " << height
                            << " which is not a multiple of 2, so cannot be "
                               "chroma-downsampled. "
                               "Truncating to "
                            << (height - 1) << " before I420 conversion.";
        has_warned_ = true;
      }
      --height;
    }

    // Create I420 buffer
    rtc::scoped_refptr<webrtc::I420Buffer> buffer =
        webrtc::I420Buffer::Create(width, height);

    // Convert to I420 and copy to buffer
    libyuv::ARGBToI420((const uint8_t*)frame_view.argb32_data_,
                       frame_view.stride_, buffer->MutableDataY(),
                       buffer->StrideY(), buffer->MutableDataU(),
                       buffer->StrideU(), buffer->MutableDataV(),
                       buffer->StrideV(), width, height);

    return buffer;
  }

 private:
  RefPtr<Argb32ExternalVideoSource> video_source_;
  bool has_warned_ = false;
};

}  // namespace

namespace Microsoft::MixedReality::WebRTC {
namespace detail {

constexpr const size_t kMaxPendingRequestCount = 64;

RefPtr<ExternalVideoTrackSource> ExternalVideoTrackSourceImpl::create(
    RefPtr<GlobalFactory> global_factory,
    std::unique_ptr<BufferAdapter> adapter) {
  auto source = new ExternalVideoTrackSourceImpl(std::move(global_factory),
                                                 std::move(adapter));
  // Note: Video track sources always start already capturing; there is no
  // start/stop mechanism at the track level in WebRTC. A source is either being
  // initialized, or is already live. However because of wrappers and interop
  // this step is delayed until |FinishCreation()| is called by the wrapper.
  return source;
}

ExternalVideoTrackSourceImpl::ExternalVideoTrackSourceImpl(
    RefPtr<GlobalFactory> global_factory,
    std::unique_ptr<BufferAdapter> adapter)
    : ExternalVideoTrackSource(std::move(global_factory)),
      track_source_(new rtc::RefCountedObject<CustomTrackSourceAdapter>()),
      adapter_(std::forward<std::unique_ptr<BufferAdapter>>(adapter)),
      capture_thread_(rtc::Thread::Create()) {
  capture_thread_->SetName("ExternalVideoTrackSource capture thread", this);
}

ExternalVideoTrackSourceImpl::~ExternalVideoTrackSourceImpl() {
  StopCapture();
}

void ExternalVideoTrackSourceImpl::FinishCreation() {
  StartCapture();
}

void ExternalVideoTrackSourceImpl::StartCapture() {
  // Check if |Shutdown()| was called, in which case the source cannot restart.
  if (!adapter_) {
    return;
  }

  // Start capture thread
  track_source_->state_ = SourceState::kLive;
  pending_requests_.clear();
  capture_thread_->Start();

  // Schedule first frame request for 10ms from now
  int64_t now = rtc::TimeMillis();
  capture_thread_->PostAt(RTC_FROM_HERE, now + 10, this, MSG_REQUEST_FRAME);
}

Result ExternalVideoTrackSourceImpl::CompleteRequest(
    uint32_t request_id,
    int64_t timestamp_ms,
    const I420AVideoFrame& frame_view) {
  // Validate pending request ID and retrieve frame timestamp
  int64_t timestamp_ms_original = -1;
  {
    rtc::CritScope lock(&request_lock_);
    for (auto it = pending_requests_.begin(); it != pending_requests_.end();
         ++it) {
      if (it->first == request_id) {
        timestamp_ms_original = it->second;
        // Remove outdated requests, including current one
        ++it;
        pending_requests_.erase(pending_requests_.begin(), it);
        break;
      }
    }
    if (timestamp_ms_original < 0) {
      return Result::kInvalidParameter;
    }
  }

  // Apply user override if any
  if (timestamp_ms != timestamp_ms_original) {
    timestamp_ms = timestamp_ms_original;
  }

  // Create and dispatch the video frame
  webrtc::VideoFrame frame{
      webrtc::VideoFrame::Builder()
          .set_video_frame_buffer(adapter_->FillBuffer(frame_view))
          .set_timestamp_ms(timestamp_ms)
          .build()};
  track_source_->DispatchFrame(frame);
  return Result::kSuccess;
}

Result ExternalVideoTrackSourceImpl::CompleteRequest(
    uint32_t request_id,
    int64_t timestamp_ms,
    const Argb32VideoFrame& frame_view) {
  // Validate pending request ID and retrieve frame timestamp
  int64_t timestamp_ms_original = -1;
  {
    rtc::CritScope lock(&request_lock_);
    for (auto it = pending_requests_.begin(); it != pending_requests_.end();
         ++it) {
      if (it->first == request_id) {
        timestamp_ms_original = it->second;
        // Remove outdated requests, including current one
        ++it;
        pending_requests_.erase(pending_requests_.begin(), it);
        break;
      }
    }
    if (timestamp_ms_original < 0) {
      return Result::kInvalidParameter;
    }
  }

  // Apply user override if any
  if (timestamp_ms != timestamp_ms_original) {
    timestamp_ms = timestamp_ms_original;
  }

  // Create and dispatch the video frame
  webrtc::VideoFrame frame{
      webrtc::VideoFrame::Builder()
          .set_video_frame_buffer(adapter_->FillBuffer(frame_view))
          .set_timestamp_ms(timestamp_ms)
          .build()};
  track_source_->DispatchFrame(frame);
  return Result::kSuccess;
}

void ExternalVideoTrackSourceImpl::StopCapture() {
  if (track_source_->state_ != SourceState::kEnded) {
    capture_thread_->Stop();
    track_source_->state_ = SourceState::kEnded;
  }
  pending_requests_.clear();
}

void ExternalVideoTrackSourceImpl::Shutdown() noexcept {
  StopCapture();
  adapter_ = nullptr;
}

// Note - This is called on the capture thread only.
void ExternalVideoTrackSourceImpl::OnMessage(rtc::Message* message) {
  switch (message->message_id) {
    case MSG_REQUEST_FRAME:
      const int64_t now = rtc::TimeMillis();

      // Request a frame from the external video source
      uint32_t request_id = 0;
      {
        rtc::CritScope lock(&request_lock_);
        // Discard an old request if no space available. This allows restarting
        // after a long delay, otherwise skipping the request generally also
        // prevent the user from calling CompleteFrame() to make some space for
        // more. The queue is still useful for just-in-time or short delays.
        if (pending_requests_.size() >= kMaxPendingRequestCount) {
          pending_requests_.erase(pending_requests_.begin());
        }
        request_id = next_request_id_++;
        pending_requests_.emplace_back(request_id, now);
      }
      adapter_->RequestFrame(*this, request_id, now);

      // Schedule a new request for 30ms from now
      //< TODO - this is unreliable and prone to drifting; figure out something
      // better
      capture_thread_->PostAt(RTC_FROM_HERE, now + 30, this, MSG_REQUEST_FRAME);
      break;
  }
}

}  // namespace detail

ExternalVideoTrackSource::ExternalVideoTrackSource(
    RefPtr<GlobalFactory> global_factory)
    : TrackedObject(std::move(global_factory),
                    ObjectType::kExternalVideoTrackSource) {}

RefPtr<ExternalVideoTrackSource> ExternalVideoTrackSource::createFromI420A(
    RefPtr<GlobalFactory> global_factory,
    RefPtr<I420AExternalVideoSource> video_source) {
  return detail::ExternalVideoTrackSourceImpl::create(
      std::move(global_factory),
      std::make_unique<I420ABufferAdapter>(std::move(video_source)));
}

RefPtr<ExternalVideoTrackSource> ExternalVideoTrackSource::createFromArgb32(
    RefPtr<GlobalFactory> global_factory,
    RefPtr<Argb32ExternalVideoSource> video_source) {
  return detail::ExternalVideoTrackSourceImpl::create(
      std::move(global_factory),
      std::make_unique<Argb32BufferAdapter>(std::move(video_source)));
}

Result I420AVideoFrameRequest::CompleteRequest(
    const I420AVideoFrame& frame_view) {
  auto impl =
      static_cast<detail::ExternalVideoTrackSourceImpl*>(&track_source_);
  return impl->CompleteRequest(request_id_, timestamp_ms_, frame_view);
}

Result Argb32VideoFrameRequest::CompleteRequest(
    const Argb32VideoFrame& frame_view) {
  auto impl =
      static_cast<detail::ExternalVideoTrackSourceImpl*>(&track_source_);
  return impl->CompleteRequest(request_id_, timestamp_ms_, frame_view);
}

}  // namespace Microsoft::MixedReality::WebRTC
