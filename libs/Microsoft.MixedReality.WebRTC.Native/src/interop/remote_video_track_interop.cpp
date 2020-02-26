// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// This is a precompiled header, it must be on its own, followed by a blank
// line, to prevent clang-format from reordering it with other headers.
#include "pch.h"

#include "interop/remote_video_track_interop.h"
#include "media/remote_video_track.h"

using namespace Microsoft::MixedReality::WebRTC;

void MRS_CALL
mrsRemoteVideoTrackAddRef(RemoteVideoTrackHandle handle) noexcept {
  if (auto track = static_cast<RemoteVideoTrack*>(handle)) {
    track->AddRef();
  } else {
    RTC_LOG(LS_WARNING)
        << "Trying to add reference to NULL RemoteVideoTrack object.";
  }
}

void MRS_CALL
mrsRemoteVideoTrackRemoveRef(RemoteVideoTrackHandle handle) noexcept {
  if (auto track = static_cast<RemoteVideoTrack*>(handle)) {
    track->RemoveRef();
  } else {
    RTC_LOG(LS_WARNING) << "Trying to remove reference from NULL "
                           "RemoteVideoTrack object.";
  }
}

void MRS_CALL mrsRemoteVideoTrackRegisterI420AFrameCallback(
    RemoteVideoTrackHandle trackHandle,
    mrsI420AVideoFrameCallback callback,
    void* user_data) noexcept {
  if (auto track = static_cast<RemoteVideoTrack*>(trackHandle)) {
    track->SetCallback(I420AFrameReadyCallback{callback, user_data});
  }
}

void MRS_CALL mrsRemoteVideoTrackRegisterArgb32FrameCallback(
    RemoteVideoTrackHandle trackHandle,
    mrsArgb32VideoFrameCallback callback,
    void* user_data) noexcept {
  if (auto track = static_cast<RemoteVideoTrack*>(trackHandle)) {
    track->SetCallback(Argb32FrameReadyCallback{callback, user_data});
  }
}

mrsResult MRS_CALL
mrsRemoteVideoTrackSetEnabled(RemoteVideoTrackHandle track_handle,
                              mrsBool enabled) noexcept {
  auto track = static_cast<RemoteVideoTrack*>(track_handle);
  if (!track) {
    return Result::kInvalidParameter;
  }
  track->SetEnabled(enabled != mrsBool::kFalse);
  return Result::kSuccess;
}

mrsBool MRS_CALL
mrsRemoteVideoTrackIsEnabled(RemoteVideoTrackHandle track_handle) noexcept {
  auto track = static_cast<RemoteVideoTrack*>(track_handle);
  if (!track) {
    return mrsBool::kFalse;
  }
  return (track->IsEnabled() ? mrsBool::kTrue : mrsBool::kFalse);
}
