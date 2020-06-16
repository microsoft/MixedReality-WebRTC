// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include "interop_api.h"

extern "C" {

/// Configuration for opening a local audio capture device (microphone) as an
/// audio track source.
struct mrsLocalAudioDeviceInitConfig {
  /// Enable auto gain control (AGC).
  mrsOptBool auto_gain_control_{mrsOptBool::kUnset};
};


/// Create an audio track source by opening a local audio capture device
/// (microphone).
MRS_API mrsResult MRS_CALL mrsAudioTrackSourceCreateFromDevice(
    const mrsLocalAudioDeviceInitConfig* init_config,
    mrsAudioTrackSourceHandle* source_handle_out) noexcept;

/// Register a custom callback to be called when the audio track source produced
/// a frame.
/// WARNING: The default platform source internal implementation currently does
/// not hook those callbacks, and therefore the callback will never be called.
/// This is a limitation of the underlying implementation.
/// See https://bugs.chromium.org/p/webrtc/issues/detail?id=11602
//MRS_API void MRS_CALL mrsAudioTrackSourceRegisterFrameCallback(
//    mrsAudioTrackSourceHandle source_handle,
//    mrsAudioFrameCallback callback,
//    void* user_data) noexcept;

}  // extern "C"
