// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include "audio_track_read_buffer.h"
#include "interop/global_factory.h"
#include "media/remote_audio_track.h"
#include "peer_connection.h"
#include "remote_audio_track_interop.h"

namespace Microsoft {
namespace MixedReality {
namespace WebRTC {

RemoteAudioTrack::RemoteAudioTrack(
    RefPtr<GlobalFactory> global_factory,
    PeerConnection& owner,
    Transceiver* transceiver,
    rtc::scoped_refptr<webrtc::AudioTrackInterface> track,
    rtc::scoped_refptr<webrtc::RtpReceiverInterface> receiver) noexcept
    : MediaTrack(std::move(global_factory),
                 ObjectType::kRemoteAudioTrack,
                 owner),
      track_(std::move(track)),
      receiver_(std::move(receiver)),
      transceiver_(transceiver) {
  RTC_CHECK(owner_);
  RTC_CHECK(track_);
  RTC_CHECK(receiver_);
  RTC_CHECK(transceiver_);
  RTC_CHECK(transceiver_->GetMediaKind() == mrsMediaKind::kAudio);
  name_ = track_->id();
  kind_ = mrsTrackKind::kAudioTrack;
  transceiver_->OnRemoteTrackAdded(this);
  track_->AddSink(this);
}

RemoteAudioTrack::~RemoteAudioTrack() {
  track_->RemoveSink(this);
  RTC_CHECK(!owner_);
}

webrtc::AudioTrackInterface* RemoteAudioTrack::impl() const {
  return track_.get();
}

webrtc::RtpReceiverInterface* RemoteAudioTrack::receiver() const {
  return receiver_.get();
}

void RemoteAudioTrack::OnTrackRemoved(PeerConnection& owner) {
  RTC_DCHECK(owner_ == &owner);
  RTC_DCHECK(receiver_ != nullptr);
  RTC_DCHECK(transceiver_ != nullptr);
  owner_ = nullptr;
  receiver_ = nullptr;
  transceiver_->OnRemoteTrackRemoved(this);
  transceiver_ = nullptr;
}

void RemoteAudioTrack::OutputToDevice(bool output) noexcept {
  output_to_device_ = output;
  if (ssrc_) {
    global_factory_->audio_mixer()->OutputSource(*ssrc_, output);
  }
  // else SSRC is unknown and we can't change the output state now. InitSsrc
  // will do it when called.
}

void RemoteAudioTrack::InitSsrc(int ssrc) {
  RTC_DCHECK(!ssrc_);
  ssrc_ = ssrc;

  // Now that we know the SSRC id, we can initialize the output state.
  // Note that the value is true by default but might have been changed
  // if OutputToDevice has been called in the track creation callback.
  global_factory_->audio_mixer()->OutputSource(ssrc, output_to_device_);
}

std::unique_ptr<AudioTrackReadBuffer> RemoteAudioTrack::CreateReadBuffer() const
    noexcept {
  return std::make_unique<AudioTrackReadBuffer>(global_factory_, track_);
}

}  // namespace WebRTC
}  // namespace MixedReality
}  // namespace Microsoft
