// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include "export.h"
#include "interop_api.h"

extern "C" {

//
// Wrapper
//

/// Add a reference to the native object associated with the given handle.
MRS_API void MRS_CALL
mrsRemoteVideoTrackAddRef(mrsRemoteVideoTrackHandle handle) noexcept;

/// Remove a reference from the native object associated with the given handle.
MRS_API void MRS_CALL
mrsRemoteVideoTrackRemoveRef(mrsRemoteVideoTrackHandle handle) noexcept;

/// Register a custom callback to be called when the remote video track received
/// a frame. The received frames is passed to the registered callback in I420
/// encoding.
MRS_API void MRS_CALL mrsRemoteVideoTrackRegisterI420AFrameCallback(
    mrsRemoteVideoTrackHandle trackHandle,
    mrsI420AVideoFrameCallback callback,
    void* user_data) noexcept;

/// Register a custom callback to be called when the remote video track received
/// a frame. The received frames is passed to the registered callback in ARGB32
/// encoding.
MRS_API void MRS_CALL mrsRemoteVideoTrackRegisterArgb32FrameCallback(
    mrsRemoteVideoTrackHandle trackHandle,
    mrsArgb32VideoFrameCallback callback,
    void* user_data) noexcept;

/// Enable or disable a remote video track. Enabled tracks output their media
/// content as usual. Disabled track output some void media content (black video
/// frames, silent audio frames). Enabling/disabling a track is a lightweight
/// concept similar to "mute", which does not require an SDP renegotiation.
MRS_API mrsResult MRS_CALL
mrsRemoteVideoTrackSetEnabled(mrsRemoteVideoTrackHandle track_handle,
                              mrsBool enabled) noexcept;

/// Query a local video track for its enabled status.
MRS_API mrsBool MRS_CALL
mrsRemoteVideoTrackIsEnabled(mrsRemoteVideoTrackHandle track_handle) noexcept;

}  // extern "C"
