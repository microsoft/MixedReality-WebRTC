// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include "interop/global_factory.h"
#include "local_video_track.h"
#include "peer_connection.h"

namespace Microsoft {
namespace MixedReality {
namespace WebRTC {

LocalVideoTrack::LocalVideoTrack(
    RefPtr<GlobalFactory> global_factory,
    rtc::scoped_refptr<webrtc::VideoTrackInterface> track) noexcept
    : MediaTrack(std::move(global_factory), ObjectType::kLocalVideoTrack),
      track_(std::move(track)) {
  RTC_CHECK(track_);
  name_ = track_->id();
  kind_ = mrsTrackKind::kVideoTrack;
  rtc::VideoSinkWants sink_settings{};
  sink_settings.rotation_applied = true;
  track_->AddOrUpdateSink(this, sink_settings);
}

LocalVideoTrack::LocalVideoTrack(
    RefPtr<GlobalFactory> global_factory,
    PeerConnection& owner,
    Transceiver* transceiver,
    rtc::scoped_refptr<webrtc::VideoTrackInterface> track,
    rtc::scoped_refptr<webrtc::RtpSenderInterface> sender) noexcept
    : MediaTrack(std::move(global_factory),
                 ObjectType::kLocalVideoTrack,
                 owner),
      track_(std::move(track)),
      sender_(std::move(sender)),
      transceiver_(transceiver) {
  RTC_CHECK(owner_);
  RTC_CHECK(transceiver_);
  RTC_CHECK(transceiver_->GetMediaKind() == mrsMediaKind::kVideo);
  RTC_CHECK(track_);
  RTC_CHECK(sender_);
  name_ = track_->id();
  kind_ = mrsTrackKind::kVideoTrack;
  transceiver_->OnLocalTrackAdded(this);
  rtc::VideoSinkWants sink_settings{};
  sink_settings.rotation_applied = true;
  track_->AddOrUpdateSink(this, sink_settings);
}

LocalVideoTrack::~LocalVideoTrack() {
  track_->RemoveSink(this);
  if (transceiver_) {
    transceiver_->SetLocalTrack(nullptr);
  }
  RTC_CHECK(!transceiver_);
  RTC_CHECK(!owner_);
}

void LocalVideoTrack::SetEnabled(bool enabled) const noexcept {
  track_->set_enabled(enabled);
}

bool LocalVideoTrack::IsEnabled() const noexcept {
  return track_->enabled();
}

webrtc::VideoTrackInterface* LocalVideoTrack::impl() const {
  return track_.get();
}

webrtc::RtpSenderInterface* LocalVideoTrack::sender() const {
  RTC_DCHECK(transceiver_->IsUnifiedPlan());  // sender invalid in Plan B
  return sender_.get();
}

void LocalVideoTrack::OnAddedToPeerConnection(
    PeerConnection& owner,
    Transceiver* transceiver,
    rtc::scoped_refptr<webrtc::RtpSenderInterface> sender) {
  RTC_CHECK(!owner_);
  RTC_CHECK(!transceiver_);
  RTC_CHECK(!sender_);
  RTC_CHECK(transceiver);
  RTC_CHECK(transceiver->GetMediaKind() == mrsMediaKind::kVideo);
  // In Plan B the RTP sender is not always available (depends on transceiver
  // direction) so |sender| is invalid here.
  RTC_CHECK(transceiver->IsPlanB() || sender);
  owner_ = &owner;
  sender_ = std::move(sender);  // NULL in Plan B
  transceiver_ = transceiver;
  transceiver_->OnLocalTrackAdded(this);
}

void LocalVideoTrack::OnRemovedFromPeerConnection(
    PeerConnection& old_owner,
    Transceiver* old_transceiver,
    rtc::scoped_refptr<webrtc::RtpSenderInterface> old_sender) {
  RTC_CHECK_EQ(owner_, &old_owner);
  RTC_CHECK_EQ(transceiver_, old_transceiver);
  RTC_CHECK(old_transceiver->GetMediaKind() == mrsMediaKind::kVideo);
  // In Plan B the RTP sender is not always available (depends on transceiver
  // direction) so |old_sender| is invalid here.
  RTC_CHECK(old_transceiver->IsPlanB() || (sender_ == old_sender.get()));
  owner_ = nullptr;
  sender_ = nullptr;
  transceiver_->OnLocalTrackRemoved(this);
  transceiver_ = nullptr;
}

void LocalVideoTrack::RemoveFromPeerConnection(
    webrtc::PeerConnectionInterface& peer) {
  if (transceiver_->IsUnifiedPlan()) {
    if (sender_) {
      peer.RemoveTrack(sender_);
      sender_ = nullptr;
      owner_ = nullptr;
      transceiver_->OnLocalTrackRemoved(this);
      transceiver_ = nullptr;
    }
  } else {
    transceiver_->SetTrackPlanB(nullptr);
    transceiver_->SyncSenderPlanB(false, &peer, nullptr, nullptr);
    owner_ = nullptr;
    transceiver_->OnLocalTrackRemoved(this);
    transceiver_ = nullptr;
  }
}

}  // namespace WebRTC
}  // namespace MixedReality
}  // namespace Microsoft
