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

namespace Microsoft::MixedReality::WebRTC {

class PeerConnection;
class VideoTransceiver;

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
class LocalVideoTrack : public VideoFrameObserver, public MediaTrack {
 public:
  /// Constructor for a track not added to any peer connection.
  LocalVideoTrack(RefPtr<GlobalFactory> global_factory,
                  rtc::scoped_refptr<webrtc::VideoTrackInterface> track,
                  mrsLocalVideoTrackInteropHandle interop_handle) noexcept;

  /// Constructor for a track added to a peer connection.
  LocalVideoTrack(RefPtr<GlobalFactory> global_factory,
                  PeerConnection& owner,
                  RefPtr<VideoTransceiver> transceiver,
                  rtc::scoped_refptr<webrtc::VideoTrackInterface> track,
                  rtc::scoped_refptr<webrtc::RtpSenderInterface> sender,
                  mrsLocalVideoTrackInteropHandle interop_handle) noexcept;

  ~LocalVideoTrack() override;

  /// Get the name of the local video track.
  std::string GetName() const noexcept override { return track_name_; }

  /// Enable or disable the video track. An enabled track streams its content
  /// from its source to the remote peer. A disabled video track only sends
  /// black frames.
  void SetEnabled(bool enabled) const noexcept;

  /// Check if the track is enabled.
  /// See |SetEnabled(bool)|.
  [[nodiscard]] bool IsEnabled() const noexcept;

  [[nodiscard]] RefPtr<VideoTransceiver> GetTransceiver() const noexcept;

  //
  // Advanced use
  //

  [[nodiscard]] webrtc::VideoTrackInterface* impl() const;
  [[nodiscard]] webrtc::RtpSenderInterface* sender() const;

  [[nodiscard]] mrsLocalVideoTrackInteropHandle GetInteropHandle() const
      noexcept {
    return interop_handle_;
  }

  /// Internal callback on added to a peer connection to update the internal
  /// state of the object.
  void OnAddedToPeerConnection(
      PeerConnection& owner,
      RefPtr<VideoTransceiver> transceiver,
      rtc::scoped_refptr<webrtc::RtpSenderInterface> sender);

  /// Internal callback on removed from a peer connection to update the internal
  /// state of the object.
  void OnRemovedFromPeerConnection(
      PeerConnection& old_owner,
      RefPtr<VideoTransceiver> old_transceiver,
      rtc::scoped_refptr<webrtc::RtpSenderInterface> old_sender);

  void RemoveFromPeerConnection(webrtc::PeerConnectionInterface& peer);

 private:
  /// Underlying core implementation.
  rtc::scoped_refptr<webrtc::VideoTrackInterface> track_;

  /// RTP sender this track is associated with.
  rtc::scoped_refptr<webrtc::RtpSenderInterface> sender_;

  /// Transceiver this track is associated with, if any.
  RefPtr<VideoTransceiver> transceiver_;

  /// Optional interop handle, if associated with an interop wrapper.
  mrsLocalVideoTrackInteropHandle interop_handle_{};

  /// Cached track name, to avoid dispatching on signaling thread.
  const std::string track_name_;
};

}  // namespace Microsoft::MixedReality::WebRTC
