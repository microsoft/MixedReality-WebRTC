// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include "audio_frame_observer.h"
#include "callback.h"
#include "interop_api.h"
#include "media_track.h"
#include "refptr.h"
#include "tracked_object.h"

namespace rtc {
template <typename T>
class scoped_refptr;
}

namespace webrtc {
class RtpReceiverInterface;
class AudioTrackInterface;
}  // namespace webrtc

namespace Microsoft::MixedReality::WebRTC {

class PeerConnection;
class Transceiver;

/// A remote audio track is a media track for a peer connection backed by a
/// remote audio stream received from the remote peer.
///
/// The remote nature of the track implies that the remote peer has control on
/// it, including enabling or disabling the track, and removing it from the peer
/// connection. The local peer only has limited control over the track.
class RemoteAudioTrack : public AudioFrameObserver, public MediaTrack {
 public:
  RemoteAudioTrack(
      RefPtr<GlobalFactory> global_factory,
      PeerConnection& owner,
      Transceiver* transceiver,
      rtc::scoped_refptr<webrtc::AudioTrackInterface> track,
      rtc::scoped_refptr<webrtc::RtpReceiverInterface> receiver) noexcept;
  ~RemoteAudioTrack() override;

  /// Get the name of the remote audio track.
  std::string GetName() const noexcept override { return track_name_; }

  /// Enable or disable the audio track. An enabled track streams its content
  /// from its source to the remote peer. A disabled audio track only sends
  /// empty audio data (silence).
  void SetEnabled(bool enabled) const noexcept { track_->set_enabled(enabled); }

  /// Check if the track is enabled.
  /// See |SetEnabled(bool)|.
  [[nodiscard]] bool IsEnabled() const noexcept { return track_->enabled(); }

  //
  // Advanced use
  //

  /// Get a handle to the remote audio track. This handle is valid until the
  /// remote track is removed from the peer connection and destroyed, which is
  /// signaled by the |TrackRemoved| event on the peer connection.
  [[nodiscard]] constexpr mrsRemoteAudioTrackHandle GetHandle() const noexcept {
    return (mrsRemoteAudioTrackHandle)this;
  }

  [[nodiscard]] webrtc::AudioTrackInterface* impl() const;
  [[nodiscard]] webrtc::RtpReceiverInterface* receiver() const;

  [[nodiscard]] constexpr Transceiver* GetTransceiver() const {
    return transceiver_;
  }

  [[nodiscard]] webrtc::MediaStreamTrackInterface* GetMediaImpl()
      const override {
    return impl();
  }

  // Automatically called - do not use.
  void OnTrackRemoved(PeerConnection& owner);

 private:
  /// Underlying core implementation.
  rtc::scoped_refptr<webrtc::AudioTrackInterface> track_;

  /// RTP sender this track is associated with.
  rtc::scoped_refptr<webrtc::RtpReceiverInterface> receiver_;

  /// Weak back-pointer to the Transceiver this track is associated with. This
  /// avoids a circular reference with the transceiver itself.
  /// Note that unlike local tracks, this is never NULL since the remote track
  /// gets destroyed when detached from the transceiver.
  Transceiver* transceiver_{nullptr};

  /// Cached track name, to avoid dispatching on signaling thread.
  const std::string track_name_;
};

}  // namespace Microsoft::MixedReality::WebRTC
