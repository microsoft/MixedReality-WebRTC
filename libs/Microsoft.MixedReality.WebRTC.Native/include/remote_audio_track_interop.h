// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include "audio_frame_observer.h"
#include "export.h"
#include "interop_api.h"

extern "C" {

//
// Wrapper
//

/// Add a reference to the native object associated with the given handle.
MRS_API void MRS_CALL
mrsRemoteAudioTrackAddRef(mrsRemoteAudioTrackHandle handle) noexcept;

/// Remove a reference from the native object associated with the given handle.
MRS_API void MRS_CALL
mrsRemoteAudioTrackRemoveRef(mrsRemoteAudioTrackHandle handle) noexcept;

/// Register a custom callback to be called when the local audio track received
/// a frame.
MRS_API void MRS_CALL
mrsRemoteAudioTrackRegisterFrameCallback(mrsRemoteAudioTrackHandle trackHandle,
                                         mrsAudioFrameCallback callback,
                                         void* user_data) noexcept;

/// Enable or disable a remote audio track. Enabled tracks output their media
/// content as usual. Disabled tracks output some void media content (silent
/// audio frames). Enabling/disabling a track is a lightweight concept similar
/// to "mute", which does not require an SDP renegotiation.
MRS_API mrsResult MRS_CALL
mrsRemoteAudioTrackSetEnabled(mrsRemoteAudioTrackHandle track_handle,
                              mrsBool enabled) noexcept;

/// Query a local audio track for its enabled status.
MRS_API mrsBool MRS_CALL
mrsRemoteAudioTrackIsEnabled(mrsRemoteAudioTrackHandle track_handle) noexcept;

}  // extern "C"
