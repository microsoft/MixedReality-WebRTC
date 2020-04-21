// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// This is a precompiled header, it must be on its own, followed by a blank
// line, to prevent clang-format from reordering it with other headers.
#include "pch.h"

#include "media/remote_audio_track.h"
#include "remote_audio_track_interop.h"

using namespace Microsoft::MixedReality::WebRTC;

MRS_API void MRS_CALL
mrsRemoteAudioTrackSetUserData(mrsRemoteAudioTrackHandle handle,
                               void* user_data) noexcept {
  if (auto track = static_cast<RemoteAudioTrack*>(handle)) {
    track->SetUserData(user_data);
  }
}

MRS_API void* MRS_CALL
mrsRemoteAudioTrackGetUserData(mrsRemoteAudioTrackHandle handle) noexcept {
  if (auto track = static_cast<RemoteAudioTrack*>(handle)) {
    return track->GetUserData();
  }
  return nullptr;
}

void MRS_CALL
mrsRemoteAudioTrackRegisterFrameCallback(mrsRemoteAudioTrackHandle trackHandle,
                                         mrsAudioFrameCallback callback,
                                         void* user_data) noexcept {
  if (auto track = static_cast<RemoteAudioTrack*>(trackHandle)) {
    track->SetCallback(AudioFrameReadyCallback{callback, user_data});
  }
}

mrsResult MRS_CALL
mrsRemoteAudioTrackSetEnabled(mrsRemoteAudioTrackHandle track_handle,
                              mrsBool enabled) noexcept {
  auto track = static_cast<RemoteAudioTrack*>(track_handle);
  if (!track) {
    return Result::kInvalidParameter;
  }
  track->SetEnabled(enabled != mrsBool::kFalse);
  return Result::kSuccess;
}

mrsBool MRS_CALL
mrsRemoteAudioTrackIsEnabled(mrsRemoteAudioTrackHandle track_handle) noexcept {
  auto track = static_cast<RemoteAudioTrack*>(track_handle);
  if (!track) {
    return mrsBool::kFalse;
  }
  return (track->IsEnabled() ? mrsBool::kTrue : mrsBool::kFalse);
}
