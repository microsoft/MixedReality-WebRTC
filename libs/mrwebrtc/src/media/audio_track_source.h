// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include "external_video_track_source_interop.h"
#include "mrs_errors.h"
#include "refptr.h"
#include "tracked_object.h"
#include "video_frame.h"

#include "api/media_stream_interface.h"

namespace Microsoft {
namespace MixedReality {
namespace WebRTC {

class AudioTrackSource;

/// Adapter for a local audio source backing one or more local audio tracks.
class AudioSourceAdapter : public webrtc::AudioSourceInterface {
 public:
  AudioSourceAdapter(rtc::scoped_refptr<webrtc::AudioSourceInterface> source);

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
  // AudioSourceInterface
  //

  // Sets the volume of the source. |volume| is in  the range of [0, 10].
  void SetVolume(double /*volume*/) override {}

  // Registers/unregisters observers to the audio source.
  void RegisterAudioObserver(AudioObserver* /*observer*/) override {}
  void UnregisterAudioObserver(AudioObserver* /*observer*/) override {}

  void AddSink(webrtc::AudioTrackSinkInterface* sink) override;
  void RemoveSink(webrtc::AudioTrackSinkInterface* sink) override;

 protected:
  rtc::scoped_refptr<webrtc::AudioSourceInterface> source_;
  std::vector<webrtc::AudioTrackSinkInterface*> sinks_;
  SourceState state_{SourceState::kEnded};
  webrtc::ObserverInterface* observer_{nullptr};
  AudioObserver* audio_observer_{nullptr};
};

/// Base class for an audio track source acting as a frame source for one or
/// more audio tracks.
class AudioTrackSource : public TrackedObject {
 public:
  AudioTrackSource(
      RefPtr<GlobalFactory> global_factory,
      ObjectType audio_track_source_type,
      rtc::scoped_refptr<webrtc::AudioSourceInterface> source) noexcept;
  ~AudioTrackSource() override;

  void SetCallback(AudioFrameReadyCallback callback) noexcept;

  inline rtc::scoped_refptr<webrtc::AudioSourceInterface> impl() const
      noexcept {
    return source_;
  }

 protected:
  rtc::scoped_refptr<webrtc::AudioSourceInterface> source_;
  std::unique_ptr<AudioFrameObserver> observer_;
  std::mutex observer_mutex_;
};

namespace detail {

//
// Helpers
//

///// Create a custom audio track source wrapping the given interop callback.
// RefPtr<AudioTrackSource> AudioTrackSourceCreateFromCustom(
//    RefPtr<GlobalFactory> global_factory,
//    mrsRequestCustomAudioFrameCallback callback,
//    void* user_data);

}  // namespace detail

}  // namespace WebRTC
}  // namespace MixedReality
}  // namespace Microsoft
