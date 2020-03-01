// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include "callback.h"
#include "interop_api.h"
#include "local_video_track.h"
#include "media/remote_video_track.h"
#include "media/transceiver.h"
#include "refptr.h"

namespace Microsoft::MixedReality::WebRTC {

class PeerConnection;

/// Transceiver containing video tracks.
class VideoTransceiver : public Transceiver {
 public:
  /// Constructor for Plan B.
  VideoTransceiver(RefPtr<GlobalFactory> global_factory,
                   PeerConnection& owner,
                   int mline_index,
                   std::string name,
                   const VideoTransceiverInitConfig& config) noexcept;

  /// Constructor for Unified Plan.
  VideoTransceiver(
      RefPtr<GlobalFactory> global_factory,
      PeerConnection& owner,
      int mline_index,
      std::string name,
      rtc::scoped_refptr<webrtc::RtpTransceiverInterface> transceiver,
      const VideoTransceiverInitConfig& config) noexcept;

  ~VideoTransceiver() override;

  Result SetDirection(Direction new_direction) noexcept;

  Result SetLocalTrack(RefPtr<LocalVideoTrack> local_track) noexcept;

  RefPtr<LocalVideoTrack> GetLocalTrack() noexcept { return local_track_; }

  RefPtr<RemoteVideoTrack> GetRemoteTrack() noexcept { return remote_track_; }

  int GetMlineIndex() const noexcept { return mline_index_; }

  //
  // Internal
  //

  void OnLocalTrackAdded(RefPtr<LocalVideoTrack> track);
  void OnRemoteTrackAdded(RefPtr<RemoteVideoTrack> track);
  void OnLocalTrackRemoved(LocalVideoTrack* track);
  void OnRemoteTrackRemoved(RemoteVideoTrack* track);
  mrsVideoTransceiverInteropHandle GetInteropHandle() const {
    return interop_handle_;
  }

 protected:
  RefPtr<LocalVideoTrack> local_track_;
  RefPtr<RemoteVideoTrack> remote_track_;

  int mline_index_ = -1;

  /// Transceiver name, for pairing with the remote peer.
  std::string name_;

  /// Optional interop handle, if associated with an interop wrapper.
  mrsVideoTransceiverInteropHandle interop_handle_{};
};

}  // namespace Microsoft::MixedReality::WebRTC
