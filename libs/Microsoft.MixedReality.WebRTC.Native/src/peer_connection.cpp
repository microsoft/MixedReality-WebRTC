// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// This is a precompiled header, it must be on its own, followed by a blank
// line, to prevent clang-format from reordering it with other headers.
#include "pch.h"

#include "audio_frame_observer.h"
#include "data_channel.h"
#include "media/local_video_track.h"
#include "peer_connection.h"
#include "sdp_utils.h"
#include "video_frame_observer.h"
#include "sdp_utils.h"

// Internal
#include "interop/global_factory.h"
#include "interop_api.h"

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

/// Implementation of PeerConnection, which also implements
/// PeerConnectionObserver at the same time to simplify interaction with
/// the underlying implementation object.
class PeerConnectionImpl : public PeerConnection,
                           public webrtc::PeerConnectionObserver {
 public:
  mrsPeerConnectionInteropHandle hhh;

  PeerConnectionImpl(RefPtr<GlobalFactory> global_factory,
                     mrsPeerConnectionInteropHandle interop_handle)
      : PeerConnection(std::move(global_factory)),
        interop_handle_(interop_handle) {}

  ~PeerConnectionImpl() noexcept { Close(); }

  void SetPeerImpl(rtc::scoped_refptr<webrtc::PeerConnectionInterface> impl) {
    peer_ = std::move(impl);
    remote_video_observer_.reset(new VideoFrameObserver());
    local_audio_observer_.reset(new AudioFrameObserver());
    remote_audio_observer_.reset(new AudioFrameObserver());
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

  void RegisterTrackAddedCallback(
      TrackAddedCallback&& callback) noexcept override {
    auto lock = std::scoped_lock{track_added_callback_mutex_};
    track_added_callback_ = std::move(callback);
  }

  void RegisterTrackRemovedCallback(
      TrackRemovedCallback&& callback) noexcept override {
    auto lock = std::scoped_lock{track_removed_callback_mutex_};
    track_removed_callback_ = std::move(callback);
  }

  void RegisterRemoteVideoFrameCallback(
      I420AFrameReadyCallback callback) noexcept override {
    if (remote_video_observer_) {
      remote_video_observer_->SetCallback(std::move(callback));
    }
  }

  void RegisterRemoteVideoFrameCallback(
      Argb32FrameReadyCallback callback) noexcept override {
    if (remote_video_observer_) {
      remote_video_observer_->SetCallback(std::move(callback));
    }
  }

  ErrorOr<RefPtr<LocalVideoTrack>> AddLocalVideoTrack(
      rtc::scoped_refptr<webrtc::VideoTrackInterface> video_track,
      mrsLocalVideoTrackInteropHandle interop_handle) noexcept override;
  webrtc::RTCError RemoveLocalVideoTrack(
      LocalVideoTrack& video_track) noexcept override;
  void RemoveLocalVideoTracksFromSource(
      ExternalVideoTrackSource& source) noexcept override;

  void RegisterLocalAudioFrameCallback(
      AudioFrameReadyCallback callback) noexcept override {
    if (local_audio_observer_) {
      local_audio_observer_->SetCallback(std::move(callback));
    }
  }

  void RegisterRemoteAudioFrameCallback(
      AudioFrameReadyCallback callback) noexcept override {
    if (remote_audio_observer_) {
      remote_audio_observer_->SetCallback(std::move(callback));
    }
  }

  bool AddLocalAudioTrack(rtc::scoped_refptr<webrtc::AudioTrackInterface>
                              audio_track) noexcept override;
  void RemoveLocalAudioTrack() noexcept override;
  void SetLocalAudioTrackEnabled(bool enabled = true) noexcept override;
  bool IsLocalAudioTrackEnabled() const noexcept override;

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
      bool reliable,
      mrsDataChannelInteropHandle dataChannelInteropHandle) noexcept override;
  void RemoveDataChannel(const DataChannel& data_channel) noexcept override;
  void RemoveAllDataChannels() noexcept override;
  void OnDataChannelAdded(const DataChannel& data_channel) noexcept override;

  mrsResult RegisterInteropCallbacks(
      const mrsPeerConnectionInteropCallbacks& callbacks) noexcept override {
    // Make a full copy of all callbacks
    interop_callbacks_ = callbacks;
    return Result::kSuccess;
  }

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

  /// Callback on remote track removed.
  void OnRemoveTrack(rtc::scoped_refptr<webrtc::RtpReceiverInterface>
                         receiver) noexcept override;

  void OnLocalDescCreated(webrtc::SessionDescriptionInterface* desc) noexcept;

  /// The underlying PC object from the core implementation. This is NULL
  /// after |Close()| is called.
  rtc::scoped_refptr<webrtc::PeerConnectionInterface> peer_;

 protected:
  /// Peer connection name assigned by the user. This has no meaning for the
  /// implementation.
  std::string name_;

  /// Handle to the interop wrapper associated with this object.
  mrsPeerConnectionInteropHandle interop_handle_;

  /// Callbacks used for interop management.
  mrsPeerConnectionInteropCallbacks interop_callbacks_{};

  /// User callback invoked when the peer connection received a new data channel
  /// from the remote peer and added it locally.
  DataChannelAddedCallback data_channel_added_callback_
      RTC_GUARDED_BY(data_channel_added_callback_mutex_);

  /// User callback invoked when the peer connection received a data channel
  /// remove message from the remote peer and removed it locally.
  DataChannelAddedCallback data_channel_removed_callback_
      RTC_GUARDED_BY(data_channel_removed_callback_mutex_);

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

  /// User callback invoked when a remote audio or video track is added.
  TrackAddedCallback track_added_callback_
      RTC_GUARDED_BY(track_added_callback_mutex_);

  /// User callback invoked when a remote audio or video track is removed.
  TrackRemovedCallback track_removed_callback_
      RTC_GUARDED_BY(track_removed_callback_mutex_);

  std::mutex data_channel_added_callback_mutex_;
  std::mutex data_channel_removed_callback_mutex_;
  std::mutex connected_callback_mutex_;
  std::mutex local_sdp_ready_to_send_callback_mutex_;
  std::mutex ice_candidate_ready_to_send_callback_mutex_;
  std::mutex ice_state_changed_callback_mutex_;
  std::mutex ice_gathering_state_changed_callback_mutex_;
  std::mutex renegotiation_needed_callback_mutex_;
  std::mutex track_added_callback_mutex_;
  std::mutex track_removed_callback_mutex_;

  rtc::scoped_refptr<webrtc::AudioTrackInterface> local_audio_track_;
  rtc::scoped_refptr<webrtc::RtpSenderInterface> local_audio_sender_;
  std::vector<rtc::scoped_refptr<webrtc::MediaStreamInterface>> remote_streams_;

  /// Collection of all local video tracks associated with this peer connection.
  std::vector<RefPtr<LocalVideoTrack>> local_video_tracks_
      RTC_GUARDED_BY(tracks_mutex_);

  /// Mutex for all collections of all tracks.
  rtc::CriticalSection tracks_mutex_;

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

  //< TODO - Clarify lifetime of those, for now same as this PeerConnection
  std::unique_ptr<AudioFrameObserver> local_audio_observer_;
  std::unique_ptr<AudioFrameObserver> remote_audio_observer_;
  std::unique_ptr<VideoFrameObserver> remote_video_observer_;

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
};

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
    if (callback_)
      callback_();
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
IceConnectionState IceStateFromImpl(
    webrtc::PeerConnectionInterface::IceConnectionState impl_state) {
  using Native = IceConnectionState;
  using Impl = webrtc::PeerConnectionInterface::IceConnectionState;
  static_assert((int)Native::kNew == (int)Impl::kIceConnectionNew);
  static_assert((int)Native::kChecking == (int)Impl::kIceConnectionChecking);
  static_assert((int)Native::kConnected == (int)Impl::kIceConnectionConnected);
  static_assert((int)Native::kCompleted == (int)Impl::kIceConnectionCompleted);
  static_assert((int)Native::kFailed == (int)Impl::kIceConnectionFailed);
  static_assert((int)Native::kDisconnected ==
                (int)Impl::kIceConnectionDisconnected);
  static_assert((int)Native::kClosed == (int)Impl::kIceConnectionClosed);
  return (IceConnectionState)impl_state;
}

/// Convert an implementation value to a native API value of the ICE gathering
/// state. This ensures API stability if the implementation changes, although
/// currently API values are mapped 1:1 with the implementation.
IceGatheringState IceGatheringStateFromImpl(
    webrtc::PeerConnectionInterface::IceGatheringState impl_state) {
  using Native = IceGatheringState;
  using Impl = webrtc::PeerConnectionInterface::IceGatheringState;
  static_assert((int)Native::kNew == (int)Impl::kIceGatheringNew);
  static_assert((int)Native::kGathering == (int)Impl::kIceGatheringGathering);
  static_assert((int)Native::kComplete == (int)Impl::kIceGatheringComplete);
  return (IceGatheringState)impl_state;
}

ErrorOr<RefPtr<LocalVideoTrack>> PeerConnectionImpl::AddLocalVideoTrack(
    rtc::scoped_refptr<webrtc::VideoTrackInterface> video_track,
    mrsLocalVideoTrackInteropHandle interop_handle) noexcept {
  if (IsClosed()) {
    return Microsoft::MixedReality::WebRTC::Error(
        Result::kInvalidOperation, "The peer connection is closed.");
  }
  auto result = peer_->AddTrack(video_track, {kAudioVideoStreamId});
  if (result.ok()) {
    RefPtr<LocalVideoTrack> track =
        new LocalVideoTrack(global_factory_, *this, std::move(video_track),
                            std::move(result.MoveValue()), interop_handle);
    {
      rtc::CritScope lock(&tracks_mutex_);
      local_video_tracks_.push_back(track);
    }
    return track;
  }
  return ErrorFromRTCError(result.MoveError());
}

webrtc::RTCError PeerConnectionImpl::RemoveLocalVideoTrack(
    LocalVideoTrack& video_track) noexcept {
  rtc::CritScope lock(&tracks_mutex_);
  auto it = std::find_if(local_video_tracks_.begin(), local_video_tracks_.end(),
                         [&video_track](const RefPtr<LocalVideoTrack>& track) {
                           return track.get() == &video_track;
                         });
  if (it == local_video_tracks_.end()) {
    return webrtc::RTCError(
        webrtc::RTCErrorType::INVALID_PARAMETER,
        "The video track is not associated with the peer connection.");
  }
  if (peer_) {
    video_track.RemoveFromPeerConnection(*peer_);
  }
  local_video_tracks_.erase(it);
  return webrtc::RTCError::OK();
}

void PeerConnectionImpl::RemoveLocalVideoTracksFromSource(
    ExternalVideoTrackSource& source) noexcept {
  if (!peer_) {
    return;
  }
  // Remove all tracks which share this video track source.
  // Currently there is no support for source sharing, so this should
  // amount to a single track.
  std::vector<rtc::scoped_refptr<webrtc::RtpSenderInterface>> senders =
      peer_->GetSenders();
  for (auto&& sender : senders) {
    rtc::scoped_refptr<webrtc::MediaStreamTrackInterface> track =
        sender->track();
    // Apparently track can be null if destroyed already
    //< FIXME - Is this an error?
    if (!track ||
        (track->kind() != webrtc::MediaStreamTrackInterface::kVideoKind)) {
      continue;
    }
    auto video_track = (webrtc::VideoTrackInterface*)track.get();
    if (video_track->GetSource() ==
        (webrtc::VideoTrackSourceInterface*)&source) {
      peer_->RemoveTrack(sender);
    }
  }
}

void PeerConnectionImpl::SetLocalAudioTrackEnabled(bool enabled) noexcept {
  if (local_audio_track_) {
    local_audio_track_->set_enabled(enabled);
  }
}

bool PeerConnectionImpl::IsLocalAudioTrackEnabled() const noexcept {
  if (local_audio_track_) {
    return local_audio_track_->enabled();
  }
  return false;
}

bool PeerConnectionImpl::AddLocalAudioTrack(
    rtc::scoped_refptr<webrtc::AudioTrackInterface> audio_track) noexcept {
  if (local_audio_track_) {
    return false;
  }
  if (local_audio_sender_) {
    // Reuse the existing sender.
    if (local_audio_sender_->SetTrack(audio_track.get())) {
      if (auto* sink = local_audio_observer_.get()) {
        // FIXME - Current implementation of AddSink() for the local audio
        // capture device is no-op. So this callback is never fired.
        audio_track->AddSink(sink);
      }
      local_audio_track_ = std::move(audio_track);
      return true;
    }
  } else if (peer_) {
    // Create a new sender.
    auto result = peer_->AddTrack(audio_track, {kAudioVideoStreamId});
    if (result.ok()) {
      if (auto* sink = local_audio_observer_.get()) {
        // FIXME - Current implementation of AddSink() for the local audio
        // capture device is no-op. So this callback is never fired.
        audio_track->AddSink(sink);
      }
      local_audio_sender_ = result.value();
      local_audio_track_ = std::move(audio_track);
      return true;
    }
  }
  return false;
}

void PeerConnectionImpl::RemoveLocalAudioTrack() noexcept {
  if (!local_audio_track_)
    return;
  if (auto* sink = local_audio_observer_.get()) {
    local_audio_track_->RemoveSink(sink);
  }
  local_audio_sender_->SetTrack(nullptr);
  local_audio_track_ = nullptr;
}

ErrorOr<std::shared_ptr<DataChannel>> PeerConnectionImpl::AddDataChannel(
    int id,
    std::string_view label,
    bool ordered,
    bool reliable,
    mrsDataChannelInteropHandle dataChannelInteropHandle) noexcept {
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
    auto data_channel = std::make_shared<DataChannel>(this, std::move(impl),
                                                      dataChannelInteropHandle);
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

  // Invoke the DataChannelRemoved callback on the wrapper if any
  if (auto interop_handle = data_channel.GetInteropHandle()) {
    auto lock = std::scoped_lock{data_channel_removed_callback_mutex_};
    auto removed_cb = data_channel_removed_callback_;
    if (removed_cb) {
      DataChannelHandle data_native_handle = (void*)&data_channel;
      removed_cb(interop_handle, data_native_handle);
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
      if (auto interop_handle = data_channel->GetInteropHandle()) {
        DataChannelHandle data_native_handle = (void*)&data_channel;
        removed_cb(interop_handle, data_native_handle);
      }
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

  // Invoke the DataChannelAdded callback on the wrapper if any
  if (auto interop_handle = data_channel.GetInteropHandle()) {
    auto lock = std::scoped_lock{data_channel_added_callback_mutex_};
    auto added_cb = data_channel_added_callback_;
    if (added_cb) {
      DataChannelHandle data_native_handle = (void*)&data_channel;
      added_cb(interop_handle, data_native_handle);
    }
  }
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
  webrtc::PeerConnectionInterface::RTCOfferAnswerOptions options;
  /*if (mandatory_receive_)*/ {  //< TODO - This is legacy, should use
                                 // transceivers
    options.offer_to_receive_audio = true;
    options.offer_to_receive_video = true;
  }
  {
    auto lock = std::scoped_lock{data_channel_mutex_};
    if (data_channels_.empty()) {
      sctp_negotiated_ = false;
    }
  }
  auto observer =
      new rtc::RefCountedObject<CreateSessionDescObserver>(this);  // 0 ref
  peer_->CreateOffer(observer, options);
  RTC_CHECK(observer->HasOneRef());  // should be == 1
  return true;
}

bool PeerConnectionImpl::CreateAnswer() noexcept {
  if (!peer_) {
    return false;
  }
  webrtc::PeerConnectionInterface::RTCOfferAnswerOptions options;
  /*if (mandatory_receive_)*/ {  //< TODO - This is legacy, should use
                                 // transceivers
    options.offer_to_receive_audio = true;
    options.offer_to_receive_video = true;
  }
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

  // Close the connection
  peer_->Close();

  // Remove local tracks
  {
    rtc::CritScope lock(&tracks_mutex_);
    while (!local_video_tracks_.empty()) {
      RefPtr<LocalVideoTrack>& ptr = local_video_tracks_.back();
      RemoveLocalVideoTrack(*ptr);
    }
  }

  // Ensure that observers (sinks) are removed, otherwise the media pipelines
  // will continue to try to feed them with data after they're destroyed
  // RemoveLocalVideoTrack(); TODO - do we need to keep a list of local tracks
  // and do something here?
  RemoveLocalAudioTrack();
  local_audio_sender_ = nullptr;
  for (auto stream : remote_streams_) {
    if (auto* sink = remote_video_observer_.get()) {
      for (auto&& video_track : stream->GetVideoTracks()) {
        video_track->RemoveSink(sink);
      }
    }
    if (auto* sink = remote_audio_observer_.get()) {
      for (auto&& audio_track : stream->GetAudioTracks()) {
        audio_track->RemoveSink(sink);
      }
    }
  }
  remote_streams_.clear();

  RemoveAllDataChannels();

  // Release the internal webrtc::PeerConnection implementation. This call will
  // get proxied to the WebRTC signaling thread, so needs to occur before the
  // global factory shuts down and terminates the threads, which potentially
  // happens just after this call when called from the destructor if this is the
  // last object alive. This is also used as a marker for |IsClosed()|.
  peer_ = nullptr;
}

bool PeerConnectionImpl::IsClosed() const noexcept {
  return (peer_ == nullptr);
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
  remote_streams_.push_back(stream);
}

void PeerConnectionImpl::OnRemoveStream(
    rtc::scoped_refptr<webrtc::MediaStreamInterface> stream) noexcept {
  RTC_LOG(LS_INFO) << "Removed stream #" << stream->id() << " with "
                   << stream->GetAudioTracks().size() << " audio tracks and "
                   << stream->GetVideoTracks().size() << " video tracks.";
  auto it = std::find(remote_streams_.begin(), remote_streams_.end(), stream);
  if (it == remote_streams_.end()) {
    return;
  }
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

  // Create an interop wrapper for the new native object if needed
  mrsDataChannelInteropHandle data_channel_interop_handle{};
  mrsDataChannelCallbacks callbacks{};
  if (auto create_cb = interop_callbacks_.data_channel_create_object) {
    data_channel_interop_handle =
        (*create_cb)(interop_handle_, config, &callbacks);
  }

  // Create a new native object
  auto data_channel =
      std::make_shared<DataChannel>(this, impl, data_channel_interop_handle);
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

  // TODO -- Invoke some callback on the C++ side

  if (data_channel_interop_handle) {
    // Register the interop callbacks
    data_channel->SetMessageCallback(DataChannel::MessageCallback{
        callbacks.message_callback, callbacks.message_user_data});
    data_channel->SetBufferingCallback(DataChannel::BufferingCallback{
        callbacks.buffering_callback, callbacks.buffering_user_data});
    data_channel->SetStateCallback(DataChannel::StateCallback{
        callbacks.state_callback, callbacks.state_user_data});

    // Invoke the DataChannelAdded callback on the wrapper
    {
      auto lock = std::scoped_lock{data_channel_added_callback_mutex_};
      auto added_cb = data_channel_added_callback_;
      if (added_cb) {
        const DataChannelHandle data_native_handle = data_channel.get();
        added_cb(data_channel_interop_handle, data_native_handle);
      }
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
  RTC_LOG(LS_INFO) << "Added track #" << receiver->id() << " of type "
                   << (int)receiver->media_type();
  for (auto&& stream : receiver->streams()) {
    RTC_LOG(LS_INFO) << "+ Track #" << receiver->id() << " with stream #"
                     << stream->id();
  }

  // Register the remote observer
  rtc::scoped_refptr<webrtc::MediaStreamTrackInterface> track =
      receiver->track();
  TrackKind trackKind = TrackKind::kUnknownTrack;
  const std::string& trackKindStr = track->kind();
  if (trackKindStr == webrtc::MediaStreamTrackInterface::kAudioKind) {
    trackKind = TrackKind::kAudioTrack;
    if (auto* sink = remote_audio_observer_.get()) {
      auto audio_track = static_cast<webrtc::AudioTrackInterface*>(track.get());
      audio_track->AddSink(sink);
    }
  } else if (trackKindStr == webrtc::MediaStreamTrackInterface::kVideoKind) {
    trackKind = TrackKind::kVideoTrack;
    if (auto* sink = remote_video_observer_.get()) {
      rtc::VideoSinkWants sink_settings{};
      sink_settings.rotation_applied =
          true;  // no exposed API for caller to handle rotation
      auto video_track = static_cast<webrtc::VideoTrackInterface*>(track.get());
      video_track->AddOrUpdateSink(sink, sink_settings);
    }
  } else {
    return;
  }

  // Invoke the TrackAdded callback
  {
    auto lock = std::scoped_lock{track_added_callback_mutex_};
    auto cb = track_added_callback_;
    if (cb) {
      cb(trackKind);
    }
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

  // Unregister the remote observer
  rtc::scoped_refptr<webrtc::MediaStreamTrackInterface> track =
      receiver->track();
  TrackKind trackKind = TrackKind::kUnknownTrack;
  const std::string& trackKindStr = track->kind();
  if (trackKindStr == webrtc::MediaStreamTrackInterface::kAudioKind) {
    trackKind = TrackKind::kAudioTrack;
    if (auto* sink = remote_audio_observer_.get()) {
      auto audio_track = static_cast<webrtc::AudioTrackInterface*>(track.get());
      audio_track->RemoveSink(sink);
    }
  } else if (trackKindStr == webrtc::MediaStreamTrackInterface::kVideoKind) {
    trackKind = TrackKind::kVideoTrack;
    if (auto* sink = remote_video_observer_.get()) {
      auto video_track = static_cast<webrtc::VideoTrackInterface*>(track.get());
      video_track->RemoveSink(sink);
    }
  } else {
    return;
  }

  // Invoke the TrackRemoved callback
  {
    auto lock = std::scoped_lock{track_removed_callback_mutex_};
    auto cb = track_removed_callback_;
    if (cb) {
      cb(trackKind);
    }
  }
}

void PeerConnectionImpl::OnLocalDescCreated(
    webrtc::SessionDescriptionInterface* desc) noexcept {
  if (!peer_) {
    return;
  }
  auto lock = std::scoped_lock{local_sdp_ready_to_send_callback_mutex_};
  auto cb = local_sdp_ready_to_send_callback_;
  rtc::scoped_refptr<webrtc::SetSessionDescriptionObserver> observer;
  if (cb) {
    std::string type{SdpTypeToString(desc->GetType())};
    ensureNullTerminatedCString(type);
    std::string sdp;
    desc->ToString(&sdp);
    ensureNullTerminatedCString(sdp);
    observer = new rtc::RefCountedObject<SessionDescObserver>(
        [cb, type = std::move(type), sdp = std::move(sdp)] {
          cb(type.c_str(), sdp.c_str());
        });
  } else {
    observer = new rtc::RefCountedObject<SessionDescObserver>();
  }
  // SetLocalDescription will invoke observer.OnSuccess() once done, which
  // will in turn invoke the |local_sdp_ready_to_send_callback_| registered if
  // any, or do nothing otherwise. The observer is a mandatory parameter.
  peer_->SetLocalDescription(observer, desc);
}

webrtc::PeerConnectionInterface::IceTransportsType ICETransportTypeToNative(
    IceTransportType mrsValue) {
  using Native = webrtc::PeerConnectionInterface::IceTransportsType;
  using Impl = IceTransportType;
  static_assert((int)Native::kNone == (int)Impl::kNone);
  static_assert((int)Native::kNoHost == (int)Impl::kNoHost);
  static_assert((int)Native::kRelay == (int)Impl::kRelay);
  static_assert((int)Native::kAll == (int)Impl::kAll);
  return static_cast<Native>(mrsValue);
}

webrtc::PeerConnectionInterface::BundlePolicy BundlePolicyToNative(
    BundlePolicy mrsValue) {
  using Native = webrtc::PeerConnectionInterface::BundlePolicy;
  using Impl = BundlePolicy;
  static_assert((int)Native::kBundlePolicyBalanced == (int)Impl::kBalanced);
  static_assert((int)Native::kBundlePolicyMaxBundle == (int)Impl::kMaxBundle);
  static_assert((int)Native::kBundlePolicyMaxCompat == (int)Impl::kMaxCompat);
  return static_cast<Native>(mrsValue);
}

}  // namespace

namespace Microsoft::MixedReality::WebRTC {

ErrorOr<RefPtr<PeerConnection>> PeerConnection::create(
    const PeerConnectionConfiguration& config,
    mrsPeerConnectionInteropHandle interop_handle) {
  // Set the default value for the HL1 workaround before creating any
  // connection. This has no effect on other platforms.
  SetFrameHeightRoundMode(FrameHeightRoundMode::kCrop);

  // Ensure the factory exists
  RefPtr<GlobalFactory> global_factory(GlobalFactory::InstancePtr());
  auto pc_factory = global_factory->GetPeerConnectionFactory();
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
  rtc_config.sdp_semantics = (config.sdp_semantic == SdpSemantic::kUnifiedPlan
                                  ? webrtc::SdpSemantics::kUnifiedPlan
                                  : webrtc::SdpSemantics::kPlanB);
  auto peer = new PeerConnectionImpl(std::move(global_factory), interop_handle);
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

PeerConnection::PeerConnection(RefPtr<GlobalFactory> global_factory)
    : TrackedObject(std::move(global_factory), ObjectType::kPeerConnection) {}

}  // namespace Microsoft::MixedReality::WebRTC
