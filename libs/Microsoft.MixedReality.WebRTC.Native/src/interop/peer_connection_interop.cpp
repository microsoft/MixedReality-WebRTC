// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// This is a precompiled header, it must be on its own, followed by a blank
// line, to prevent clang-format from reordering it with other headers.
#include "pch.h"

#include "interop/global_factory.h"
#include "peer_connection.h"
#include "peer_connection_interop.h"

using namespace Microsoft::MixedReality::WebRTC;

void MRS_CALL mrsPeerConnectionAddRef(PeerConnectionHandle handle) noexcept {
  if (auto peer = static_cast<PeerConnection*>(handle)) {
    peer->AddRef();
  } else {
    RTC_LOG(LS_WARNING)
        << "Trying to add reference to NULL PeerConnection object.";
  }
}

void MRS_CALL mrsPeerConnectionRemoveRef(PeerConnectionHandle handle) noexcept {
  if (auto peer = static_cast<PeerConnection*>(handle)) {
    peer->RemoveRef();
  } else {
    RTC_LOG(LS_WARNING)
        << "Trying to remove reference from NULL PeerConnection object.";
  }
}

void MRS_CALL mrsPeerConnectionRegisterIceGatheringStateChangedCallback(
    PeerConnectionHandle peer_handle,
    mrsPeerConnectionIceGatheringStateChangedCallback callback,
    void* user_data) noexcept {
  if (auto peer = static_cast<PeerConnection*>(peer_handle)) {
    peer->RegisterIceGatheringStateChangedCallback(
        Callback<IceGatheringState>{callback, user_data});
  }
}

mrsResult MRS_CALL
mrsPeerConnectionAddAudioTransceiver(PeerConnectionHandle peer_handle,
                                     const AudioTransceiverInitConfig* config,
                                     AudioTransceiverHandle* handle) noexcept {
  if (!handle || !config) {
    return Result::kInvalidParameter;
  }
  *handle = nullptr;
  if (auto peer = static_cast<PeerConnection*>(peer_handle)) {
    ErrorOr<RefPtr<AudioTransceiver>> audio_transceiver =
        peer->AddAudioTransceiver(*config);
    if (audio_transceiver.ok()) {
      *handle = (AudioTransceiverHandle)audio_transceiver.value().release();
      return Result::kSuccess;
    }
    return audio_transceiver.error().result();
  }
  return Result::kInvalidNativeHandle;
}

mrsResult MRS_CALL
mrsPeerConnectionAddVideoTransceiver(PeerConnectionHandle peer_handle,
                                     const VideoTransceiverInitConfig* config,
                                     VideoTransceiverHandle* handle) noexcept {
  if (!handle || !config) {
    return Result::kInvalidParameter;
  }
  *handle = nullptr;
  if (auto peer = static_cast<PeerConnection*>(peer_handle)) {
    ErrorOr<RefPtr<VideoTransceiver>> video_transceiver =
        peer->AddVideoTransceiver(*config);
    if (video_transceiver.ok()) {
      *handle = (AudioTransceiverHandle)video_transceiver.value().release();
      return Result::kSuccess;
    }
    return video_transceiver.error().result();
  }
  return Result::kInvalidNativeHandle;
}
