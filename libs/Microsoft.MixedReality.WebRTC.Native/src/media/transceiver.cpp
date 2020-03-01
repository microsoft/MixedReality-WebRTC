// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include "interop/global_factory.h"
#include "peer_connection.h"
#include "transceiver.h"
#include "utils.h"

namespace Microsoft::MixedReality::WebRTC {

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

Transceiver::Transceiver(RefPtr<GlobalFactory> global_factory,
                         MediaKind kind,
                         PeerConnection& owner,
                         Direction desired_direction) noexcept
    : TrackedObject(std::move(global_factory),
                    kind == MediaKind::kAudio ? ObjectType::kAudioTransceiver
                                              : ObjectType::kVideoTransceiver),
      owner_(&owner),
      kind_(kind),
      desired_direction_(desired_direction),
      plan_b_(new PlanBEmulation) {
  RTC_CHECK(owner_);
  //< TODO
  // RTC_CHECK(owner.sdp_semantic == webrtc::SdpSemantics::kPlanB);
}

Transceiver::Transceiver(
    RefPtr<GlobalFactory> global_factory,
    MediaKind kind,
    PeerConnection& owner,
    rtc::scoped_refptr<webrtc::RtpTransceiverInterface> transceiver,
    Direction desired_direction) noexcept
    : TrackedObject(std::move(global_factory),
                    kind == MediaKind::kAudio ? ObjectType::kAudioTransceiver
                                              : ObjectType::kVideoTransceiver),
      owner_(&owner),
      kind_(kind),
      desired_direction_(desired_direction),
      transceiver_(std::move(transceiver)) {
  RTC_CHECK(owner_);
  //< TODO
  // RTC_CHECK(owner.sdp_semantic == webrtc::SdpSemantics::kUnifiedPlan);
}

Transceiver::~Transceiver() {
  // RTC_CHECK(!owner_);
}

std::string Transceiver::GetName() const {
  return "TODO";
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
  static_assert((int)Direction::kSendRecv == (int)RtpDir::kSendRecv, "");
  static_assert((int)Direction::kSendOnly == (int)RtpDir::kSendOnly, "");
  static_assert((int)Direction::kRecvOnly == (int)RtpDir::kRecvOnly, "");
  static_assert((int)Direction::kInactive == (int)RtpDir::kInactive, "");
  return (Direction)rtp_direction;
}

Transceiver::OptDirection Transceiver::FromRtp(
    std::optional<webrtc::RtpTransceiverDirection> rtp_direction) {
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
  auto lock = std::scoped_lock{cb_mutex_};
  if (auto cb = state_updated_callback_) {
    cb(reason, direction_, desired_direction_);
  }
}

}  // namespace Microsoft::MixedReality::WebRTC
