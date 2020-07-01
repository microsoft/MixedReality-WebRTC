// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// This is a precompiled header, it must be on its own, followed by a blank
// line, to prevent clang-format from reordering it with other headers.
#include "pch.h"

#include "global_factory.h"
#include "local_video_track_interop.h"
#include "media/external_video_track_source.h"
#include "media/local_video_track.h"
#include "media/video_track_source.h"
#include "utils.h"

using namespace Microsoft::MixedReality::WebRTC;

mrsResult MRS_CALL mrsLocalVideoTrackCreateFromSource(
    const mrsLocalVideoTrackInitSettings* init_settings,
    mrsVideoTrackSourceHandle source_handle,
    mrsLocalVideoTrackHandle* track_handle_out) noexcept {
  if (!init_settings) {
    RTC_LOG(LS_ERROR) << "Invalid NULL local video track init settings.";
    return Result::kInvalidParameter;
  }
  if (mrsBool::kFalse == mrsSdpIsValidToken(init_settings->track_name)) {
    RTC_LOG(LS_ERROR) << "Local video track name " << init_settings->track_name
                      << " is not a valid SDP token.";
    return Result::kInvalidParameter;
  }
  if (!source_handle) {
    RTC_LOG(LS_ERROR) << "Invalid track source handle.";
    return Result::kInvalidParameter;
  }
  if (!track_handle_out) {
    RTC_LOG(LS_ERROR) << "Invalid NULL local video track handle reference.";
    return Result::kInvalidParameter;
  }
  *track_handle_out = nullptr;

  RefPtr<GlobalFactory> global_factory(GlobalFactory::InstancePtr());
  auto pc_factory = global_factory->GetPeerConnectionFactory();
  if (!pc_factory) {
    return Result::kInvalidOperation;
  }

  // Create the audio track
  auto source = static_cast<VideoTrackSource*>(source_handle);
  rtc::scoped_refptr<webrtc::VideoTrackInterface> video_track =
      pc_factory->CreateVideoTrack(init_settings->track_name,
                                   source->impl().get());
  if (!video_track) {
    RTC_LOG(LS_ERROR) << "Failed to create local video track from source.";
    return Result::kUnknownError;
  }

  // Create the audio track wrapper
  RefPtr<LocalVideoTrack> track =
      new LocalVideoTrack(std::move(global_factory), std::move(video_track));
  *track_handle_out = track.release();
  return Result::kSuccess;
}

void MRS_CALL mrsLocalVideoTrackRegisterI420AFrameCallback(
    mrsLocalVideoTrackHandle trackHandle,
    mrsI420AVideoFrameCallback callback,
    void* user_data) noexcept {
  if (auto track = static_cast<LocalVideoTrack*>(trackHandle)) {
    track->SetCallback(I420AFrameReadyCallback{callback, user_data});
  }
}

void MRS_CALL mrsLocalVideoTrackRegisterArgb32FrameCallback(
    mrsLocalVideoTrackHandle trackHandle,
    mrsArgb32VideoFrameCallback callback,
    void* user_data) noexcept {
  if (auto track = static_cast<LocalVideoTrack*>(trackHandle)) {
    track->SetCallback(Argb32FrameReadyCallback{callback, user_data});
  }
}

mrsResult MRS_CALL
mrsLocalVideoTrackSetEnabled(mrsLocalVideoTrackHandle track_handle,
                             mrsBool enabled) noexcept {
  auto track = static_cast<LocalVideoTrack*>(track_handle);
  if (!track) {
    return Result::kInvalidParameter;
  }
  track->SetEnabled(enabled != mrsBool::kFalse);
  return Result::kSuccess;
}

mrsBool MRS_CALL
mrsLocalVideoTrackIsEnabled(mrsLocalVideoTrackHandle track_handle) noexcept {
  auto track = static_cast<LocalVideoTrack*>(track_handle);
  if (!track) {
    return mrsBool::kFalse;
  }
  return (track->IsEnabled() ? mrsBool::kTrue : mrsBool::kFalse);
}
