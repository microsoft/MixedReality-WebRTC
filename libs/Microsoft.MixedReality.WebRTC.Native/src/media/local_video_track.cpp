// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include "interop/global_factory.h"
#include "local_video_track.h"
#include "peer_connection.h"

namespace Microsoft::MixedReality::WebRTC {

LocalVideoTrack::LocalVideoTrack(
    RefPtr<GlobalFactory> global_factory,
    PeerConnection& owner,
    rtc::scoped_refptr<webrtc::VideoTrackInterface> track,
    rtc::scoped_refptr<webrtc::RtpSenderInterface> sender,
    mrsLocalVideoTrackInteropHandle interop_handle) noexcept
    : TrackedObject(std::move(global_factory), ObjectType::kLocalVideoTrack),
      owner_(&owner),
      track_(std::move(track)),
      sender_(std::move(sender)),
      interop_handle_(interop_handle) {
  RTC_CHECK(owner_);
  rtc::VideoSinkWants sink_settings{};
  sink_settings.rotation_applied = true;
  track_->AddOrUpdateSink(this, sink_settings);
}

LocalVideoTrack::~LocalVideoTrack() {
  track_->RemoveSink(this);
  if (owner_) {
    owner_->RemoveLocalVideoTrack(*this);
  }
  RTC_CHECK(!owner_);
}

std::string LocalVideoTrack::GetName() const noexcept {
  return track_->id();
}

bool LocalVideoTrack::IsEnabled() const noexcept {
  return track_->enabled();
}

void LocalVideoTrack::SetEnabled(bool enabled) const noexcept {
  track_->set_enabled(enabled);
}

webrtc::VideoTrackInterface* LocalVideoTrack::impl() const {
  return track_.get();
}

webrtc::RtpSenderInterface* LocalVideoTrack::sender() const {
  return sender_.get();
}

void LocalVideoTrack::RemoveFromPeerConnection(
    webrtc::PeerConnectionInterface& peer) {
  if (sender_) {
    peer.RemoveTrack(sender_);
    sender_ = nullptr;
    owner_ = nullptr;
  }
}

}  // namespace Microsoft::MixedReality::WebRTC
