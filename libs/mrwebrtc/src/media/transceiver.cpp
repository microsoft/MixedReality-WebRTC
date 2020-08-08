// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include "interop/global_factory.h"
#include "peer_connection.h"
#include "transceiver.h"
#include "utils.h"

namespace Microsoft {
namespace MixedReality {
namespace WebRTC {

struct Transceiver::PlanBEmulation {
  /// RTP sender, indicating that the transceiver wants to send and/or is
  /// already sending.
  rtc::scoped_refptr<webrtc::RtpSenderInterface> rtp_sender_;

  /// RTP receiver, indicating that the transceiver is receiving.
  rtc::scoped_refptr<webrtc::RtpReceiverInterface> rtp_receiver_;

  /// Local media stream track sending through the RTP sender.
  /// This is kept separated from the RTP sender because it can be set and
  /// cleared independently of it, and when set it should not force the creation
  /// of an RTP sender to be consistent with the hot-swap of tracks on
  /// transceivers not changing any transceiver direction nor generating a
  /// renegotiation in Unified Plan.
  rtc::scoped_refptr<webrtc::MediaStreamTrackInterface> sender_track_;
};

RefPtr<Transceiver> Transceiver::CreateForPlanB(
    RefPtr<GlobalFactory> global_factory,
    MediaKind kind,
    PeerConnection& owner,
    int mline_index,
    std::string name,
    std::vector<std::string> stream_ids,
    Direction desired_direction) noexcept {
  return new Transceiver(std::move(global_factory), kind, owner, mline_index,
                         std::move(name), std::move(stream_ids),
                         desired_direction);
}

RefPtr<Transceiver> Transceiver::CreateForUnifiedPlan(
    RefPtr<GlobalFactory> global_factory,
    MediaKind kind,
    PeerConnection& owner,
    int mline_index,
    std::string name,
    std::vector<std::string> stream_ids,
    rtc::scoped_refptr<webrtc::RtpTransceiverInterface> transceiver,
    Direction desired_direction) noexcept {
  return new Transceiver(std::move(global_factory), kind, owner, mline_index,
                         std::move(name), std::move(stream_ids),
                         std::move(transceiver), desired_direction);
}

Transceiver::Transceiver(RefPtr<GlobalFactory> global_factory,
                         MediaKind kind,
                         PeerConnection& owner,
                         int mline_index,
                         std::string name,
                         std::vector<std::string> stream_ids,
                         Direction desired_direction) noexcept
    : TrackedObject(std::move(global_factory),
                    kind == MediaKind::kAudio ? ObjectType::kAudioTransceiver
                                              : ObjectType::kVideoTransceiver),
      owner_(&owner),
      kind_(kind),
      mline_index_(mline_index),
      stream_ids_(std::move(stream_ids)),
      desired_direction_(desired_direction),
      plan_b_(new PlanBEmulation) {
  RTC_CHECK(owner_);
  name_ = std::move(name);
  //< TODO
  // RTC_CHECK(owner.sdp_semantic == webrtc::SdpSemantics::kPlanB);
}

Transceiver::Transceiver(
    RefPtr<GlobalFactory> global_factory,
    MediaKind kind,
    PeerConnection& owner,
    int mline_index,
    std::string name,
    std::vector<std::string> stream_ids,
    rtc::scoped_refptr<webrtc::RtpTransceiverInterface> transceiver,
    Direction desired_direction) noexcept
    : TrackedObject(std::move(global_factory),
                    kind == MediaKind::kAudio ? ObjectType::kAudioTransceiver
                                              : ObjectType::kVideoTransceiver),
      owner_(&owner),
      kind_(kind),
      mline_index_(mline_index),
      stream_ids_(std::move(stream_ids)),
      desired_direction_(desired_direction),
      transceiver_(std::move(transceiver)) {
  RTC_CHECK(owner_);
  RTC_DCHECK(transceiver_);
  RTC_DCHECK(
      ((transceiver_->media_type() == cricket::MediaType::MEDIA_TYPE_AUDIO) &&
       (kind == MediaKind::kAudio)) ||
      ((transceiver_->media_type() == cricket::MediaType::MEDIA_TYPE_VIDEO) &&
       (kind == MediaKind::kVideo)));
  name_ = std::move(name);
  //< TODO
  // RTC_CHECK(owner.sdp_semantic == webrtc::SdpSemantics::kUnifiedPlan);
}

Transceiver::~Transceiver() {
  // RTC_CHECK(!owner_);

  // Keep the tracks alive for now. This prevents them from being destroyed when
  // detaching them below, which would invoke their destructor while the
  // transceiver is in an inconsistent state (in the middle of being destroyed),
  // and would trigger some assertion in Debug build.
  RefPtr<MediaTrack> local_track = local_track_;
  RefPtr<MediaTrack> remote_track = remote_track_;

  // Detach the local track from this transceiver. This will clear the
  // |local_track_| member.
  if (local_track_) {
    rtc::scoped_refptr<webrtc::RtpSenderInterface> rtp_sender;
    if (IsUnifiedPlan()) {
      rtp_sender = transceiver_->sender();
    }
    if (GetMediaKind() == MediaKind::kAudio) {
      auto const track = (LocalAudioTrack*)local_track_.get();
      track->OnRemovedFromPeerConnection(*owner_, this, rtp_sender);
    } else {
      RTC_DCHECK(GetMediaKind() == MediaKind::kVideo);
      auto const track = (LocalVideoTrack*)local_track_.get();
      track->OnRemovedFromPeerConnection(*owner_, this, rtp_sender);
    }
  }
  RTC_DCHECK(!local_track_);

  // Detach the remote track too. This transceiver is its sole owner, so this
  // will destroy it.
  if (remote_track_) {
    if (GetMediaKind() == MediaKind::kAudio) {
      auto const track = (RemoteAudioTrack*)remote_track_.get();
      track->OnTrackRemoved(*owner_);
    } else {
      RTC_DCHECK(GetMediaKind() == MediaKind::kVideo);
      auto const track = (RemoteVideoTrack*)remote_track_.get();
      track->OnTrackRemoved(*owner_);
    }
  }
  RTC_DCHECK(!remote_track_);

  // Be sure to clean-up WebRTC objects before unregistering ourself, which
  // could lead to the GlobalFactory being destroyed and the WebRTC threads
  // stopped.
  transceiver_ = nullptr;
  plan_b_ = nullptr;
}

Result Transceiver::SetDirection(Direction new_direction) noexcept {
  if (new_direction == desired_direction_) {
    return Result::kSuccess;
  }
  desired_direction_ = new_direction;
  if (transceiver_) {  // Unified Plan
    // Change the RTP transceiver direction; this will trigger a
    // |RenegotiationNeeded| event.
    transceiver_->SetDirection(ToRtp(new_direction));
  } else {  // Plan B
    RTC_DCHECK(plan_b_);
    // Force a manual |RenegotiationNeeded| event for parity with Unified Plan.
    owner_->InvokeRenegotiationNeeded();
  }
  FireStateUpdatedEvent(mrsTransceiverStateUpdatedReason::kSetDirection);
  return Result::kSuccess;
}

bool Transceiver::HasSender(webrtc::RtpSenderInterface* sender) const {
  if (transceiver_) {
    return (transceiver_->sender() == sender);
  } else {
    return (plan_b_->rtp_sender_ == sender);
  }
}

bool Transceiver::HasReceiver(webrtc::RtpReceiverInterface* receiver) const {
  if (transceiver_) {
    return (transceiver_->receiver() == receiver);
  } else {
    return (plan_b_->rtp_receiver_ == receiver);
  }
}

Result Transceiver::SetLocalTrackImpl(RefPtr<MediaTrack> local_track) noexcept {
  if (local_track_ == local_track) {
    return Result::kSuccess;
  }
  Result result = Result::kSuccess;
  webrtc::MediaStreamTrackInterface* const new_track =
      local_track ? local_track->GetMediaImpl() : nullptr;
  rtc::scoped_refptr<webrtc::RtpSenderInterface> rtp_sender;
  if (IsUnifiedPlan()) {
    rtp_sender = transceiver_->sender();
    if (!rtp_sender->SetTrack(new_track)) {
      result = Result::kInvalidOperation;
    }
  } else {
    RTC_DCHECK(IsPlanB());
    SetTrackPlanB(new_track);
  }
  if (result != Result::kSuccess) {
    if (local_track) {
      RTC_LOG(LS_ERROR) << "Failed to set local audio track "
                        << local_track->GetName() << " of audio transceiver "
                        << GetName() << ".";
    } else {
      RTC_LOG(LS_ERROR)
          << "Failed to clear local audio track from audio transceiver "
          << GetName() << ".";
    }
    return result;
  }
  if (local_track_) {
    // Detach old local track
    // Keep a pointer to the track because |local_track_| gets NULL'd. No need
    // to keep a reference, because |owner_| has one.
    if (GetMediaKind() == MediaKind::kAudio) {
      auto const track = (LocalAudioTrack*)local_track_.get();
      track->OnRemovedFromPeerConnection(*owner_, this, rtp_sender);
    } else {
      RTC_DCHECK(GetMediaKind() == MediaKind::kVideo);
      auto const track = (LocalVideoTrack*)local_track_.get();
      track->OnRemovedFromPeerConnection(*owner_, this, rtp_sender);
    }
  }
  local_track_ = std::move(local_track);
  if (local_track_) {
    // Attach new local track
    if (GetMediaKind() == MediaKind::kAudio) {
      auto const track = (LocalAudioTrack*)local_track_.get();
      track->OnAddedToPeerConnection(*owner_, this, rtp_sender);
    } else {
      RTC_DCHECK(GetMediaKind() == MediaKind::kVideo);
      auto const track = (LocalVideoTrack*)local_track_.get();
      track->OnAddedToPeerConnection(*owner_, this, rtp_sender);
    }
  }
  return Result::kSuccess;
}

void Transceiver::OnLocalTrackAdded(RefPtr<LocalAudioTrack> track) {
  RTC_DCHECK(GetMediaKind() == MediaKind::kAudio);
  // this may be called multiple times with the same track
  if (local_track_ != track) {
    RTC_DCHECK(!local_track_);
    local_track_ = std::move(track);
  }
}

void Transceiver::OnLocalTrackAdded(RefPtr<LocalVideoTrack> track) {
  RTC_DCHECK(GetMediaKind() == MediaKind::kVideo);
  // this may be called multiple times with the same track
  if (local_track_ != track) {
    RTC_DCHECK(!local_track_);
    local_track_ = std::move(track);
  }
}

void Transceiver::OnRemoteTrackAdded(RefPtr<RemoteAudioTrack> track) {
  RTC_DCHECK(GetMediaKind() == MediaKind::kAudio);
  // this may be called multiple times with the same track
  if (remote_track_ != track) {
    RTC_DCHECK(!remote_track_);
    remote_track_ = std::move(track);
  }
}

void Transceiver::OnRemoteTrackAdded(RefPtr<RemoteVideoTrack> track) {
  RTC_DCHECK(GetMediaKind() == MediaKind::kVideo);
  // this may be called multiple times with the same track
  if (remote_track_ != track) {
    RTC_DCHECK(!remote_track_);
    remote_track_ = std::move(track);
  }
}

void Transceiver::OnLocalTrackRemoved(LocalAudioTrack* track) {
  RTC_DCHECK(GetMediaKind() == MediaKind::kAudio);
  RTC_DCHECK_EQ(track, local_track_.get());
  local_track_ = nullptr;
}

void Transceiver::OnLocalTrackRemoved(LocalVideoTrack* track) {
  RTC_DCHECK(GetMediaKind() == MediaKind::kVideo);
  RTC_DCHECK_EQ(track, local_track_.get());
  local_track_ = nullptr;
}

void Transceiver::OnRemoteTrackRemoved(RemoteAudioTrack* track) {
  RTC_DCHECK(GetMediaKind() == MediaKind::kAudio);
  RTC_DCHECK_EQ(track, remote_track_.get());
  remote_track_ = nullptr;
}

void Transceiver::OnRemoteTrackRemoved(RemoteVideoTrack* track) {
  RTC_DCHECK(GetMediaKind() == MediaKind::kVideo);
  RTC_DCHECK_EQ(track, remote_track_.get());
  remote_track_ = nullptr;
}

rtc::scoped_refptr<webrtc::RtpTransceiverInterface> Transceiver::impl() const {
  return transceiver_;
}

webrtc::RtpTransceiverDirection Transceiver::ToRtp(Direction direction) {
  using RtpDir = webrtc::RtpTransceiverDirection;
  static_assert((int)Direction::kSendRecv == (int)RtpDir::kSendRecv, "");
  static_assert((int)Direction::kSendOnly == (int)RtpDir::kSendOnly, "");
  static_assert((int)Direction::kRecvOnly == (int)RtpDir::kRecvOnly, "");
  static_assert((int)Direction::kInactive == (int)RtpDir::kInactive, "");
  return (RtpDir)direction;
}

Transceiver::Direction Transceiver::FromRtp(
    webrtc::RtpTransceiverDirection rtp_direction) {
  using RtpDir = webrtc::RtpTransceiverDirection;
  return (Direction)rtp_direction;
}

Transceiver::OptDirection Transceiver::FromRtp(
    absl::optional<webrtc::RtpTransceiverDirection> rtp_direction) {
  using RtpDir = webrtc::RtpTransceiverDirection;
  static_assert((int)OptDirection::kSendRecv == (int)RtpDir::kSendRecv, "");
  static_assert((int)OptDirection::kSendOnly == (int)RtpDir::kSendOnly, "");
  static_assert((int)OptDirection::kRecvOnly == (int)RtpDir::kRecvOnly, "");
  static_assert((int)OptDirection::kInactive == (int)RtpDir::kInactive, "");
  if (rtp_direction.has_value()) {
    return (OptDirection)FromRtp(rtp_direction.value());
  }
  return OptDirection::kNotSet;
}

Transceiver::Direction Transceiver::FromSendRecv(bool send, bool recv) {
  if (send) {
    return (recv ? Direction::kSendRecv : Direction::kSendOnly);
  } else {
    return (recv ? Direction::kRecvOnly : Direction::kInactive);
  }
}

Transceiver::OptDirection Transceiver::OptFromSendRecv(bool send, bool recv) {
  if (send) {
    return (recv ? OptDirection::kSendRecv : OptDirection::kSendOnly);
  } else {
    return (recv ? OptDirection::kRecvOnly : OptDirection::kInactive);
  }
}

std::vector<std::string> Transceiver::DecodeStreamIDs(
    const char* encoded_stream_ids) {
  if (IsStringNullOrEmpty(encoded_stream_ids)) {
    return {};
  }
  std::vector<std::string> ids;
  rtc::split(encoded_stream_ids, ';', &ids);
  return ids;
}

std::string Transceiver::EncodeStreamIDs(
    const std::vector<std::string>& stream_ids) {
  if (stream_ids.empty()) {
    return {};
  }
  return rtc::join(stream_ids, ';');
}

std::string Transceiver::BuildEncodedStreamIDForPlanB(int mline_index) const {
  std::vector<std::string> items;
  items.reserve(1 + stream_ids_.size());

  // First item is "mrsw#<mline_index>"
  {
    rtc::StringBuilder builder;
    builder << "mrsw#" << mline_index;
    items.push_back(builder.str());
  }

  // Other items are the stream IDs
  for (auto&& id : stream_ids_) {
    items.push_back(id);
  }

  return rtc::join(items, ';');
}

bool Transceiver::DecodedStreamIDForPlanB(
    const std::string& encoded_string,
    int& mline_index_out,
    std::string& name,
    std::vector<std::string>& stream_ids_out) {
  if (encoded_string.empty()) {
    mline_index_out = -1;
    stream_ids_out.clear();
    return false;
  }
  rtc::split(encoded_string, ';', &stream_ids_out);
  // Use encoded mline index as transceiver name
  name = std::move(stream_ids_out[0]);
  stream_ids_out.erase(stream_ids_out.begin());
  if ((name.size() < 6) || (strncmp(name.c_str(), "mrsw#", 5) != 0)) {
    RTC_LOG(LS_ERROR) << "RTP receiver stream ID does not start with the magic "
                         "prefix 'mrsw#' for automatic Plan B track pairing.";
    return false;
  }
  mline_index_out = (int)strtol(name.c_str() + 5, nullptr, 10);
  if (mline_index_out < 0) {
    RTC_LOG(LS_ERROR)
        << "Invalid RTP receiver stream ID " << name.c_str()
        << " does not resolve to a valid media line index (got "
        << mline_index_out
        << ", expected positive integer for automatic Plan B track pairing).";
    return false;
  }
  return true;
}

void Transceiver::SyncSenderPlanB(bool needed,
                                  webrtc::PeerConnectionInterface* peer,
                                  const char* media_kind,
                                  const char* stream_id) {
  RTC_DCHECK(plan_b_);
  if (needed && !plan_b_->rtp_sender_) {
    // Create a new RTP sender without a track, and add it to the peer
    // connection. This produces a send offer when calling |CreateOffer()| or
    // |CreateAnswer()|.
    plan_b_->rtp_sender_ = peer->CreateSender(media_kind, stream_id);
    if (plan_b_->sender_track_) {
      plan_b_->rtp_sender_->SetTrack(plan_b_->sender_track_);
    }
  } else if (!needed && plan_b_->rtp_sender_) {
    // Remove the RTP sender from the peer connection, and destroy it. This
    // prevents producing a send offer when calling |CreateOffer()| or
    // |CreateAnswer()|.
    peer->RemoveTrackNew(plan_b_->rtp_sender_);
    plan_b_->rtp_sender_ = nullptr;
  }
}

void Transceiver::SetReceiverPlanB(
    rtc::scoped_refptr<webrtc::RtpReceiverInterface> receiver) {
  RTC_DCHECK(plan_b_);
  plan_b_->rtp_receiver_ = std::move(receiver);
}

void Transceiver::SetTrackPlanB(webrtc::MediaStreamTrackInterface* new_track) {
  RTC_DCHECK(plan_b_);
  plan_b_->sender_track_ = new_track;
  if (plan_b_->rtp_sender_) {
    RTC_DCHECK(
        !new_track ||
        ((plan_b_->rtp_sender_->media_type() ==
          cricket::MediaType::MEDIA_TYPE_AUDIO) &&
         (new_track->kind() ==
          webrtc::MediaStreamTrackInterface::kAudioKind)) ||
        ((plan_b_->rtp_sender_->media_type() ==
          cricket::MediaType::MEDIA_TYPE_VIDEO) &&
         (new_track->kind() == webrtc::MediaStreamTrackInterface::kVideoKind)));
    plan_b_->rtp_sender_->SetTrack(new_track);
  }
}

void Transceiver::OnAssociated(int mline_index) {
  assert(mline_index >= 0);
  // Transceiver recycling is not used, to transceivers can only be associated
  // once, from their initial non-associated state.
  assert(mline_index_ < 0);
  mline_index_ = mline_index;
  std::lock_guard<std::mutex> lock(cb_mutex_);
  if (auto cb = associated_callback_) {
    cb(mline_index);
  }
}

void Transceiver::OnSessionDescUpdated(bool remote, bool forced) {
  // Parse state to check for changes
  bool changed = false;
  if (transceiver_) {  // Unified Plan
    // Check negotiated direction
    auto negotiated = transceiver_->current_direction();
    if (negotiated.has_value()) {
      auto newValue = FromRtp(negotiated);
      if (newValue != direction_) {
        direction_ = newValue;
        changed = true;
      }
    }

    // Check desired direction
    auto desired = transceiver_->direction();
    {
      auto newValue = FromRtp(desired);
      if (newValue != desired_direction_) {
        desired_direction_ = newValue;
        changed = true;
      }
    }
  } else {
    // Check negotiated direction
    bool has_sender = (plan_b_->rtp_sender_ != nullptr);
    bool has_receiver = (plan_b_->rtp_receiver_ != nullptr);
    Transceiver::OptDirection negotiated_dir =
        OptFromSendRecv(has_sender, has_receiver);
    if (negotiated_dir != direction_) {
      direction_ = negotiated_dir;
      changed = true;
    }

    // TODO - Check desired direction?
  }

  // Invoke interop callback if any
  if (changed || forced) {
    FireStateUpdatedEvent(remote
                              ? mrsTransceiverStateUpdatedReason::kRemoteDesc
                              : mrsTransceiverStateUpdatedReason::kLocalDesc);
  }
}

void Transceiver::FireStateUpdatedEvent(
    mrsTransceiverStateUpdatedReason reason) {
  std::lock_guard<std::mutex> lock(cb_mutex_);
  if (auto cb = state_updated_callback_) {
    cb(reason, direction_, desired_direction_);
  }
}

}  // namespace WebRTC
}  // namespace MixedReality
}  // namespace Microsoft
