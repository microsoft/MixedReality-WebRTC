// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include "interop_api.h"

extern "C" {

//
// Wrapper
//

/// Add a reference to the native object associated with the given handle.
MRS_API void MRS_CALL
mrsPeerConnectionAddRef(mrsPeerConnectionHandle handle) noexcept;

/// Remove a reference from the native object associated with the given handle.
MRS_API void MRS_CALL
mrsPeerConnectionRemoveRef(mrsPeerConnectionHandle handle) noexcept;

/// Callback fired when the state of the ICE connection changed.
using mrsPeerConnectionIceGatheringStateChangedCallback =
    void(MRS_CALL*)(void* user_data, mrsIceGatheringState new_state);

/// Register a callback fired when the ICE connection state changes.
MRS_API void MRS_CALL mrsPeerConnectionRegisterIceGatheringStateChangedCallback(
    mrsPeerConnectionHandle peer_handle,
    mrsPeerConnectionIceGatheringStateChangedCallback callback,
    void* user_data) noexcept;

/// Create a new transceiver attached to the given peer connection.
MRS_API mrsResult MRS_CALL
mrsPeerConnectionAddTransceiver(mrsPeerConnectionHandle peer_handle,
                                const mrsTransceiverInitConfig* config,
                                mrsTransceiverHandle* handle) noexcept;

}  // extern "C"
