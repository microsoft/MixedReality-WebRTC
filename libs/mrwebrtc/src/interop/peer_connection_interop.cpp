// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// This is a precompiled header, it must be on its own, followed by a blank
// line, to prevent clang-format from reordering it with other headers.
#include "pch.h"

#include "interop/global_factory.h"
#include "media/audio_track_read_buffer.h"
#include "media/transceiver.h"
#include "peer_connection.h"
#include "peer_connection_interop.h"

using namespace Microsoft::MixedReality::WebRTC;

void MRS_CALL mrsPeerConnectionRegisterTransceiverAddedCallback(
    mrsPeerConnectionHandle peer_handle,
    mrsPeerConnectionTransceiverAddedCallback callback,
    void* user_data) noexcept {
  if (auto peer = static_cast<PeerConnection*>(peer_handle)) {
    peer->RegisterTransceiverAddedCallback(
        Callback<const mrsTransceiverAddedInfo*>{callback, user_data});
  }
}

void MRS_CALL mrsPeerConnectionRegisterIceGatheringStateChangedCallback(
    mrsPeerConnectionHandle peer_handle,
    mrsPeerConnectionIceGatheringStateChangedCallback callback,
    void* user_data) noexcept {
  if (auto peer = static_cast<PeerConnection*>(peer_handle)) {
    peer->RegisterIceGatheringStateChangedCallback(
        Callback<mrsIceGatheringState>{callback, user_data});
  }
}

mrsResult MRS_CALL
mrsPeerConnectionAddTransceiver(mrsPeerConnectionHandle peer_handle,
                                const mrsTransceiverInitConfig* config,
                                mrsTransceiverHandle* handle) noexcept {
  if (!handle || !config) {
    return Result::kInvalidParameter;
  }
  *handle = nullptr;
  if (auto peer = static_cast<PeerConnection*>(peer_handle)) {
    ErrorOr<Transceiver*> result = peer->AddTransceiver(*config);
    if (result.ok()) {
      *handle = result.value()->GetHandle();
      return Result::kSuccess;
    }
    return result.error().result();
  }
  return Result::kInvalidNativeHandle;
}
