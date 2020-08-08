// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include "interop_api.h"

extern "C" {

//
// Reference-counted object API
//
// Reference-counted objects are a subset of the base objects for which a
// reference count is maintained. These are objects created by the user and not
// owned by another object. On creation they hold a single reference; the user
// must call |mrsRefCountedObjectRemoveRef()| to remove that reference and
// destroy the object once it is not needed anymore. Additional references can
// be added with |mrsRefCountedObjectAddRef()|, and must removed in the same
// way.
//

/// Add a reference to the native object associated with the given handle.
///
/// Objects are constructed with a single reference. More references can be
/// added, but the caller must ensure they are removed once unused.
MRS_API void MRS_CALL
mrsRefCountedObjectAddRef(mrsRefCountedObjectHandle handle) noexcept;

/// Remove a reference from the native object associated with the given handle.
/// If this is the last reference, the object is destroyed and the handle
/// becomes invalid and should not be used again.
MRS_API void MRS_CALL
mrsRefCountedObjectRemoveRef(mrsRefCountedObjectHandle handle) noexcept;

}  // extern "C"
