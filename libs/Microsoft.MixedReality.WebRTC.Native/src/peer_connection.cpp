// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// This is a precompiled header, it must be on its own, followed by a blank
// line, to prevent clang-format from reordering it with other headers.
#include "pch.h"

#include "audio_frame_observer.h"
#include "data_channel.h"
#include "interop/global_factory.h"
#include "interop_api.h"
#include "media/local_audio_track.h"
#include "media/local_video_track.h"
#include "media/remote_audio_track.h"
#include "media/remote_video_track.h"
#include "peer_connection.h"
#include "sdp_utils.h"
#include "utils.h"
#include "video_frame_observer.h"
#include "sdp_utils.h"

#include <functional>

#if defined(_M_IX86) /* x86 */ && defined(WINAPI_FAMILY) && \
    (WINAPI_FAMILY == WINAPI_FAMILY_APP) /* UWP app */ &&   \
    defined(_WIN32_WINNT_WIN10) &&                          \
    _WIN32_WINNT >= _WIN32_WINNT_WIN10 /* Win10 */

// Defined in
// external/webrtc-uwp-sdk/webrtc/xplatform/webrtc/third_party/winuwp_h264/H264Encoder/H264Encoder.cc
static constexpr int kFrameHeightCrop = 1;
extern int webrtc__WinUWPH264EncoderImpl__frame_height_round_mode;

// Stop WinRT from polluting the global namespace
// https://developercommunity.visualstudio.com/content/problem/859178/asyncinfoh-defines-the-error-symbol-at-global-name.html
#define _HIDE_GLOBAL_ASYNC_STATUS 1

#include <Windows.Foundation.h>
#include <windows.graphics.holographic.h>
#include <wrl\client.h>
#include <wrl\wrappers\corewrappers.h>

namespace {

bool CheckIfHololens() {
  // The best way to check if we are running on Hololens is checking if this is
  // a x86 Windows device with a transparent holographic display (AR).

  using namespace Microsoft::WRL;
  using namespace Microsoft::WRL::Wrappers;
  using namespace ABI::Windows::Foundation;
  using namespace ABI::Windows::Graphics::Holographic;

#define RETURN_IF_ERROR(...) \
  if (FAILED(__VA_ARGS__)) { \
    return false;            \
  }

  RoInitializeWrapper initialize(RO_INIT_MULTITHREADED);

  // HolographicSpace.IsAvailable
  ComPtr<IHolographicSpaceStatics2> holo_space_statics;
  RETURN_IF_ERROR(GetActivationFactory(
      HStringReference(
          RuntimeClass_Windows_Graphics_Holographic_HolographicSpace)
          .Get(),
      &holo_space_statics));
  boolean is_holo_space_available;
  RETURN_IF_ERROR(
      holo_space_statics->get_IsAvailable(&is_holo_space_available));
  if (!is_holo_space_available) {
    // Not a holographic device.
    return false;
  }

  // HolographicDisplay.GetDefault().IsOpaque
  ComPtr<IHolographicDisplayStatics> holo_display_statics;
  RETURN_IF_ERROR(GetActivationFactory(
      HStringReference(
          RuntimeClass_Windows_Graphics_Holographic_HolographicDisplay)
          .Get(),
      &holo_display_statics));
  ComPtr<IHolographicDisplay> holo_display;
  RETURN_IF_ERROR(holo_display_statics->GetDefault(&holo_display));
  boolean is_opaque;
  RETURN_IF_ERROR(holo_display->get_IsOpaque(&is_opaque));
  // Hololens if not opaque (otherwise VR).
  return !is_opaque;
#undef RETURN_IF_ERROR
}

bool IsHololens() {
  static bool is_hololens = CheckIfHololens();
  return is_hololens;
}

}  // namespace

namespace Microsoft::MixedReality::WebRTC {
void PeerConnection::SetFrameHeightRoundMode(FrameHeightRoundMode value) {
  if (IsHololens()) {
    webrtc__WinUWPH264EncoderImpl__frame_height_round_mode = (int)value;
  }
}
}  // namespace Microsoft::MixedReality::WebRTC

#else

namespace Microsoft::MixedReality::WebRTC {
void PeerConnection::SetFrameHeightRoundMode(FrameHeightRoundMode /*value*/) {}

}  // namespace Microsoft::MixedReality::WebRTC

#endif

namespace {

using namespace Microsoft::MixedReality::WebRTC;

Result ResultFromRTCErrorType(webrtc::RTCErrorType type) {
  using namespace webrtc;
  switch (type) {
    case RTCErrorType::NONE:
      return Result::kSuccess;
    case RTCErrorType::UNSUPPORTED_OPERATION:
    case RTCErrorType::UNSUPPORTED_PARAMETER:
      return Result::kUnsupported;
    case RTCErrorType::INVALID_PARAMETER:
    case RTCErrorType::INVALID_RANGE:
      return Result::kInvalidParameter;
    case RTCErrorType::INVALID_STATE:
      return Result::kNotInitialized;
    default:
      return Result::kUnknownError;
  }
}

Microsoft::MixedReality::WebRTC::Error ErrorFromRTCError(
    const webrtc::RTCError& error) {
  return Microsoft::MixedReality::WebRTC::Error(
      ResultFromRTCErrorType(error.type()), error.message());
}

Microsoft::MixedReality::WebRTC::Error ErrorFromRTCError(
    webrtc::RTCError&& error) {
  // Ideally would move the std::string out of |error|, but doesn't look
  // possible at the moment.
  return Microsoft::MixedReality::WebRTC::Error(
      ResultFromRTCErrorType(error.type()), error.message());
}

mrsMediaKind MediaKindFromRtc(cricket::MediaType media_type) {
  switch (media_type) {
    case cricket::MediaType::MEDIA_TYPE_AUDIO:
      return mrsMediaKind::kAudio;
    case cricket::MediaType::MEDIA_TYPE_VIDEO:
      return mrsMediaKind::kVideo;
    default:
      RTC_LOG(LS_ERROR) << "Invalid media type, expected audio or video.";
      RTC_NOTREACHED();
      // Silence error about uninitialized variable when assigning the result of
      // this function, and return some visibly invalid value.
      return (mrsMediaKind)-1;
  }
}

cricket::MediaType MediaKindToRtc(mrsMediaKind media_kind) {
  switch (media_kind) {
    case mrsMediaKind::kAudio:
      return cricket::MediaType::MEDIA_TYPE_AUDIO;
    case mrsMediaKind::kVideo:
      return cricket::MediaType::MEDIA_TYPE_VIDEO;
    default:
      RTC_LOG(LS_ERROR) << "Unknown media kind, expected audio or video.";
      RTC_NOTREACHED();
      // Silence error about uninitialized variable when assigning the result of
      // this function, and return some visibly invalid value (mrsMediaKind is
      // audio or video only).
      return cricket::MediaType::MEDIA_TYPE_DATA;
  }
}

class PeerConnectionImpl;

class StreamObserver : public webrtc::ObserverInterface {
 public:
  StreamObserver(PeerConnectionImpl& owner,
                 rtc::scoped_refptr<webrtc::MediaStreamInterface> stream)
      : owner_(owner), stream_(std::move(stream)) {}

 protected:
  PeerConnectionImpl& owner_;
  rtc::scoped_refptr<webrtc::MediaStreamInterface> stream_;

  //
  // ObserverInterface
  //

  void OnChanged() override;
};

/// Implementation of PeerConnection, which also implements
/// PeerConnectionObserver at the same time to simplify interaction with
/// the underlying implementation object.
class PeerConnectionImpl : public PeerConnection,
                           public webrtc::PeerConnectionObserver {
 public:
  PeerConnectionImpl(RefPtr<GlobalFactory> global_factory)
      : PeerConnection(std::move(global_factory)) {}

  ~PeerConnectionImpl() noexcept { Close(); }

  void SetPeerImpl(rtc::scoped_refptr<webrtc::PeerConnectionInterface> impl) {
    peer_ = std::move(impl);
  }

  void SetName(std::string_view name) override { name_ = name; }

  std::string GetName() const override { return name_; }

  void RegisterLocalSdpReadytoSendCallback(
      LocalSdpReadytoSendCallback&& callback) noexcept override {
    auto lock = std::scoped_lock{local_sdp_ready_to_send_callback_mutex_};
    local_sdp_ready_to_send_callback_ = std::move(callback);
  }

  void RegisterIceCandidateReadytoSendCallback(
      IceCandidateReadytoSendCallback&& callback) noexcept override {
    auto lock = std::scoped_lock{ice_candidate_ready_to_send_callback_mutex_};
    ice_candidate_ready_to_send_callback_ = std::move(callback);
  }

  void RegisterIceStateChangedCallback(
      IceStateChangedCallback&& callback) noexcept override {
    auto lock = std::scoped_lock{ice_state_changed_callback_mutex_};
    ice_state_changed_callback_ = std::move(callback);
  }

  void RegisterIceGatheringStateChangedCallback(
      IceGatheringStateChangedCallback&& callback) noexcept override {
    auto lock = std::scoped_lock{ice_gathering_state_changed_callback_mutex_};
    ice_gathering_state_changed_callback_ = std::move(callback);
  }

  void RegisterRenegotiationNeededCallback(
      RenegotiationNeededCallback&& callback) noexcept override {
    auto lock = std::scoped_lock{renegotiation_needed_callback_mutex_};
    renegotiation_needed_callback_ = std::move(callback);
  }

  bool AddIceCandidate(const char* sdp_mid,
                       const int sdp_mline_index,
                       const char* candidate) noexcept override;
  bool SetRemoteDescriptionAsync(const char* type,
                                 const char* sdp,
                                 Callback<> callback) noexcept override;

  void RegisterConnectedCallback(
      ConnectedCallback&& callback) noexcept override {
    auto lock = std::scoped_lock{connected_callback_mutex_};
    connected_callback_ = std::move(callback);
  }

  mrsResult SetBitrate(const BitrateSettings& settings) noexcept override {
    webrtc::BitrateSettings bitrate;
    bitrate.start_bitrate_bps = settings.start_bitrate_bps;
    bitrate.min_bitrate_bps = settings.min_bitrate_bps;
    bitrate.max_bitrate_bps = settings.max_bitrate_bps;
    return ResultFromRTCErrorType(peer_->SetBitrate(bitrate).type());
  }

  bool CreateOffer() noexcept override;
  bool CreateAnswer() noexcept override;
  void Close() noexcept override;
  bool IsClosed() const noexcept override;

  void RegisterTransceiverAddedCallback(
      TransceiverAddedCallback&& callback) noexcept override {
    auto lock = std::scoped_lock{callbacks_mutex_};
    transceiver_added_callback_ = std::move(callback);
  }

  ErrorOr<Transceiver*> AddTransceiver(
      const mrsTransceiverInitConfig& config) noexcept override;

  void RegisterVideoTrackAddedCallback(
      VideoTrackAddedCallback&& callback) noexcept override {
    auto lock = std::scoped_lock{media_track_callback_mutex_};
    video_track_added_callback_ = std::move(callback);
  }

  void RegisterVideoTrackRemovedCallback(
      VideoTrackRemovedCallback&& callback) noexcept override {
    auto lock = std::scoped_lock{media_track_callback_mutex_};
    video_track_removed_callback_ = std::move(callback);
  }

  void RegisterAudioTrackAddedCallback(
      AudioTrackAddedCallback&& callback) noexcept override {
    auto lock = std::scoped_lock{media_track_callback_mutex_};
    audio_track_added_callback_ = std::move(callback);
  }

  void RegisterAudioTrackRemovedCallback(
      AudioTrackRemovedCallback&& callback) noexcept override {
    auto lock = std::scoped_lock{media_track_callback_mutex_};
    audio_track_removed_callback_ = std::move(callback);
  }

  void RegisterDataChannelAddedCallback(
      DataChannelAddedCallback callback) noexcept override {
    auto lock = std::scoped_lock{data_channel_added_callback_mutex_};
    data_channel_added_callback_ = std::move(callback);
  }

  void RegisterDataChannelRemovedCallback(
      DataChannelRemovedCallback callback) noexcept override {
    auto lock = std::scoped_lock{data_channel_removed_callback_mutex_};
    data_channel_removed_callback_ = std::move(callback);
  }

  ErrorOr<std::shared_ptr<DataChannel>> AddDataChannel(
      int id,
      std::string_view label,
      bool ordered,
      bool reliable) noexcept override;
  void RemoveDataChannel(const DataChannel& data_channel) noexcept override;
  void RemoveAllDataChannels() noexcept override;
  void OnDataChannelAdded(const DataChannel& data_channel) noexcept override;

  void OnStreamChanged(
      rtc::scoped_refptr<webrtc::MediaStreamInterface> stream) noexcept;

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
  /// Peer connection name assigned by the user. This has no meaning for the
  /// implementation.
  std::string name_;

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
  std::unordered_multimap<str, std::shared_ptr<DataChannel>>
      data_channel_from_label_ RTC_GUARDED_BY(data_channel_mutex_);

  /// Mutex for data structures related to data channels.
  std::mutex data_channel_mutex_;

  /// Flag to indicate if SCTP was negotiated during the initial SDP handshake
  /// (m=application), which allows subsequently to use data channels. If this
  /// is false then data channels will never connnect. This is set to true if a
  /// data channel is created before the connection is established, which will
  /// force the connection to negotiate the necessary SCTP information. See
  /// https://stackoverflow.com/questions/43788872/how-are-data-channels-negotiated-between-two-peers-with-webrtc
  bool sctp_negotiated_ = true;

 private:
  PeerConnectionImpl(const PeerConnectionImpl&) = delete;
  PeerConnectionImpl& operator=(const PeerConnectionImpl&) = delete;

  bool IsPlanB() const {
    return (peer_->GetConfiguration().sdp_semantics ==
            webrtc::SdpSemantics::kPlanB);
  }
  bool IsUnifiedPlan() const {
    return (peer_->GetConfiguration().sdp_semantics ==
            webrtc::SdpSemantics::kUnifiedPlan);
  }

  /// Insert a new transceiver wrapper at the given media line index into
  /// |transceivers_|. This will keep the transceiver alive until it is
  /// destroyed by |Close()|.
  Error InsertTransceiverAtMlineIndex(int mline_index,
                                      RefPtr<Transceiver> transceiver);

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
  void AddRemoteMediaTrack(
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
      return;
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
      auto lock = std::scoped_lock{media_track_callback_mutex_};
      // Read the function pointer inside the lock to avoid race condition
      auto cb = *track_added_cb;
      if (cb) {
        Media::ExecTrackAdded(remote_media_track.get(), transceiver,
                              remote_media_track->GetName().c_str(), cb);
      }
    }
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
      auto lock = std::scoped_lock{media_track_callback_mutex_};
      // Read the function pointer inside the lock to avoid race condition
      auto cb = *track_removed_cb;
      if (cb) {
        cb(media_track.get(), transceiver.get());
      }
    }
    // |media_track| goes out of scope and destroys the C++ instance
  }
};

void StreamObserver::OnChanged() {
  owner_.OnStreamChanged(stream_);
}

class CreateSessionDescObserver
    : public webrtc::CreateSessionDescriptionObserver {
 public:
  CreateSessionDescObserver(RefPtr<PeerConnectionImpl> peer_connection)
      : peer_connection_(
            std::forward<RefPtr<PeerConnectionImpl>>(peer_connection)) {}

  //
  // CreateSessionDescriptionObserver interface
  //

  /// This callback transfers the ownership of the |desc|.
  /// TODO(deadbeef): Make this take an std::unique_ptr<> to avoid confusion
  /// around ownership.
  void OnSuccess(webrtc::SessionDescriptionInterface* desc) noexcept override {
    peer_connection_->OnLocalDescCreated(desc);
  }

  /// The OnFailure callback takes an RTCError, which consists of an
  /// error code and a string.
  /// RTCError is non-copyable, so it must be passed using std::move.
  /// Earlier versions of the API used a string argument. This version
  /// is deprecated; in order to let clients remove the old version, it has a
  /// default implementation. If both versions are unimplemented, the
  /// result will be a runtime error (stack overflow). This is intentional.
  void OnFailure(webrtc::RTCError error) noexcept override {}

 protected:
  RefPtr<PeerConnectionImpl> peer_connection_;
};

/// Simple observer utility delegating to a given callback on success.
class SessionDescObserver : public webrtc::SetSessionDescriptionObserver {
 public:
  SessionDescObserver() = default;
  template <typename Closure>
  SessionDescObserver(Closure&& callback)
      : callback_(std::forward<Closure>(callback)) {}
  void OnSuccess() override {
    if (callback_) {
      callback_();
    }
  }
  void OnFailure(webrtc::RTCError error) override {
    RTC_LOG(LS_ERROR) << "Error setting session description: "
                      << error.message();
  }
  void OnFailure(const std::string& error) override {
    RTC_LOG(LS_ERROR) << "Error setting session description: " << error;
  }

 protected:
  std::function<void()> callback_;
  ~SessionDescObserver() override = default;
};

struct SetRemoteSessionDescObserver
    : public webrtc::SetRemoteDescriptionObserverInterface {
 public:
  SetRemoteSessionDescObserver() = default;
  template <typename Closure>
  SetRemoteSessionDescObserver(Closure&& callback)
      : callback_(std::forward<Closure>(callback)) {}
  void OnSetRemoteDescriptionComplete(webrtc::RTCError error) override {
    RTC_LOG(LS_INFO) << "Remote description set. err=" << error.message();
    if (error.ok() && callback_) {
      callback_();
    }
  }

 protected:
  std::function<void()> callback_;
};

const std::string kAudioVideoStreamId("local_av_stream");

/// The API must ensure that all strings passed to the caller are
/// null-terminated. This is a helper to ensure that calling c_str()
/// on the given std::string will yield a null-terminated string.
void ensureNullTerminatedCString(std::string& str) {
  if (str.empty() || (str.back() != '\0')) {
    str.push_back('\0');
  }
}

/// Convert an implementation value to a native API value of the ICE connection
/// state. This ensures API stability if the implementation changes, although
/// currently API values are mapped 1:1 with the implementation.
mrsIceConnectionState IceStateFromImpl(
    webrtc::PeerConnectionInterface::IceConnectionState impl_state) {
  using Native = mrsIceConnectionState;
  using Impl = webrtc::PeerConnectionInterface::IceConnectionState;
  static_assert((int)Native::kNew == (int)Impl::kIceConnectionNew);
  static_assert((int)Native::kChecking == (int)Impl::kIceConnectionChecking);
  static_assert((int)Native::kConnected == (int)Impl::kIceConnectionConnected);
  static_assert((int)Native::kCompleted == (int)Impl::kIceConnectionCompleted);
  static_assert((int)Native::kFailed == (int)Impl::kIceConnectionFailed);
  static_assert((int)Native::kDisconnected ==
                (int)Impl::kIceConnectionDisconnected);
  static_assert((int)Native::kClosed == (int)Impl::kIceConnectionClosed);
  return (mrsIceConnectionState)impl_state;
}

/// Convert an implementation value to a native API value of the ICE gathering
/// state. This ensures API stability if the implementation changes, although
/// currently API values are mapped 1:1 with the implementation.
mrsIceGatheringState IceGatheringStateFromImpl(
    webrtc::PeerConnectionInterface::IceGatheringState impl_state) {
  using Native = mrsIceGatheringState;
  using Impl = webrtc::PeerConnectionInterface::IceGatheringState;
  static_assert((int)Native::kNew == (int)Impl::kIceGatheringNew);
  static_assert((int)Native::kGathering == (int)Impl::kIceGatheringGathering);
  static_assert((int)Native::kComplete == (int)Impl::kIceGatheringComplete);
  return (mrsIceGatheringState)impl_state;
}

ErrorOr<std::shared_ptr<DataChannel>> PeerConnectionImpl::AddDataChannel(
    int id,
    std::string_view label,
    bool ordered,
    bool reliable) noexcept {
  if (IsClosed()) {
    return Error(Result::kPeerConnectionClosed);
  }
  if (!sctp_negotiated_) {
    // Don't try to create a data channel without SCTP negotiation, it will get
    // stuck in the kConnecting state forever.
    return Error(Result::kSctpNotNegotiated);
  }
  webrtc::DataChannelInit config{};
  config.ordered = ordered;
  config.reliable = reliable;
  if (id < 0) {
    // In-band data channel with automatic ID assignment
    config.id = -1;
  } else if (id <= 0xFFFF) {
    // Out-of-band negotiated data channel with pre-established ID
    config.id = id;
  } else {
    // Valid IDs are 0-65535 (16 bits)
    return Error(Result::kOutOfRange);
  }
  std::string labelString{label};
  if (rtc::scoped_refptr<webrtc::DataChannelInterface> impl =
          peer_->CreateDataChannel(labelString, &config)) {
    // Create the native object
    auto data_channel = std::make_shared<DataChannel>(this, std::move(impl));
    {
      auto lock = std::scoped_lock{data_channel_mutex_};
      data_channels_.push_back(data_channel);
      if (!labelString.empty()) {
        data_channel_from_label_.emplace(std::move(labelString), data_channel);
      }
      if (config.id >= 0) {
        data_channel_from_id_.try_emplace(config.id, data_channel);
      }
    }

    // For in-band channels, the creating side (here) doesn't receive an
    // OnDataChannel() message, so invoke the DataChannelAdded event right now.
    if (!data_channel->impl()->negotiated()) {
      OnDataChannelAdded(*data_channel.get());
    }

    return data_channel;
  }
  return Error(Result::kUnknownError);
}

void PeerConnectionImpl::RemoveDataChannel(
    const DataChannel& data_channel) noexcept {
  // Cache variables which require a dispatch to the signaling thread
  // to minimize the risk of a deadlock with the data channel lock below.
  const int id = data_channel.id();
  const str label = data_channel.label();

  // Move the channel to destroy out of the internal data structures
  std::shared_ptr<DataChannel> data_channel_ptr;
  {
    auto lock = std::scoped_lock{data_channel_mutex_};

    // The channel must be owned by this PeerConnection, so must be known
    // already
    auto const it = std::find_if(
        std::begin(data_channels_), std::end(data_channels_),
        [&data_channel](const std::shared_ptr<DataChannel>& other) {
          return other.get() == &data_channel;
        });
    RTC_DCHECK(it != data_channels_.end());
    // Be sure a reference is kept. This should not be a problem in theory
    // because the caller should have a reference to it, but this is safer.
    data_channel_ptr = std::move(*it);
    data_channels_.erase(it);

    // Clean-up interop maps
    auto it_id = data_channel_from_id_.find(id);
    if (it_id != data_channel_from_id_.end()) {
      data_channel_from_id_.erase(it_id);
    }
    if (!label.empty()) {
      auto it_label = data_channel_from_label_.find(label);
      if (it_label != data_channel_from_label_.end()) {
        data_channel_from_label_.erase(it_label);
      }
    }
  }

  // Close the WebRTC data channel
  webrtc::DataChannelInterface* const impl = data_channel.impl();
  impl->UnregisterObserver();  // force here, as ~DataChannel() didn't run yet
  impl->Close();

  // Invoke the DataChannelRemoved callback
  {
    auto lock = std::scoped_lock{data_channel_removed_callback_mutex_};
    auto removed_cb = data_channel_removed_callback_;
    if (removed_cb) {
      mrsDataChannelHandle data_native_handle = (void*)&data_channel;
      removed_cb(data_native_handle);
    }
  }

  // Clear the back pointer to the peer connection, and let the shared pointer
  // go out of scope and destroy the object if that was the last reference.
  data_channel_ptr->OnRemovedFromPeerConnection();
}

void PeerConnectionImpl::RemoveAllDataChannels() noexcept {
  auto lock_cb = std::scoped_lock{data_channel_removed_callback_mutex_};
  auto removed_cb = data_channel_removed_callback_;
  auto lock = std::scoped_lock{data_channel_mutex_};
  for (auto&& data_channel : data_channels_) {
    // Close the WebRTC data channel
    webrtc::DataChannelInterface* const impl = data_channel->impl();
    impl->UnregisterObserver();  // force here, as ~DataChannel() didn't run yet
    impl->Close();

    // Invoke the DataChannelRemoved callback on the wrapper if any
    if (removed_cb) {
      DataChannel* const dc = data_channel.get();
      mrsDataChannelHandle data_native_handle = (void*)dc;
      removed_cb(data_native_handle);
    }

    // Clear the back pointer
    data_channel->OnRemovedFromPeerConnection();
  }
  data_channel_from_id_.clear();
  data_channel_from_label_.clear();
  data_channels_.clear();
}

void PeerConnectionImpl::OnDataChannelAdded(
    const DataChannel& data_channel) noexcept {
  // The channel must be owned by this PeerConnection, so must be known already.
  // It was added in AddDataChannel() when the DataChannel object was created.
#if RTC_DCHECK_IS_ON
  {
    auto lock = std::scoped_lock{data_channel_mutex_};
    RTC_DCHECK(std::find_if(
                   data_channels_.begin(), data_channels_.end(),
                   [&data_channel](const std::shared_ptr<DataChannel>& other) {
                     return other.get() == &data_channel;
                   }) != data_channels_.end());
  }
#endif  // RTC_DCHECK_IS_ON

  // Invoke the DataChannelAdded callback
  {
    auto lock = std::scoped_lock{data_channel_added_callback_mutex_};
    auto added_cb = data_channel_added_callback_;
    if (added_cb) {
      mrsDataChannelAddedInfo info{};
      info.handle = (void*)&data_channel;
      info.id = data_channel.id();
      info.flags = data_channel.flags();
      str label_str = data_channel.label();  // keep alive
      info.label = label_str.c_str();
      added_cb(&info);
    }
  }
}

void PeerConnectionImpl::OnStreamChanged(
    rtc::scoped_refptr<webrtc::MediaStreamInterface> stream) noexcept {
  webrtc::AudioTrackVector audio_tracks = stream->GetAudioTracks();
  webrtc::VideoTrackVector video_tracks = stream->GetVideoTracks();
  RTC_LOG(LS_INFO) << "Media stream #" << stream->id()
                   << " changed: " << audio_tracks.size()
                   << " audio tracks and " << video_tracks.size()
                   << " video tracks.";
  // TODO - Clarify if media streams are of any use and if/how they can be used.
  // The old API from ~2013 was based on them, but the WebRTC 1.0 standard is
  // not, and it seems the media streams are now in Unified Plan just some
  // metadata, though in Plan B they might still have a role for A/V tracks
  // symchronization.
}

bool PeerConnectionImpl::AddIceCandidate(const char* sdp_mid,
                                         const int sdp_mline_index,
                                         const char* candidate) noexcept {
  if (!peer_) {
    return false;
  }
  webrtc::SdpParseError error;
  std::unique_ptr<webrtc::IceCandidateInterface> ice_candidate(
      webrtc::CreateIceCandidate(sdp_mid, sdp_mline_index, candidate, &error));
  if (!ice_candidate) {
    return false;
  }
  if (!peer_->AddIceCandidate(ice_candidate.get())) {
    return false;
  }
  return true;
}

bool PeerConnectionImpl::CreateOffer() noexcept {
  if (!peer_) {
    return false;
  }
  {
    auto lock = std::scoped_lock{data_channel_mutex_};
    if (data_channels_.empty()) {
      sctp_negotiated_ = false;
    }
  }
  // For PlanB, add RTP senders and receivers to simulate transceivers,
  // otherwise the offer will not contain anything.
  // - Senders are added manually with |PeerConnectionInterface::CreateSender()|
  // and kept in the transceiver C++ object itself (see |PlanBEmulation|).
  // - Receivers are added using the legacy options |offer_to_receive_audio| and
  // |offer_to_receive_video|. Those are global per-SDP session options but in
  // Plan B there is a single media line per media kind, so it doesn't matter.
  webrtc::PeerConnectionInterface::RTCOfferAnswerOptions offer_options{};
  if (IsPlanB()) {
    offer_options.offer_to_receive_audio = false;
    offer_options.offer_to_receive_video = false;
    int mline_index = 0;
    for (auto&& tr : transceivers_) {
      std::string encoded_stream_id =
          tr->BuildEncodedStreamIDForPlanB(mline_index);
      const char* media_kind_str = nullptr;
      const Transceiver::Direction dir = tr->GetDesiredDirection();
      const bool need_sender = ((dir == Transceiver::Direction::kSendOnly) ||
                                (dir == Transceiver::Direction::kSendRecv));
      const bool need_receiver = ((dir == Transceiver::Direction::kRecvOnly) ||
                                  (dir == Transceiver::Direction::kSendRecv));
      if (tr->GetMediaKind() == mrsMediaKind::kAudio) {
        media_kind_str = "audio";
        if (need_receiver) {
          // Force an RTP receiver to be created.
          offer_options.offer_to_receive_audio = true;
        }
      } else {
        RTC_DCHECK(tr->GetMediaKind() == mrsMediaKind::kVideo);
        media_kind_str = "video";
        if (need_receiver) {
          // Force an RTP receiver to be created.
          offer_options.offer_to_receive_video = true;
        }
      }
      tr->SyncSenderPlanB(need_sender, peer_, media_kind_str,
                          encoded_stream_id.c_str());
      ++mline_index;
    }
  }
  auto observer =
      new rtc::RefCountedObject<CreateSessionDescObserver>(this);  // 0 ref
  peer_->CreateOffer(observer, offer_options);
  RTC_CHECK(observer->HasOneRef());  // should be == 1
  return true;
}

bool PeerConnectionImpl::CreateAnswer() noexcept {
  if (!peer_) {
    return false;
  }
  webrtc::PeerConnectionInterface::RTCOfferAnswerOptions options{};
  auto observer =
      new rtc::RefCountedObject<CreateSessionDescObserver>(this);  // 0 ref
  peer_->CreateAnswer(observer, options);
  RTC_CHECK(observer->HasOneRef());  // should be == 1
  return true;
}

void PeerConnectionImpl::Close() noexcept {
  if (!peer_) {
    return;
  }

  // Keep a reference to the implementation while shutting down, but clear the
  // visible value in |peer_| to ensure |IsClosed()| returns false. This
  // prevents other methods from being able to create new objects like
  // transceivers.
  rtc::scoped_refptr<webrtc::PeerConnectionInterface> pc(std::move(peer_));

  // Close the connection
  pc->Close();

  {
    rtc::CritScope lock(&transceivers_mutex_);

    // Force-remove remote tracks. It doesn't look like the TrackRemoved
    // callback is called when Close() is used, so force it here.
    auto cb_lock = std::scoped_lock{media_track_callback_mutex_};
    auto audio_cb = audio_track_removed_callback_;
    auto video_cb = video_track_removed_callback_;
    for (auto&& transceiver : transceivers_) {
      if (auto remote_track = transceiver->GetRemoteTrack()) {
        if (remote_track->GetKind() == mrsTrackKind::kAudioTrack) {
          RefPtr<RemoteAudioTrack> audio_track(
              static_cast<RemoteAudioTrack*>(remote_track));  // keep alive
          audio_track->OnTrackRemoved(*this);
          if (audio_cb) {
            audio_cb(audio_track.get(), transceiver.get());
          }
        } else if (remote_track->GetKind() == mrsTrackKind::kVideoTrack) {
          RefPtr<RemoteVideoTrack> video_track(
              static_cast<RemoteVideoTrack*>(remote_track));  // keep alive
          video_track->OnTrackRemoved(*this);
          if (video_cb) {
            video_cb(video_track.get(), transceiver.get());
          }
        }
      }
    }

    // Clear and destroy transceivers (unless some implementation somewhere has
    // a reference, which should not happen).
    transceivers_.clear();
  }

  remote_streams_.clear();

  RemoveAllDataChannels();

  // Release the internal webrtc::PeerConnection implementation. This call will
  // get proxied to the WebRTC signaling thread, so needs to occur before the
  // global factory shuts down and terminates the threads, which potentially
  // happens just after this call when called from the destructor if this is the
  // last object alive.
  pc = nullptr;
}

bool PeerConnectionImpl::IsClosed() const noexcept {
  return (peer_ == nullptr);
}

ErrorOr<Transceiver*> PeerConnectionImpl::AddTransceiver(
    const mrsTransceiverInitConfig& config) noexcept {
  if (IsClosed()) {
    return Error(Result::kInvalidOperation, "The peer connection is closed.");
  }

  std::string name;
  if (!IsStringNullOrEmpty(config.name)) {
    name = config.name;
  } else {
    name = rtc::CreateRandomUuid();
  }
  if (!SdpIsValidToken(name)) {
    rtc::StringBuilder str("Invalid transceiver name: ");
    str << name;
    return Error(Result::kInvalidParameter, str.Release());
  }
  std::vector<std::string> stream_ids =
      Transceiver::DecodeStreamIDs(config.stream_ids);

  RefPtr<Transceiver> transceiver;
  int mline_index = -1;
  switch (peer_->GetConfiguration().sdp_semantics) {
    case webrtc::SdpSemantics::kPlanB: {
      // Plan B doesn't have transceivers; just create a wrapper.
      mline_index = (int)transceivers_.size();  // append
      transceiver = Transceiver::CreateForPlanB(
          global_factory_, config.media_kind, *this, mline_index, name,
          std::move(stream_ids), config.desired_direction);
      // Manually invoke the renegotiation needed event for parity with Unified
      // Plan, like the internal implementation would do.
      OnRenegotiationNeeded();
    } break;
    case webrtc::SdpSemantics::kUnifiedPlan: {
      // Create the low-level implementation object
      webrtc::RtpTransceiverInit init{};
      init.direction = Transceiver::ToRtp(config.desired_direction);
      init.stream_ids = stream_ids;
      const cricket::MediaType rtc_media_type =
          MediaKindToRtc(config.media_kind);
      webrtc::RTCErrorOr<rtc::scoped_refptr<webrtc::RtpTransceiverInterface>>
          ret = peer_->AddTransceiver(rtc_media_type, init);
      if (!ret.ok()) {
        return ErrorFromRTCError(ret.MoveError());
      }
      rtc::scoped_refptr<webrtc::RtpTransceiverInterface> impl(ret.MoveValue());

      // Find the mline index from the position inside the transceiver list
      {
        auto transceivers = peer_->GetTransceivers();
        auto it_tr =
            std::find_if(transceivers.begin(), transceivers.end(),
                         [&impl](auto const& tr) { return (tr == impl); });
        RTC_DCHECK(it_tr != transceivers.end());
        mline_index = (int)std::distance(transceivers.begin(), it_tr);
      }

      // Create the transceiver wrapper
      transceiver = Transceiver::CreateForUnifiedPlan(
          global_factory_, config.media_kind, *this, mline_index, name,
          std::move(stream_ids), std::move(impl), config.desired_direction);
    } break;
    default:
      return Error(Result::kUnknownError, "Unknown SDP semantic.");
  }
  RTC_DCHECK(transceiver);
  Transceiver* tr_view = transceiver.get();
  InsertTransceiverAtMlineIndex(mline_index, transceiver);

  // Invoke the TransceiverAdded callback
  {
    auto lock = std::scoped_lock{callbacks_mutex_};
    if (auto cb = transceiver_added_callback_) {
      mrsTransceiverAddedInfo info{};
      info.transceiver_handle = transceiver.get();
      info.transceiver_name = name.c_str();
      info.media_kind = config.media_kind;
      info.mline_index = mline_index;
      info.encoded_stream_ids_ = config.stream_ids;
      info.desired_direction = config.desired_direction;
      cb(&info);
    }
  }

  return tr_view;
}

bool PeerConnectionImpl::SetRemoteDescriptionAsync(
    const char* type,
    const char* sdp,
    Callback<> callback) noexcept {
  if (!peer_) {
    return false;
  }
  {
    auto lock = std::scoped_lock{data_channel_mutex_};
    if (data_channels_.empty()) {
      sctp_negotiated_ = false;
    }
  }
  std::string sdp_type_str(type);
  auto sdp_type = SdpTypeFromString(sdp_type_str);
  if (!sdp_type.has_value())
    return false;
  std::string remote_desc(sdp);
  webrtc::SdpParseError error;
  std::unique_ptr<webrtc::SessionDescriptionInterface> session_description(
      webrtc::CreateSessionDescription(sdp_type.value(), remote_desc, &error));
  if (!session_description)
    return false;
  rtc::scoped_refptr<webrtc::SetRemoteDescriptionObserverInterface> observer =
      new rtc::RefCountedObject<SetRemoteSessionDescObserver>([this, callback] {
        if (IsUnifiedPlan()) {
          SynchronizeTransceiversUnifiedPlan(/*remote=*/true);
        } else {
          RTC_DCHECK(IsPlanB());
          // In Plan B there is no RTP transceiver, and all remote tracks which
          // start/stop receiving are triggering a TrackAdded or TrackRemoved
          // event. Therefore there is no extra work to do like there was in
          // Unified Plan above.

          // TODO - Clarify if this is really needed (and if we really need to
          // force the update) for parity with Unified Plan and to get the
          // transceiver update event fired once and once only.
          for (auto&& tr : transceivers_) {
            tr->OnSessionDescUpdated(/*remote=*/true, /*forced=*/true);
          }
        }
        // Fire completed callback to signal remote description was applied.
        callback();
      });
  peer_->SetRemoteDescription(std::move(session_description),
                              std::move(observer));
  return true;
}

void PeerConnectionImpl::OnSignalingChange(
    webrtc::PeerConnectionInterface::SignalingState new_state) noexcept {
  // See https://w3c.github.io/webrtc-pc/#rtcsignalingstate-enum
  switch (new_state) {
    case webrtc::PeerConnectionInterface::kStable:
      // Transitioning *to* stable means final answer received.
      // Otherwise the only possible way to be in the stable state is at start,
      // but this callback would not be invoked then because there's no
      // transition.
      {
        auto lock = std::scoped_lock{connected_callback_mutex_};
        connected_callback_();
      }
      break;
    case webrtc::PeerConnectionInterface::kHaveLocalOffer:
      break;
    case webrtc::PeerConnectionInterface::kHaveLocalPrAnswer:
      break;
    case webrtc::PeerConnectionInterface::kHaveRemoteOffer:
      break;
    case webrtc::PeerConnectionInterface::kHaveRemotePrAnswer:
      break;
    case webrtc::PeerConnectionInterface::kClosed:
      break;
  }
}

void PeerConnectionImpl::OnAddStream(
    rtc::scoped_refptr<webrtc::MediaStreamInterface> stream) noexcept {
  RTC_LOG(LS_INFO) << "Added stream #" << stream->id() << " with "
                   << stream->GetAudioTracks().size() << " audio tracks and "
                   << stream->GetVideoTracks().size() << " video tracks.";
  auto observer = std::make_unique<StreamObserver>(*this, stream);
  stream->RegisterObserver(observer.get());
  remote_streams_.emplace(std::move(observer), stream);
}

void PeerConnectionImpl::OnRemoveStream(
    rtc::scoped_refptr<webrtc::MediaStreamInterface> stream) noexcept {
  RTC_LOG(LS_INFO) << "Removed stream #" << stream->id() << " with "
                   << stream->GetAudioTracks().size() << " audio tracks and "
                   << stream->GetVideoTracks().size() << " video tracks.";
  auto it = std::find_if(
      remote_streams_.begin(), remote_streams_.end(),
      [&stream](
          std::pair<const std::unique_ptr<StreamObserver>,
                    rtc::scoped_refptr<webrtc::MediaStreamInterface>>& pair) {
        return pair.second == stream;
      });
  if (it == remote_streams_.end()) {
    return;
  }
  stream->UnregisterObserver(it->first.get());
  remote_streams_.erase(it);
}

void PeerConnectionImpl::OnDataChannel(
    rtc::scoped_refptr<webrtc::DataChannelInterface> impl) noexcept {
  // If receiving a new data channel, then obviously SCTP has been negotiated so
  // it is safe to create other ones.
  sctp_negotiated_ = true;

  // Read the data channel config
  std::string label = impl->label();
  mrsDataChannelConfig config{};
  config.id = impl->id();
  config.label = label.c_str();
  if (impl->ordered()) {
    config.flags = (mrsDataChannelConfigFlags)(
        (uint32_t)config.flags | (uint32_t)mrsDataChannelConfigFlags::kOrdered);
  }
  if (impl->reliable()) {
    config.flags = (mrsDataChannelConfigFlags)(
        (uint32_t)config.flags |
        (uint32_t)mrsDataChannelConfigFlags::kReliable);
  }

  // Create a new native object
  auto data_channel = std::make_shared<DataChannel>(this, impl);
  {
    auto lock = std::scoped_lock{data_channel_mutex_};
    data_channels_.push_back(data_channel);
    if (!label.empty()) {
      // Move |label| into the map to avoid copy
      auto it =
          data_channel_from_label_.emplace(std::move(label), data_channel);
      // Update the address to the moved item in case it changed
      config.label = it->first.c_str();
    }
    if (data_channel->id() >= 0) {
      data_channel_from_id_.try_emplace(data_channel->id(), data_channel);
    }
  }

  // Invoke the DataChannelAdded callback
  {
    auto lock = std::scoped_lock{data_channel_added_callback_mutex_};
    auto added_cb = data_channel_added_callback_;
    if (added_cb) {
      mrsDataChannelAddedInfo info{};
      info.handle = data_channel.get();
      info.id = config.id;
      info.flags = config.flags;
      info.label = config.label;
      added_cb(&info);
    }
  }
}

void PeerConnectionImpl::OnRenegotiationNeeded() noexcept {
  auto lock = std::scoped_lock{renegotiation_needed_callback_mutex_};
  auto cb = renegotiation_needed_callback_;
  if (cb) {
    cb();
  }
}

void PeerConnectionImpl::OnIceConnectionChange(
    webrtc::PeerConnectionInterface::IceConnectionState new_state) noexcept {
  auto lock = std::scoped_lock{ice_state_changed_callback_mutex_};
  auto cb = ice_state_changed_callback_;
  if (cb) {
    cb(IceStateFromImpl(new_state));
  }
}

void PeerConnectionImpl::OnIceGatheringChange(
    webrtc::PeerConnectionInterface::IceGatheringState new_state) noexcept {
  auto lock = std::scoped_lock{ice_gathering_state_changed_callback_mutex_};
  auto cb = ice_gathering_state_changed_callback_;
  if (cb) {
    cb(IceGatheringStateFromImpl(new_state));
  }
}

void PeerConnectionImpl::OnIceCandidate(
    const webrtc::IceCandidateInterface* candidate) noexcept {
  auto lock = std::scoped_lock{ice_candidate_ready_to_send_callback_mutex_};
  auto cb = ice_candidate_ready_to_send_callback_;
  if (cb) {
    std::string sdp;
    if (!candidate->ToString(&sdp))
      return;
    ensureNullTerminatedCString(sdp);
    std::string sdp_mid = candidate->sdp_mid();
    ensureNullTerminatedCString(sdp_mid);
    cb(sdp.c_str(), candidate->sdp_mline_index(), sdp_mid.c_str());
  }
}

void PeerConnectionImpl::OnAddTrack(
    rtc::scoped_refptr<webrtc::RtpReceiverInterface> receiver,
    const std::vector<rtc::scoped_refptr<webrtc::MediaStreamInterface>>&
    /*streams*/) noexcept {
  RTC_LOG(LS_INFO) << "Added receiver #" << receiver->id() << " of type "
                   << (int)receiver->media_type();
  for (auto&& stream : receiver->streams()) {
    RTC_LOG(LS_INFO) << "+ Track #" << receiver->track()->id()
                     << " with stream #" << stream->id();
  }
  rtc::scoped_refptr<webrtc::MediaStreamTrackInterface> track =
      receiver->track();
  const std::string& track_kind_str = track->kind();
  if (track_kind_str == webrtc::MediaStreamTrackInterface::kAudioKind) {
    AddRemoteMediaTrack<mrsMediaKind::kAudio>(std::move(track), receiver.get(),
                                              &audio_track_added_callback_);
  } else if (track_kind_str == webrtc::MediaStreamTrackInterface::kVideoKind) {
    AddRemoteMediaTrack<mrsMediaKind::kVideo>(std::move(track), receiver.get(),
                                              &video_track_added_callback_);
  }
}

void PeerConnectionImpl::OnTrack(
    rtc::scoped_refptr<webrtc::RtpTransceiverInterface> transceiver) noexcept {
  RTC_LOG(LS_INFO) << "Added transceiver mid=#" << transceiver->mid().value()
                   << " of type " << (int)transceiver->media_type()
                   << " with desired direction "
                   << (int)transceiver->direction();
  auto receiver = transceiver->receiver();
  if (auto track = receiver->track()) {
    RTC_LOG(LS_INFO) << "Recv with track #" << track->id()
                     << " enabled=" << track->enabled();
  } else {
    RTC_LOG(LS_INFO) << "Recv with NULL track";
  }
  for (auto&& id : receiver->stream_ids()) {
    RTC_LOG(LS_INFO) << "+ Stream #" << id;
  }
  auto sender = transceiver->sender();
  if (auto track = sender->track()) {
    RTC_LOG(LS_INFO) << "Send #" << track->id()
                     << " enabled=" << track->enabled();
  } else {
    RTC_LOG(LS_INFO) << "Send with NULL track";
  }
  for (auto&& id : sender->stream_ids()) {
    RTC_LOG(LS_INFO) << "+ Stream #" << id;
  }
}

void PeerConnectionImpl::OnRemoveTrack(
    rtc::scoped_refptr<webrtc::RtpReceiverInterface> receiver) noexcept {
  RTC_LOG(LS_INFO) << "Removed track #" << receiver->id() << " of type "
                   << (int)receiver->media_type();
  for (auto&& stream : receiver->streams()) {
    RTC_LOG(LS_INFO) << "- Track #" << receiver->id() << " with stream #"
                     << stream->id();
  }
  rtc::scoped_refptr<webrtc::MediaStreamTrackInterface> track =
      receiver->track();
  const std::string& track_kind_str = track->kind();
  if (track_kind_str == webrtc::MediaStreamTrackInterface::kAudioKind) {
    RemoveRemoteMediaTrack<mrsMediaKind::kAudio>(
        receiver.get(), &audio_track_removed_callback_);
  } else if (track_kind_str == webrtc::MediaStreamTrackInterface::kVideoKind) {
    RemoveRemoteMediaTrack<mrsMediaKind::kVideo>(
        receiver.get(), &video_track_removed_callback_);
  }
}

void PeerConnectionImpl::OnLocalDescCreated(
    webrtc::SessionDescriptionInterface* desc) noexcept {
  if (!peer_) {
    return;
  }
  rtc::scoped_refptr<webrtc::SetSessionDescriptionObserver> observer =
      new rtc::RefCountedObject<SessionDescObserver>([this] {
        if (IsUnifiedPlan()) {
          SynchronizeTransceiversUnifiedPlan(/*remote=*/false);
        } else {
          RTC_DCHECK(IsPlanB());
          // Senders are already created during |CreateOffer()|, and receivers
          // won't be created until the answer is received, so there is nothing
          // to do here.
        }

        // Fire interop callback, if any
        {
          auto lock = std::scoped_lock{local_sdp_ready_to_send_callback_mutex_};
          if (auto cb = local_sdp_ready_to_send_callback_) {
            auto desc = peer_->local_description();
            std::string type{SdpTypeToString(desc->GetType())};
            std::string sdp;
            desc->ToString(&sdp);
            cb(type.c_str(), sdp.c_str());
          }
        }
      });
  // SetLocalDescription will invoke observer.OnSuccess() once done, which
  // will in turn invoke the |local_sdp_ready_to_send_callback_| registered if
  // any, or do nothing otherwise. The observer is a mandatory parameter.
  peer_->SetLocalDescription(observer, desc);
}

Error PeerConnectionImpl::InsertTransceiverAtMlineIndex(
    int mline_index,
    RefPtr<Transceiver> transceiver) {
  RTC_CHECK(mline_index >= 0);
  rtc::CritScope lock(&transceivers_mutex_);
  if ((size_t)mline_index >= transceivers_.size()) {
    // Insert empty entries for now; they should be filled when processing
    // other added remote tracks or when finishing the transceiver status
    // update.
    while ((size_t)mline_index >= transceivers_.size() + 1) {
      transceivers_.push_back(nullptr);
    }
    transceivers_.push_back(transceiver);
  } else {
    if (transceivers_[mline_index]) {
      RTC_LOG(LS_ERROR) << "Trying to insert transceiver (name="
                        << transceiver->GetName().c_str()
                        << ") at mline index #" << mline_index
                        << ", but another transceiver (name="
                        << transceivers_[mline_index]->GetName().c_str()
                        << ") already exists with the same index.";
      return Error(Result::kUnknownError,
                   "Duplicate transceiver for mline index");
    }
    transceivers_[mline_index] = transceiver;
  }
  return Error(Result::kSuccess);
}

ErrorOr<Transceiver*>
PeerConnectionImpl::GetOrCreateTransceiverForNewRemoteTrack(
    mrsMediaKind media_kind,
    webrtc::RtpReceiverInterface* receiver) {
  RTC_DCHECK(MediaKindFromRtc(receiver->media_type()) == media_kind);

  // Try to find an existing |Transceiver| instance for the given RTP receiver
  // of the remote track.
  {
    auto it_tr = std::find_if(transceivers_.begin(), transceivers_.end(),
                              [receiver](const RefPtr<Transceiver>& tr) {
                                return tr->HasReceiver(receiver);
                              });
    if (it_tr != transceivers_.end()) {
      RTC_DCHECK(media_kind == (*it_tr)->GetMediaKind());
      Transceiver* transceiver = static_cast<Transceiver*>(it_tr->get());
      return transceiver;
    }
  }

  if (IsUnifiedPlan()) {
    // The new remote track should already have a low-level implementation RTP
    // transceiver from applying the remote description. But the wrapper for it
    // was not created yet. Find the RTP transceiver of the RTP receiver,
    // bearing in mind its mline index is not necessarily contiguous in the
    // wrapper array.
    auto transceivers = peer_->GetTransceivers();
    auto it_impl = std::find_if(
        transceivers.begin(), transceivers.end(),
        [receiver](auto&& tr) { return tr->receiver() == receiver; });
    if (it_impl == transceivers.end()) {
      return Error(
          Result::kNotFound,
          "Failed to match RTP receiver with an existing RTP transceiver.");
    }
    rtc::scoped_refptr<webrtc::RtpTransceiverInterface> impl = *it_impl;
    const int mline_index = (int)std::distance(transceivers.begin(), it_impl);
    std::string name = impl->mid().value_or(std::string{});
    std::vector<std::string> stream_ids =
        ExtractTransceiverStreamIDsFromReceiver(receiver);

    // Create a new transceiver wrapper for it
    return CreateTransceiverUnifiedPlan(media_kind, mline_index,
                                        std::move(name), std::move(stream_ids),
                                        std::move(impl));
  } else {
    RTC_DCHECK(IsPlanB());
    // In Plan B, since there is no guarantee about the order of tracks, they
    // are matched by stream ID (msid).
    int mline_index = -1;
    std::string name;
    std::vector<std::string> stream_ids;
    if (!ExtractTransceiverInfoFromReceiverPlanB(receiver, mline_index, name,
                                                 stream_ids)) {
      return Error(
          Result::kUnknownError,
          "Failed to associate RTP receiver with Plan B emulated mline index "
          "for track pairing.");
    }
    const std::string encoded_stream_ids =
        Transceiver::EncodeStreamIDs(stream_ids);

    // Create the |Transceiver| instance
    const mrsTransceiverDirection desired_direction =
        Transceiver::Direction::kRecvOnly;
    RefPtr<Transceiver> transceiver = Transceiver::CreateForPlanB(
        global_factory_, media_kind, *this, mline_index, name,
        std::move(stream_ids), desired_direction);
    transceiver->SetReceiverPlanB(receiver);
    {
      // Insert the transceiver in the peer connection's collection. The peer
      // connection will add a reference to it and keep it alive.
      auto err = InsertTransceiverAtMlineIndex(mline_index, transceiver);
      if (!err.ok()) {
        return err;
      }
    }

    // Invoke the TransceiverAdded callback
    {
      auto lock = std::scoped_lock{callbacks_mutex_};
      if (auto cb = transceiver_added_callback_) {
        mrsTransceiverAddedInfo info{};
        info.transceiver_handle = transceiver.get();
        info.transceiver_name = name.c_str();
        info.media_kind = media_kind;
        info.mline_index = mline_index;
        info.encoded_stream_ids_ = encoded_stream_ids.c_str();
        info.desired_direction = desired_direction;
        cb(&info);
      }
    }

    return transceiver.get();
  }
}

void PeerConnectionImpl::SynchronizeTransceiversUnifiedPlan(bool remote) {
  auto rtp_transceivers = peer_->GetTransceivers();
  const int num_transceivers = static_cast<int>(rtp_transceivers.size());
  for (int mline_index = 0; mline_index < num_transceivers; ++mline_index) {
    // RTP transceivers are in mline_index order by design
    const rtc::scoped_refptr<webrtc::RtpTransceiverInterface>& tr =
        rtp_transceivers[mline_index];
    RefPtr<Transceiver> transceiver;
    {
      rtc::CritScope lock(&transceivers_mutex_);
      if (mline_index < transceivers_.size()) {
        transceiver = transceivers_[mline_index];
      }
      RTC_DCHECK(!transceiver || (transceiver->impl() == tr));
    }
    if (!transceiver) {
      // If transceiver is created from the result of applying a
      // remote description, then the transceiver name is extracted
      // from the receiver.
      std::string name = tr->mid().value_or(std::string{});
      std::vector<std::string> stream_ids =
          ExtractTransceiverStreamIDsFromReceiver(tr->receiver());
      ErrorOr<Transceiver*> err = CreateTransceiverUnifiedPlan(
          MediaKindFromRtc(tr->media_type()), mline_index, std::move(name),
          std::move(stream_ids), tr);
      if (!err.ok()) {
        RTC_LOG(LS_ERROR) << "Failed to create a Transceiver object to "
                             "hold a new RTP transceiver.";
        continue;
      }
      transceiver = err.MoveValue();
    }
    // Ensure the Transceiver object is in sync with its RTP counterpart
    transceiver->OnSessionDescUpdated(remote);
  }
}

ErrorOr<Transceiver*> PeerConnectionImpl::CreateTransceiverUnifiedPlan(
    mrsMediaKind media_kind,
    int mline_index,
    std::string name,
    const std::vector<std::string>& stream_ids,
    rtc::scoped_refptr<webrtc::RtpTransceiverInterface> rtp_transceiver) {
  const Transceiver::Direction desired_direction =
      Transceiver::FromRtp(rtp_transceiver->direction());
  RefPtr<Transceiver> transceiver = Transceiver::CreateForUnifiedPlan(
      global_factory_, media_kind, *this, mline_index, std::move(name),
      stream_ids, std::move(rtp_transceiver), desired_direction);
  auto err = InsertTransceiverAtMlineIndex(mline_index, transceiver);
  if (!err.ok()) {
    return err;
  }
  {
    auto lock = std::scoped_lock{callbacks_mutex_};
    if (auto cb = transceiver_added_callback_) {
      std::string encoded_stream_ids = Transceiver::EncodeStreamIDs(stream_ids);
      mrsTransceiverAddedInfo info{};
      info.transceiver_handle = transceiver.get();
      info.transceiver_name = name.c_str();
      info.media_kind = media_kind;
      info.mline_index = mline_index;
      info.encoded_stream_ids_ = encoded_stream_ids.c_str();
      info.desired_direction = desired_direction;
      cb(&info);
    }
  }
  return transceiver.get();
}

std::string PeerConnectionImpl::ExtractTransceiverNameFromSender(
    webrtc::RtpSenderInterface* sender) {
  // Find the pairing name as the first stream ID.
  // See |LocalAudioTrack::GetName()|, |RemoteAudioTrack::GetName()|,
  // |LocalVideoTrack::GetName()|, |RemoteVideoTrack::GetName()|.
  auto ids = sender->stream_ids();
  if (!ids.empty()) {
    return ids[0];
  }
  // Fallback on track's ID, even though it's not pairable in Unified Plan (and
  // technically neither in Plan B, although this works in practice).
  if (rtc::scoped_refptr<webrtc::MediaStreamTrackInterface> track =
          sender->track()) {
    return track->id();
  }
  return {};
}

std::vector<std::string>
PeerConnectionImpl::ExtractTransceiverStreamIDsFromReceiver(
    webrtc::RtpReceiverInterface* receiver) {
  // BUG
  // webrtc::RtpReceiverInterface::stream_ids() is not proxied correctly, does
  // not resolve to its implementation and instead always returns an empty
  // vector. Use ::streams() instead even if deprecated. Fixed by
  // https://webrtc.googlesource.com/src/+/5b1477839d8569291b88dfe950089d0ebf34bc8f
#if 0
    return receiver->stream_ids();
#else
  // Use internal implementation of stream_ids()
  auto streams = receiver->streams();
  std::vector<std::string> stream_ids;
  stream_ids.reserve(streams.size());
  for (auto&& stream : streams) {
    stream_ids.push_back(stream->id());
  }
  return stream_ids;
#endif
}

bool PeerConnectionImpl::ExtractTransceiverInfoFromReceiverPlanB(
    webrtc::RtpReceiverInterface* receiver,
    int& mline_index,
    std::string& name,
    std::vector<std::string>& stream_ids) {
  std::vector<std::string> raw_stream_ids =
      ExtractTransceiverStreamIDsFromReceiver(receiver);
  if (raw_stream_ids.empty()) {
    RTC_LOG(LS_ERROR)
        << "RTP receiver has no stream ID for automatic Plan B track pairing.";
    return false;
  }
  // In Plan B the receiver has a single stream ID; we encode inside it all we
  // need: mline index, and streams IDs
  std::string encoded_str = std::move(raw_stream_ids[0]);
  return Transceiver::DecodedStreamIDForPlanB(encoded_str, mline_index, name,
                                              stream_ids);
}

webrtc::PeerConnectionInterface::IceTransportsType ICETransportTypeToNative(
    mrsIceTransportType value) {
  using Native = webrtc::PeerConnectionInterface::IceTransportsType;
  using Impl = mrsIceTransportType;
  static_assert((int)Native::kNone == (int)Impl::kNone);
  static_assert((int)Native::kNoHost == (int)Impl::kNoHost);
  static_assert((int)Native::kRelay == (int)Impl::kRelay);
  static_assert((int)Native::kAll == (int)Impl::kAll);
  return static_cast<Native>(value);
}

webrtc::PeerConnectionInterface::BundlePolicy BundlePolicyToNative(
    mrsBundlePolicy value) {
  using Native = webrtc::PeerConnectionInterface::BundlePolicy;
  using Impl = mrsBundlePolicy;
  static_assert((int)Native::kBundlePolicyBalanced == (int)Impl::kBalanced);
  static_assert((int)Native::kBundlePolicyMaxBundle == (int)Impl::kMaxBundle);
  static_assert((int)Native::kBundlePolicyMaxCompat == (int)Impl::kMaxCompat);
  return static_cast<Native>(value);
}

}  // namespace

namespace Microsoft::MixedReality::WebRTC {

ErrorOr<RefPtr<PeerConnection>> PeerConnection::create(
    const mrsPeerConnectionConfiguration& config) {
  // Set the default value for the HL1 workaround before creating any
  // connection. This has no effect on other platforms.
  SetFrameHeightRoundMode(FrameHeightRoundMode::kCrop);

  // Ensure the factory exists
  RefPtr<GlobalFactory> global_factory(GlobalFactory::InstancePtr());
  rtc::scoped_refptr<webrtc::PeerConnectionFactoryInterface> pc_factory =
      global_factory->GetPeerConnectionFactory();
  if (!pc_factory) {
    return Error(Result::kUnknownError);
  }

  // Setup the connection configuration
  webrtc::PeerConnectionInterface::RTCConfiguration rtc_config;
  if (config.encoded_ice_servers != nullptr) {
    std::string encoded_ice_servers{config.encoded_ice_servers};
    rtc_config.servers = DecodeIceServers(encoded_ice_servers);
  }
  rtc_config.enable_rtp_data_channel = false;  // Always false for security
  rtc_config.enable_dtls_srtp = true;          // Always true for security
  rtc_config.type = ICETransportTypeToNative(config.ice_transport_type);
  rtc_config.bundle_policy = BundlePolicyToNative(config.bundle_policy);
  rtc_config.sdp_semantics =
      (config.sdp_semantic == mrsSdpSemantic::kUnifiedPlan
           ? webrtc::SdpSemantics::kUnifiedPlan
           : webrtc::SdpSemantics::kPlanB);
  auto peer = new PeerConnectionImpl(std::move(global_factory));
  webrtc::PeerConnectionDependencies dependencies(peer);
  rtc::scoped_refptr<webrtc::PeerConnectionInterface> impl =
      pc_factory->CreatePeerConnection(rtc_config, std::move(dependencies));
  if (impl.get() == nullptr) {
    return Error(Result::kUnknownError);
  }
  peer->SetPeerImpl(std::move(impl));
  return RefPtr<PeerConnection>(peer);
}

void PeerConnection::GetStats(webrtc::RTCStatsCollectorCallback* callback) {
  ((PeerConnectionImpl*)this)->peer_->GetStats(callback);
}

void PeerConnection::InvokeRenegotiationNeeded() {
  ((PeerConnectionImpl*)this)->OnRenegotiationNeeded();
}

PeerConnection::PeerConnection(RefPtr<GlobalFactory> global_factory)
    : TrackedObject(std::move(global_factory), ObjectType::kPeerConnection) {}

}  // namespace Microsoft::MixedReality::WebRTC
