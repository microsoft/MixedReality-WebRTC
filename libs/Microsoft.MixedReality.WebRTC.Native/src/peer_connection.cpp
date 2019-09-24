// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

// This is a precompiled header, it must be on its own, followed by a blank
// line, to prevent clang-format from reordering it with other headers.
#include "pch.h"

#include "api.h"
#include "audio_frame_observer.h"
#include "data_channel.h"
#include "peer_connection.h"
#include "video_frame_observer.h"

#include <functional>

namespace {

/// Simple observer utility delegating to a given callback on success.
struct SessionDescObserver : public webrtc::SetSessionDescriptionObserver {
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
  void OnSetRemoteDescriptionComplete(webrtc::RTCError error) override {}
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

}  // namespace

namespace Microsoft::MixedReality::WebRTC {

rtc::scoped_refptr<PeerConnection> PeerConnection::create(
    webrtc::PeerConnectionFactoryInterface& factory,
    const webrtc::PeerConnectionInterface::RTCConfiguration& config,
    mrsPeerConnectionInteropHandle interop_handle) {
  // Create the PeerConnection object
  rtc::scoped_refptr<PeerConnection> peer =
      new rtc::RefCountedObject<PeerConnection>(interop_handle);

  // Create the underlying implementation
  webrtc::PeerConnectionDependencies dependencies(peer);
  rtc::scoped_refptr<webrtc::PeerConnectionInterface> impl =
      factory.CreatePeerConnection(config, std::move(dependencies));
  if (impl.get() == nullptr) {
    return {};
  }

  // Acquire ownership of the underlying implementation
  peer->peer_ = std::move(impl);
  peer->local_video_observer_.reset(new VideoFrameObserver());
  peer->remote_video_observer_.reset(new VideoFrameObserver());
  peer->local_audio_observer_.reset(new AudioFrameObserver());
  peer->remote_audio_observer_.reset(new AudioFrameObserver());
  return peer;
}

PeerConnection::PeerConnection(mrsPeerConnectionInteropHandle interop_handle)
    : interop_handle_(interop_handle) {}

PeerConnection::~PeerConnection() noexcept {
  // Close the connection
  peer_->Close();

  // Ensure that observers (sinks) are removed, otherwise the media pipelines
  // will continue to try to feed them with data after they're destroyed
  RemoveLocalVideoTrack();
  RemoveLocalAudioTrack();
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

  // Beware, RemoveDataChannel() erases from data_channels_
  while (!data_channels_.empty()) {
    RemoveDataChannel(*data_channels_.back());
  }
}

bool PeerConnection::AddLocalVideoTrack(
    rtc::scoped_refptr<webrtc::VideoTrackInterface> video_track) noexcept {
  if (local_video_track_) {
    return false;
  }
  auto result = peer_->AddTrack(video_track, {kAudioVideoStreamId});
  if (result.ok()) {
    if (local_video_observer_) {
      rtc::VideoSinkWants sink_settings{};
      sink_settings.rotation_applied = true;
      video_track->AddOrUpdateSink(local_video_observer_.get(), sink_settings);
    }
    local_video_sender_ = result.value();
    local_video_track_ = std::move(video_track);
    return true;
  }
  return false;
}

void PeerConnection::RemoveLocalVideoTrack() noexcept {
  if (!local_video_track_)
    return;
  if (auto* sink = local_video_observer_.get()) {
    local_video_track_->RemoveSink(sink);
  }
  peer_->RemoveTrack(local_video_sender_);
  local_video_track_ = nullptr;
  local_video_sender_ = nullptr;
}

void PeerConnection::SetLocalVideoTrackEnabled(bool enabled) noexcept {
  if (local_video_track_) {
    local_video_track_->set_enabled(enabled);
  }
}

bool PeerConnection::IsLocalVideoTrackEnabled() const noexcept {
  if (local_video_track_) {
    return local_video_track_->enabled();
  }
  return false;
}

void PeerConnection::SetLocalAudioTrackEnabled(bool enabled) noexcept {
  if (local_audio_track_) {
    local_audio_track_->set_enabled(enabled);
  }
}

bool PeerConnection::IsLocalAudioTrackEnabled() const noexcept {
  if (local_audio_track_) {
    return local_audio_track_->enabled();
  }
  return false;
}

bool PeerConnection::AddLocalAudioTrack(
    rtc::scoped_refptr<webrtc::AudioTrackInterface> audio_track) noexcept {
  if (local_audio_track_) {
    return false;
  }
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
  return false;
}

void PeerConnection::RemoveLocalAudioTrack() noexcept {
  if (!local_audio_track_)
    return;
  if (auto* sink = local_audio_observer_.get()) {
    local_audio_track_->RemoveSink(sink);
  }
  peer_->RemoveTrack(local_audio_sender_);
  local_audio_track_ = nullptr;
  local_audio_sender_ = nullptr;
}

webrtc::RTCErrorOr<std::shared_ptr<DataChannel>> PeerConnection::AddDataChannel(
    int id,
    std::string_view label,
    bool ordered,
    bool reliable,
    mrsDataChannelInteropHandle dataChannelInteropHandle) noexcept {
  if (!sctp_negotiated_) {
    // Don't try to create a data channel without SCTP negotiation, it will get
    // stuck in the kConnecting state forever.
    return webrtc::RTCError(webrtc::RTCErrorType::INVALID_STATE,
                            "SCTP not negotiated");
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
    return webrtc::RTCError(webrtc::RTCErrorType::INVALID_RANGE);
  }
  std::string labelString{label};
  if (rtc::scoped_refptr<webrtc::DataChannelInterface> impl =
          peer_->CreateDataChannel(labelString, &config)) {
    // Create the native object
    auto data_channel = std::make_shared<DataChannel>(this, std::move(impl),
                                                      dataChannelInteropHandle);
    data_channels_.push_back(data_channel);
    if (!labelString.empty()) {
      data_channel_from_label_.emplace(std::move(labelString), data_channel);
    }
    if (config.id >= 0) {
      data_channel_from_id_.try_emplace(config.id, data_channel);
    }

    // For in-band channels, the creating side (here) doesn't receive an
    // OnDataChannel() message, so invoke the DataChannelAdded event right now.
    if (!data_channel->impl()->negotiated()) {
      OnDataChannelAdded(*data_channel.get());
    }

    return data_channel;
  }
  return webrtc::RTCError(webrtc::RTCErrorType::INTERNAL_ERROR);
}

void PeerConnection::RemoveDataChannel(
    const DataChannel& data_channel) noexcept {
  // The channel must be owned by this PeerConnection, so must be known already
  auto const it =
      std::find_if(data_channels_.begin(), data_channels_.end(),
                   [&data_channel](const std::shared_ptr<DataChannel>& other) {
                     return other.get() == &data_channel;
                   });
  RTC_CHECK(it != data_channels_.end());

  // Clean-up interop maps
  {
    auto it_id = data_channel_from_id_.find(data_channel.id());
    if (it_id != data_channel_from_id_.end()) {
      data_channel_from_id_.erase(it_id);
    }
    const str label = data_channel.label();
    if (!label.empty()) {
      auto it_label = data_channel_from_label_.find(label);
      if (it_label != data_channel_from_label_.end()) {
        data_channel_from_label_.erase(it_label);
      }
    }
  }

  // Close the data channel
  webrtc::DataChannelInterface* const impl = data_channel.impl();
  impl->UnregisterObserver();  // force here, as ~DataChannel() didn't run yet
  impl->Close();

  // Invoke the DataChannelRemoved callback on the wrapper if any
  if (auto interop_handle = data_channel.GetInteropHandle()) {
    auto lock = std::lock_guard{data_channel_removed_callback_mutex_};
    auto removed_cb = data_channel_removed_callback_;
    if (removed_cb) {
      DataChannelHandle data_native_handle = (void*)&data_channel;
      removed_cb(interop_handle, data_native_handle);
    }
  }

  // Remove the channel last, to be sure a reference is kept.
  // This should not be a problem in theory because the caller should have a
  // reference to it, but this is safer.
  (*it)->OnRemovedFromPeerConnection();  // clear back pointer
  data_channels_.erase(it);
}

void PeerConnection::OnDataChannelAdded(
    const DataChannel& data_channel) noexcept {
  // The channel must be owned by this PeerConnection, so must be known already.
  // It was added in AddDataChannel() when the DataChannel object was created.
  RTC_CHECK(
      std::find_if(data_channels_.begin(), data_channels_.end(),
                   [&data_channel](const std::shared_ptr<DataChannel>& other) {
                     return other.get() == &data_channel;
                   }) != data_channels_.end());

  // Invoke the DataChannelAdded callback on the wrapper if any
  if (auto interop_handle = data_channel.GetInteropHandle()) {
    auto lock = std::lock_guard{data_channel_added_callback_mutex_};
    auto added_cb = data_channel_added_callback_;
    if (added_cb) {
      DataChannelHandle data_native_handle = (void*)&data_channel;
      added_cb(interop_handle, data_native_handle);
    }
  }
}

bool PeerConnection::AddIceCandidate(const char* sdp_mid,
                                     const int sdp_mline_index,
                                     const char* candidate) noexcept {
  if (!peer_)
    return false;
  webrtc::SdpParseError error;
  std::unique_ptr<webrtc::IceCandidateInterface> ice_candidate(
      webrtc::CreateIceCandidate(sdp_mid, sdp_mline_index, candidate, &error));
  if (!ice_candidate)
    return false;
  if (!peer_->AddIceCandidate(ice_candidate.get()))
    return false;
  return true;
}

bool PeerConnection::CreateOffer() noexcept {
  if (!peer_)
    return false;
  webrtc::PeerConnectionInterface::RTCOfferAnswerOptions options;
  /*if (mandatory_receive_)*/ {  //< TODO - This is legacy, should use
                                 // transceivers
    options.offer_to_receive_audio = true;
    options.offer_to_receive_video = true;
  }
  if (data_channels_.empty()) {
    sctp_negotiated_ = false;
  }
  peer_->CreateOffer(this, options);
  return true;
}

bool PeerConnection::CreateAnswer() noexcept {
  if (!peer_)
    return false;
  webrtc::PeerConnectionInterface::RTCOfferAnswerOptions options;
  /*if (mandatory_receive_)*/ {  //< TODO - This is legacy, should use
                                 // transceivers
    options.offer_to_receive_audio = true;
    options.offer_to_receive_video = true;
  }
  peer_->CreateAnswer(this, options);
  return true;
}

bool PeerConnection::SetRemoteDescription(const char* type,
                                          const char* sdp) noexcept {
  if (!peer_)
    return false;
  if (data_channels_.empty()) {
    sctp_negotiated_ = false;
  }
  std::string sdp_type_str(type);
  auto sdp_type = webrtc::SdpTypeFromString(sdp_type_str);
  if (!sdp_type.has_value())
    return false;
  std::string remote_desc(sdp);
  webrtc::SdpParseError error;
  std::unique_ptr<webrtc::SessionDescriptionInterface> session_description(
      webrtc::CreateSessionDescription(sdp_type.value(), remote_desc, &error));
  if (!session_description)
    return false;
  rtc::scoped_refptr<webrtc::SetRemoteDescriptionObserverInterface> observer =
      new rtc::RefCountedObject<SetRemoteSessionDescObserver>();
  peer_->SetRemoteDescription(std::move(session_description),
                              std::move(observer));
  return true;
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
        auto lock = std::lock_guard{connected_callback_mutex_};
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
  }
}

void PeerConnection::OnAddStream(
    rtc::scoped_refptr<webrtc::MediaStreamInterface> stream) noexcept {
  RTC_LOG(LS_INFO) << "Added stream #" << stream->id() << " with "
                   << stream->GetAudioTracks().size() << " audio tracks and "
                   << stream->GetVideoTracks().size() << " video tracks.";
  remote_streams_.push_back(stream);
}

void PeerConnection::OnRemoveStream(
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

void PeerConnection::OnDataChannel(
    rtc::scoped_refptr<webrtc::DataChannelInterface> impl) noexcept {
  // If receiving a new data channel, then obviously SCTP has been negotiated so
  // it is safe to create other ones.
  sctp_negotiated_ = true;

  // Read the data channel config
  std::string label = impl->label();
  mrsDataChannelConfig config;
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
  data_channels_.push_back(data_channel);
  if (!label.empty()) {
    // Move |label| into the map to avoid copy
    auto it = data_channel_from_label_.emplace(std::move(label), data_channel);
    // Update the address to the moved item in case it changed
    config.label = it->first.c_str();
  }
  if (data_channel->id() >= 0) {
    data_channel_from_id_.try_emplace(data_channel->id(), data_channel);
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
      auto lock = std::lock_guard{data_channel_added_callback_mutex_};
      auto added_cb = data_channel_added_callback_;
      if (added_cb) {
        const DataChannelHandle data_native_handle = data_channel.get();
        added_cb(data_channel_interop_handle, data_native_handle);
      }
    }
  }
}

void PeerConnection::OnRenegotiationNeeded() noexcept {
  auto lock = std::lock_guard{renegotiation_needed_callback_mutex_};
  auto cb = renegotiation_needed_callback_;
  if (cb) {
    cb();
  }
}

void PeerConnection::OnIceConnectionChange(
    webrtc::PeerConnectionInterface::IceConnectionState new_state) noexcept {
  auto lock = std::lock_guard{ice_state_changed_callback_mutex_};
  auto cb = ice_state_changed_callback_;
  if (cb) {
    cb(IceStateFromImpl(new_state));
  }
}

void PeerConnection::OnIceCandidate(
    const webrtc::IceCandidateInterface* candidate) noexcept {
  auto lock = std::lock_guard{ice_candidate_ready_to_send_callback_mutex_};
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

void PeerConnection::OnAddTrack(
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
    auto lock = std::lock_guard{track_added_callback_mutex_};
    auto cb = track_added_callback_;
    if (cb) {
      cb(trackKind);
    }
  }
}

void PeerConnection::OnRemoveTrack(
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
    auto lock = std::lock_guard{track_removed_callback_mutex_};
    auto cb = track_removed_callback_;
    if (cb) {
      cb(trackKind);
    }
  }
}

void PeerConnection::OnSuccess(
    webrtc::SessionDescriptionInterface* desc) noexcept {
  auto lock = std::lock_guard{local_sdp_ready_to_send_callback_mutex_};
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

}  // namespace Microsoft::MixedReality::WebRTC
