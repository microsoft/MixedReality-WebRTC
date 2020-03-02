// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include "audio_frame_observer.h"
#include "export.h"
#include "interop_api.h"

extern "C" {

/// Configuration for opening a local audio capture device and creating a local
/// audio track.
struct mrsLocalAudioTrackInitConfig {
  /// Handle of the local audio track interop wrapper, if any, which will be
  /// associated with the native local audio track object.
  mrsLocalAudioTrackInteropHandle track_interop_handle{};
};

/// Add a reference to the native object associated with the given handle.
MRS_API void MRS_CALL
mrsLocalAudioTrackAddRef(mrsLocalAudioTrackHandle handle) noexcept;

/// Remove a reference from the native object associated with the given handle.
MRS_API void MRS_CALL
mrsLocalAudioTrackRemoveRef(mrsLocalAudioTrackHandle handle) noexcept;

/// Create a new local audio track by opening a local audio capture device
/// (webcam).
MRS_API mrsResult MRS_CALL mrsLocalAudioTrackCreateFromDevice(
    const mrsLocalAudioTrackInitConfig* config,
    const char* track_name,
    mrsLocalAudioTrackHandle* track_handle_out) noexcept;

/// Register a custom callback to be called when the local audio track captured
/// a frame.
MRS_API void MRS_CALL
mrsLocalAudioTrackRegisterFrameCallback(mrsLocalAudioTrackHandle trackHandle,
                                        mrsAudioFrameCallback callback,
                                        void* user_data) noexcept;

/// Enable or disable a local audio track. Enabled tracks output their media
/// content as usual. Disabled track output some void media content (silent
/// audio frames). Enabling/disabling a track is a lightweight concept similar
/// to "mute", which does not require an SDP renegotiation.
MRS_API mrsResult MRS_CALL
mrsLocalAudioTrackSetEnabled(mrsLocalAudioTrackHandle track_handle,
                             mrsBool enabled) noexcept;

/// Query a local audio track for its enabled status.
MRS_API mrsBool MRS_CALL
mrsLocalAudioTrackIsEnabled(mrsLocalAudioTrackHandle track_handle) noexcept;

}  // extern "C"
