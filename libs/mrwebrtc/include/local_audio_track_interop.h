// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include "audio_frame_observer.h"
#include "audio_track_source_interop.h"
#include "export.h"
#include "interop_api.h"

extern "C" {

/// Configuration for creating a local audio track.
struct mrsLocalAudioTrackInitSettings {
  /// Track name. This must be a valid SDP token (see |mrsSdpTokenIsValid()|), or
  /// |nullptr| to let the implementation generate a valid unique track name.
  const char* track_name{};
};

/// Add a reference to the native object associated with the given handle.
MRS_API void MRS_CALL
mrsLocalAudioTrackAddRef(mrsLocalAudioTrackHandle handle) noexcept;

/// Remove a reference from the native object associated with the given handle.
MRS_API void MRS_CALL
mrsLocalAudioTrackRemoveRef(mrsLocalAudioTrackHandle handle) noexcept;

/// Create a new local audio track from an audio track source.
MRS_API mrsResult MRS_CALL mrsLocalAudioTrackCreateFromSource(
    const mrsLocalAudioTrackInitSettings* init_settings,
    mrsAudioTrackSourceHandle source_handle,
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
