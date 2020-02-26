// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include "callback.h"
#include "interop_api.h"
#include "media_track.h"
#include "refptr.h"
#include "tracked_object.h"
#include "video_frame_observer.h"

namespace rtc {
template <typename T>
class scoped_refptr;
}

namespace webrtc {
class RtpReceiverInterface;
class VideoTrackInterface;
}  // namespace webrtc

namespace Microsoft::MixedReality::WebRTC {

class PeerConnection;
class VideoTransceiver;

/// A remote video track is a media track for a peer connection backed by a
/// remote video stream received from the remote peer.
///
/// The remote nature of the track implies that the remote peer has control on
/// it, including enabling or disabling the track, and removing it from the peer
/// connection. The local peer only has limited control over the track.
class RemoteVideoTrack : public VideoFrameObserver, public MediaTrack {
 public:
  RemoteVideoTrack(RefPtr<GlobalFactory> global_factory,
                   PeerConnection& owner,
                   RefPtr<VideoTransceiver> transceiver,
                   rtc::scoped_refptr<webrtc::VideoTrackInterface> track,
                   rtc::scoped_refptr<webrtc::RtpReceiverInterface> receiver,
                   mrsRemoteVideoTrackInteropHandle interop_handle) noexcept;
  ~RemoteVideoTrack() override;

  /// Get the name of the remote video track.
  std::string GetName() const noexcept override { return track_name_; }

  /// Enable or disable the video track. An enabled track streams its content
  /// from its source to the remote peer. A disabled video track only sends
  /// black frames.
  void SetEnabled(bool enabled) const noexcept;

  /// Check if the track is enabled.
  /// See |SetEnabled(bool)|.
  [[nodiscard]] bool IsEnabled() const noexcept;

  //
  // Advanced use
  //

  [[nodiscard]] webrtc::VideoTrackInterface* impl() const;
  [[nodiscard]] webrtc::RtpReceiverInterface* receiver() const;
  [[nodiscard]] VideoTransceiver* GetTransceiver() const;

  [[nodiscard]] mrsRemoteVideoTrackInteropHandle GetInteropHandle() const
      noexcept {
    return interop_handle_;
  }

  // Automatically called - do not use.
  void OnTrackRemoved(PeerConnection& owner);

 private:
  /// Underlying core implementation.
  rtc::scoped_refptr<webrtc::VideoTrackInterface> track_;

  /// RTP sender this track is associated with.
  rtc::scoped_refptr<webrtc::RtpReceiverInterface> receiver_;

  /// Transceiver this track is associated with, if any.
  RefPtr<VideoTransceiver> transceiver_;

  /// Optional interop handle, if associated with an interop wrapper.
  mrsRemoteVideoTrackInteropHandle interop_handle_{};

  /// Cached track name, to avoid dispatching on signaling thread.
  const std::string track_name_;
};

}  // namespace Microsoft::MixedReality::WebRTC
