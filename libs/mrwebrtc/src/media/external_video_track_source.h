// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include "external_video_track_source_interop.h"
#include "mrs_errors.h"
#include "refptr.h"
#include "tracked_object.h"
#include "video_frame.h"
#include "video_track_source.h"

namespace Microsoft {
namespace MixedReality {
namespace WebRTC {

class ExternalVideoTrackSource;

namespace detail {

/// Adapter for the frame buffer of an external video track source,
/// to support various frame encodings in a unified way.
class BufferAdapter {
 public:
  virtual ~BufferAdapter() = default;

  /// Request a new video frame with the specified request ID.
  virtual Result RequestFrame(ExternalVideoTrackSource& track_source,
                              uint32_t request_id,
                              int64_t time_ms) noexcept = 0;

  /// Allocate a new video frame buffer with a video frame received from a
  /// fulfilled frame request.
  virtual rtc::scoped_refptr<webrtc::VideoFrameBuffer> FillBuffer(
      const I420AVideoFrame& frame_view) = 0;
  virtual rtc::scoped_refptr<webrtc::VideoFrameBuffer> FillBuffer(
      const Argb32VideoFrame& frame_view) = 0;
};

/// Adapter to bridge a video track source to the underlying core
/// implementation.
struct CustomTrackSourceAdapter : public rtc::AdaptedVideoTrackSource {
  void DispatchFrame(const webrtc::VideoFrame& frame) { OnFrame(frame); }

  // VideoTrackSourceInterface
  bool is_screencast() const override { return false; }
  absl::optional<bool> needs_denoising() const override {
    return absl::nullopt;
  }

  // MediaSourceInterface
  SourceState state() const override { return state_; }
  bool remote() const override { return false; }

  SourceState state_ = SourceState::kInitializing;
};

}  // namespace detail

/// Frame request for an external video source producing video frames encoded in
/// I420 format, with optional Alpha (opacity) plane.
struct I420AVideoFrameRequest {
  /// Video track source the request is related to.
  ExternalVideoTrackSource& track_source_;

  /// Video frame timestamp, in milliseconds.
  std::int64_t timestamp_ms_;

  /// Unique identifier of the request.
  const std::uint32_t request_id_;

  /// Complete the request by making the track source consume the given video
  /// frame and have it deliver the frame to all its video tracks.
  Result CompleteRequest(const I420AVideoFrame& frame_view);
};

/// Custom video source producing video frames encoded in I420 format, with
/// optional Alpha (opacity) plane.
class I420AExternalVideoSource : public RefCountedBase {
 public:
  /// Produce a video frame for a request initiated by an external track source.
  ///
  /// This callback is invoked automatically by the track source whenever a new
  /// video frame is needed (pull model). The custom video source implementation
  /// must either return an error, or produce a new video frame and call the
  /// |CompleteRequest()| request on the |frame_request| object.
  virtual Result FrameRequested(I420AVideoFrameRequest& frame_request) = 0;
};

/// Frame request for an external video source producing video frames encoded in
/// ARGB 32-bit-per-pixel format.
struct Argb32VideoFrameRequest {
  /// Video track source the request is related to.
  ExternalVideoTrackSource& track_source_;

  /// Video frame timestamp, in milliseconds.
  std::int64_t timestamp_ms_;

  /// Unique identifier of the request.
  const std::uint32_t request_id_;

  /// Complete the request by making the track source consume the given video
  /// frame and have it deliver the frame to all its video tracks.
  Result CompleteRequest(const Argb32VideoFrame& frame_view);
};

/// Custom video source producing vidoe frames encoded in ARGB 32-bit-per-pixel
/// format.
class Argb32ExternalVideoSource : public RefCountedBase {
 public:
  /// Produce a video frame for a request initiated by an external track source.
  ///
  /// This callback is invoked automatically by the track source whenever a new
  /// video frame is needed (pull model). The custom video source implementation
  /// must either return an error, or produce a new video frame and call the
  /// |CompleteRequest()| request on the |track_source| object, passing the
  /// |request_id| of the current request being completed.
  virtual Result FrameRequested(Argb32VideoFrameRequest& frame_request) = 0;
};

/// Video track source acting as an adapter for an external source of raw
/// frames.
class ExternalVideoTrackSource : public VideoTrackSource,
                                 public rtc::MessageHandler {
 public:
  using SourceState = webrtc::MediaSourceInterface::SourceState;

  /// Helper to create an external video track source from a custom I420A video
  /// frame request callback.
  static RefPtr<ExternalVideoTrackSource> createFromI420A(
      RefPtr<GlobalFactory> global_factory,
      RefPtr<I420AExternalVideoSource> video_source);

  /// Helper to create an external video track source from a custom ARGB32 video
  /// frame request callback.
  static RefPtr<ExternalVideoTrackSource> createFromArgb32(
      RefPtr<GlobalFactory> global_factory,
      RefPtr<Argb32ExternalVideoSource> video_source);

  static RefPtr<ExternalVideoTrackSource> create(
      RefPtr<GlobalFactory> global_factory,
      std::unique_ptr<detail::BufferAdapter> adapter);

  ~ExternalVideoTrackSource() override;

  /// Finish the creation of the video track source, and start capturing.
  /// See |mrsExternalVideoTrackSourceFinishCreation()| for details.
  void FinishCreation();

  /// Start the video capture. This will begin to produce video frames and start
  /// calling the video frame callback.
  void StartCapture();

  /// Complete a given video frame request with the provided I420A frame.
  /// The caller must know the source expects an I420A frame; there is no check
  /// to confirm the source is I420A-based or ARGB32-based.
  Result CompleteRequest(uint32_t request_id,
                         int64_t timestamp_ms,
                         const I420AVideoFrame& frame);

  /// Complete a given video frame request with the provided ARGB32 frame.
  /// The caller must know the source expects an ARGB32 frame; there is no check
  /// to confirm the source is I420A-based or ARGB32-based.
  Result CompleteRequest(uint32_t request_id,
                         int64_t timestamp_ms,
                         const Argb32VideoFrame& frame);

  /// Stop the video capture. This will stop producing video frames.
  void StopCapture();

  /// Shutdown the source and release the buffer adapter and its callback.
  void Shutdown() noexcept;

 protected:
  ExternalVideoTrackSource(
      RefPtr<GlobalFactory> global_factory,
      std::unique_ptr<detail::BufferAdapter> adapter,
      rtc::scoped_refptr<detail::CustomTrackSourceAdapter> source);
  // void Run(rtc::Thread* thread) override;
  void OnMessage(rtc::Message* message) override;
  detail::CustomTrackSourceAdapter* GetSourceImpl() const {
    return (detail::CustomTrackSourceAdapter*)source_.get();
  }

  std::unique_ptr<detail::BufferAdapter> adapter_;
  std::unique_ptr<rtc::Thread> capture_thread_;

  /// Collection of pending frame requests
  std::deque<std::pair<uint32_t, int64_t>> pending_requests_
      RTC_GUARDED_BY(request_lock_);  //< TODO : circular buffer to avoid alloc

  /// Next available ID for a frame request.
  uint32_t next_request_id_ RTC_GUARDED_BY(request_lock_){};

  /// Lock for frame requests.
  rtc::CriticalSection request_lock_;
};

namespace detail {

/// Create an I420A external video track source wrapping the given interop
/// callback.
RefPtr<ExternalVideoTrackSource> ExternalVideoTrackSourceCreateFromI420A(
    RefPtr<GlobalFactory> global_factory,
    mrsRequestExternalI420AVideoFrameCallback callback,
    void* user_data);

/// Create an ARGB32 external video track source wrapping the given interop
/// callback.
RefPtr<ExternalVideoTrackSource> ExternalVideoTrackSourceCreateFromArgb32(
    RefPtr<GlobalFactory> global_factory,
    mrsRequestExternalArgb32VideoFrameCallback callback,
    void* user_data);

}  // namespace detail

}  // namespace WebRTC
}  // namespace MixedReality
}  // namespace Microsoft
