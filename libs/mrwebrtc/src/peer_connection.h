// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include "audio_frame_observer.h"
#include "callback.h"
#include "data_channel.h"
#include "media/transceiver.h"
#include "mrs_errors.h"
#include "peer_connection_interop.h"
#include "refptr.h"
#include "toggle_audio_mixer.h"
#include "tracked_object.h"
#include "utils.h"
#include "video_frame_observer.h"

namespace Microsoft {
namespace MixedReality {
namespace WebRTC {

class PeerConnection;
class LocalAudioTrack;
class LocalVideoTrack;
class ExternalVideoTrackSource;
class DataChannel;

/// Settings for bitrate connection limits for a peer connection.
struct BitrateSettings {
  /// Start bitrate in bits per seconds when the connection is established.
  /// After that the connection will monitor the network bandwidth and media
  /// quality, and automatically adjust the bitrate.
  absl::optional<int> start_bitrate_bps;

  /// Minimum bitrate in bits per seconds.
  absl::optional<int> min_bitrate_bps;

  /// Maximum bitrate in bits per seconds.
  absl::optional<int> max_bitrate_bps;
};

/// The PeerConnection class is the entry point to most of WebRTC.
/// It encapsulates a single connection between a local peer and a remote peer,
/// and hosts some critical events for signaling.
///
/// The high level flow to establish a connection is as follow:
/// - Create a peer connection object from a factory with
/// PeerConnection::create().
/// - Register custom callbacks to the various signaling events, and dispatch
/// signaling messages back and forth using the chosen signaling solution.
/// - Optionally add audio/video/data tracks. These can also be added after the
/// connection is established, but see remark below.
/// - Create a peer connection offer with |CreateOffer()|, or wait for the
/// remote peer to send an offer, and respond with an answer with
/// |CreateAnswer()|.
///
/// At any point, before or after the connection is initated (|CreateOffer()| or
/// |CreateAnswer()|) or established (|RegisterConnectedCallback()|), some
/// audio, video, and data tracks can be added to it, with the following notable
/// remarks and restrictions:
/// - Data tracks use the DTLS/SCTP protocol and are encrypted; this requires a
/// handshake to exchange encryption secrets. This exchange is only performed
/// during the initial connection handshake if at least one data track is
/// present. As a consequence, at least one data track needs to be added before
/// calling |CreateOffer()| or |CreateAnswer()| if the application ever need to
/// use data channels. Otherwise trying to add a data channel after that initial
/// handshake will always fail.
/// - Adding and removing any kind of tracks after the connection has been
/// initiated result in a |RenegotiationNeeded| event to perform a new track
/// negotiation, which requires signaling to be working. Therefore it is
/// recommended when this is known in advance to add tracks before starting to
/// establish a connection, in order to perform the first handshake with the
/// correct tracks offer/answer right away.
class PeerConnection : public TrackedObject,
                       public webrtc::PeerConnectionObserver {
 public:
  /// Create a new PeerConnection based on the given |config|.
  /// This serves as the constructor for PeerConnection.
  static ErrorOr<RefPtr<PeerConnection>> create(
      const mrsPeerConnectionConfiguration& config);

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
  using LocalSdpReadytoSendCallback = Callback<mrsSdpMessageType, const char*>;

  /// Register a custom LocalSdpReadytoSendCallback invoked when a local SDP
  /// message is ready to be sent by the user to the remote peer. Users MUST
  /// register a callback and handle sending SDP messages to the remote peer,
  /// otherwise the connection cannot be established, even on local network.
  /// Only one callback can be registered at a time.
  void RegisterLocalSdpReadytoSendCallback(
      LocalSdpReadytoSendCallback&& callback) noexcept {
    std::lock_guard<std::mutex> lock(local_sdp_ready_to_send_callback_mutex_);
    local_sdp_ready_to_send_callback_ = std::move(callback);
  }

  /// Callback invoked when a local ICE candidate message is ready to be sent to
  /// the remote peer via the signalling solution.
  using IceCandidateReadytoSendCallback = Callback<const mrsIceCandidate*>;

  /// Register a custom |IceCandidateReadytoSendCallback| invoked when a local
  /// ICE candidate has been generated and is ready to be sent. Users MUST
  /// register a callback and handle sending ICE candidates, otherwise the
  /// connection cannot be established (except on local networks with direct IP
  /// access, where NAT traversal is not needed). Only one callback can be
  /// registered at a time.
  void RegisterIceCandidateReadytoSendCallback(
      IceCandidateReadytoSendCallback&& callback) noexcept {
    std::lock_guard<std::mutex> lock(
        ice_candidate_ready_to_send_callback_mutex_);
    ice_candidate_ready_to_send_callback_ = std::move(callback);
  }

  /// Callback invoked when the state of the ICE connection changed.
  /// Note that the current implementation (M71) mixes the state of ICE and
  /// DTLS, so this does not correspond exactly to the ICE connection state of
  /// the WebRTC 1.0 standard.
  using IceStateChangedCallback = Callback<mrsIceConnectionState>;

  /// Register a custom |IceStateChangedCallback| invoked when the state of the
  /// ICE connection changed. Only one callback can be registered at a time.
  void RegisterIceStateChangedCallback(
      IceStateChangedCallback&& callback) noexcept {
    std::lock_guard<std::mutex> lock(ice_state_changed_callback_mutex_);
    ice_state_changed_callback_ = std::move(callback);
  }

  /// Callback invoked when the state of the ICE gathering changed.
  using IceGatheringStateChangedCallback = Callback<mrsIceGatheringState>;

  /// Register a custom |IceStateChangedCallback| invoked when the state of the
  /// ICE gathering process changed. Only one callback can be registered at a
  /// time.
  void RegisterIceGatheringStateChangedCallback(
      IceGatheringStateChangedCallback&& callback) noexcept {
    std::lock_guard<std::mutex> lock(
        ice_gathering_state_changed_callback_mutex_);
    ice_gathering_state_changed_callback_ = std::move(callback);
  }

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
  void RegisterRenegotiationNeededCallback(
      RenegotiationNeededCallback&& callback) noexcept {
    std::lock_guard<std::mutex> lock(renegotiation_needed_callback_mutex_);
    renegotiation_needed_callback_ = std::move(callback);
  }

  /// Notify the WebRTC engine that an ICE candidate has been received from the
  /// remote peer. The parameters correspond to the SDP message data provided by
  /// the |IceCandidateReadytoSendCallback|, after being transmitted to the
  /// other peer.
  Error AddIceCandidate(const mrsIceCandidate& candidate) noexcept;

  /// Callback invoked when |SetRemoteDescriptionAsync()| finished applying a
  /// remote description, successfully or not. The first parameter is the result
  /// of the operation, and the second one contains the error message if the
  /// description was not successfully applied.
  using RemoteDescriptionAppliedCallback = Callback<mrsResult, const char*>;

  /// Notify the WebRTC engine that an SDP message has been received from the
  /// remote peer. The parameters correspond to the SDP message data provided by
  /// the |LocalSdpReadytoSendCallback|, after being transmitted to the
  /// other peer.
  Error SetRemoteDescriptionAsync(
      mrsSdpMessageType type,
      const char* sdp,
      RemoteDescriptionAppliedCallback callback) noexcept;

  //
  // Connection
  //

  /// Callback invoked when the peer connection is established.
  /// This guarantees that the handshake process has terminated successfully,
  /// but does not guarantee that ICE exchanges are done.
  using ConnectedCallback = Callback<>;

  /// Register a custom |ConnectedCallback| invoked when the connection is
  /// established. Only one callback can be registered at a time.
  void RegisterConnectedCallback(ConnectedCallback&& callback) noexcept {
    std::lock_guard<std::mutex> lock(connected_callback_mutex_);
    connected_callback_ = std::move(callback);
  }

  /// Set the connection bitrate limits. These settings limit the network
  /// bandwidth use of the peer connection.
  mrsResult SetBitrate(const BitrateSettings& settings) noexcept {
    webrtc::BitrateSettings bitrate;
    bitrate.start_bitrate_bps = settings.start_bitrate_bps;
    bitrate.min_bitrate_bps = settings.min_bitrate_bps;
    bitrate.max_bitrate_bps = settings.max_bitrate_bps;
    return ResultFromRTCErrorType(peer_->SetBitrate(bitrate).type());
  }

  /// Create an SDP offer to attempt to establish a connection with the remote
  /// peer. Once the offer message is ready, the |LocalSdpReadytoSendCallback|
  /// callback is invoked to deliver the message.
  bool CreateOffer() noexcept;

  /// Create an SDP answer to accept a previously-received offer to establish a
  /// connection wit the remote peer. Once the answer message is ready, the
  /// |LocalSdpReadytoSendCallback| callback is invoked to deliver the message.
  bool CreateAnswer() noexcept;

  /// Close the peer connection. After the connection is closed, it cannot be
  /// opened again with the same C++ object. Instantiate a new |PeerConnection|
  /// object instead to create a new connection. No-op if already closed.
  void Close() noexcept;

  /// Check if the connection is closed. This returns |true| once |Close()| has
  /// been called.
  bool IsClosed() const noexcept;

  //
  // Transceivers
  //

  /// Callback invoked when a transceiver is added to the peer connection.
  using TransceiverAddedCallback = Callback<const mrsTransceiverAddedInfo*>;

  /// Register a custom TransceiverAddedCallback invoked when a transceiver is
  /// is added to the peer connection. Only one callback can be registered at a
  /// time.
  void RegisterTransceiverAddedCallback(
      TransceiverAddedCallback&& callback) noexcept {
    std::lock_guard<std::mutex> lock(callbacks_mutex_);
    transceiver_added_callback_ = std::move(callback);
  }

  /// Add a new audio or video transceiver to the peer connection.
  /// The transceiver is owned by the peer connection until it is closed with
  /// |Close()|, and the pointer is valid until that time.
  ErrorOr<Transceiver*> AddTransceiver(
      const mrsTransceiverInitConfig& config) noexcept;

  //
  // Video
  //

  /// Callback invoked when a remote video track is added to the peer
  /// connection.
  using VideoTrackAddedCallback = Callback<const mrsRemoteVideoTrackAddedInfo*>;

  /// Register a custom |VideoTrackAddedCallback| invoked when a remote video
  /// track is added to the peer connection. Only one callback can be registered
  /// at a time.
  void RegisterVideoTrackAddedCallback(
      VideoTrackAddedCallback&& callback) noexcept {
    std::lock_guard<std::mutex> lock(media_track_callback_mutex_);
    video_track_added_callback_ = std::move(callback);
  }

  /// Callback invoked when a remote video track is removed from the peer
  /// connection.
  using VideoTrackRemovedCallback =
      Callback<mrsRemoteVideoTrackHandle, mrsTransceiverHandle>;

  /// Register a custom |VideoTrackRemovedCallback| invoked when a remote video
  /// track is removed from the peer connection. Only one callback can be
  /// registered at a time.
  void RegisterVideoTrackRemovedCallback(
      VideoTrackRemovedCallback&& callback) noexcept {
    std::lock_guard<std::mutex> lock(media_track_callback_mutex_);
    video_track_removed_callback_ = std::move(callback);
  }

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

  /// Callback invoked when a remote audio track is added to the peer
  /// connection.
  using AudioTrackAddedCallback = Callback<const mrsRemoteAudioTrackAddedInfo*>;

  /// Register a custom AudioTrackAddedCallback invoked when a remote audio
  /// track is is added to the peer connection. Only one callback can be
  /// registered at a time.
  void RegisterAudioTrackAddedCallback(
      AudioTrackAddedCallback&& callback) noexcept {
    std::lock_guard<std::mutex> lock(media_track_callback_mutex_);
    audio_track_added_callback_ = std::move(callback);
  }

  /// Callback invoked when a remote audio track is removed from the peer
  /// connection.
  using AudioTrackRemovedCallback =
      Callback<mrsRemoteAudioTrackHandle, mrsTransceiverHandle>;

  /// Register a custom AudioTrackRemovedCallback invoked when a remote audio
  /// track is removed from the peer connection. Only one callback can be
  /// registered at a time.
  void RegisterAudioTrackRemovedCallback(
      AudioTrackRemovedCallback&& callback) noexcept {
    std::lock_guard<std::mutex> lock(media_track_callback_mutex_);
    audio_track_removed_callback_ = std::move(callback);
  }

  //
  // Data channel
  //

  /// Callback invoked when a new data channel is received from the remote peer
  /// and added locally.
  using DataChannelAddedCallback = Callback<const mrsDataChannelAddedInfo*>;

  /// Callback invoked when a data channel is removed from the remote peer and
  /// removed locally.
  using DataChannelRemovedCallback = Callback<mrsDataChannelHandle>;

  /// Register a custom callback invoked when a new data channel is received
  /// from the remote peer and added locally. Only one callback can be
  /// registered at a time.
  void RegisterDataChannelAddedCallback(
      DataChannelAddedCallback callback) noexcept {
    std::lock_guard<std::mutex> lock(data_channel_added_callback_mutex_);
    data_channel_added_callback_ = std::move(callback);
  }

  /// Register a custom callback invoked when a data channel is removed by the
  /// remote peer and removed locally. Only one callback can be registered at a
  /// time.
  void RegisterDataChannelRemovedCallback(
      DataChannelRemovedCallback callback) noexcept {
    std::lock_guard<std::mutex> lock(data_channel_removed_callback_mutex_);
    data_channel_removed_callback_ = std::move(callback);
  }

  /// Create a new data channel and add it to the peer connection.
  /// This invokes the DataChannelAdded callback.
  ErrorOr<std::shared_ptr<DataChannel>> AddDataChannel(int id,
                                                       absl::string_view label,
                                                       bool ordered,
                                                       bool reliable) noexcept;

  /// Close a given data channel and remove it from the peer connection.
  /// This invokes the DataChannelRemoved callback.
  void RemoveDataChannel(const DataChannel& data_channel) noexcept;

  /// Close and remove from the peer connection all data channels at once.
  /// This invokes the DataChannelRemoved callback for each data channel.
  void RemoveAllDataChannels() noexcept;

  /// Notification from a non-negotiated DataChannel that it is open, so that
  /// the PeerConnection can fire a DataChannelAdded event. This is called
  /// automatically by non-negotiated data channels; do not call manually.
  void OnDataChannelAdded(const DataChannel& data_channel) noexcept;

  void OnStreamChanged(
      rtc::scoped_refptr<webrtc::MediaStreamInterface> stream) noexcept;

  // Internal use.
  void GetStats(webrtc::RTCStatsCollectorCallback* callback);
  void InvokeRenegotiationNeeded();

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
  void OnDataChannel(rtc::scoped_refptr<webrtc::DataChannelInterface>
                         data_channel) noexcept override;

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
                                new_state) noexcept override;

  /// A new ICE candidate has been gathered.
  void OnIceCandidate(
      const webrtc::IceCandidateInterface* candidate) noexcept override;

  /// Callback on remote track added.
  void OnAddTrack(
      rtc::scoped_refptr<webrtc::RtpReceiverInterface> receiver,
      const std::vector<rtc::scoped_refptr<webrtc::MediaStreamInterface>>&
          streams) noexcept override;

  void OnTrack(rtc::scoped_refptr<webrtc::RtpTransceiverInterface>
                   transceiver) noexcept override;

  /// Callback on remote track removed.
  void OnRemoveTrack(rtc::scoped_refptr<webrtc::RtpReceiverInterface>
                         receiver) noexcept override;

  void OnLocalDescCreated(webrtc::SessionDescriptionInterface* desc) noexcept;

  //
  // Internal
  //

  /// The underlying PC object from the core implementation. This is NULL
  /// after |Close()| is called.
  rtc::scoped_refptr<webrtc::PeerConnectionInterface> peer_;

 protected:
  /// User callback invoked when the peer connection received a new data channel
  /// from the remote peer and added it locally.
  DataChannelAddedCallback data_channel_added_callback_
      RTC_GUARDED_BY(data_channel_added_callback_mutex_);

  /// User callback invoked when the peer connection received a data channel
  /// remove message from the remote peer and removed it locally.
  DataChannelRemovedCallback data_channel_removed_callback_
      RTC_GUARDED_BY(data_channel_removed_callback_mutex_);

  /// User callback invoked when a transceiver is added to the peer connection,
  /// whether manually with |AddTransceiver()| or automatically during
  /// |SetRemoteDescription()|.
  TransceiverAddedCallback transceiver_added_callback_
      RTC_GUARDED_BY(callbacks_mutex_);

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

  /// User callback invoked when the ICE gathering state changed.
  IceGatheringStateChangedCallback ice_gathering_state_changed_callback_
      RTC_GUARDED_BY(ice_gathering_state_changed_callback_mutex_);

  /// User callback invoked when SDP renegotiation is needed.
  RenegotiationNeededCallback renegotiation_needed_callback_
      RTC_GUARDED_BY(renegotiation_needed_callback_mutex_);

  /// User callback invoked when a remote audio track is added.
  AudioTrackAddedCallback audio_track_added_callback_
      RTC_GUARDED_BY(media_track_callback_mutex_);

  /// User callback invoked when a remote audio track is removed.
  AudioTrackRemovedCallback audio_track_removed_callback_
      RTC_GUARDED_BY(media_track_callback_mutex_);

  /// User callback invoked when a remote video track is added.
  VideoTrackAddedCallback video_track_added_callback_
      RTC_GUARDED_BY(media_track_callback_mutex_);

  /// User callback invoked when a remote video track is removed.
  VideoTrackRemovedCallback video_track_removed_callback_
      RTC_GUARDED_BY(media_track_callback_mutex_);

  std::mutex data_channel_added_callback_mutex_;
  std::mutex data_channel_removed_callback_mutex_;
  std::mutex callbacks_mutex_;
  std::mutex connected_callback_mutex_;
  std::mutex local_sdp_ready_to_send_callback_mutex_;
  std::mutex ice_candidate_ready_to_send_callback_mutex_;
  std::mutex ice_state_changed_callback_mutex_;
  std::mutex ice_gathering_state_changed_callback_mutex_;
  std::mutex renegotiation_needed_callback_mutex_;
  std::mutex media_track_callback_mutex_;

  class StreamObserver : public webrtc::ObserverInterface {
   public:
    StreamObserver(PeerConnection& owner,
                   rtc::scoped_refptr<webrtc::MediaStreamInterface> stream)
        : owner_(owner), stream_(std::move(stream)) {}

   protected:
    PeerConnection& owner_;
    rtc::scoped_refptr<webrtc::MediaStreamInterface> stream_;

    //
    // ObserverInterface
    //

    void OnChanged() override { owner_.OnStreamChanged(stream_); }
  };

  std::unordered_map<std::unique_ptr<StreamObserver>,
                     rtc::scoped_refptr<webrtc::MediaStreamInterface>>
      remote_streams_;

  /// Collection of all transceivers of this peer connection.
  std::vector<RefPtr<Transceiver>> transceivers_
      RTC_GUARDED_BY(transceivers_mutex_);

  /// Mutex for the collections of transceivers.
  rtc::CriticalSection transceivers_mutex_;

  /// Collection of all data channels associated with this peer connection.
  std::vector<std::shared_ptr<DataChannel>> data_channels_
      RTC_GUARDED_BY(data_channel_mutex_);

  /// Collection of data channels from their unique ID.
  /// This contains only data channels pre-negotiated or opened by the remote
  /// peer, as data channels opened locally won't have immediately a unique ID.
  std::unordered_map<int, std::shared_ptr<DataChannel>> data_channel_from_id_
      RTC_GUARDED_BY(data_channel_mutex_);

  /// Collection of data channels from their label.
  /// This contains only data channels with a non-empty label.
  std::unordered_multimap<std::string, std::shared_ptr<DataChannel>>
      data_channel_from_label_ RTC_GUARDED_BY(data_channel_mutex_);

  /// Mutex for data structures related to data channels.
  std::mutex data_channel_mutex_;

  /// Flag to indicate if SCTP was negotiated during the initial SDP handshake
  /// (m=application), which allows subsequently to use data channels. If this
  /// is false then data channels will never connnect. This is set to true if a
  /// data channel is created before the connection is established, which will
  /// force the connection to negotiate the necessary SCTP information. See
  /// https://stackoverflow.com/questions/43788872/how-are-data-channels-negotiated-between-two-peers-with-webrtc
  ///
  /// FIXME - See Note on
  /// https://w3c.github.io/webrtc-pc/#dictionary-rtcdatachannelinit-members, it
  /// looks like this is only a problem for negotiated (out-of-band) channels.
  bool sctp_negotiated_ = true;

  rtc::scoped_refptr<ToggleAudioMixer> audio_mixer_;

 private:
  PeerConnection(RefPtr<GlobalFactory> global_factory);
  PeerConnection(const PeerConnection&) = delete;
  ~PeerConnection() noexcept { Close(); }
  PeerConnection& operator=(const PeerConnection&) = delete;

  bool IsPlanB() const {
    return (peer_->GetConfiguration().sdp_semantics ==
            webrtc::SdpSemantics::kPlanB);
  }
  bool IsUnifiedPlan() const {
    return (peer_->GetConfiguration().sdp_semantics ==
            webrtc::SdpSemantics::kUnifiedPlan);
  }

  /// Find the |Transceiver| wrapper of an RTP transceiver, or |nullptr| if the
  /// RTP transceiver doesn't have a wrapper yet.
  RefPtr<Transceiver> FindWrapperFromRtpTransceiver(
      webrtc::RtpTransceiverInterface* tr) const;

  /// Extract the media line index from an RTP transceiver, or -1 if not
  /// associated.
  static int ExtractMlineIndexFromRtpTransceiver(
      webrtc::RtpTransceiverInterface* tr);

  /// Get an existing or create a new |Transceiver| wrapper for a given RTP
  /// receiver of a newly added remote track. The receiver should have an RTP
  /// transceiver already, and this only takes care of finding/creating a
  /// wrapper for it, so should never fail as long as the receiver is indeed
  /// associated with this peer connection. This is only called for new remote
  /// tracks, so will only create new transceiver wrappers for some of the new
  /// RTP transceivers. The callback on remote description applied will use
  /// |GetOrCreateTransceiverWrapper()| to create the other ones.
  ErrorOr<Transceiver*> GetOrCreateTransceiverForNewRemoteTrack(
      mrsMediaKind media_kind,
      webrtc::RtpReceiverInterface* receiver);

  /// Ensure each RTP transceiver has a corresponding |Transceiver| instance
  /// associated with it. This is called each time a local or remote description
  /// was just applied on the local peer.
  void SynchronizeTransceiversUnifiedPlan(bool remote);

  /// Create a new |Transceiver| instance for an exist RTP transceiver not
  /// associated with any. This automatically inserts the transceiver into the
  /// peer connection, and return a raw pointer to it valid until the peer
  /// connection is closed.
  ErrorOr<Transceiver*> CreateTransceiverUnifiedPlan(
      mrsMediaKind media_kind,
      int mline_index,
      std::string name,
      const std::vector<std::string>& string_ids,
      rtc::scoped_refptr<webrtc::RtpTransceiverInterface> rtp_transceiver);

  static std::string ExtractTransceiverNameFromSender(
      webrtc::RtpSenderInterface* sender);

  static std::vector<std::string> ExtractTransceiverStreamIDsFromReceiver(
      webrtc::RtpReceiverInterface* receive);

  static bool ExtractTransceiverInfoFromReceiverPlanB(
      webrtc::RtpReceiverInterface* receiver,
      int& mline_index,
      std::string& name,
      std::vector<std::string>& stream_ids);

  /// Media trait to specialize some implementations for audio or video while
  /// retaining a single copy of the code.
  template <mrsMediaKind MEDIA_KIND>
  struct MediaTrait;

  template <>
  struct MediaTrait<mrsMediaKind::kAudio> {
    constexpr static const mrsMediaKind kMediaKind = mrsMediaKind::kAudio;
    using RtcMediaTrackInterfaceT = webrtc::AudioTrackInterface;
    using RemoteMediaTrackT = RemoteAudioTrack;
    using RemoteMediaTrackHandleT = mrsRemoteAudioTrackHandle;
    using RemoteMediaTrackConfigT = mrsRemoteAudioTrackConfig;
    using MediaTrackAddedCallbackT =
        Callback<const mrsRemoteAudioTrackAddedInfo*>;
    using MediaTrackRemovedCallbackT =
        Callback<mrsRemoteAudioTrackHandle, mrsTransceiverHandle>;
    static void ExecTrackAdded(
        mrsRemoteAudioTrackHandle track_handle,
        mrsTransceiverHandle transceiver_handle,
        const char* track_name,
        const MediaTrackAddedCallbackT& callback) noexcept {
      mrsRemoteAudioTrackAddedInfo info{};
      info.track_handle = track_handle;
      info.audio_transceiver_handle = transceiver_handle;
      info.track_name = track_name;
      callback(&info);
    }
  };

  template <>
  struct MediaTrait<mrsMediaKind::kVideo> {
    constexpr static const mrsMediaKind kMediaKind = mrsMediaKind::kVideo;
    using RtcMediaTrackInterfaceT = webrtc::VideoTrackInterface;
    using RemoteMediaTrackT = RemoteVideoTrack;
    using RemoteMediaTrackHandleT = mrsRemoteVideoTrackHandle;
    using RemoteMediaTrackConfigT = mrsRemoteVideoTrackConfig;
    using MediaTrackAddedCallbackT =
        Callback<const mrsRemoteVideoTrackAddedInfo*>;
    using MediaTrackRemovedCallbackT =
        Callback<mrsRemoteVideoTrackHandle, mrsTransceiverHandle>;
    static void ExecTrackAdded(
        mrsRemoteVideoTrackHandle track_handle,
        mrsTransceiverHandle transceiver_handle,
        const char* track_name,
        const MediaTrackAddedCallbackT& callback) noexcept {
      mrsRemoteVideoTrackAddedInfo info{};
      info.track_handle = track_handle;
      info.audio_transceiver_handle = transceiver_handle;
      info.track_name = track_name;
      callback(&info);
    }
  };

  /// Create a new remote media (audio or video) track wrapper for an existing
  /// RTP media receiver which was just created or started receiving (Unified
  /// Plan) or was created for a newly receiving track (Plan B).
  template <mrsMediaKind MEDIA_KIND>
  RefPtr<typename MediaTrait<MEDIA_KIND>::RemoteMediaTrackT>
  AddRemoteMediaTrack(
      rtc::scoped_refptr<webrtc::MediaStreamTrackInterface> track,
      webrtc::RtpReceiverInterface* receiver,
      typename MediaTrait<MEDIA_KIND>::MediaTrackAddedCallbackT*
          track_added_cb) {
    using Media = MediaTrait<MEDIA_KIND>;

    rtc::scoped_refptr<typename Media::RtcMediaTrackInterfaceT> media_track(
        static_cast<typename Media::RtcMediaTrackInterfaceT*>(track.release()));

    // Get or create the transceiver wrapper based on the RTP receiver. Because
    // this callback is fired before the one at the end of the remote
    // description being applied, the transceiver wrappers for the newly added
    // RTP transceivers have not been created yet, so create them here.
    // Note that the returned transceiver is always added to |transceivers_| and
    // therefore kept alive by the peer connection.
    auto ret =
        GetOrCreateTransceiverForNewRemoteTrack(Media::kMediaKind, receiver);
    if (!ret.ok()) {
      return {};
    }
    Transceiver* const transceiver = ret.value();

    // Create the native object. Note that the transceiver passed as argument
    // will acquire a reference and keep it alive.
    RefPtr<typename Media::RemoteMediaTrackT> remote_media_track = new
        typename Media::RemoteMediaTrackT(global_factory_, *this, transceiver,
                                          std::move(media_track),
                                          std::move(receiver));

    // Invoke the TrackAdded callback, which will set the native handle on the
    // interop wrapper (if created above)
    {
      std::lock_guard<std::mutex> lock(media_track_callback_mutex_);
      // Read the function pointer inside the lock to avoid race condition
      auto cb = *track_added_cb;
      if (cb) {
        Media::ExecTrackAdded(remote_media_track.get(), transceiver,
                              remote_media_track->GetName().c_str(), cb);
      }
    }
    return remote_media_track;
  }

  /// Destroy an existing remote media (audio or video) track wrapper for an
  /// existing RTP media receiver which stopped receiving (Unified Plan) or is
  /// about to be destroyed itself (Plan B).
  template <mrsMediaKind MEDIA_KIND>
  void RemoveRemoteMediaTrack(
      webrtc::RtpReceiverInterface* receiver,
      typename MediaTrait<MEDIA_KIND>::MediaTrackRemovedCallbackT*
          track_removed_cb) {
    using Media = MediaTrait<MEDIA_KIND>;

    rtc::CritScope tracks_lock(&transceivers_mutex_);
    auto it = std::find_if(transceivers_.begin(), transceivers_.end(),
                           [receiver](const RefPtr<Transceiver>& tr) {
                             return tr->HasReceiver(receiver);
                           });
    if (it == transceivers_.end()) {
      RTC_LOG(LS_ERROR)
          << "Trying to remove receiver " << receiver->id().c_str()
          << " from peer connection " << GetName()
          << " but no transceiver was found which owns such receiver.";
      return;
    }
    RefPtr<Transceiver> transceiver = *it;
    RTC_DCHECK(transceiver->GetMediaKind() == MEDIA_KIND);
    RefPtr<typename Media::RemoteMediaTrackT> media_track(
        static_cast<typename Media::RemoteMediaTrackT*>(
            transceiver->GetRemoteTrack()));
    media_track->OnTrackRemoved(*this);

    // Invoke the TrackRemoved callback
    {
      std::lock_guard<std::mutex> lock(media_track_callback_mutex_);
      // Read the function pointer inside the lock to avoid race condition
      auto cb = *track_removed_cb;
      if (cb) {
        cb(media_track.get(), transceiver.get());
      }
    }
    // |media_track| goes out of scope and destroys the C++ instance
  }
};

}  // namespace WebRTC
}  // namespace MixedReality
}  // namespace Microsoft
