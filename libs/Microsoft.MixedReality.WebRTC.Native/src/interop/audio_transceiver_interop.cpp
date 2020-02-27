// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// This is a precompiled header, it must be on its own, followed by a blank
// line, to prevent clang-format from reordering it with other headers.
#include "pch.h"

#include "audio_transceiver_interop.h"
#include "media/audio_transceiver.h"

using namespace Microsoft::MixedReality::WebRTC;

void MRS_CALL
mrsAudioTransceiverAddRef(mrsAudioTransceiverHandle handle) noexcept {
  if (auto transceiver = static_cast<AudioTransceiver*>(handle)) {
    transceiver->AddRef();
  } else {
    RTC_LOG(LS_WARNING)
        << "Trying to add reference to NULL AudioTransceiver object.";
  }
}

void MRS_CALL
mrsAudioTransceiverRemoveRef(mrsAudioTransceiverHandle handle) noexcept {
  if (auto transceiver = static_cast<AudioTransceiver*>(handle)) {
    transceiver->RemoveRef();
  } else {
    RTC_LOG(LS_WARNING)
        << "Trying to remove reference from NULL AudioTransceiver object.";
  }
}

void MRS_CALL mrsAudioTransceiverRegisterStateUpdatedCallback(
    mrsAudioTransceiverHandle handle,
    mrsAudioTransceiverStateUpdatedCallback callback,
    void* user_data) noexcept {
  if (auto transceiver = static_cast<AudioTransceiver*>(handle)) {
    transceiver->RegisterStateUpdatedCallback(
        Transceiver::StateUpdatedCallback{callback, user_data});
  }
}

mrsResult MRS_CALL mrsAudioTransceiverSetDirection(
    mrsAudioTransceiverHandle transceiver_handle,
    mrsTransceiverDirection new_direction) noexcept {
  if (auto transceiver = static_cast<AudioTransceiver*>(transceiver_handle)) {
    return transceiver->SetDirection(new_direction);
  }
  return Result::kInvalidNativeHandle;
}

mrsResult MRS_CALL mrsAudioTransceiverSetLocalTrack(
    mrsAudioTransceiverHandle transceiver_handle,
    mrsLocalAudioTrackHandle track_handle) noexcept {
  if (!transceiver_handle) {
    return Result::kInvalidNativeHandle;
  }
  auto transceiver = static_cast<AudioTransceiver*>(transceiver_handle);
  auto track = static_cast<LocalAudioTrack*>(track_handle);
  return transceiver->SetLocalTrack(track);
}

mrsResult MRS_CALL mrsAudioTransceiverGetLocalTrack(
    mrsAudioTransceiverHandle transceiver_handle,
    mrsLocalAudioTrackHandle* track_handle_out) noexcept {
  if (!track_handle_out) {
    return Result::kInvalidParameter;
  }
  if (auto transceiver = static_cast<AudioTransceiver*>(transceiver_handle)) {
    *track_handle_out = transceiver->GetLocalTrack().release();
    return Result::kSuccess;
  }
  return Result::kInvalidNativeHandle;
}

mrsResult MRS_CALL mrsAudioTransceiverGetRemoteTrack(
    mrsAudioTransceiverHandle transceiver_handle,
    mrsRemoteAudioTrackHandle* track_handle_out) noexcept {
  if (!track_handle_out) {
    return Result::kInvalidParameter;
  }
  if (auto transceiver = static_cast<AudioTransceiver*>(transceiver_handle)) {
    *track_handle_out = transceiver->GetRemoteTrack().release();
    return Result::kSuccess;
  }
  return Result::kInvalidNativeHandle;
}
