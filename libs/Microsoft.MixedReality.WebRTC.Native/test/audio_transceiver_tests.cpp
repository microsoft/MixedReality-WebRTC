// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include "interop/audio_transceiver_interop.h"
#include "interop_api.h"
#include "interop/local_audio_track_interop.h"
#include "interop/remote_audio_track_interop.h"

#include "simple_interop.h"

namespace {

const mrsPeerConnectionInteropHandle kFakeInteropPeerConnectionHandle =
    (void*)0x1;

const mrsRemoteAudioTrackInteropHandle kFakeInteropRemoteAudioTrackHandle =
    (void*)0x2;

const mrsAudioTransceiverInteropHandle kFakeInteropAudioTransceiverHandle =
    (void*)0x3;

/// Fake interop callback always returning the same fake remote audio track
/// interop handle, for tests which do not care about it.
mrsRemoteAudioTrackInteropHandle MRS_CALL FakeIterop_RemoteAudioTrackCreate(
    mrsPeerConnectionInteropHandle /*parent*/,
    const mrsRemoteAudioTrackConfig& /*config*/) noexcept {
  return kFakeInteropRemoteAudioTrackHandle;
}

// PeerConnectionAudioTrackAddedCallback
using AudioTrackAddedCallback =
    InteropCallback<mrsRemoteAudioTrackInteropHandle,
                    RemoteAudioTrackHandle,
                    mrsAudioTransceiverInteropHandle,
                    AudioTransceiverHandle>;

// PeerConnectionI420AudioFrameCallback
using I420AudioFrameCallback = InteropCallback<const AudioFrame&>;

}  // namespace

TEST(AudioTransceiver, InvalidName) {
  LocalPeerPairRaii pair;
  AudioTransceiverHandle transceiver_handle1{};
  AudioTransceiverInitConfig transceiver_config{};
  transceiver_config.name = "invalid name with space";
  ASSERT_EQ(Result::kInvalidParameter,
            mrsPeerConnectionAddAudioTransceiver(
                pair.pc1(), &transceiver_config, &transceiver_handle1));
  ASSERT_EQ(nullptr, transceiver_handle1);
}

TEST(AudioTransceiver, SetDirection) {
  LocalPeerPairRaii pair;

  // In order to allow creating interop wrappers from native code, register the
  // necessary interop callbacks.
  mrsPeerConnectionInteropCallbacks interop{};
  interop.remote_audio_track_create_object = &FakeIterop_RemoteAudioTrackCreate;
  ASSERT_EQ(Result::kSuccess,
            mrsPeerConnectionRegisterInteropCallbacks(pair.pc1(), &interop));
  ASSERT_EQ(Result::kSuccess,
            mrsPeerConnectionRegisterInteropCallbacks(pair.pc2(), &interop));

  // Register event for renegotiation needed
  Event renegotiation_needed1_ev;
  InteropCallback renegotiation_needed1_cb = [&renegotiation_needed1_ev]() {
    renegotiation_needed1_ev.Set();
  };
  mrsPeerConnectionRegisterRenegotiationNeededCallback(
      pair.pc1(), CB(renegotiation_needed1_cb));
  Event renegotiation_needed2_ev;
  InteropCallback renegotiation_needed2_cb = [&renegotiation_needed2_ev]() {
    renegotiation_needed2_ev.Set();
  };
  mrsPeerConnectionRegisterRenegotiationNeededCallback(
      pair.pc2(), CB(renegotiation_needed2_cb));

  // Add a transceiver to the local peer (#1)
  AudioTransceiverHandle transceiver_handle1{};
  {
    AudioTransceiverInitConfig transceiver_config{};
    transceiver_config.name = "audio_transceiver_1";
    transceiver_config.transceiver_interop_handle =
        kFakeInteropAudioTransceiverHandle;
    renegotiation_needed1_ev.Reset();
    ASSERT_EQ(Result::kSuccess,
              mrsPeerConnectionAddAudioTransceiver(
                  pair.pc1(), &transceiver_config, &transceiver_handle1));
    ASSERT_NE(nullptr, transceiver_handle1);
    ASSERT_TRUE(renegotiation_needed1_ev.IsSignaled());
    renegotiation_needed1_ev.Reset();
  }

  // Register event for transceiver state update
  Event state_updated1_ev_local;
  Event state_updated1_ev_remote;
  Event state_updated1_ev_setdir;
  mrsTransceiverDirection dir_desired1 = mrsTransceiverDirection::kInactive;
  mrsTransceiverOptDirection dir_negotiated1 =
      mrsTransceiverOptDirection::kNotSet;
  InteropCallback<mrsTransceiverStateUpdatedReason, mrsTransceiverOptDirection,
                  mrsTransceiverDirection>
      state_updated1_cb = [&](mrsTransceiverStateUpdatedReason reason,
                              mrsTransceiverOptDirection negotiated,
                              mrsTransceiverDirection desired) {
        dir_negotiated1 = negotiated;
        dir_desired1 = desired;
        switch (reason) {
          case mrsTransceiverStateUpdatedReason::kLocalDesc:
            state_updated1_ev_local.Set();
            break;
          case mrsTransceiverStateUpdatedReason::kRemoteDesc:
            state_updated1_ev_remote.Set();
            break;
          case mrsTransceiverStateUpdatedReason::kSetDirection:
            state_updated1_ev_setdir.Set();
            break;
        }
      };
  mrsAudioTransceiverRegisterStateUpdatedCallback(transceiver_handle1,
                                                  CB(state_updated1_cb));

  // Check audio transceiver #1 consistency
  {
    // Default values inchanged (callback was just registered)
    ASSERT_EQ(mrsTransceiverOptDirection::kNotSet, dir_negotiated1);
    ASSERT_EQ(mrsTransceiverDirection::kInactive, dir_desired1);

    // Local audio track is NULL
    LocalAudioTrackHandle track_handle_local{};
    ASSERT_EQ(Result::kSuccess, mrsAudioTransceiverGetLocalTrack(
                                    transceiver_handle1, &track_handle_local));
    ASSERT_EQ(nullptr, track_handle_local);

    // Remote audio track is NULL
    RemoteAudioTrackHandle track_handle_remote{};
    ASSERT_EQ(Result::kSuccess, mrsAudioTransceiverGetRemoteTrack(
                                    transceiver_handle1, &track_handle_remote));
    ASSERT_EQ(nullptr, track_handle_remote);
  }

  // Connect #1 and #2
  pair.ConnectAndWait();

  // Because the state updated event handler is registered after the transceiver
  // is created, the state is stale, and applying the local description during
  // |CreateOffer()| will generate some event.
  ASSERT_TRUE(state_updated1_ev_local.WaitFor(10s));
  state_updated1_ev_local.Reset();

  // Wait for transceiver to be updated; this happens *after* connect,
  // during SetRemoteDescription().
  ASSERT_TRUE(state_updated1_ev_remote.WaitFor(10s));
  state_updated1_ev_remote.Reset();

  // Check audio transceiver #1 consistency
  {
    // Desired state is Send+Receive, negotiated is Send only because the remote
    // peer refused to send (no track added for that).
    ASSERT_EQ(mrsTransceiverOptDirection::kSendOnly, dir_negotiated1);
    ASSERT_EQ(mrsTransceiverDirection::kSendRecv, dir_desired1);
  }

  // Set transceiver #1 direction to Receive
  ASSERT_EQ(Result::kSuccess,
            mrsAudioTransceiverSetDirection(
                transceiver_handle1, mrsTransceiverDirection::kRecvOnly));
  ASSERT_TRUE(state_updated1_ev_setdir.IsSignaled());
  state_updated1_ev_setdir.Reset();

  // Check audio transceiver #1 consistency
  {
    // Desired state is Receive, negotiated is still Send+Receive
    ASSERT_EQ(mrsTransceiverOptDirection::kSendOnly,
              dir_negotiated1);  // no change
    ASSERT_EQ(mrsTransceiverDirection::kRecvOnly, dir_desired1);
  }

  // Renegotiate
  pair.ConnectAndWait();

  // Wait for transceiver to be updated; this happens *after* connect, during
  // SetRemoteDescription()
  // Note: here the local description doesn't generate a state updated event
  // because the local state was set with SetDirection() so is already correct.
  // When the peer is creating the offer (#1), the desired direction is exactly
  // the one advertized in the local description.
  ASSERT_FALSE(state_updated1_ev_local.IsSignaled());
  ASSERT_TRUE(state_updated1_ev_remote.WaitFor(10s));
  state_updated1_ev_remote.Reset();

  // Check audio transceiver #1 consistency
  {
    // Desired state is Receive, negotiated is Inactive because remote peer
    // refused to send (no track added for that).
    ASSERT_EQ(mrsTransceiverOptDirection::kInactive, dir_negotiated1);
    ASSERT_EQ(mrsTransceiverDirection::kRecvOnly, dir_desired1);
  }

  // Clean-up
  mrsAudioTransceiverRemoveRef(transceiver_handle1);
}

TEST(AudioTransceiver, SetDirection_InvalidHandle) {
  ASSERT_EQ(Result::kInvalidNativeHandle,
            mrsAudioTransceiverSetDirection(
                nullptr, mrsTransceiverDirection::kRecvOnly));
}

TEST(AudioTransceiver, SetLocalTrack_InvalidHandle) {
  LocalAudioTrackHandle dummy = (void*)0x1;  // looks legit
  ASSERT_EQ(Result::kInvalidNativeHandle,
            mrsAudioTransceiverSetLocalTrack(nullptr, dummy));
}
