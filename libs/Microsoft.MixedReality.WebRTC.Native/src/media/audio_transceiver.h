// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include "callback.h"
#include "interop_api.h"
#include "media/local_audio_track.h"
#include "media/remote_audio_track.h"
#include "media/transceiver.h"
#include "refptr.h"

namespace Microsoft::MixedReality::WebRTC {

class PeerConnection;

/// Transceiver containing audio tracks.
class AudioTransceiver : public Transceiver {
 public:
  /// Constructor for Plan B.
  AudioTransceiver(RefPtr<GlobalFactory> global_factory,
                   PeerConnection& owner,
                   int mline_index,
                   std::string name,
                   const AudioTransceiverInitConfig& config) noexcept;

  /// Constructor for Unified Plan.
  AudioTransceiver(
      RefPtr<GlobalFactory> global_factory,
      PeerConnection& owner,
      int mline_index,
      std::string name,
      rtc::scoped_refptr<webrtc::RtpTransceiverInterface> transceiver,
      const AudioTransceiverInitConfig& config) noexcept;

  ~AudioTransceiver() override;

  Result SetDirection(Direction new_direction) noexcept;

  Result SetLocalTrack(RefPtr<LocalAudioTrack> local_track) noexcept;

  RefPtr<LocalAudioTrack> GetLocalTrack() noexcept { return local_track_; }

  RefPtr<RemoteAudioTrack> GetRemoteTrack() noexcept { return remote_track_; }

  int GetMlineIndex() const noexcept { return mline_index_; }

  //
  // Internal
  //

  void OnLocalTrackAdded(RefPtr<LocalAudioTrack> track);
  void OnRemoteTrackAdded(RefPtr<RemoteAudioTrack> track);
  void OnLocalTrackRemoved(LocalAudioTrack* track);
  void OnRemoteTrackRemoved(RemoteAudioTrack* track);
  mrsAudioTransceiverInteropHandle GetInteropHandle() const {
    return interop_handle_;
  }

 protected:
  RefPtr<LocalAudioTrack> local_track_;
  RefPtr<RemoteAudioTrack> remote_track_;

  int mline_index_ = -1;

  /// Transceiver name, for pairing with the remote peer.
  std::string name_;

  /// Optional interop handle, if associated with an interop wrapper.
  mrsAudioTransceiverInteropHandle interop_handle_{};
};

}  // namespace Microsoft::MixedReality::WebRTC
