// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include "audio_frame_observer.h"
#include "callback.h"
#include "data_channel.h"
#include "mrs_errors.h"
#include "refptr.h"
#include "tracked_object.h"
#include "video_frame_observer.h"

namespace Microsoft::MixedReality::WebRTC {

class PeerConnection;
class LocalVideoTrack;
class ExternalVideoTrackSource;
class DataChannel;

/// Settings for bitrate connection limits for a peer connection.
struct BitrateSettings {
  /// Start bitrate in bits per seconds when the connection is established.
  /// After that the connection will monitor the network bandwidth and media
  /// quality, and automatically adjust the bitrate.
  std::optional<int> start_bitrate_bps;

  /// Minimum bitrate in bits per seconds.
  std::optional<int> min_bitrate_bps;

  /// Maximum bitrate in bits per seconds.
  std::optional<int> max_bitrate_bps;
};

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
class PeerConnection : public TrackedObject {
 public:
  /// Create a new PeerConnection based on the given |config|.
  /// This serves as the constructor for PeerConnection.
  static ErrorOr<RefPtr<PeerConnection>> create(
      const PeerConnectionConfiguration& config,
      mrsPeerConnectionInteropHandle interop_handle);

  /// Set the name of the peer connection. This is a friendly name opaque to the
  /// implementation, used mainly for debugging and logging.
  virtual void SetName(std::string_view name) = 0;

  //
  // Signaling
  //

  /// Callback invoked when a local SDP message is ready to be sent to the
  /// remote peer by the signalling solution, after being generated
  /// asynchronously by |CreateOffer()| or |CreateAnswer()|.
  ///
  /// The callback parameters are:
  /// - The null-terminated type of the SDP message. Valid values are "offer" or
  /// "answer".
  /// - The null-terminated SDP message content.
  using LocalSdpReadytoSendCallback = Callback<const char*, const char*>;

  /// Register a custom LocalSdpReadytoSendCallback invoked when a local SDP
  /// message is ready to be sent by the user to the remote peer. Users MUST
  /// register a callback and handle sending SDP messages to the remote peer,
  /// otherwise the connection cannot be established, even on local network.
  /// Only one callback can be registered at a time.
  virtual void RegisterLocalSdpReadytoSendCallback(
      LocalSdpReadytoSendCallback&& callback) noexcept = 0;

  /// Callback invoked when a local ICE candidate message is ready to be sent to
  /// the remote peer via the signalling solution.
  ///
  /// The callback parameters are:
  /// - The null-terminated ICE message content.
  /// - The mline index.
  /// - The MID string value.
  using IceCandidateReadytoSendCallback =
      Callback<const char*, int, const char*>;

  /// Register a custom |IceCandidateReadytoSendCallback| invoked when a local
  /// ICE candidate has been generated and is ready to be sent. Users MUST
  /// register a callback and handle sending ICE candidates, otherwise the
  /// connection cannot be established (except on local networks with direct IP
  /// access, where NAT traversal is not needed). Only one callback can be
  /// registered at a time.
  virtual void RegisterIceCandidateReadytoSendCallback(
      IceCandidateReadytoSendCallback&& callback) noexcept = 0;

  /// Callback invoked when the state of the ICE connection changed.
  /// Note that the current implementation (M71) mixes the state of ICE and
  /// DTLS, so this does not correspond exactly to the ICE connection state of
  /// the WebRTC 1.0 standard.
  using IceStateChangedCallback = Callback<IceConnectionState>;

  /// Register a custom |IceStateChangedCallback| invoked when the state of the
  /// ICE connection changed. Only one callback can be registered at a time.
  virtual void RegisterIceStateChangedCallback(
      IceStateChangedCallback&& callback) noexcept = 0;

  /// Callback invoked when the state of the ICE gathering changed.
  using IceGatheringStateChangedCallback = Callback<IceGatheringState>;

  /// Register a custom |IceStateChangedCallback| invoked when the state of the
  /// ICE gathering process changed. Only one callback can be registered at a
  /// time.
  virtual void RegisterIceGatheringStateChangedCallback(
      IceGatheringStateChangedCallback&& callback) noexcept = 0;

  /// Callback invoked when some SDP negotiation needs to be initiated, often
  /// because some tracks have been added to or removed from the peer
  /// connection, to notify the remote peer of the change and negotiate a new
  /// session. Typically an implementation will call |CreateOffer()| when
  /// receiving this notification to initiate a new SDP exchange. Failing to do
  /// so will prevent the remote peer from being informed about track changes.
  /// It is possible to delay calling |CreateOffer()| if the user expects
  /// multiple changes in a short timeframe, to group together all changes and
  /// perform a single renegotiation, for optimization purpose.
  using RenegotiationNeededCallback = Callback<>;

  /// Register a custom |RenegotiationNeededCallback| invoked when a session
  /// renegotiation is needed. Only one callback can be registered at a time.
  virtual void RegisterRenegotiationNeededCallback(
      RenegotiationNeededCallback&& callback) noexcept = 0;

  /// Notify the WebRTC engine that an ICE candidate has been received from the
  /// remote peer. The parameters correspond to the SDP message data provided by
  /// the |IceCandidateReadytoSendCallback|, after being transmitted to the
  /// other peer.
  virtual bool AddIceCandidate(const char* sdp_mid,
                               const int sdp_mline_index,
                               const char* candidate) noexcept = 0;

  /// Notify the WebRTC engine that an SDP message has been received from the
  /// remote peer. The parameters correspond to the SDP message data provided by
  /// the |LocalSdpReadytoSendCallback|, after being transmitted to the
  /// other peer.
  virtual bool SetRemoteDescriptionAsync(const char* type,
                                         const char* sdp,
                                         Callback<> callback) noexcept = 0;

  //
  // Connection
  //

  /// Callback invoked when the peer connection is established.
  /// This guarantees that the handshake process has terminated successfully,
  /// but does not guarantee that ICE exchanges are done.
  using ConnectedCallback = Callback<>;

  /// Register a custom |ConnectedCallback| invoked when the connection is
  /// established. Only one callback can be registered at a time.
  virtual void RegisterConnectedCallback(
      ConnectedCallback&& callback) noexcept = 0;

  /// Set the connection bitrate limits. These settings limit the network
  /// bandwidth use of the peer connection.
  virtual mrsResult SetBitrate(const BitrateSettings& settings) noexcept = 0;

  /// Create an SDP offer to attempt to establish a connection with the remote
  /// peer. Once the offer message is ready, the |LocalSdpReadytoSendCallback|
  /// callback is invoked to deliver the message.
  virtual bool CreateOffer() noexcept = 0;

  /// Create an SDP answer to accept a previously-received offer to establish a
  /// connection wit the remote peer. Once the answer message is ready, the
  /// |LocalSdpReadytoSendCallback| callback is invoked to deliver the message.
  virtual bool CreateAnswer() noexcept = 0;

  /// Close the peer connection. After the connection is closed, it cannot be
  /// opened again with the same C++ object. Instantiate a new |PeerConnection|
  /// object instead to create a new connection. No-op if already closed.
  virtual void Close() noexcept = 0;

  /// Check if the connection is closed. This returns |true| once |Close()| has
  /// been called.
  virtual bool IsClosed() const noexcept = 0;

  //
  // Remote tracks
  //

  /// Callback fired when a remote track is added to the peer connection.
  using TrackAddedCallback = Callback<TrackKind>;

  /// Register a custom TrackAddedCallback.
  virtual void RegisterTrackAddedCallback(
      TrackAddedCallback&& callback) noexcept = 0;

  /// Callback fired when a remote track is removed from the peer connection.
  using TrackRemovedCallback = Callback<TrackKind>;

  /// Register a custom TrackRemovedCallback.
  virtual void RegisterTrackRemovedCallback(
      TrackRemovedCallback&& callback) noexcept = 0;

  //
  // Video
  //

  /// Register a custom callback invoked when a remote video frame has been
  /// received and decompressed, and is ready to be displayed locally.
  virtual void RegisterRemoteVideoFrameCallback(
      I420AFrameReadyCallback callback) noexcept = 0;

  /// Register a custom callback invoked when a remote video frame has been
  /// received and decompressed, and is ready to be displayed locally.
  virtual void RegisterRemoteVideoFrameCallback(
      Argb32FrameReadyCallback callback) noexcept = 0;

  /// Add a video track to the peer connection. If no RTP sender/transceiver
  /// exist, create a new one for that track.
  virtual ErrorOr<RefPtr<LocalVideoTrack>> AddLocalVideoTrack(
      rtc::scoped_refptr<webrtc::VideoTrackInterface> video_track,
      mrsLocalVideoTrackInteropHandle interop_handle) noexcept = 0;

  /// Remove a local video track from the peer connection.
  /// The underlying RTP sender/transceiver are kept alive but inactive.
  virtual webrtc::RTCError RemoveLocalVideoTrack(
      LocalVideoTrack& video_track) noexcept = 0;

  /// Remove all tracks sharing the given video track source.
  /// Note that currently video source sharing is not supported, so this will
  /// remove at most a single track backed by the given source.
  virtual void RemoveLocalVideoTracksFromSource(
      ExternalVideoTrackSource& source) noexcept = 0;

  /// Rounding mode of video frame height for |SetFrameHeightRoundMode()|.
  /// This is only used on HoloLens 1 (UWP x86).
  enum class FrameHeightRoundMode {
    /// Leave frames unchanged.
    kNone = 0,

    /// Crop frame height to the nearest multiple of 16 pixels.
    /// ((height - nearestLowerMultipleOf16) / 2) rows are cropped from the top
    /// and (height - nearestLowerMultipleOf16 - croppedRowsTop) rows are
    /// cropped from the bottom.
    kCrop = 1,

    /// Pad frame height to the nearest multiple of 16 pixels.
    /// ((nearestHigherMultipleOf16 - height) / 2) rows are added symmetrically
    /// at the top and (nearestHigherMultipleOf16 - height - addedRowsTop) rows
    /// are added symmetrically at the bottom.
    kPad = 2
  };

  /// [HoloLens 1 only]
  /// Use this function to select whether resolutions where height is not a
  /// multiple of 16 pixels should be cropped, padded or left unchanged.
  /// Defaults to FrameHeightRoundMode::kCrop to avoid severe artifacts produced
  /// by the H.264 hardware encoder on HoloLens 1 due to a bug. The default
  /// value is applied when creating the first peer connection, so can be
  /// overridden with |SetFrameHeightRoundMode()| after that.
  static void SetFrameHeightRoundMode(FrameHeightRoundMode value);

  //
  // Audio
  //

  /// Register a custom callback invoked when a local audio frame is ready to be
  /// output.
  ///
  /// FIXME - Current implementation of AddSink() for the local audio capture
  /// device is no-op. So this callback is never fired.
  virtual void RegisterLocalAudioFrameCallback(
      AudioFrameReadyCallback callback) noexcept = 0;

  /// Register a custom callback invoked when a remote audio frame has been
  /// received and uncompressed, and is ready to be output locally.
  virtual void RegisterRemoteAudioFrameCallback(
      AudioFrameReadyCallback callback) noexcept = 0;

  /// Add to the peer connection an audio track backed by a local audio capture
  /// device. If no RTP sender/transceiver exist, create a new one for that
  /// track.
  ///
  /// Note: currently a single local video track is supported per peer
  /// connection.
  virtual bool AddLocalAudioTrack(
      rtc::scoped_refptr<webrtc::AudioTrackInterface> audio_track) noexcept = 0;

  /// Remove the existing local audio track from the peer connection.
  /// The underlying RTP sender/transceiver are kept alive but inactive.
  ///
  /// Note: currently a single local audio track is supported per peer
  /// connection.
  virtual void RemoveLocalAudioTrack() noexcept = 0;

  /// Enable or disable the local audio track. Disabled audio tracks are still
  /// active but are silent, and do not consume network bandwidth. Additionally,
  /// enabling/disabling the local audio track does not require an SDP exchange.
  /// Therefore this is a cheaper alternative to removing and re-adding the
  /// track.
  ///
  /// Note: currently a single local audio track is supported per peer
  /// connection.
  virtual void MRS_API
  SetLocalAudioTrackEnabled(bool enabled = true) noexcept = 0;

  /// Check if the local audio frame is enabled.
  ///
  /// Note: currently a single local audio track is supported per peer
  /// connection.
  virtual bool IsLocalAudioTrackEnabled() const noexcept = 0;

  //
  // Data channel
  //

  /// Callback invoked when a new data channel is received from the remote peer
  /// and added locally.
  using DataChannelAddedCallback =
      Callback<mrsDataChannelInteropHandle, DataChannelHandle>;

  /// Callback invoked when a data channel is removed from the remote peer and
  /// removed locally.
  using DataChannelRemovedCallback =
      Callback<mrsDataChannelInteropHandle, DataChannelHandle>;

  /// Register a custom callback invoked when a new data channel is received
  /// from the remote peer and added locally. Only one callback can be
  /// registered at a time.
  virtual void RegisterDataChannelAddedCallback(
      DataChannelAddedCallback callback) noexcept = 0;

  /// Register a custom callback invoked when a data channel is removed by the
  /// remote peer and removed locally. Only one callback can be registered at a
  /// time.
  virtual void RegisterDataChannelRemovedCallback(
      DataChannelRemovedCallback callback) noexcept = 0;

  /// Create a new data channel and add it to the peer connection.
  /// This invokes the DataChannelAdded callback.
  ErrorOr<std::shared_ptr<DataChannel>> virtual AddDataChannel(
      int id,
      std::string_view label,
      bool ordered,
      bool reliable,
      mrsDataChannelInteropHandle dataChannelInteropHandle) noexcept = 0;

  /// Close a given data channel and remove it from the peer connection.
  /// This invokes the DataChannelRemoved callback.
  virtual void MRS_API
  RemoveDataChannel(const DataChannel& data_channel) noexcept = 0;

  /// Close and remove from the peer connection all data channels at once.
  /// This invokes the DataChannelRemoved callback for each data channel.
  virtual void RemoveAllDataChannels() noexcept = 0;

  /// Notification from a non-negotiated DataChannel that it is open, so that
  /// the PeerConnection can fire a DataChannelAdded event. This is called
  /// automatically by non-negotiated data channels; do not call manually.
  virtual void MRS_API
  OnDataChannelAdded(const DataChannel& data_channel) noexcept = 0;

  /// Internal use.
  void GetStats(webrtc::RTCStatsCollectorCallback* callback);

  //
  // Advanced use
  //

  /// Register some interop callbacks to allow the native implementation to
  /// interact with the interop layer. This is called automatically; do not call
  /// manually.
  virtual mrsResult RegisterInteropCallbacks(
      const mrsPeerConnectionInteropCallbacks& callbacks) noexcept = 0;

 protected:
  PeerConnection(RefPtr<GlobalFactory> global_factory);
};

}  // namespace Microsoft::MixedReality::WebRTC
