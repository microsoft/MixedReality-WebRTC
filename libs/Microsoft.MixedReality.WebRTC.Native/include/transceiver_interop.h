// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include "export.h"
#include "interop_api.h"

extern "C" {

using mrsTransceiverStateUpdatedCallback =
    void(MRS_CALL*)(void* user_data,
                    mrsTransceiverStateUpdatedReason reason,
                    mrsTransceiverOptDirection negotiated_direction,
                    mrsTransceiverDirection desired_direction);

/// Add a reference to the native object associated with the given handle.
MRS_API void MRS_CALL
mrsTransceiverAddRef(mrsTransceiverHandle handle) noexcept;

/// Remove a reference from the native object associated with the given handle.
MRS_API void MRS_CALL
mrsTransceiverRemoveRef(mrsTransceiverHandle handle) noexcept;

MRS_API void MRS_CALL mrsTransceiverRegisterStateUpdatedCallback(
    mrsTransceiverHandle handle,
    mrsTransceiverStateUpdatedCallback callback,
    void* user_data) noexcept;

/// Set the new desired transceiver direction.
MRS_API mrsResult MRS_CALL
mrsTransceiverSetDirection(mrsTransceiverHandle transceiver_handle,
                           mrsTransceiverDirection new_direction) noexcept;

/// Set the local audio track associated with this transceiver. This new track
/// replaces the existing one, if any. This doesn't require any SDP
/// renegotiation. This fails if the transceiver is a video transceiver.
MRS_API mrsResult MRS_CALL mrsTransceiverSetLocalAudioTrack(
    mrsTransceiverHandle transceiver_handle,
    mrsLocalAudioTrackHandle track_handle) noexcept;

/// Set the local video track associated with this transceiver. This new track
/// replaces the existing one, if any. This doesn't require any SDP
/// renegotiation. This fails if the transceiver is an audio transceiver.
MRS_API mrsResult MRS_CALL mrsTransceiverSetLocalVideoTrack(
    mrsTransceiverHandle transceiver_handle,
    mrsLocalVideoTrackHandle track_handle) noexcept;

/// Get the local audio track associated with this transceiver, if any. This
/// fails if the transceiver is a video transceiver.
MRS_API mrsResult MRS_CALL mrsTransceiverGetLocalAudioTrack(
    mrsTransceiverHandle transceiver_handle,
    mrsLocalAudioTrackHandle* track_handle_out) noexcept;

/// Get the local video track associated with this transceiver, if any. This
/// fails if the transceiver is an audio transceiver.
MRS_API mrsResult MRS_CALL mrsTransceiverGetLocalVideoTrack(
    mrsTransceiverHandle transceiver_handle,
    mrsLocalVideoTrackHandle* track_handle_out) noexcept;

/// Get the remote audio track associated with this transceiver, if any. This
/// fails if the transceiver is a video transceiver.
MRS_API mrsResult MRS_CALL mrsTransceiverGetRemoteAudioTrack(
    mrsTransceiverHandle transceiver_handle,
    mrsRemoteAudioTrackHandle* track_handle_out) noexcept;

/// Get the remote video track associated with this transceiver, if any. This
/// fails if the transceiver is an audio transceiver.
MRS_API mrsResult MRS_CALL mrsTransceiverGetRemoteVideoTrack(
    mrsTransceiverHandle transceiver_handle,
    mrsRemoteVideoTrackHandle* track_handle_out) noexcept;

}  // extern "C"
