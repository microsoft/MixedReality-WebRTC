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

/// Information provided to the TransceiverAdded event handler about a
/// transceiver newly created as a result of applying a remote description on
/// the local peer connection, and newly added to that peer connection.
struct mrsTransceiverAddedInfo {
  /// Handle of the newly-created transceiver.
  mrsTransceiverHandle transceiver_handle{nullptr};

  /// Name of the newly-added transceiver.
  const char* transceiver_name{nullptr};

  /// Media kind of the newly-create transceiver.
  mrsMediaKind media_kind{(mrsMediaKind)-1};

  /// Media line index of the transceiver in the peer connection.
  int mline_index{-1};

  /// Encoded stream IDs; a semi-colon separated list of media stream IDs
  /// associated with the transceiver.
  const char* encoded_stream_ids_{nullptr};

  /// Initial value of the desired transceiver direction.
  mrsTransceiverDirection desired_direction{mrsTransceiverDirection::kInactive};
};

/// Callback invoked when a transceiver is added to the peer connection as a
/// result of a remote description being applied.
using mrsPeerConnectionTransceiverAddedCallback =
    void(MRS_CALL*)(void* user_data, const mrsTransceiverAddedInfo* info);

/// Register a callback invoked when a new transceiver is added to the peer
/// connection as a result of applying a remote description from a remote peer.
MRS_API void MRS_CALL mrsPeerConnectionRegisterTransceiverAddedCallback(
    mrsPeerConnectionHandle peer_handle,
    mrsPeerConnectionTransceiverAddedCallback callback,
    void* user_data) noexcept;

/// Callback invoked when the state of the ICE connection changed.
using mrsPeerConnectionIceGatheringStateChangedCallback =
    void(MRS_CALL*)(void* user_data, mrsIceGatheringState new_state);

/// Register a callback invoked when the ICE connection state changes.
MRS_API void MRS_CALL mrsPeerConnectionRegisterIceGatheringStateChangedCallback(
    mrsPeerConnectionHandle peer_handle,
    mrsPeerConnectionIceGatheringStateChangedCallback callback,
    void* user_data) noexcept;

/// Create a new transceiver attached to the given peer connection.
MRS_API mrsResult MRS_CALL
mrsPeerConnectionAddTransceiver(mrsPeerConnectionHandle peer_handle,
                                const mrsTransceiverInitConfig* config,
                                mrsTransceiverHandle* handle) noexcept;

/// Experimental. Render or not remote audio tracks from a peer connection on
/// the system audio device.
///
/// The default behavior is for every remote audio frame to be passed to
/// remote audio frame callbacks, as well as rendered automatically on the
/// system audio device. If `false` is passed to this function, remote audio
/// frames will still be received and passed to callbacks, but won't be rendered
/// on the system device.
///
/// Changing the default behavior is not supported on UWP.
MRS_API mrsResult MRS_CALL
mrsPeerConnectionRenderRemoteAudio(mrsPeerConnectionHandle peerHandle,
                                   bool render);
MRS_API mrsResult MRS_CALL
mrsAudioTrackReadBufferCreate(mrsPeerConnectionHandle peerHandle,
                              int bufferMs,
                              AudioTrackReadBufferHandle* readStreamOut);

MRS_API mrsResult MRS_CALL
mrsAudioTrackReadBufferRead(AudioTrackReadBufferHandle readStream,
                            int sampleRate,
                            float data[],
                            int dataLen,
                            int numChannels);

MRS_API void MRS_CALL
mrsAudioTrackReadBufferDestroy(AudioTrackReadBufferHandle readStream);

}  // extern "C"
