// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include "interop_api.h"

extern "C" {

/// Configuration for creating a local video track.
struct mrsLocalVideoTrackInitSettings {
  /// Track name. This must be a valid SDP token (see |mrsSdpIsValidToken()|),
  /// or |nullptr| to let the implementation generate a valid unique track name.
  const char* track_name{};
};

/// Create a new local video track from a video track source.
MRS_API mrsResult MRS_CALL mrsLocalVideoTrackCreateFromSource(
    const mrsLocalVideoTrackInitSettings* init_settings,
    mrsVideoTrackSourceHandle source_handle,
    mrsLocalVideoTrackHandle* track_handle_out) noexcept;

/// Register a custom callback to be called when the local video track captured
/// a frame. The captured frames is passed to the registered callback in I420
/// encoding.
MRS_API void MRS_CALL mrsLocalVideoTrackRegisterI420AFrameCallback(
    mrsLocalVideoTrackHandle trackHandle,
    mrsI420AVideoFrameCallback callback,
    void* user_data) noexcept;

/// Register a custom callback to be called when the local video track captured
/// a frame. The captured frames is passed to the registered callback in ARGB32
/// encoding.
MRS_API void MRS_CALL mrsLocalVideoTrackRegisterArgb32FrameCallback(
    mrsLocalVideoTrackHandle trackHandle,
    mrsArgb32VideoFrameCallback callback,
    void* user_data) noexcept;

/// Enable or disable a local video track. Enabled tracks output their media
/// content as usual. Disabled track output some void media content (black video
/// frames, silent audio frames). Enabling/disabling a track is a lightweight
/// concept similar to "mute", which does not require an SDP renegotiation.
MRS_API mrsResult MRS_CALL
mrsLocalVideoTrackSetEnabled(mrsLocalVideoTrackHandle track_handle,
                             mrsBool enabled) noexcept;

/// Query a local video track for its enabled status.
MRS_API mrsBool MRS_CALL
mrsLocalVideoTrackIsEnabled(mrsLocalVideoTrackHandle track_handle) noexcept;

}  // extern "C"
