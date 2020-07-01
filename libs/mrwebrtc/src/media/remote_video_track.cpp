// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include "interop/global_factory.h"
#include "peer_connection.h"
#include "remote_video_track.h"

namespace Microsoft {
namespace MixedReality {
namespace WebRTC {

RemoteVideoTrack::RemoteVideoTrack(
    RefPtr<GlobalFactory> global_factory,
    PeerConnection& owner,
    Transceiver* transceiver,
    rtc::scoped_refptr<webrtc::VideoTrackInterface> track,
    rtc::scoped_refptr<webrtc::RtpReceiverInterface> receiver) noexcept
    : MediaTrack(std::move(global_factory),
                 ObjectType::kRemoteVideoTrack,
                 owner),
      track_(std::move(track)),
      receiver_(std::move(receiver)),
      transceiver_(transceiver) {
  RTC_CHECK(owner_);
  RTC_CHECK(track_);
  RTC_CHECK(receiver_);
  RTC_CHECK(transceiver_);
  RTC_CHECK(transceiver_->GetMediaKind() == mrsMediaKind::kVideo);
  name_ = track_->id();
  kind_ = mrsTrackKind::kVideoTrack;
  transceiver_->OnRemoteTrackAdded(this);
  rtc::VideoSinkWants sink_settings{};
  sink_settings.rotation_applied = true;
  track_->AddOrUpdateSink(this, sink_settings);
}

RemoteVideoTrack::~RemoteVideoTrack() {
  track_->RemoveSink(this);
  RTC_CHECK(!owner_);
}

bool RemoteVideoTrack::IsEnabled() const noexcept {
  return track_->enabled();
}

void RemoteVideoTrack::SetEnabled(bool enabled) const noexcept {
  track_->set_enabled(enabled);
}

webrtc::VideoTrackInterface* RemoteVideoTrack::impl() const {
  return track_.get();
}

webrtc::RtpReceiverInterface* RemoteVideoTrack::receiver() const {
  return receiver_.get();
}

void RemoteVideoTrack::OnTrackRemoved(PeerConnection& owner) {
  RTC_DCHECK(owner_ == &owner);
  RTC_DCHECK(receiver_ != nullptr);
  RTC_DCHECK(transceiver_ != nullptr);
  owner_ = nullptr;
  receiver_ = nullptr;
  transceiver_->OnRemoteTrackRemoved(this);
  transceiver_ = nullptr;
}

}  // namespace WebRTC
}  // namespace MixedReality
}  // namespace Microsoft
