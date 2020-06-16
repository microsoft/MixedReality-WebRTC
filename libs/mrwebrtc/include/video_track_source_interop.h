// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include "interop_api.h"

extern "C" {

/// Register a custom callback to be called when the video track source produced
/// a frame. The produced frame is passed to the registered callback in I420
/// encoding.
MRS_API void MRS_CALL mrsVideoTrackSourceRegisterFrameCallback(
    mrsVideoTrackSourceHandle source_handle,
    mrsI420AVideoFrameCallback callback,
    void* user_data) noexcept;

/// Register a custom callback to be called when the video track source produced
/// a frame. The produced frame is passed to the registered callback in ARGB32
/// encoding.
MRS_API void MRS_CALL mrsVideoTrackSourceRegisterArgb32FrameCallback(
    mrsVideoTrackSourceHandle source_handle,
    mrsArgb32VideoFrameCallback callback,
    void* user_data) noexcept;

}  // extern "C"
