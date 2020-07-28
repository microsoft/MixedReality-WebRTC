// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include "mrs_errors.h"
#include "refptr.h"
#include "tracked_object.h"
#include "video_frame.h"
#include "video_frame_observer.h"
#include "video_track_source_interop.h"

#include "api/mediastreaminterface.h"

namespace Microsoft {
namespace MixedReality {
namespace WebRTC {

class VideoTrackSource;

/// Adapter for a local video source backing one or more local video tracks.
class VideoSourceAdapter : public webrtc::VideoTrackSourceInterface {
 public:
  VideoSourceAdapter(
      rtc::scoped_refptr<webrtc::VideoTrackSourceInterface> source);

  //
  // NotifierInterface
  //

  void RegisterObserver(webrtc::ObserverInterface* observer) override;
  void UnregisterObserver(webrtc::ObserverInterface* observer) override;

  //
  // MediaSourceInterface
  //

  SourceState state() const override { return state_; }
  bool remote() const override { return false; }

  //
  // VideoTrackSourceInterface
  //

  // Indicates that parameters suitable for screencasts should be automatically
  // applied to RtpSenders.
  bool is_screencast() const override { return false; }

  // Indicates that the encoder should denoise video before encoding it.
  // If it is not set, the default configuration is used which is different
  // depending on video codec.
  absl::optional<bool> needs_denoising() const override { return absl::nullopt; }

  // Returns false if no stats are available, e.g, for a remote source, or a
  // source which has not seen its first frame yet.
  //
  // Implementation should avoid blocking.
  bool GetStats(Stats* /*stats*/) override { return false; }

 protected:
  rtc::scoped_refptr<webrtc::VideoTrackSourceInterface> source_;
  SourceState state_{SourceState::kEnded};
  webrtc::ObserverInterface* observer_{nullptr};
};

/// Base class for a video track source acting as a frame source for one or more
/// video tracks.
class VideoTrackSource : public TrackedObject {
 public:
  VideoTrackSource(
      RefPtr<GlobalFactory> global_factory,
      ObjectType video_track_source_type,
      rtc::scoped_refptr<webrtc::VideoTrackSourceInterface> source) noexcept;
  ~VideoTrackSource() override;

  void SetCallback(I420AFrameReadyCallback callback) noexcept;
  void SetCallback(Argb32FrameReadyCallback callback) noexcept;

  inline rtc::scoped_refptr<webrtc::VideoTrackSourceInterface> impl()
      const noexcept {
    return source_;
  }

 private:
  template <class T>
  void SetCallbackImpl(T callback) noexcept;

 protected:
  rtc::scoped_refptr<webrtc::VideoTrackSourceInterface> source_;
  std::unique_ptr<VideoFrameObserver> observer_;
  std::mutex observer_mutex_;
};

}  // namespace WebRTC
}  // namespace MixedReality
}  // namespace Microsoft
