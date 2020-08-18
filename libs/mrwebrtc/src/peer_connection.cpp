// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// This is a precompiled header, it must be on its own, followed by a blank
// line, to prevent clang-format from reordering it with other headers.
#include "pch.h"

#include "audio_frame_observer.h"
#include "common_audio/resampler/include/resampler.h"
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

#include <functional>

// Include implementation because we cannot access the mline index from the
// RtpTransceiverInterface. This is a not-so-clean workaround.
// See PeerConnection::ExtractMlineIndexFromRtpTransceiver() for details.
#pragma warning(push, 2)
#pragma warning(disable : 4100)
#include "pc/rtptransceiver.h"
#pragma warning(pop)

#if defined(_M_IX86) /* x86 */ && defined(WINAPI_FAMILY) && \
    (WINAPI_FAMILY == WINAPI_FAMILY_APP) /* UWP app */ &&   \
    defined(_WIN32_WINNT_WIN10) &&                          \
    _WIN32_WINNT >= _WIN32_WINNT_WIN10 /* Win10 */

// Stop WinRT from polluting the global namespace
// https://developercommunity.visualstudio.com/content/problem/859178/asyncinfoh-defines-the-error-symbol-at-global-name.html
#define _HIDE_GLOBAL_ASYNC_STATUS 1

#include "third_party/winuwp_h264/H264Encoder/H264Encoder.h"

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

namespace Microsoft {
namespace MixedReality {
namespace WebRTC {
void PeerConnection::SetFrameHeightRoundMode(FrameHeightRoundMode value) {
#define CHECK_ENUM_VALUE(NAME1, NAME2)                                           \
  static_assert((int)FrameHeightRoundMode::k##NAME1 ==                           \
                    (int)webrtc::WinUWPH264EncoderImpl::FrameHeightRoundMode::k##NAME2, \
                "WinUWPH264EncoderImpl::FrameHeightRoundMode does not match FrameHeightRoundMode");
  CHECK_ENUM_VALUE(None, NoChange);
  CHECK_ENUM_VALUE(Crop, Crop);
  CHECK_ENUM_VALUE(Pad, Pad);
#undef CHECK_ENUM_VALUE
  if (IsHololens()) {
    webrtc::WinUWPH264EncoderImpl::global_frame_height_round_mode.store(
        (webrtc::WinUWPH264EncoderImpl::FrameHeightRoundMode)value);
  }
}
}  // namespace WebRTC
}  // namespace MixedReality
}  // namespace Microsoft

#else

namespace Microsoft {
namespace MixedReality {
namespace WebRTC {
void PeerConnection::SetFrameHeightRoundMode(FrameHeightRoundMode /*value*/) {}
}  // namespace WebRTC
}  // namespace MixedReality
}  // namespace Microsoft

#endif

namespace {

using namespace Microsoft::MixedReality::WebRTC;

class CreateSessionDescObserver
    : public webrtc::CreateSessionDescriptionObserver {
 public:
  CreateSessionDescObserver(RefPtr<PeerConnection> peer_connection)
      : peer_connection_(
            std::forward<RefPtr<PeerConnection>>(peer_connection)) {}

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
  RefPtr<PeerConnection> peer_connection_;
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

/// Custom observer for SetRemoteDescription(), invoked when the call completes
/// (successfully or not) to notify the original caller.
struct SetRemoteSessionDescObserver
    : public webrtc::SetRemoteDescriptionObserverInterface {
 public:
  SetRemoteSessionDescObserver() = default;
  template <typename Closure>
  SetRemoteSessionDescObserver(Closure&& callback)
      : callback_(std::forward<Closure>(callback)) {}
  void OnSetRemoteDescriptionComplete(webrtc::RTCError error) override {
    if (error.ok()) {
      RTC_LOG(LS_INFO) << "Remote description successfully set.";
    } else {
      RTC_LOG(LS_ERROR) << "Error setting remote description: "
                        << error.message();
    }
    // #313 - Always call the callback if provided; this is used in some interop
    // to complete some async task / promise.
    if (callback_) {
      callback_(ResultFromRTCErrorType(error.type()), error.message());
    }
  }

 protected:
  std::function<void(mrsResult, const char*)> callback_;
};

/// Convert an implementation value to a native API value of the ICE connection
/// state. This ensures API stability if the implementation changes, although
/// currently API values are mapped 1:1 with the implementation.
mrsIceConnectionState IceStateFromImpl(
    webrtc::PeerConnectionInterface::IceConnectionState impl_state) {
  using Native = mrsIceConnectionState;
  using Impl = webrtc::PeerConnectionInterface::IceConnectionState;
  static_assert((int)Native::kNew == (int)Impl::kIceConnectionNew, "");
  static_assert((int)Native::kChecking == (int)Impl::kIceConnectionChecking,
                "");
  static_assert((int)Native::kConnected == (int)Impl::kIceConnectionConnected,
                "");
  static_assert((int)Native::kCompleted == (int)Impl::kIceConnectionCompleted,
                "");
  static_assert((int)Native::kFailed == (int)Impl::kIceConnectionFailed, "");
  static_assert((int)Native::kDisconnected == (int)Impl::kIceConnectionDisconnected, "");
  static_assert((int)Native::kClosed == (int)Impl::kIceConnectionClosed, "");
  return (mrsIceConnectionState)impl_state;
}

/// Convert an implementation value to a native API value of the ICE gathering
/// state. This ensures API stability if the implementation changes, although
/// currently API values are mapped 1:1 with the implementation.
mrsIceGatheringState IceGatheringStateFromImpl(
    webrtc::PeerConnectionInterface::IceGatheringState impl_state) {
  using Native = mrsIceGatheringState;
  using Impl = webrtc::PeerConnectionInterface::IceGatheringState;
  static_assert((int)Native::kNew == (int)Impl::kIceGatheringNew, "");
  static_assert((int)Native::kGathering == (int)Impl::kIceGatheringGathering,
                "");
  static_assert((int)Native::kComplete == (int)Impl::kIceGatheringComplete, "");
  return (mrsIceGatheringState)impl_state;
}

webrtc::PeerConnectionInterface::IceTransportsType ICETransportTypeToNative(
    mrsIceTransportType value) {
  using Native = webrtc::PeerConnectionInterface::IceTransportsType;
  using Impl = mrsIceTransportType;
  static_assert((int)Native::kNone == (int)Impl::kNone, "");
  static_assert((int)Native::kNoHost == (int)Impl::kNoHost, "");
  static_assert((int)Native::kRelay == (int)Impl::kRelay, "");
  static_assert((int)Native::kAll == (int)Impl::kAll, "");
  return static_cast<Native>(value);
}

webrtc::PeerConnectionInterface::BundlePolicy BundlePolicyToNative(
    mrsBundlePolicy value) {
  using Native = webrtc::PeerConnectionInterface::BundlePolicy;
  using Impl = mrsBundlePolicy;
  static_assert((int)Native::kBundlePolicyBalanced == (int)Impl::kBalanced, "");
  static_assert((int)Native::kBundlePolicyMaxBundle == (int)Impl::kMaxBundle,
                "");
  static_assert((int)Native::kBundlePolicyMaxCompat == (int)Impl::kMaxCompat,
                "");
  return static_cast<Native>(value);
}

}  // namespace

namespace Microsoft {
namespace MixedReality {
namespace WebRTC {

ErrorOr<std::shared_ptr<DataChannel>> PeerConnection::AddDataChannel(
    int id,
    absl::string_view label,
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
    config.negotiated = false;
  } else if (id <= 0xFFFF) {
    // Out-of-band negotiated data channel with pre-established ID
    config.id = id;
    config.negotiated = true;
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
      std::lock_guard<std::mutex> lock(data_channel_mutex_);
      data_channels_.push_back(data_channel);
      if (!labelString.empty()) {
        data_channel_from_label_.emplace(std::move(labelString), data_channel);
      }
      if (config.id >= 0) {
        data_channel_from_id_.emplace(config.id, data_channel);
      }
    }

    // For in-band channels, the creating side (here) doesn't receive an
    // OnDataChannel() message, so invoke the DataChannelAdded event right now.
    // For out-of-band channels, the standard doesn't ask to raise that event,
    // but we do it anyway for convenience and for consistency.
    // Call from the signaling thread so that user callbacks can access the
    // channel state (e.g. register channel callbacks) without it being changed
    // concurrently by WebRTC.
    global_factory_->GetSignalingThread()->Invoke<void>(RTC_FROM_HERE, [&]() {
      OnDataChannelAdded(*data_channel.get());
    });

    return data_channel;
  }
  return Error(Result::kUnknownError);
}

void PeerConnection::RemoveDataChannel(
    const DataChannel& data_channel) noexcept {
  // Cache variables which require a dispatch to the signaling thread
  // to minimize the risk of a deadlock with the data channel lock below.
  const int id = data_channel.id();
  const std::string label = data_channel.label();

  // Move the channel to destroy out of the internal data structures
  std::shared_ptr<DataChannel> data_channel_ptr;
  {
    std::lock_guard<std::mutex> lock(data_channel_mutex_);

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
    std::lock_guard<std::mutex> lock(data_channel_removed_callback_mutex_);
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

void PeerConnection::RemoveAllDataChannels() noexcept {
  std::lock_guard<std::mutex> lock_cb(data_channel_removed_callback_mutex_);
  auto removed_cb = data_channel_removed_callback_;
  std::lock_guard<std::mutex> lock(data_channel_mutex_);
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

void PeerConnection::OnDataChannelAdded(
    const DataChannel& data_channel) noexcept {
  // The channel must be owned by this PeerConnection, so must be known already.
  // It was added in AddDataChannel() when the DataChannel object was created.
#if RTC_DCHECK_IS_ON
  {
    std::lock_guard<std::mutex> lock(data_channel_mutex_);
    RTC_DCHECK(std::find_if(
                   data_channels_.begin(), data_channels_.end(),
                   [&data_channel](const std::shared_ptr<DataChannel>& other) {
                     return other.get() == &data_channel;
                   }) != data_channels_.end());
  }
#endif  // RTC_DCHECK_IS_ON

  // Invoke the DataChannelAdded callback
  {
    std::lock_guard<std::mutex> lock(data_channel_added_callback_mutex_);
    auto added_cb = data_channel_added_callback_;
    if (added_cb) {
      mrsDataChannelAddedInfo info{};
      info.handle = (void*)&data_channel;
      info.id = data_channel.id();
      info.flags = data_channel.flags();
      std::string label_str = data_channel.label();  // keep alive
      info.label = label_str.c_str();
      added_cb(&info);

      // The user assumes an initial state of kConnecting; if this has already
      // changed, fire the event to notify any callback that has been
      // registered.
      if (data_channel.impl()->state() !=
          webrtc::DataChannelInterface::kConnecting) {
        data_channel.InvokeOnStateChange();
      }
    }
  }
}

void PeerConnection::OnStreamChanged(
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

Error PeerConnection::AddIceCandidate(
    const mrsIceCandidate& candidate) noexcept {
  if (!peer_) {
    return Error(Result::kInvalidOperation);
  }
  webrtc::SdpParseError error;
  std::unique_ptr<webrtc::IceCandidateInterface> ice_candidate(
      webrtc::CreateIceCandidate(candidate.sdp_mid, candidate.sdp_mline_index,
                                 candidate.content, &error));
  if (!ice_candidate) {
    return Error(Result::kInvalidParameter, error.description);
  }
  if (!peer_->AddIceCandidate(ice_candidate.get())) {
    return Error(Result::kUnknownError,
                 "Failed to add ICE candidate to peer connection");
  }
  return Error(Result::kSuccess);
}

bool PeerConnection::CreateOffer() noexcept {
  if (!peer_) {
    return false;
  }
  {
    std::lock_guard<std::mutex> lock(data_channel_mutex_);
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

bool PeerConnection::CreateAnswer() noexcept {
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

void PeerConnection::Close() noexcept {
  if (!peer_) {
    return;
  }

  // Close the connection
  peer_->Close();

  // At this point no callbacks should be called anymore, so it's safe to reset
  // the transceiver/track data.

  {
    rtc::CritScope lock(&transceivers_mutex_);

    // Force-remove remote tracks. It doesn't look like the TrackRemoved
    // callback is called when Close() is used, so force it here.
    std::lock_guard<std::mutex> cb_lock{media_track_callback_mutex_};
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
  peer_ = nullptr;
}

bool PeerConnection::IsClosed() const noexcept {
  return (peer_ == nullptr);
}

ErrorOr<Transceiver*> PeerConnection::AddTransceiver(
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
  const int mline_index = -1;  // just created, so not associated yet
  switch (peer_->GetConfiguration().sdp_semantics) {
    case webrtc::SdpSemantics::kPlanB: {
      // Plan B doesn't have transceivers; just create a wrapper.
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

      // Create the transceiver wrapper
      transceiver = Transceiver::CreateForUnifiedPlan(
          global_factory_, config.media_kind, *this, mline_index, name,
          std::move(stream_ids), std::move(impl), config.desired_direction);
    } break;
    default:
      return Error(Result::kUnknownError, "Unknown SDP semantic.");
  }
  RTC_DCHECK(transceiver);
  {
    rtc::CritScope lock(&transceivers_mutex_);
    transceivers_.push_back(transceiver);
  }

  // Invoke the TransceiverAdded callback
  {
    std::lock_guard<std::mutex> lock(callbacks_mutex_);
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

  return transceiver.get();
}

Error PeerConnection::SetRemoteDescriptionAsync(
    mrsSdpMessageType type,
    const char* sdp,
    RemoteDescriptionAppliedCallback callback) noexcept {
  if (!peer_) {
    return Error(mrsResult::kInvalidOperation);
  }
  {
    std::lock_guard<std::mutex> lock(data_channel_mutex_);
    if (data_channels_.empty()) {
      sctp_negotiated_ = false;
    }
  }
  const webrtc::SdpType sdp_type = SdpTypeFromApiType(type);
  std::string remote_desc(sdp);
  webrtc::SdpParseError error;
  std::unique_ptr<webrtc::SessionDescriptionInterface> session_description(
      webrtc::CreateSessionDescription(sdp_type, remote_desc, &error));
  if (!session_description) {
    return Error(mrsResult::kInvalidParameter, error.description.c_str());
  }
  rtc::scoped_refptr<webrtc::SetRemoteDescriptionObserverInterface> observer =
      new rtc::RefCountedObject<SetRemoteSessionDescObserver>(
          [this, callback](mrsResult result, const char* error_message) {
            if (result == Result::kSuccess) {
              if (IsUnifiedPlan()) {
                SynchronizeTransceiversUnifiedPlan(/*remote=*/true);
              } else {
                RTC_DCHECK(IsPlanB());
                // In Plan B there is no RTP transceiver, and all remote tracks
                // which start/stop receiving are triggering a TrackAdded or
                // TrackRemoved event. Therefore there is no extra work to do
                // like there was in Unified Plan above.

                // TODO - Clarify if this is really needed (and if we really
                // need to force the update) for parity with Unified Plan and to
                // get the transceiver update event fired once and once only.
                for (auto&& tr : transceivers_) {
                  tr->OnSessionDescUpdated(/*remote=*/true, /*forced=*/true);
                }
              }
            }
            // Fire completed callback to signal remote description was applied.
            callback(result, error_message);
          });
  peer_->SetRemoteDescription(std::move(session_description),
                              std::move(observer));
  return Error(mrsResult::kSuccess);
}

void PeerConnection::OnSignalingChange(
    webrtc::PeerConnectionInterface::SignalingState new_state) noexcept {
  // See https://w3c.github.io/webrtc-pc/#rtcsignalingstate-enum
  switch (new_state) {
    case webrtc::PeerConnectionInterface::kStable:
      // Transitioning *to* stable means final answer received.
      // Otherwise the only possible way to be in the stable state is at start,
      // but this callback would not be invoked then because there's no
      // transition.
      {
        std::lock_guard<std::mutex> lock(connected_callback_mutex_);
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

void PeerConnection::OnAddStream(
    rtc::scoped_refptr<webrtc::MediaStreamInterface> stream) noexcept {
  RTC_LOG(LS_INFO) << "Added stream #" << stream->id() << " with "
                   << stream->GetAudioTracks().size() << " audio tracks and "
                   << stream->GetVideoTracks().size() << " video tracks.";
  auto observer = std::make_unique<StreamObserver>(*this, stream);
  stream->RegisterObserver(observer.get());
  remote_streams_.emplace(std::move(observer), stream);
}

void PeerConnection::OnRemoveStream(
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

void PeerConnection::OnDataChannel(
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
    std::lock_guard<std::mutex> lock(data_channel_mutex_);
    data_channels_.push_back(data_channel);
    if (!label.empty()) {
      // Move |label| into the map to avoid copy
      auto it =
          data_channel_from_label_.emplace(std::move(label), data_channel);
      // Update the address to the moved item in case it changed
      config.label = it->first.c_str();
    }
    if (data_channel->id() >= 0) {
      data_channel_from_id_.emplace(data_channel->id(), data_channel);
    }
  }

  // Invoke the DataChannelAdded callback
  {
    std::lock_guard<std::mutex> lock(data_channel_added_callback_mutex_);
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

void PeerConnection::OnRenegotiationNeeded() noexcept {
  std::lock_guard<std::mutex> lock(renegotiation_needed_callback_mutex_);
  auto cb = renegotiation_needed_callback_;
  if (cb) {
    cb();
  }
}

void PeerConnection::OnIceConnectionChange(
    webrtc::PeerConnectionInterface::IceConnectionState new_state) noexcept {
  std::lock_guard<std::mutex> lock(ice_state_changed_callback_mutex_);
  auto cb = ice_state_changed_callback_;
  if (cb) {
    cb(IceStateFromImpl(new_state));
  }
}

void PeerConnection::OnIceGatheringChange(
    webrtc::PeerConnectionInterface::IceGatheringState new_state) noexcept {
  std::lock_guard<std::mutex> lock(ice_gathering_state_changed_callback_mutex_);
  auto cb = ice_gathering_state_changed_callback_;
  if (cb) {
    cb(IceGatheringStateFromImpl(new_state));
  }
}

void PeerConnection::OnIceCandidate(
    const webrtc::IceCandidateInterface* candidate) noexcept {
  std::lock_guard<std::mutex> lock(ice_candidate_ready_to_send_callback_mutex_);
  auto cb = ice_candidate_ready_to_send_callback_;
  if (cb) {
    std::string sdp;
    if (!candidate->ToString(&sdp)) {
      RTC_LOG(LS_ERROR) << "Failed to stringify ICE candidate into SDP format.";
      return;
    }
    std::string sdp_mid = candidate->sdp_mid();
    mrsIceCandidate ice_candidate{};
    ice_candidate.sdp_mid = sdp_mid.c_str();
    ice_candidate.sdp_mline_index = candidate->sdp_mline_index();
    ice_candidate.content = sdp.c_str();
    cb(&ice_candidate);
  }
}

void PeerConnection::OnAddTrack(
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
    RefPtr<RemoteAudioTrack> track_wrapper =
        AddRemoteMediaTrack<mrsMediaKind::kAudio>(
            std::move(track), receiver.get(), &audio_track_added_callback_);
    if (audio_mixer_) {
      // The track won't be output by the mixer until OutputSource is called.
      // We need to get the ssrc of the receiver in order to match the track to
      // the corresponding audio source in the AudioMixer. There doesn't seem to
      // be a way to get the ssrc from RTPReceiverInterface directly -
      // GetSources() returns no elements if called at this point, nor when the
      // first frame arrives. The easiest way seems to be requesting the stats
      // for the receiver and getting it from there.
      struct SetSsrcObserver : public webrtc::RTCStatsCollectorCallback {
        SetSsrcObserver(RefPtr<RemoteAudioTrack> track)
            : track_(std::move(track)) {}
        virtual void OnStatsDelivered(
            const rtc::scoped_refptr<const webrtc::RTCStatsReport>&
                report) noexcept override {
          const auto& stats =
              report->GetStatsOfType<webrtc::RTCInboundRTPStreamStats>();
          RTC_DCHECK_EQ(stats.size(), 1);
          // This starts to output the track - or not, if the user has called
          // OutputToDevice(false) in the track added callback.
          track_->InitSsrc(*stats[0]->ssrc);
        }
        RefPtr<RemoteAudioTrack> track_;
      };
      auto stats_observer =
          new rtc::RefCountedObject<SetSsrcObserver>(track_wrapper);
      peer_->GetStats(receiver, stats_observer);
    }
  } else if (track_kind_str == webrtc::MediaStreamTrackInterface::kVideoKind) {
    AddRemoteMediaTrack<mrsMediaKind::kVideo>(std::move(track), receiver.get(),
                                              &video_track_added_callback_);
  }
}

void PeerConnection::OnTrack(
    rtc::scoped_refptr<webrtc::RtpTransceiverInterface> transceiver) noexcept {
  RTC_LOG(LS_INFO) << "Added transceiver mid=#" << transceiver->mid().value()
                   << " of type '" << ToString(transceiver->media_type())
                   << "' with desired direction "
                   << ToString(transceiver->direction());
  auto sender = transceiver->sender();
  if (auto track = sender->track()) {
    RTC_LOG(LS_INFO) << "Send #" << track->id()
                     << " enabled=" << ToString(track->enabled());
  } else {
    RTC_LOG(LS_INFO) << "Send with NULL track";
  }
  for (auto&& id : sender->stream_ids()) {
    RTC_LOG(LS_INFO) << "+ Stream #" << id;
  }
  auto receiver = transceiver->receiver();
  if (auto track = receiver->track()) {
    RTC_LOG(LS_INFO) << "Recv with track #" << track->id()
                     << " enabled=" << ToString(track->enabled());
  } else {
    RTC_LOG(LS_INFO) << "Recv with NULL track";
  }
  for (auto&& id : receiver->stream_ids()) {
    RTC_LOG(LS_INFO) << "+ Stream #" << id;
  }
}

void PeerConnection::OnRemoveTrack(
    rtc::scoped_refptr<webrtc::RtpReceiverInterface> receiver) noexcept {
  RTC_LOG(LS_INFO) << "Removed track #" << receiver->id() << " of type '"
                   << ToString(receiver->media_type()) << "'";
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

void PeerConnection::OnLocalDescCreated(
    webrtc::SessionDescriptionInterface* desc) noexcept {
  RTC_DCHECK(peer_);
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
          std::lock_guard<std::mutex> lock(local_sdp_ready_to_send_callback_mutex_);
          if (auto cb = local_sdp_ready_to_send_callback_) {
            auto desc = peer_->local_description();
            const mrsSdpMessageType type = ApiTypeFromSdpType(desc->GetType());
            std::string sdp;
            desc->ToString(&sdp);
            cb(type, sdp.c_str());
          }
        }
      });
  // SetLocalDescription will invoke observer.OnSuccess() once done, which
  // will in turn invoke the |local_sdp_ready_to_send_callback_| registered if
  // any, or do nothing otherwise. The observer is a mandatory parameter.
  peer_->SetLocalDescription(observer, desc);
}

RefPtr<Transceiver> PeerConnection::FindWrapperFromRtpTransceiver(
    webrtc::RtpTransceiverInterface* rtp_tr) const {
  RTC_DCHECK(rtp_tr);
  rtc::CritScope lock(&transceivers_mutex_);
  auto it = std::find_if(transceivers_.begin(), transceivers_.end(),
                         [&rtp_tr](const RefPtr<Transceiver>& tr) {
                           return (tr->impl() == rtp_tr);
                         });
  if (it != transceivers_.end()) {
    return *it;
  }
  return nullptr;
}

int PeerConnection::ExtractMlineIndexFromRtpTransceiver(
    webrtc::RtpTransceiverInterface* tr) {
  RTC_DCHECK(tr);
  // We don't have access to the actual mline index, it is not exposed in the
  // RTP transceiver API, so instead we cast to the actual implementation type
  // and retrieve the mline index from it.
  // #295 - Note that we cannot rely on the (implementation detail) fact that
  // Google chose to use the mline index as the mid value because this breaks
  // interoperability with other implementations.
  auto tr_impl =
      (rtc::RefCountedObject<
          webrtc::RtpTransceiverProxyWithInternal<webrtc::RtpTransceiver>>*)tr;
  absl::optional<std::size_t> mline_index = tr_impl->internal()->mline_index();
  if (!mline_index.has_value()) {
    return -1;
  }
  const std::size_t value = mline_index.value();
  RTC_CHECK(value >= 0);
  RTC_CHECK(value <= (long)INT_MAX);
  return static_cast<int>(value);
}

ErrorOr<Transceiver*> PeerConnection::GetOrCreateTransceiverForNewRemoteTrack(
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
    const int mline_index = ExtractMlineIndexFromRtpTransceiver(impl);
    RTC_DCHECK(mline_index >= 0);  // should be always associated here
    absl::optional<std::string> mid = impl->mid();
    RTC_CHECK(mid.has_value());  // should be always true here
    std::string name = mid.value();
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
      rtc::CritScope lock(&transceivers_mutex_);
      transceivers_.push_back(transceiver);
    }

    // Invoke the TransceiverAdded callback
    {
      std::lock_guard<std::mutex> lock(callbacks_mutex_);
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

void PeerConnection::SynchronizeTransceiversUnifiedPlan(bool remote) {
  // Get RTP transceivers sorted by address in memory
  auto rtp_transceivers = peer_->GetTransceivers();
  std::sort(
      rtp_transceivers.begin(), rtp_transceivers.end(),
      [](auto const& t1, auto const& t2) { return (t1.get() < t2.get()); });
  // Get transceiver wrappers sorted by RTP transceiver address in memory
  std::vector<RefPtr<Transceiver>> wrappers;
  {
    rtc::CritScope lock(&transceivers_mutex_);
    wrappers = transceivers_;
  }
  std::sort(wrappers.begin(), wrappers.end(),
            [](auto const& t1, auto const& t2) {
              return (t1->impl().get() < t2->impl().get());
            });
  // Match transceiver wrappers with their implementation, and create wrappers
  // for the ones without one yet.
  RTC_DCHECK_GE(rtp_transceivers.size(), wrappers.size());
  auto it_wrapper = wrappers.begin();
  RTC_LOG(LS_INFO) << "Synchronizing " << rtp_transceivers.size()
                   << " RTP transceivers with " << wrappers.size()
                   << " transceiver wrappers (remote = " << remote << ").";
  for (auto&& rtp_tr : rtp_transceivers) {
    const int mline_index = ExtractMlineIndexFromRtpTransceiver(rtp_tr);
    // Compare the current item on the sorted arrays of RTP transceiver and
    // transceiver wrappers; if the current items don't match, this means
    // there's a missing wrapper for an existing RTP transceiver, so create it.
    if ((it_wrapper == wrappers.end()) || ((*it_wrapper)->impl() != rtp_tr)) {
      std::string name = rtp_tr->mid().value_or(std::string{});
      RTC_LOG(LS_INFO) << "Creating new wrapper for RTP transceiver mid='"
                       << name.c_str() << "' (#" << mline_index << ")";
      std::vector<std::string> stream_ids =
          ExtractTransceiverStreamIDsFromReceiver(rtp_tr->receiver());
      ErrorOr<Transceiver*> err = CreateTransceiverUnifiedPlan(
          MediaKindFromRtc(rtp_tr->media_type()), mline_index, std::move(name),
          std::move(stream_ids), rtp_tr);
      if (!err.ok()) {
        RTC_LOG(LS_ERROR) << "Failed to create a Transceiver object to hold a "
                             "new RTP transceiver.";
        continue;
      }
      it_wrapper = wrappers.insert(it_wrapper, err.MoveValue());
    }
    RTC_DCHECK(it_wrapper != wrappers.end());
    // Ensure the Transceiver object is in sync with its RTP counterpart
    (*it_wrapper)->OnSessionDescUpdated(remote);
    // Check if newly associated
    if ((*it_wrapper)->GetMlineIndex() != mline_index) {
      RTC_DCHECK(mline_index >= 0);
      RTC_DCHECK(!remote);  // already created associated with remote
      (*it_wrapper)->OnAssociated(mline_index);
    }
    ++it_wrapper;
  }
}

ErrorOr<Transceiver*> PeerConnection::CreateTransceiverUnifiedPlan(
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
  {
    rtc::CritScope lock(&transceivers_mutex_);
    transceivers_.push_back(transceiver);
  }
  {
    std::lock_guard<std::mutex> lock(callbacks_mutex_);
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

std::string PeerConnection::ExtractTransceiverNameFromSender(
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
PeerConnection::ExtractTransceiverStreamIDsFromReceiver(
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

bool PeerConnection::ExtractTransceiverInfoFromReceiverPlanB(
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
  auto peer = new PeerConnection(std::move(global_factory));
  webrtc::PeerConnectionDependencies dependencies(peer);
  rtc::scoped_refptr<webrtc::PeerConnectionInterface> impl =
      pc_factory->CreatePeerConnection(rtc_config, std::move(dependencies));
  if (impl.get() == nullptr) {
    return Error(Result::kUnknownError);
  }
  peer->peer_ = std::move(impl);
  return RefPtr<PeerConnection>(peer);
}

void PeerConnection::GetStats(webrtc::RTCStatsCollectorCallback* callback) {
  peer_->GetStats(callback);
}

void PeerConnection::InvokeRenegotiationNeeded() {
  OnRenegotiationNeeded();
}

PeerConnection::PeerConnection(RefPtr<GlobalFactory> global_factory)
    : TrackedObject(std::move(global_factory), ObjectType::kPeerConnection),
      audio_mixer_(global_factory_->audio_mixer()) {}

}  // namespace WebRTC
}  // namespace MixedReality
}  // namespace Microsoft
