// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// This is a precompiled header, it must be on its own, followed by a blank
// line, to prevent clang-format from reordering it with other headers.
#include "pch.h"

#include "media/local_audio_track.h"
#include "media/local_video_track.h"
#include "media/remote_audio_track.h"
#include "media/remote_video_track.h"
#include "media/transceiver.h"
#include "transceiver_interop.h"

using namespace Microsoft::MixedReality::WebRTC;

void MRS_CALL mrsTransceiverRegisterAssociatedCallback(
    mrsTransceiverHandle handle,
    mrsTransceiverAssociatedCallback callback,
    void* user_data) noexcept {
  if (auto transceiver = static_cast<Transceiver*>(handle)) {
    transceiver->RegisterAssociatedCallback(
        Transceiver::AssociatedCallback{callback, user_data});
  }
}

void MRS_CALL mrsTransceiverRegisterStateUpdatedCallback(
    mrsTransceiverHandle handle,
    mrsTransceiverStateUpdatedCallback callback,
    void* user_data) noexcept {
  if (auto transceiver = static_cast<Transceiver*>(handle)) {
    transceiver->RegisterStateUpdatedCallback(
        Transceiver::StateUpdatedCallback{callback, user_data});
  }
}

mrsResult MRS_CALL
mrsTransceiverSetDirection(mrsTransceiverHandle transceiver_handle,
                           mrsTransceiverDirection new_direction) noexcept {
  if (auto transceiver = static_cast<Transceiver*>(transceiver_handle)) {
    return transceiver->SetDirection(new_direction);
  }
  return Result::kInvalidNativeHandle;
}

mrsResult MRS_CALL mrsTransceiverSetLocalAudioTrack(
    mrsTransceiverHandle transceiver_handle,
    mrsLocalAudioTrackHandle track_handle) noexcept {
  if (!transceiver_handle) {
    return Result::kInvalidNativeHandle;
  }
  auto transceiver = static_cast<Transceiver*>(transceiver_handle);
  if (transceiver->GetMediaKind() != mrsMediaKind::kAudio) {
    return Result::kInvalidMediaKind;
  }
  auto track = static_cast<LocalAudioTrack*>(track_handle);
  return transceiver->SetLocalTrack(track);
}

mrsResult MRS_CALL mrsTransceiverSetLocalVideoTrack(
    mrsTransceiverHandle transceiver_handle,
    mrsLocalVideoTrackHandle track_handle) noexcept {
  if (!transceiver_handle) {
    return Result::kInvalidNativeHandle;
  }
  auto transceiver = static_cast<Transceiver*>(transceiver_handle);
  if (transceiver->GetMediaKind() != mrsMediaKind::kVideo) {
    return Result::kInvalidMediaKind;
  }
  auto track = static_cast<LocalVideoTrack*>(track_handle);
  return transceiver->SetLocalTrack(track);
}

mrsResult MRS_CALL mrsTransceiverGetLocalAudioTrack(
    mrsTransceiverHandle transceiver_handle,
    mrsLocalAudioTrackHandle* track_handle_out) noexcept {
  if (!track_handle_out) {
    return Result::kInvalidParameter;
  }
  if (auto transceiver = static_cast<Transceiver*>(transceiver_handle)) {
    if (transceiver->GetMediaKind() != mrsMediaKind::kAudio) {
      return Result::kInvalidMediaKind;
    }
    *track_handle_out = transceiver->GetLocalAudioTrack().get();
    return Result::kSuccess;
  }
  return Result::kInvalidNativeHandle;
}

mrsResult MRS_CALL mrsTransceiverGetLocalVideoTrack(
    mrsTransceiverHandle transceiver_handle,
    mrsLocalVideoTrackHandle* track_handle_out) noexcept {
  if (!track_handle_out) {
    return Result::kInvalidParameter;
  }
  if (auto transceiver = static_cast<Transceiver*>(transceiver_handle)) {
    if (transceiver->GetMediaKind() != mrsMediaKind::kVideo) {
      return Result::kInvalidMediaKind;
    }
    *track_handle_out = transceiver->GetLocalVideoTrack().get();
    return Result::kSuccess;
  }
  return Result::kInvalidNativeHandle;
}

mrsResult MRS_CALL mrsTransceiverGetRemoteAudioTrack(
    mrsTransceiverHandle transceiver_handle,
    mrsRemoteAudioTrackHandle* track_handle_out) noexcept {
  if (!track_handle_out) {
    return Result::kInvalidParameter;
  }
  if (auto transceiver = static_cast<Transceiver*>(transceiver_handle)) {
    if (transceiver->GetMediaKind() != mrsMediaKind::kAudio) {
      return Result::kInvalidMediaKind;
    }
    *track_handle_out = transceiver->GetRemoteAudioTrack().get();
    return Result::kSuccess;
  }
  return Result::kInvalidNativeHandle;
}

mrsResult MRS_CALL mrsTransceiverGetRemoteVideoTrack(
    mrsTransceiverHandle transceiver_handle,
    mrsRemoteVideoTrackHandle* track_handle_out) noexcept {
  if (!track_handle_out) {
    return Result::kInvalidParameter;
  }
  if (auto transceiver = static_cast<Transceiver*>(transceiver_handle)) {
    if (transceiver->GetMediaKind() != mrsMediaKind::kVideo) {
      return Result::kInvalidMediaKind;
    }
    *track_handle_out = transceiver->GetRemoteVideoTrack().get();
    return Result::kSuccess;
  }
  return Result::kInvalidNativeHandle;
}
