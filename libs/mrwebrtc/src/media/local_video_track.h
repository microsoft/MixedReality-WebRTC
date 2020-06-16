// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include "callback.h"
#include "interop_api.h"
#include "media/media_track.h"
#include "refptr.h"
#include "tracked_object.h"
#include "video_frame_observer.h"

namespace rtc {
template <typename T>
class scoped_refptr;
}

namespace webrtc {
class RtpSenderInterface;
class VideoTrackInterface;
}  // namespace webrtc

namespace Microsoft {
namespace MixedReality {
namespace WebRTC {

class PeerConnection;
class Transceiver;

/// A local video track is a media track for a peer connection backed by a local
/// source, and transmitted to a remote peer.
///
/// The local nature of the track implies that the local peer has control on it,
/// including enabling or disabling the track, and removing it from the peer
/// connection. This is in contrast with a remote track	reflecting a track sent
/// by the remote peer, for which the local peer has limited control.
///
/// The local video track is backed by a local video track source. This is
/// typically a video capture device (e.g. webcam), but can	also be a source
/// producing programmatically generated frames. The local video track itself
/// has no knowledge about how the source produces the frames.
class LocalVideoTrack : public MediaTrack, public VideoFrameObserver {
 public:
  /// Constructor for a track not added to any peer connection.
  LocalVideoTrack(RefPtr<GlobalFactory> global_factory,
                  rtc::scoped_refptr<webrtc::VideoTrackInterface> track) noexcept;

  /// Constructor for a track added to a peer connection.
  LocalVideoTrack(RefPtr<GlobalFactory> global_factory,
                  PeerConnection& owner,
                  Transceiver* transceiver,
                  rtc::scoped_refptr<webrtc::VideoTrackInterface> track,
                  rtc::scoped_refptr<webrtc::RtpSenderInterface> sender) noexcept;

  ~LocalVideoTrack() override;

  /// Enable or disable the video track. An enabled track streams its content
  /// from its source to the remote peer. A disabled video track only sends
  /// black frames.
  void SetEnabled(bool enabled) const noexcept;

  /// Check if the track is enabled.
  /// See |SetEnabled(bool)|.
  MRS_NODISCARD bool IsEnabled() const noexcept;

  MRS_NODISCARD Transceiver* GetTransceiver() const noexcept {
    return transceiver_;
  }

  //
  // Advanced use
  //

  MRS_NODISCARD webrtc::VideoTrackInterface* impl() const;
  MRS_NODISCARD webrtc::RtpSenderInterface* sender() const;

  MRS_NODISCARD webrtc::MediaStreamTrackInterface* GetMediaImpl()
      const override {
    return impl();
  }

  /// Internal callback on added to a peer connection to update the internal
  /// state of the object.
  void OnAddedToPeerConnection(
      PeerConnection& owner,
      Transceiver* transceiver,
      rtc::scoped_refptr<webrtc::RtpSenderInterface> sender);

  /// Internal callback on removed from a peer connection to update the internal
  /// state of the object.
  void OnRemovedFromPeerConnection(
      PeerConnection& old_owner,
      Transceiver* old_transceiver,
      rtc::scoped_refptr<webrtc::RtpSenderInterface> old_sender);

  void RemoveFromPeerConnection(webrtc::PeerConnectionInterface& peer);

 private:
  /// Underlying core implementation.
  rtc::scoped_refptr<webrtc::VideoTrackInterface> track_;

  /// RTP sender this track is associated with.
  rtc::scoped_refptr<webrtc::RtpSenderInterface> sender_;

  /// Weak back-pointer to the Transceiver this track is associated with, if
  /// any. This avoids a circular reference with the transceiver itself.
  Transceiver* transceiver_{nullptr};
};

}  // namespace WebRTC
}  // namespace MixedReality
}  // namespace Microsoft
