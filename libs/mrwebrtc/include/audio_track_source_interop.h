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

/// Add a reference to the native object associated with the given handle.
MRS_API void MRS_CALL
mrsAudioTrackSourceAddRef(mrsAudioTrackSourceHandle handle) noexcept;

/// Remove a reference from the native object associated with the given handle.
MRS_API void MRS_CALL
mrsAudioTrackSourceRemoveRef(mrsAudioTrackSourceHandle handle) noexcept;

/// Assign some name to the track source, for logging and debugging.
MRS_API void MRS_CALL
mrsAudioTrackSourceSetName(mrsAudioTrackSourceHandle handle,
                           const char* name) noexcept;

/// Get the name to the track source. The caller must provide a buffer with a
/// sufficent size to copy the name to, including a null terminator character.
/// The |buffer| argument points to the raw buffer, and the |buffer_size| to the
/// capacity of the buffer, in bytes. On return, if the buffer has enough
/// capacity for the name and its null terminator, the name is copied to the
/// buffer, and the actual buffer size consumed (including null terminator) is
/// returned in |buffer_size|. If not, then the function returns
/// |mrsResult::kBufferTooSmall|, and |buffer_size| contains the total size that
/// the buffer would need for the call to succeed, such that the caller can
/// retry with a buffer with that capacity.
MRS_API mrsResult MRS_CALL
mrsAudioTrackSourceGetName(mrsAudioTrackSourceHandle handle,
                           char* buffer,
                           uint64_t* buffer_size) noexcept;

/// Assign some opaque user data to the audio track source. The implementation
/// will store the pointer in the audio track source object and not touch it. It
/// can be retrieved with |mrsAudioTrackSourceGetUserData()| at any point during
/// the object lifetime. This is not multithread-safe.
MRS_API void MRS_CALL
mrsAudioTrackSourceSetUserData(mrsAudioTrackSourceHandle handle,
                               void* user_data) noexcept;

/// Get the opaque user data pointer previously assigned to the audio track
/// source with |mrsAudioTrackSourceSetUserData()|. If no value was previously
/// assigned, return |nullptr|. This is not multithread-safe.
MRS_API void* MRS_CALL
mrsAudioTrackSourceGetUserData(mrsAudioTrackSourceHandle handle) noexcept;

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
