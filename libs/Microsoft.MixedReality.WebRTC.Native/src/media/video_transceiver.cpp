// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include "interop/global_factory.h"
#include "video_transceiver.h"

namespace Microsoft::MixedReality::WebRTC {

VideoTransceiver::VideoTransceiver(
    RefPtr<GlobalFactory> global_factory,
    PeerConnection& owner,
    int mline_index,
    std::string name,
    const VideoTransceiverInitConfig& config) noexcept
    : Transceiver(std::move(global_factory),
                  MediaKind::kVideo,
                  owner,
                  config.desired_direction),
      mline_index_(mline_index),
      name_(std::move(name)),
      interop_handle_(config.transceiver_interop_handle) {}

VideoTransceiver::VideoTransceiver(
    RefPtr<GlobalFactory> global_factory,
    PeerConnection& owner,
    int mline_index,
    std::string name,
    rtc::scoped_refptr<webrtc::RtpTransceiverInterface> transceiver,
    const VideoTransceiverInitConfig& config) noexcept
    : Transceiver(std::move(global_factory),
                  MediaKind::kVideo,
                  owner,
                  transceiver,
                  config.desired_direction),
      mline_index_(mline_index),
      name_(std::move(name)),
      interop_handle_(config.transceiver_interop_handle) {}

VideoTransceiver::~VideoTransceiver() {
  // Be sure to clean-up WebRTC objects before unregistering ourself, which
  // could lead to the GlobalFactory being destroyed and the WebRTC threads
  // stopped.
  transceiver_ = nullptr;
}

Result VideoTransceiver::SetDirection(Direction new_direction) noexcept {
  if (transceiver_) {  // Unified Plan
    if (new_direction == desired_direction_) {
      return Result::kSuccess;
    }
    transceiver_->SetDirection(ToRtp(new_direction));
  } else {  // Plan B
    // nothing to do
  }
  desired_direction_ = new_direction;
  FireStateUpdatedEvent(mrsTransceiverStateUpdatedReason::kSetDirection);
  return Result::kSuccess;
}

Result VideoTransceiver::SetLocalTrack(
    RefPtr<LocalVideoTrack> local_track) noexcept {
  if (local_track_ == local_track) {
    return Result::kSuccess;
  }
  Result result = Result::kSuccess;
  webrtc::MediaStreamTrackInterface* const new_track =
      local_track ? local_track->impl() : nullptr;
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
      RTC_LOG(LS_ERROR) << "Failed to set local video track "
                        << local_track->GetName() << " of video transceiver "
                        << GetName() << ".";
    } else {
      RTC_LOG(LS_ERROR)
          << "Failed to clear local video track from video transceiver "
          << GetName() << ".";
    }
    return result;
  }
  if (local_track_) {
    // Detach old local track
    // Keep a pointer to the track because |local_track_| gets NULL'd. No need
    // to keep a reference, because |owner_| has one.
    LocalVideoTrack* const track = local_track_.get();
    track->OnRemovedFromPeerConnection(*owner_, this, rtp_sender);
    owner_->OnLocalTrackRemovedFromVideoTransceiver(*this, *track);
  }
  local_track_ = std::move(local_track);
  if (local_track_) {
    // Attach new local track
    local_track_->OnAddedToPeerConnection(*owner_, this, rtp_sender);
    owner_->OnLocalTrackAddedToVideoTransceiver(*this, *local_track_);
  }
  return Result::kSuccess;
}

void VideoTransceiver::OnLocalTrackAdded(RefPtr<LocalVideoTrack> track) {
  // this may be called multiple times with the same track
  RTC_DCHECK(!local_track_ || (local_track_ == track));
  local_track_ = std::move(track);
}

void VideoTransceiver::OnRemoteTrackAdded(RefPtr<RemoteVideoTrack> track) {
  // this may be called multiple times with the same track
  RTC_DCHECK(!remote_track_ || (remote_track_ == track));
  remote_track_ = std::move(track);
}

void VideoTransceiver::OnLocalTrackRemoved(LocalVideoTrack* track) {
  RTC_DCHECK_EQ(track, local_track_.get());
  local_track_ = nullptr;
}

void VideoTransceiver::OnRemoteTrackRemoved(RemoteVideoTrack* track) {
  RTC_DCHECK_EQ(track, remote_track_.get());
  remote_track_ = nullptr;
}

}  // namespace Microsoft::MixedReality::WebRTC
