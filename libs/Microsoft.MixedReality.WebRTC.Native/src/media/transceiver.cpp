// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include "interop/global_factory.h"
#include "peer_connection.h"
#include "transceiver.h"
#include "utils.h"

namespace Microsoft::MixedReality::WebRTC {

Transceiver::Transceiver(RefPtr<GlobalFactory> global_factory,
                         MediaKind kind,
                         PeerConnection& owner) noexcept
    : TrackedObject(std::move(global_factory),
                    kind == MediaKind::kAudio ? ObjectType::kAudioTransceiver
                                              : ObjectType::kVideoTransceiver),
      owner_(&owner),
      kind_(kind) {
  RTC_CHECK(owner_);
  //< TODO
  // RTC_CHECK(owner.sdp_semantic == webrtc::SdpSemantics::kPlanB);
}

Transceiver::Transceiver(
    RefPtr<GlobalFactory> global_factory,
    MediaKind kind,
    PeerConnection& owner,
    rtc::scoped_refptr<webrtc::RtpTransceiverInterface> transceiver) noexcept
    : TrackedObject(std::move(global_factory),
                    kind == MediaKind::kAudio ? ObjectType::kAudioTransceiver
                                              : ObjectType::kVideoTransceiver),
      owner_(&owner),
      kind_(kind),
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

void Transceiver::OnSessionDescUpdated(bool remote) {
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

    // Check desired direciton
    auto desired = transceiver_->direction();
    {
      auto newValue = FromRtp(desired);
      if (newValue != desired_direction_) {
        desired_direction_ = newValue;
        changed = true;
      }
    }
  } else {
    assert(false);  // not called in Plan B for now...
  }

  // Invoke interop callback if any
  if (changed) {
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
