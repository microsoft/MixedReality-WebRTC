// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include "export.h"
#include "interop_api.h"

extern "C" {

/// Assign some opaque user data to the remote video track. The implementation
/// will store the pointer in the remote video track object and not touch it. It
/// can be retrieved with |mrsRemoteVideoTrackGetUserData()| at any point during
/// the remote video track lifetime. This is not multithread-safe.
MRS_API void MRS_CALL
mrsRemoteVideoTrackSetUserData(mrsRemoteVideoTrackHandle handle,
                               void* user_data) noexcept;

/// Get the opaque user data pointer previously assigned to the remote video
/// track with |mrsRemoteVideoTrackSetUserData()|. If no value was previously
/// assigned, return |nullptr|. This is not multithread-safe.
MRS_API void* MRS_CALL
mrsRemoteVideoTrackGetUserData(mrsRemoteVideoTrackHandle handle) noexcept;

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
/// content as usual. Disabled tracks output some void media content (black
/// video frames, silent audio frames). Enabling/disabling a track is a
/// lightweight concept similar to "mute", which does not require an SDP
/// renegotiation.
MRS_API mrsResult MRS_CALL
mrsRemoteVideoTrackSetEnabled(mrsRemoteVideoTrackHandle track_handle,
                              mrsBool enabled) noexcept;

/// Query a local video track for its enabled status.
MRS_API mrsBool MRS_CALL
mrsRemoteVideoTrackIsEnabled(mrsRemoteVideoTrackHandle track_handle) noexcept;

}  // extern "C"
