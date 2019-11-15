// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include "export.h"
#include "interop/interop_api.h"

extern "C" {

//
// Wrapper
//

/// Add a reference to the native object associated with the given handle.
MRS_API void MRS_CALL mrsPeerConnectionAddRef(PeerConnectionHandle handle);

/// Remove a reference from the native object associated with the given handle.
MRS_API void MRS_CALL mrsPeerConnectionRemoveRef(PeerConnectionHandle handle);

}  // extern "C"
