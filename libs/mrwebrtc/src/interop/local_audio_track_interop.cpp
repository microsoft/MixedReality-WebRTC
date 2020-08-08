// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// This is a precompiled header, it must be on its own, followed by a blank
// line, to prevent clang-format from reordering it with other headers.
#include "pch.h"

#include "interop/global_factory.h"
#include "local_audio_track_interop.h"
#include "media/audio_track_source.h"
#include "media/local_audio_track.h"
#include "utils.h"

using namespace Microsoft::MixedReality::WebRTC;

mrsResult MRS_CALL mrsLocalAudioTrackCreateFromSource(
    const mrsLocalAudioTrackInitSettings* init_settings,
    mrsAudioTrackSourceHandle source_handle,
    mrsLocalAudioTrackHandle* track_handle_out) noexcept {
  if (!init_settings) {
    RTC_LOG(LS_ERROR) << "Invalid NULL local audio track init settings.";
    return Result::kInvalidParameter;
  }
  if (mrsBool::kFalse == mrsSdpIsValidToken(init_settings->track_name)) {
    RTC_LOG(LS_ERROR) << "Local audio track name " << init_settings->track_name
                      << " is not a valid SDP token.";
    return Result::kInvalidParameter;
  }
  if (!source_handle) {
    RTC_LOG(LS_ERROR) << "Invalid track source handle.";
    return Result::kInvalidParameter;
  }
  if (!track_handle_out) {
    RTC_LOG(LS_ERROR) << "Invalid NULL local audio track handle reference.";
    return Result::kInvalidParameter;
  }
  *track_handle_out = nullptr;

  RefPtr<GlobalFactory> global_factory(GlobalFactory::InstancePtr());
  auto pc_factory = global_factory->GetPeerConnectionFactory();
  if (!pc_factory) {
    return Result::kInvalidOperation;
  }

  // Create the audio track
  auto source = static_cast<AudioTrackSource*>(source_handle);
  rtc::scoped_refptr<webrtc::AudioTrackInterface> audio_track =
      pc_factory->CreateAudioTrack(init_settings->track_name,
                                   source->impl().get());
  if (!audio_track) {
    RTC_LOG(LS_ERROR) << "Failed to create local audio track from source.";
    return Result::kUnknownError;
  }

  // Create the audio track wrapper
  RefPtr<LocalAudioTrack> track =
      new LocalAudioTrack(std::move(global_factory), std::move(audio_track));
  *track_handle_out = track.release();
  return Result::kSuccess;
}

void MRS_CALL
mrsLocalAudioTrackRegisterFrameCallback(mrsLocalAudioTrackHandle trackHandle,
                                        mrsAudioFrameCallback callback,
                                        void* user_data) noexcept {
  if (auto track = static_cast<LocalAudioTrack*>(trackHandle)) {
    track->SetCallback(AudioFrameReadyCallback{callback, user_data});
  }
}

mrsResult MRS_CALL
mrsLocalAudioTrackSetEnabled(mrsLocalAudioTrackHandle track_handle,
                             mrsBool enabled) noexcept {
  auto track = static_cast<LocalAudioTrack*>(track_handle);
  if (!track) {
    return Result::kInvalidParameter;
  }
  track->SetEnabled(enabled != mrsBool::kFalse);
  return Result::kSuccess;
}

mrsBool MRS_CALL
mrsLocalAudioTrackIsEnabled(mrsLocalAudioTrackHandle track_handle) noexcept {
  auto track = static_cast<LocalAudioTrack*>(track_handle);
  if (!track) {
    return mrsBool::kFalse;
  }
  return (track->IsEnabled() ? mrsBool::kTrue : mrsBool::kFalse);
}
