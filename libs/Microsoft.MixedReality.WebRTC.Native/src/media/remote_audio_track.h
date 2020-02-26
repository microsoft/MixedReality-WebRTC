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
class AudioTransceiver;

/// A remote audio track is a media track for a peer connection backed by a
/// remote audio stream received from the remote peer.
///
/// The remote nature of the track implies that the remote peer has control on
/// it, including enabling or disabling the track, and removing it from the peer
/// connection. The local peer only has limited control over the track.
class RemoteAudioTrack : public AudioFrameObserver, public MediaTrack {
 public:
  RemoteAudioTrack(RefPtr<GlobalFactory> global_factory,
                   PeerConnection& owner,
                   RefPtr<AudioTransceiver> transceiver,
                   rtc::scoped_refptr<webrtc::AudioTrackInterface> track,
                   rtc::scoped_refptr<webrtc::RtpReceiverInterface> receiver,
                   mrsRemoteAudioTrackInteropHandle interop_handle) noexcept;
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

  [[nodiscard]] webrtc::AudioTrackInterface* impl() const;
  [[nodiscard]] webrtc::RtpReceiverInterface* receiver() const;
  [[nodiscard]] AudioTransceiver* GetTransceiver() const;

  [[nodiscard]] mrsRemoteAudioTrackInteropHandle GetInteropHandle() const
      noexcept {
    return interop_handle_;
  }

  // Automatically called - do not use.
  void OnTrackRemoved(PeerConnection& owner);

 private:
  /// Underlying core implementation.
  rtc::scoped_refptr<webrtc::AudioTrackInterface> track_;

  /// RTP sender this track is associated with.
  rtc::scoped_refptr<webrtc::RtpReceiverInterface> receiver_;

  /// Transceiver this track is associated with, if any.
  RefPtr<AudioTransceiver> transceiver_;

  /// Optional interop handle, if associated with an interop wrapper.
  mrsRemoteAudioTrackInteropHandle interop_handle_{};

  /// Cached track name, to avoid dispatching on signaling thread.
  const std::string track_name_;
};

}  // namespace Microsoft::MixedReality::WebRTC
