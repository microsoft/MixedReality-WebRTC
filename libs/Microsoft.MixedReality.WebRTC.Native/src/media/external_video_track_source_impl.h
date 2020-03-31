// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include "media/base/adaptedvideotracksource.h"

#include "callback.h"
#include "external_video_track_source.h"
#include "interop_api.h"

namespace Microsoft::MixedReality::WebRTC::detail {

/// Adapater for the frame buffer of an external video track source,
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

/// Video track source acting as an adapter for an external source of raw
/// frames.
class ExternalVideoTrackSourceImpl : public ExternalVideoTrackSource,
                                     // public rtc::AdaptedVideoTrackSource,
                                     // public rtc::Runnable,
                                     public rtc::MessageHandler {
 public:
  using SourceState = webrtc::MediaSourceInterface::SourceState;

  static RefPtr<ExternalVideoTrackSource> create(
      RefPtr<GlobalFactory> global_factory,
      std::unique_ptr<BufferAdapter> adapter);

  ~ExternalVideoTrackSourceImpl() override;

  void SetName(std::string name) { name_ = std::move(name); }
  std::string GetName() const override { return name_; }

  void FinishCreation() override;

  /// Start the video capture. This will begin to produce video frames and start
  /// calling the video frame callback.
  void StartCapture() override;

  /// Complete a video frame request with a given I420A video frame.
  Result CompleteRequest(uint32_t request_id,
                         int64_t timestamp_ms,
                         const I420AVideoFrame& frame) override;

  /// Complete a video frame request with a given ARGB32 video frame.
  Result CompleteRequest(uint32_t request_id,
                         int64_t timestamp_ms,
                         const Argb32VideoFrame& frame) override;

  /// Stop the video capture. This will stop producing video frames.
  void StopCapture() override;

  /// Shutdown the source and release the buffer adapter and its callback.
  void Shutdown() noexcept override;

  webrtc::VideoTrackSourceInterface* impl() const { return track_source_; }

 protected:
  ExternalVideoTrackSourceImpl(RefPtr<GlobalFactory> global_factory,
                               std::unique_ptr<BufferAdapter> adapter);
  // void Run(rtc::Thread* thread) override;
  void OnMessage(rtc::Message* message) override;

  rtc::scoped_refptr<CustomTrackSourceAdapter> track_source_;

  std::unique_ptr<BufferAdapter> adapter_;
  std::unique_ptr<rtc::Thread> capture_thread_;

  /// Collection of pending frame requests
  std::deque<std::pair<uint32_t, int64_t>> pending_requests_
      RTC_GUARDED_BY(request_lock_);  //< TODO : circular buffer to avoid alloc

  /// Next available ID for a frame request.
  uint32_t next_request_id_ RTC_GUARDED_BY(request_lock_){};

  /// Lock for frame requests.
  rtc::CriticalSection request_lock_;

  /// Friendly track source name, for debugging.
  std::string name_;
};

}  // namespace Microsoft::MixedReality::WebRTC::detail
