// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// This is a precompiled header, it must be on its own, followed by a blank
// line, to prevent clang-format from reordering it with other headers.
#include "pch.h"

#include "media/remote_audio_track.h"
#include "remote_audio_track_interop.h"
#include "media/audio_track_read_buffer.h"
#include "utils.h"

using namespace Microsoft::MixedReality::WebRTC;

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

void MRS_CALL
mrsRemoteAudioTrackOutputToDevice(mrsRemoteAudioTrackHandle track_handle,
    bool output) noexcept {
  if (auto track = static_cast<RemoteAudioTrack*>(track_handle)) {
      track->OutputToDevice(output);
  }
}

mrsBool MRS_CALL mrsRemoteAudioTrackIsOutputToDevice(
    mrsRemoteAudioTrackHandle track_handle) noexcept {
  if (auto track = static_cast<const RemoteAudioTrack*>(track_handle)) {
    return track->IsOutputToDevice() ? mrsBool::kTrue : mrsBool::kFalse;
  }
  return mrsBool::kFalse;
}

mrsResult MRS_CALL
mrsRemoteAudioTrackCreateReadBuffer(mrsRemoteAudioTrackHandle track_handle,
                              mrsAudioTrackReadBufferHandle* audioBufferOut) {
  if (!audioBufferOut) {
      return Result::kInvalidParameter;
  }

  *audioBufferOut = nullptr;
  if (auto track = static_cast<RemoteAudioTrack*>(track_handle)) {
    auto buffer = track->CreateReadBuffer();
    *audioBufferOut = buffer.release();
    return Result::kSuccess;
  }
  return Result::kInvalidNativeHandle;
}

#define LOG_INVALID_ARG_IF(...) \
  (__VA_ARGS__) && ((RTC_LOG_F(LS_ERROR) << "Invalid argument: " #__VA_ARGS__), true)

mrsResult MRS_CALL
mrsAudioTrackReadBufferRead(mrsAudioTrackReadBufferHandle buffer,
                            int sample_rate,
                            int num_channels,
                            mrsAudioTrackReadBufferPadBehavior pad_behavior,
                            float* samples_out,
                            int num_samples_max,
                            int* num_samples_read_out,
                            mrsBool* has_overrun_out) {
  if (!buffer) {
    return Result::kInvalidNativeHandle;
  }
  if (LOG_INVALID_ARG_IF(sample_rate <= 0)) {
    return Result::kInvalidParameter;
  }

  if (LOG_INVALID_ARG_IF(num_channels <= 0)) {
    return Result::kInvalidParameter;
  }

  if (LOG_INVALID_ARG_IF(!IsValidAudioTrackBufferPadBehavior(pad_behavior))) {
    return Result::kInvalidParameter;
  }

  if (LOG_INVALID_ARG_IF(num_samples_max < 0)) {
    return Result::kInvalidParameter;
  }

  if (LOG_INVALID_ARG_IF(num_samples_max > 0 && !samples_out)) {
    return Result::kInvalidParameter;
  }

  if (LOG_INVALID_ARG_IF(!num_samples_read_out)) {
    return Result::kInvalidParameter;
  }

  if(LOG_INVALID_ARG_IF(!has_overrun_out)) {
    return Result::kInvalidParameter;
  }

  auto stream = static_cast<AudioTrackReadBuffer*>(buffer);
  bool has_overrun;
  Result res = stream->Read(sample_rate, num_channels, pad_behavior, samples_out,
               num_samples_max, num_samples_read_out, &has_overrun);
  *has_overrun_out = has_overrun ? mrsBool::kTrue : mrsBool::kFalse;
  return res;
}

void MRS_CALL
mrsAudioTrackReadBufferDestroy(mrsAudioTrackReadBufferHandle buffer) {
  if (auto ars = static_cast<AudioTrackReadBuffer*>(buffer)) {
    delete ars;
  }
}
