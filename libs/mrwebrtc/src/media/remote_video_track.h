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

namespace Microsoft {
namespace MixedReality {
namespace WebRTC {

class PeerConnection;
class Transceiver;

/// A remote video track is a media track for a peer connection backed by a
/// remote video stream received from the remote peer.
///
/// The remote nature of the track implies that the remote peer has control on
/// it, including enabling or disabling the track, and removing it from the peer
/// connection. The local peer only has limited control over the track.
class RemoteVideoTrack : public MediaTrack, public VideoFrameObserver {
 public:
  RemoteVideoTrack(
      RefPtr<GlobalFactory> global_factory,
      PeerConnection& owner,
      Transceiver* transceiver,
      rtc::scoped_refptr<webrtc::VideoTrackInterface> track,
      rtc::scoped_refptr<webrtc::RtpReceiverInterface> receiver) noexcept;
  ~RemoteVideoTrack() override;

  /// Enable or disable the video track. An enabled track streams its content
  /// from its source to the remote peer. A disabled video track only sends
  /// black frames.
  void SetEnabled(bool enabled) const noexcept;

  /// Check if the track is enabled.
  /// See |SetEnabled(bool)|.
  MRS_NODISCARD bool IsEnabled() const noexcept;

  //
  // Advanced use
  //

  /// Get a handle to the remote video track. This handle is valid until the
  /// remote track is removed from the peer connection and destroyed, which is
  /// signaled by the |TrackRemoved| event on the peer connection.
  MRS_NODISCARD constexpr mrsRemoteVideoTrackHandle GetHandle() const noexcept {
    return (mrsRemoteVideoTrackHandle)this;
  }

  MRS_NODISCARD webrtc::VideoTrackInterface* impl() const;
  MRS_NODISCARD webrtc::RtpReceiverInterface* receiver() const;

  MRS_NODISCARD constexpr Transceiver* GetTransceiver() const {
    return transceiver_;
  }

  MRS_NODISCARD webrtc::MediaStreamTrackInterface* GetMediaImpl()
      const override {
    return impl();
  }

  // Automatically called - do not use.
  void OnTrackRemoved(PeerConnection& owner);

 private:
  /// Underlying core implementation.
  rtc::scoped_refptr<webrtc::VideoTrackInterface> track_;

  /// RTP sender this track is associated with.
  rtc::scoped_refptr<webrtc::RtpReceiverInterface> receiver_;

  /// Weak back-pointer to the Transceiver this track is associated with. This
  /// avoids a circular reference with the transceiver itself.
  /// Note that unlike local tracks, this is never NULL since the remote track
  /// gets destroyed when detached from the transceiver.
  Transceiver* transceiver_{nullptr};
};

}  // namespace WebRTC
}  // namespace MixedReality
}  // namespace Microsoft
