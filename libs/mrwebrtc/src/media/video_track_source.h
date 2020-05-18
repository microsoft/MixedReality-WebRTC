// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include "external_video_track_source_interop.h"
#include "mrs_errors.h"
#include "refptr.h"
#include "tracked_object.h"
#include "video_frame.h"

#include "api/mediastreaminterface.h"

namespace Microsoft {
namespace MixedReality {
namespace WebRTC {

class VideoTrackSource;

/// Adapter for a local audio source backing one or more local audio tracks.
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
  // TODO(perkj): Remove these once all known applications have moved to
  // explicitly setting suitable parameters for screencasts and don't need this
  // implicit behavior.
  bool is_screencast() const override { return false; }

  // Indicates that the encoder should denoise video before encoding it.
  // If it is not set, the default configuration is used which is different
  // depending on video codec.
  // TODO(perkj): Remove this once denoising is done by the source, and not by
  // the encoder.
  absl::optional<bool> needs_denoising() const { return absl::nullopt; }

  // Returns false if no stats are available, e.g, for a remote source, or a
  // source which has not seen its first frame yet.
  //
  // Implementation should avoid blocking.
  bool GetStats(Stats* /*stats*/) { return false; }

 protected:
  rtc::scoped_refptr<webrtc::VideoTrackSourceInterface> source_;
  SourceState state_{SourceState::kEnded};
  webrtc::ObserverInterface* observer_{nullptr};
};

/// Base class for a video track source acting as a frame source for one or more
/// video tracks.
class VideoTrackSource : public TrackedObject {
 public:
  ///// Helper to create an audio track source from a custom audio frame request
  ///// callback.
  // static RefPtr<VideoTrackSource> createFromCustom(
  //    RefPtr<GlobalFactory> global_factory,
  //    RefPtr<CustomAudioSource> audio_source);

  VideoTrackSource(
      RefPtr<GlobalFactory> global_factory,
      rtc::scoped_refptr<webrtc::VideoTrackSourceInterface> source) noexcept;
  ~VideoTrackSource() override;

  void SetName(absl::string_view name) {
    name_.assign(name.data(), name.size());
  }

  /// Get the name of the audio track source.
  std::string GetName() const noexcept override { return name_; }

  void SetCallback(I420AFrameReadyCallback callback) noexcept;

  inline rtc::scoped_refptr<webrtc::VideoTrackSourceInterface> impl() const
      noexcept {
    return source_;
  }

 protected:
  rtc::scoped_refptr<webrtc::VideoTrackSourceInterface> source_;
  std::string name_;
  std::unique_ptr<VideoFrameObserver> observer_;
  std::mutex observer_mutex_;
};

namespace detail {

//
// Helpers
//

///// Create a custom audio track source wrapping the given interop callback.
// RefPtr<VideoTrackSource> VideoTrackSourceCreateFromCustom(
//    RefPtr<GlobalFactory> global_factory,
//    mrsRequestCustomAudioFrameCallback callback,
//    void* user_data);

}  // namespace detail

}  // namespace WebRTC
}  // namespace MixedReality
}  // namespace Microsoft
