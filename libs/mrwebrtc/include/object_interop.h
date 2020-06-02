// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include "interop_api.h"

extern "C" {

//
// Object API
//
// Most MixedReality-WebRTC object types derive from a base object class which
// exposes a common set of functionalities via the below API. Those objects
// contain a name and some opaque user data storage.
//

/// Set the name of the object.
///
/// The name is stored internally and only used by the implementation for
/// logging, and in general is mainly intended for logging and debugging. Some
/// objects initialize their name with a relevant value related to the object
/// itself, others with a generic name related to the type of object.
/// |mrsObjectSetName()| overrides this default name.
///
/// |handle| - Handle to the native object.
/// |name| - A null-terminated UTF-8 encoded string.
MRS_API void MRS_CALL mrsObjectSetName(mrsObjectHandle handle,
                                       const char* name) noexcept;

/// Get the name to the object.
///
/// The caller must provide a buffer with a sufficent size to copy the name to,
/// including a null terminator character. The |buffer| argument points to the
/// raw buffer, and the |buffer_size| to the capacity of the buffer, in bytes.
/// On return, if the buffer has enough capacity for the name and its null
/// terminator, the name is copied to the buffer, and the actual buffer size
/// consumed (including null terminator) is returned in |buffer_size|. If not,
/// then the function returns |mrsResult::kBufferTooSmall|, and |buffer_size|
/// contains the total size that the buffer would need for the call to
/// succeed, such that the caller can retry with a buffer with that
/// capacity. The returned string is always null-terminated and UTF-8 encoded.
MRS_API mrsResult MRS_CALL mrsObjectGetName(mrsObjectHandle handle,
                                            char* buffer,
                                            uint64_t* buffer_size) noexcept;

/// Assign some opaque user data to the object. The implementation
/// will store the pointer in the object and not touch it. It
/// can be retrieved with |mrsObjectGetUserData()| at any point during
/// the object lifetime. This is not multithread-safe.
MRS_API void MRS_CALL mrsObjectSetUserData(mrsObjectHandle handle,
                                           void* user_data) noexcept;

/// Get the opaque user data pointer previously assigned to the object with
/// |mrsObjectSetUserData()|. If no value was previously assigned, return
/// |nullptr|. This is not multithread-safe.
MRS_API void* MRS_CALL mrsObjectGetUserData(mrsObjectHandle handle) noexcept;

}  // extern "C"
