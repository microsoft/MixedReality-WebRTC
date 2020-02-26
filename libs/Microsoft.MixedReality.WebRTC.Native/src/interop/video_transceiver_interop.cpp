// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// This is a precompiled header, it must be on its own, followed by a blank
// line, to prevent clang-format from reordering it with other headers.
#include "pch.h"

#include "interop/video_transceiver_interop.h"
#include "media/video_transceiver.h"

using namespace Microsoft::MixedReality::WebRTC;

void MRS_CALL
mrsVideoTransceiverAddRef(VideoTransceiverHandle handle) noexcept {
  if (auto transceiver = static_cast<VideoTransceiver*>(handle)) {
    transceiver->AddRef();
  } else {
    RTC_LOG(LS_WARNING)
        << "Trying to add reference to NULL VideoTransceiver object.";
  }
}

void MRS_CALL
mrsVideoTransceiverRemoveRef(VideoTransceiverHandle handle) noexcept {
  if (auto transceiver = static_cast<VideoTransceiver*>(handle)) {
    transceiver->RemoveRef();
  } else {
    RTC_LOG(LS_WARNING)
        << "Trying to remove reference from NULL VideoTransceiver object.";
  }
}

void MRS_CALL mrsVideoTransceiverRegisterStateUpdatedCallback(
    VideoTransceiverHandle handle,
    mrsVideoTransceiverStateUpdatedCallback callback,
    void* user_data) noexcept {
  if (auto transceiver = static_cast<VideoTransceiver*>(handle)) {
    transceiver->RegisterStateUpdatedCallback(
        Transceiver::StateUpdatedCallback{callback, user_data});
  }
}

mrsResult MRS_CALL mrsVideoTransceiverSetDirection(
    VideoTransceiverHandle transceiver_handle,
    mrsTransceiverDirection new_direction) noexcept {
  if (auto transceiver = static_cast<VideoTransceiver*>(transceiver_handle)) {
    return transceiver->SetDirection(new_direction);
  }
  return Result::kInvalidNativeHandle;
}

mrsResult MRS_CALL
mrsVideoTransceiverSetLocalTrack(VideoTransceiverHandle transceiver_handle,
                                 LocalVideoTrackHandle track_handle) noexcept {
  if (!transceiver_handle) {
    return Result::kInvalidNativeHandle;
  }
  auto transceiver = static_cast<VideoTransceiver*>(transceiver_handle);
  auto track = static_cast<LocalVideoTrack*>(track_handle);
  return transceiver->SetLocalTrack(track);
}

mrsResult MRS_CALL mrsVideoTransceiverGetLocalTrack(
    VideoTransceiverHandle transceiver_handle,
    LocalVideoTrackHandle* track_handle_out) noexcept {
  if (!track_handle_out) {
    return Result::kInvalidParameter;
  }
  if (auto transceiver = static_cast<VideoTransceiver*>(transceiver_handle)) {
    *track_handle_out = transceiver->GetLocalTrack().release();
    return Result::kSuccess;
  }
  return Result::kInvalidNativeHandle;
}

mrsResult MRS_CALL mrsVideoTransceiverGetRemoteTrack(
    VideoTransceiverHandle transceiver_handle,
    RemoteVideoTrackHandle* track_handle_out) noexcept {
  if (!track_handle_out) {
    return Result::kInvalidParameter;
  }
  if (auto transceiver = static_cast<VideoTransceiver*>(transceiver_handle)) {
    *track_handle_out = transceiver->GetRemoteTrack().release();
    return Result::kSuccess;
  }
  return Result::kInvalidNativeHandle;
}
