// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include "interop_api.h"

extern "C" {

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
