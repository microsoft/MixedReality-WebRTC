// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// This is a precompiled header, it must be on its own, followed by a blank
// line, to prevent clang-format from reordering it with other headers.
#include "pch.h"

#include "interop/local_video_track_interop.h"
#include "local_video_track.h"

using namespace Microsoft::MixedReality::WebRTC;

void MRS_CALL mrsLocalVideoTrackAddRef(LocalVideoTrackHandle handle) noexcept {
  if (auto track = static_cast<LocalVideoTrack*>(handle)) {
    track->AddRef();
  } else {
    RTC_LOG(LS_WARNING)
        << "Trying to add reference to NULL LocalVideoTrack object.";
  }
}

void MRS_CALL
mrsLocalVideoTrackRemoveRef(LocalVideoTrackHandle handle) noexcept {
  if (auto track = static_cast<LocalVideoTrack*>(handle)) {
    track->Release();
  } else {
    RTC_LOG(LS_WARNING) << "Trying to remove reference from NULL "
                           "LocalVideoTrack object.";
  }
}

void MRS_CALL mrsLocalVideoTrackRegisterI420FrameCallback(
    LocalVideoTrackHandle trackHandle,
    PeerConnectionI420VideoFrameCallback callback,
    void* user_data) noexcept {
  if (auto track = static_cast<LocalVideoTrack*>(trackHandle)) {
    track->SetCallback(I420FrameReadyCallback{callback, user_data});
  }
}

void MRS_CALL mrsLocalVideoTrackRegisterARGBFrameCallback(
    LocalVideoTrackHandle trackHandle,
    PeerConnectionARGBVideoFrameCallback callback,
    void* user_data) noexcept {
  if (auto track = static_cast<LocalVideoTrack*>(trackHandle)) {
    track->SetCallback(ARGBFrameReadyCallback{callback, user_data});
  }
}

mrsResult MRS_CALL
mrsLocalVideoTrackSetEnabled(LocalVideoTrackHandle track_handle,
                             mrsBool enabled) noexcept {
  auto track = static_cast<LocalVideoTrack*>(track_handle);
  if (!track) {
    return MRS_E_INVALID_PARAMETER;
  }
  track->SetEnabled(enabled != mrsBool::kFalse);
  return MRS_SUCCESS;
}

mrsBool MRS_CALL
mrsLocalVideoTrackIsEnabled(LocalVideoTrackHandle track_handle) noexcept {
  auto track = static_cast<LocalVideoTrack*>(track_handle);
  if (!track) {
    return mrsBool::kFalse;
  }
  return (track->IsEnabled() ? mrsBool::kTrue : mrsBool::kFalse);
}
