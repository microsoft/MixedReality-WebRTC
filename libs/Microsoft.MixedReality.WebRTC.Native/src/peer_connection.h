// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#pragma once

#include "audio_frame_observer.h"
#include "callback.h"
#include "data_channel_observer.h"
#include "video_frame_observer.h"

namespace Microsoft::MixedReality::WebRTC {

class PeerConnection;

/// The PeerConnection class is the entry point to most of WebRTC.
/// It encapsulates a single connection between a local peer and a remote peer,
/// and hosts some critical events for signaling and video rendering.
///
/// The high level flow to establish a connection is as follow:
/// - Create a peer connection object from a factory with
/// PeerConnection::create().
/// - Register a custom callback to the various signaling events.
/// - Optionally add audio/video/data tracks. These can also be added after the
/// connection is established, but see remark below.
/// - Create a peer connection offer, or wait for the remote peer to send an
/// offer, and respond with an answer.
///
/// At any point, before or after the connection is initated (CreateOffer() or
/// CreateAnswer()) or established (RegisterConnectedCallback()), some audio,
/// video, and data tracks can be added to it, with the following notable
/// remarks and restrictions:
/// - Data tracks use the DTLS/SCTP protocol and are encrypted; this requires a
/// handshake to exchange encryption secrets. This exchange is only performed
/// during the initial connection handshake if at least one data track is
/// present. As a consequence, at least one data track needs to be added before
/// calling CreateOffer() or CreateAnswer() if the application ever need to use
/// data channels. Otherwise trying to add a data channel after that initial
/// handshake will always fail.
/// - Adding and removing any kind of tracks after the connection has been
/// initiated result in a RenegotiationNeeded event to perform a new track
/// negotitation, which requires signaling to be working. Therefore it is
/// recommended when this is known in advance to add tracks before starting to
/// establish a connection, to perform the first handshake with the correct
/// tracks offer/answer right away.
class PeerConnection : public webrtc::PeerConnectionObserver,
                       public webrtc::CreateSessionDescriptionObserver {
 public:
  /// Create a new PeerConnection using the given |factory|, based on the given
  /// |config|. This serves as the constructor for PeerConnection.
  static rtc::scoped_refptr<PeerConnection> create(
      webrtc::PeerConnectionFactoryInterface& factory,
      const webrtc::PeerConnectionInterface::RTCConfiguration& config);

  ~PeerConnection() noexcept override;

  //
  // Signaling
  //

  /// Callback fired when a local SDP message is ready to be sent to the remote
  /// peer by the signalling solution. The callback parameters are:
  /// - The null-terminated type of the SDP message. Valid values are "offer",
  /// "answer", and "ice".
  /// - The null-terminated SDP message content.
  using LocalSdpReadytoSendCallback = Callback<const char*, const char*>;

  /// Register a custom LocalSdpReadytoSendCallback.
  void RegisterLocalSdpReadytoSendCallback(
      LocalSdpReadytoSendCallback&& callback) noexcept {
    auto lock = std::lock_guard{local_sdp_ready_to_send_callback_mutex_};
    local_sdp_ready_to_send_callback_ = std::move(callback);
  }

  /// Callback fired when a local ICE candidate message is ready to be sent to
  /// the remote peer by the signalling solution. The callback parameters are:
  /// - The null-terminated ICE message content.
  /// - The mline index.
  /// - The MID string value.
  using IceCandidateReadytoSendCallback =
      Callback<const char*, int, const char*>;

  /// Register a custom IceCandidateReadytoSendCallback.
  void RegisterIceCandidateReadytoSendCallback(
      IceCandidateReadytoSendCallback&& callback) noexcept {
    auto lock = std::lock_guard{ice_candidate_ready_to_send_callback_mutex_};
    ice_candidate_ready_to_send_callback_ = std::move(callback);
  }

  /// Callback fired when the state of the ICE connection changed.
  /// Note that the current implementation (m71) mixes the state of ICE and
  /// DTLS, so this does not correspond exactly to
  using IceStateChangedCallback = Callback<IceConnectionState>;

  /// Register a custom IceStateChangedCallback.
  void RegisterIceStateChangedCallback(
      IceStateChangedCallback&& callback) noexcept {
    auto lock = std::lock_guard{ice_state_changed_callback_mutex_};
    ice_state_changed_callback_ = std::move(callback);
  }

  /// Callback fired when some SDP negotiation needs to be initiated, often
  /// because some tracks have been added to or removed from the peer
  /// connection, to notify the remote peer of the change.
  /// Typically an implementation will call CreateOffer() when receiving this
  /// notification to initiate a new SDP exchange. Failing to do so will prevent
  /// the remote peer from being informed about track changes.
  using RenegotiationNeededCallback = Callback<>;

  /// Register a custom RenegotiationNeededCallback.
  void RegisterRenegotiationNeededCallback(
      RenegotiationNeededCallback&& callback) noexcept {
    auto lock = std::lock_guard{renegotiation_needed_callback_mutex_};
    renegotiation_needed_callback_ = std::move(callback);
  }

  /// Notify the WebRTC engine that an ICE candidate has been received.
  bool AddIceCandidate(const char* sdp_mid,
                       const int sdp_mline_index,
                       const char* candidate) noexcept;

  /// Notify the WebRTC engine that an SDP offer message has been received.
  bool SetRemoteDescription(const char* type, const char* sdp) noexcept;

  //
  // Connection
  //

  /// Callback fired when the peer connection is established.
  /// This guarantees that the handshake process has terminated successfully,
  /// but does not guarantee that ICE exchanges are done.
  using ConnectedCallback = Callback<>;

  /// Register a custom ConnectedCallback.
  void RegisterConnectedCallback(ConnectedCallback&& callback) noexcept {
    auto lock = std::lock_guard{connected_callback_mutex_};
    connected_callback_ = std::move(callback);
  }

  /// Create an SDP offer to attempt to establish a connection with the remote
  /// peer. Once the offer message is ready, the LocalSdpReadytoSendCallback
  /// callback is invoked to deliver the message.
  bool CreateOffer() noexcept;

  /// Create an SDP answer to accept a previously-received offer to establish a
  /// connection wit the remote peer. Once the answer message is ready, the
  /// LocalSdpReadytoSendCallback callback is invoked to deliver the message.
  bool CreateAnswer() noexcept;

  //
  // Remote tracks
  //

  /// Callback fired when a remote track is added to the peer connection.
  using TrackAddedCallback = Callback<TrackKind>;

  /// Register a custom TrackAddedCallback.
  void RegisterTrackAddedCallback(TrackAddedCallback&& callback) noexcept {
    auto lock = std::lock_guard{track_added_callback_mutex_};
    track_added_callback_ = std::move(callback);
  }

  /// Callback fired when a remote track is removed from the peer connection.
  using TrackRemovedCallback = Callback<TrackKind>;

  /// Register a custom TrackRemovedCallback.
  void RegisterTrackRemovedCallback(TrackRemovedCallback&& callback) noexcept {
    auto lock = std::lock_guard{track_removed_callback_mutex_};
    track_removed_callback_ = std::move(callback);
  }

  //
  // Video
  //

  /// Register a custom callback invoked when a local video frame is ready to be
  /// displayed.
  void RegisterLocalVideoFrameCallback(
      I420FrameReadyCallback callback) noexcept {
    if (local_video_observer_) {
      local_video_observer_->SetCallback(std::move(callback));
    }
  }

  /// Register a custom callback invoked when a local video frame is ready to be
  /// displayed.
  void RegisterLocalVideoFrameCallback(
      ARGBFrameReadyCallback callback) noexcept {
    if (local_video_observer_) {
      local_video_observer_->SetCallback(std::move(callback));
    }
  }

  /// Register a custom callback invoked when a remote video frame has been
  /// received and decompressed, and is ready to be displayed locally.
  void RegisterRemoteVideoFrameCallback(
      I420FrameReadyCallback callback) noexcept {
    if (remote_video_observer_) {
      remote_video_observer_->SetCallback(std::move(callback));
    }
  }

  /// Register a custom callback invoked when a remote video frame has been
  /// received and decompressed, and is ready to be displayed locally.
  void RegisterRemoteVideoFrameCallback(
      ARGBFrameReadyCallback callback) noexcept {
    if (remote_video_observer_) {
      remote_video_observer_->SetCallback(std::move(callback));
    }
  }

  /// Add to the peer connection an audio track backed by a local audio capture
  /// device.
  bool AddLocalVideoTrack(
      rtc::scoped_refptr<webrtc::VideoTrackInterface> video_track) noexcept;
  void RemoveLocalVideoTrack() noexcept;

  //
  // Audio
  //

  /// Register a custom callback invoked when a local audio frame is ready to be
  /// output.
  ///
  /// FIXME - Current implementation of AddSink() for the local audio capture
  /// device is no-op. So this callback is never fired.
  void RegisterLocalAudioFrameCallback(
      AudioFrameReadyCallback callback) noexcept {
    if (local_audio_observer_) {
      local_audio_observer_->SetCallback(std::move(callback));
    }
  }

  /// Register a custom callback invoked when a remote audio frame has been
  /// received and uncompressed, and is ready to be output locally.
  void RegisterRemoteAudioFrameCallback(
      AudioFrameReadyCallback callback) noexcept {
    if (remote_audio_observer_) {
      remote_audio_observer_->SetCallback(std::move(callback));
    }
  }

  bool AddLocalAudioTrack(
      rtc::scoped_refptr<webrtc::AudioTrackInterface> audio_track) noexcept;
  void RemoveLocalAudioTrack() noexcept;

  //
  // Data channel
  //

  mrsResult AddDataChannel(int id,
                           const char* label,
                           bool ordered,
                           bool reliable,
                           DataChannelMessageCallback message_callback,
                           DataChannelBufferingCallback buffering_callback,
                           DataChannelStateCallback state_callback) noexcept;
  bool RemoveDataChannel(int id) noexcept;
  bool RemoveDataChannel(const char* label) noexcept;

  bool SendDataChannelMessage(int id, const void* data, uint64_t size) noexcept;

  //
  // Advanced use
  //

  /// Retrieve the underlying PeerConnectionInterface from the core
  /// implementation, for direct manipulation. This allows direct access to the
  /// core PeerConnection API, but will break this class if not used carefully.
  rtc::scoped_refptr<webrtc::PeerConnectionInterface> GetImpl() const {
    return peer_;
  }

 protected:
  //
  // PeerConnectionObserver interface
  //

  // Triggered when the SignalingState changed.
  void OnSignalingChange(webrtc::PeerConnectionInterface::SignalingState
                             new_state) noexcept override;

  // Triggered when media is received on a new stream from remote peer.
  void OnAddStream(rtc::scoped_refptr<webrtc::MediaStreamInterface>
                       stream) noexcept override;

  // Triggered when a remote peer closes a stream.
  void OnRemoveStream(rtc::scoped_refptr<webrtc::MediaStreamInterface>
                          stream) noexcept override;

  // Triggered when a remote peer opens a data channel.
  void OnDataChannel(
      rtc::scoped_refptr<webrtc::DataChannelInterface> data_channel)
#if defined(WINUWP)
      noexcept(false)
#else
      noexcept
#endif
          override;

  /// Triggered when renegotiation is needed. For example, an ICE restart
  /// has begun, or a track has been added or removed.
  void OnRenegotiationNeeded() noexcept override;

  /// Called any time the IceConnectionState changes.
  ///
  /// From the Google implementation:
  /// "Note that our ICE states lag behind the standard slightly. The most
  /// notable differences include the fact that "failed" occurs after 15
  /// seconds, not 30, and this actually represents a combination ICE + DTLS
  /// state, so it may be "failed" if DTLS fails while ICE succeeds."
  void OnIceConnectionChange(webrtc::PeerConnectionInterface::IceConnectionState
                                 new_state) noexcept override;

  /// Called any time the IceGatheringState changes.
  void OnIceGatheringChange(webrtc::PeerConnectionInterface::IceGatheringState
                            /*new_state*/) noexcept override {}

  /// A new ICE candidate has been gathered.
  void OnIceCandidate(
      const webrtc::IceCandidateInterface* candidate) noexcept override;

  /// Callback on remote track added.
  void OnAddTrack(
      rtc::scoped_refptr<webrtc::RtpReceiverInterface> receiver,
      const std::vector<rtc::scoped_refptr<webrtc::MediaStreamInterface>>&
          streams) noexcept override;

  /// Callback on remote track removed.
  void OnRemoveTrack(rtc::scoped_refptr<webrtc::RtpReceiverInterface>
                         receiver) noexcept override;

  /// Protected constructor. Use PeerConnection::create() instead.
  PeerConnection();

  //
  // CreateSessionDescriptionObserver interface
  //

  /// This callback transfers the ownership of the |desc|.
  /// TODO(deadbeef): Make this take an std::unique_ptr<> to avoid confusion
  /// around ownership.
  void OnSuccess(webrtc::SessionDescriptionInterface* desc) noexcept override;

  /// The OnFailure callback takes an RTCError, which consists of an
  /// error code and a string.
  /// RTCError is non-copyable, so it must be passed using std::move.
  /// Earlier versions of the API used a string argument. This version
  /// is deprecated; in order to let clients remove the old version, it has a
  /// default implementation. If both versions are unimplemented, the
  /// result will be a runtime error (stack overflow). This is intentional.
  void OnFailure(webrtc::RTCError error) noexcept override {}

 protected:
  /// The underlying PC object from the core implementation.
  rtc::scoped_refptr<webrtc::PeerConnectionInterface> peer_;

  /// User callback invoked when the peer connection is established.
  /// This is generally invoked even if ICE didn't finish.
  ConnectedCallback connected_callback_
      RTC_GUARDED_BY(connected_callback_mutex_);

  /// User callback invoked when a local SDP message has been crafted by the
  /// core engine and is ready to be sent by the signaling solution.
  LocalSdpReadytoSendCallback local_sdp_ready_to_send_callback_
      RTC_GUARDED_BY(local_sdp_ready_to_send_callback_mutex_);

  /// User callback invoked when a local ICE message has been crafted by the
  /// core engine and is ready to be sent by the signaling solution.
  IceCandidateReadytoSendCallback ice_candidate_ready_to_send_callback_
      RTC_GUARDED_BY(ice_candidate_ready_to_send_callback_mutex_);

  /// User callback invoked when the ICE connection state changed.
  IceStateChangedCallback ice_state_changed_callback_
      RTC_GUARDED_BY(ice_state_changed_callback_mutex_);

  /// User callback invoked when SDP renegotiation is needed.
  RenegotiationNeededCallback renegotiation_needed_callback_
      RTC_GUARDED_BY(renegotiation_needed_callback_mutex_);

  /// User callback invoked when a remote audio or video track is added.
  TrackAddedCallback track_added_callback_
      RTC_GUARDED_BY(track_added_callback_mutex_);

  /// User callback invoked when a remote audio or video track is removed.
  TrackRemovedCallback track_removed_callback_
      RTC_GUARDED_BY(track_removed_callback_mutex_);

  std::mutex connected_callback_mutex_;
  std::mutex local_sdp_ready_to_send_callback_mutex_;
  std::mutex ice_candidate_ready_to_send_callback_mutex_;
  std::mutex ice_state_changed_callback_mutex_;
  std::mutex renegotiation_needed_callback_mutex_;
  std::mutex track_added_callback_mutex_;
  std::mutex track_removed_callback_mutex_;

  rtc::scoped_refptr<webrtc::VideoTrackInterface> local_video_track_;
  rtc::scoped_refptr<webrtc::AudioTrackInterface> local_audio_track_;
  rtc::scoped_refptr<webrtc::RtpSenderInterface> local_video_sender_;
  rtc::scoped_refptr<webrtc::RtpSenderInterface> local_audio_sender_;
  std::vector<rtc::scoped_refptr<webrtc::MediaStreamInterface>> remote_streams_;

  /// Collection of data channels from their unique ID.
  /// This contains only data channels pre-negotiated or opened by the remote
  /// peer, as data channels opened locally won't have immediately a unique ID.
  std::unordered_map<int, std::shared_ptr<DataChannelObserver>>
      data_channel_from_id_;

  /// Collection of data channels from their label.
  /// This contains only data channels with a non-empty label.
  std::unordered_multimap<std::string, std::shared_ptr<DataChannelObserver>>
      data_channel_from_label_;

  //< TODO - Clarify lifetime of those, for now same as this PeerConnection
  std::unique_ptr<AudioFrameObserver> local_audio_observer_;
  std::unique_ptr<AudioFrameObserver> remote_audio_observer_;
  std::unique_ptr<VideoFrameObserver> local_video_observer_;
  std::unique_ptr<VideoFrameObserver> remote_video_observer_;

  /// Flag to indicate if SCTP was negotiated during the initial SDP handshake
  /// (m=application), which allows subsequently to use data channels. If this
  /// is false then data channels will never connnect. This is set to true if a
  /// data channel is created before the connection is established, which will
  /// force the connection to negotiate the necessary SCTP information. See
  /// https://stackoverflow.com/questions/43788872/how-are-data-channels-negotiated-between-two-peers-with-webrtc
  bool sctp_negotiated_ = true;

 private:
  PeerConnection(const PeerConnection&) = delete;
  PeerConnection& operator=(const PeerConnection&) = delete;
};

}  // namespace Microsoft::MixedReality::WebRTC
