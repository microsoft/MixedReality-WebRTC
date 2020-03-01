// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include "external_video_track_source_interop.h"
#include "interop_api.h"
#include "local_video_track_interop.h"
#include "remote_video_track_interop.h"
#include "video_transceiver_interop.h"

#include "simple_interop.h"
#include "video_test_utils.h"

namespace {

class VideoTransceiverTests : public TestUtils::TestBase,
                              public testing::WithParamInterface<SdpSemantic> {
};

const mrsPeerConnectionInteropHandle kFakeInteropPeerConnectionHandle =
    (void*)0x1;

const mrsRemoteVideoTrackInteropHandle kFakeInteropRemoteVideoTrackHandle =
    (void*)0x2;

const mrsVideoTransceiverInteropHandle kFakeInteropVideoTransceiverHandle =
    (void*)0x3;

/// Fake interop callback always returning the same fake remote video track
/// interop handle, for tests which do not care about it.
mrsRemoteVideoTrackInteropHandle MRS_CALL FakeIterop_RemoteVideoTrackCreate(
    mrsPeerConnectionInteropHandle /*parent*/,
    const mrsRemoteVideoTrackConfig& /*config*/) noexcept {
  return kFakeInteropRemoteVideoTrackHandle;
}

struct FakeInteropRaii {
  FakeInteropRaii(std::initializer_list<mrsPeerConnectionHandle> handles)
      : handles_(handles) {
    setup();
  }
  ~FakeInteropRaii() { cleanup(); }
  void setup() {
    mrsPeerConnectionInteropCallbacks interop{};
    interop.remote_video_track_create_object =
        &FakeIterop_RemoteVideoTrackCreate;
    for (auto&& h : handles_) {
      ASSERT_EQ(Result::kSuccess,
                mrsPeerConnectionRegisterInteropCallbacks(h, &interop));
    }
  }
  void cleanup() {}
  std::vector<mrsPeerConnectionHandle> handles_;
};

// PeerConnectionVideoTrackAddedCallback
using VideoTrackAddedCallback =
    InteropCallback<mrsRemoteVideoTrackInteropHandle,
                    mrsRemoteVideoTrackHandle,
                    mrsVideoTransceiverInteropHandle,
                    mrsVideoTransceiverHandle>;

// PeerConnectionI420VideoFrameCallback
using I420VideoFrameCallback = InteropCallback<const I420AVideoFrame&>;

}  // namespace

INSTANTIATE_TEST_CASE_P(,
                        VideoTransceiverTests,
                        testing::ValuesIn(TestUtils::TestSemantics),
                        TestUtils::SdpSemanticToString);

TEST_P(VideoTransceiverTests, InvalidName) {
  PeerConnectionConfiguration pc_config{};
  pc_config.sdp_semantic = GetParam();
  LocalPeerPairRaii pair(pc_config);
  mrsVideoTransceiverHandle transceiver_handle1{};
  VideoTransceiverInitConfig transceiver_config{};
  transceiver_config.name = "invalid name with space";
  ASSERT_EQ(Result::kInvalidParameter,
            mrsPeerConnectionAddVideoTransceiver(
                pair.pc1(), &transceiver_config, &transceiver_handle1));
  ASSERT_EQ(nullptr, transceiver_handle1);
}

TEST_P(VideoTransceiverTests, SetDirection) {
  PeerConnectionConfiguration pc_config{};
  pc_config.sdp_semantic = GetParam();
  LocalPeerPairRaii pair(pc_config);
  FakeInteropRaii interop({pair.pc1(), pair.pc2()});

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
  mrsVideoTransceiverHandle transceiver_handle1{};
  {
    VideoTransceiverInitConfig transceiver_config{};
    transceiver_config.name = "video_transceiver_1";
    transceiver_config.transceiver_interop_handle =
        kFakeInteropVideoTransceiverHandle;
    renegotiation_needed1_ev.Reset();
    ASSERT_EQ(Result::kSuccess,
              mrsPeerConnectionAddVideoTransceiver(
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
  mrsVideoTransceiverRegisterStateUpdatedCallback(transceiver_handle1,
                                                  CB(state_updated1_cb));

  // Check video transceiver #1 consistency
  {
    // Default values inchanged (callback was just registered)
    ASSERT_EQ(mrsTransceiverOptDirection::kNotSet, dir_negotiated1);
    ASSERT_EQ(mrsTransceiverDirection::kInactive, dir_desired1);

    // Local video track is NULL
    mrsLocalVideoTrackHandle track_handle_local{};
    ASSERT_EQ(Result::kSuccess, mrsVideoTransceiverGetLocalTrack(
                                    transceiver_handle1, &track_handle_local));
    ASSERT_EQ(nullptr, track_handle_local);

    // Remote video track is NULL
    mrsRemoteVideoTrackHandle track_handle_remote{};
    ASSERT_EQ(Result::kSuccess, mrsVideoTransceiverGetRemoteTrack(
                                    transceiver_handle1, &track_handle_remote));
    ASSERT_EQ(nullptr, track_handle_remote);
  }

  // Connect #1 and #2
  pair.ConnectAndWait();

  // The transceiver is created in its desired state, and peer #1 creates the
  // offer, so there is no event for updating the state due to a local
  // description.
  ASSERT_FALSE(state_updated1_ev_local.IsSignaled());

  // Wait for transceiver to be updated; this happens *after* connect,
  // during SetRemoteDescription().
  ASSERT_TRUE(state_updated1_ev_remote.WaitFor(10s));
  state_updated1_ev_remote.Reset();

  // Check video transceiver #1 consistency
  {
    // Desired state is Send+Receive, negotiated is Send only because the remote
    // peer refused to send (no track added for that).
    ASSERT_EQ(mrsTransceiverOptDirection::kSendOnly, dir_negotiated1);
    ASSERT_EQ(mrsTransceiverDirection::kSendRecv, dir_desired1);
  }

  // Set transceiver #1 direction to Receive
  ASSERT_EQ(Result::kSuccess,
            mrsVideoTransceiverSetDirection(
                transceiver_handle1, mrsTransceiverDirection::kRecvOnly));
  ASSERT_TRUE(state_updated1_ev_setdir.IsSignaled());
  state_updated1_ev_setdir.Reset();

  // Check video transceiver #1 consistency
  {
    // Desired state is Receive, negotiated is still Send only
    ASSERT_EQ(mrsTransceiverOptDirection::kSendOnly,
              dir_negotiated1);  // no change
    ASSERT_EQ(mrsTransceiverDirection::kRecvOnly, dir_desired1);
  }

  // Renegotiate once the previous exchange is done
  ASSERT_TRUE(pair.WaitExchangeCompletedFor(5s));
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

  // Check video transceiver #1 consistency
  {
    // Desired state is Receive, negotiated is Inactive because remote peer
    // refused to send (no track added for that).
    ASSERT_EQ(mrsTransceiverOptDirection::kInactive, dir_negotiated1);
    ASSERT_EQ(mrsTransceiverDirection::kRecvOnly, dir_desired1);
  }

  // Clean-up
  mrsVideoTransceiverRemoveRef(transceiver_handle1);
}

TEST_F(VideoTransceiverTests, SetDirection_InvalidHandle) {
  ASSERT_EQ(Result::kInvalidNativeHandle,
            mrsVideoTransceiverSetDirection(
                nullptr, mrsTransceiverDirection::kRecvOnly));
}

TEST_P(VideoTransceiverTests, SetLocalTrackSendRecv) {
  PeerConnectionConfiguration pc_config{};
  pc_config.sdp_semantic = GetParam();
  LocalPeerPairRaii pair(pc_config);
  FakeInteropRaii interop({pair.pc1(), pair.pc2()});

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
  mrsVideoTransceiverHandle transceiver_handle1{};
  {
    VideoTransceiverInitConfig transceiver_config{};
    transceiver_config.name = "video_transceiver_1";
    transceiver_config.transceiver_interop_handle =
        kFakeInteropVideoTransceiverHandle;
    transceiver_config.desired_direction = mrsTransceiverDirection::kInactive;
    renegotiation_needed1_ev.Reset();
    ASSERT_EQ(Result::kSuccess,
              mrsPeerConnectionAddVideoTransceiver(
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
  mrsVideoTransceiverRegisterStateUpdatedCallback(transceiver_handle1,
                                                  CB(state_updated1_cb));

  // Start in Send+Receive mode for this test
  state_updated1_ev_setdir.Reset();
  ASSERT_EQ(Result::kSuccess,
            mrsVideoTransceiverSetDirection(
                transceiver_handle1, mrsTransceiverDirection::kSendRecv));
  ASSERT_TRUE(state_updated1_ev_setdir.WaitFor(10s));
  state_updated1_ev_setdir.Reset();

  // Check video transceiver #1 consistency
  {
    // Default values inchanged (callback was just registered)
    ASSERT_EQ(mrsTransceiverOptDirection::kNotSet, dir_negotiated1);
    ASSERT_EQ(mrsTransceiverDirection::kSendRecv, dir_desired1);

    // Local video track is NULL
    mrsLocalVideoTrackHandle track_handle_local{};
    ASSERT_EQ(Result::kSuccess, mrsVideoTransceiverGetLocalTrack(
                                    transceiver_handle1, &track_handle_local));
    ASSERT_EQ(nullptr, track_handle_local);

    // Remote video track is NULL
    mrsRemoteVideoTrackHandle track_handle_remote{};
    ASSERT_EQ(Result::kSuccess, mrsVideoTransceiverGetRemoteTrack(
                                    transceiver_handle1, &track_handle_remote));
    ASSERT_EQ(nullptr, track_handle_remote);
  }

  // Connect #1 and #2
  pair.ConnectAndWait();

  // Wait for transceiver to be updated; this happens *after* connect,
  // during SetRemoteDescription().
  ASSERT_TRUE(state_updated1_ev_remote.WaitFor(10s));
  state_updated1_ev_remote.Reset();

  // Check video transceiver #1 consistency
  {
    // Desired state is Send+Receive, negotiated is Send only because the remote
    // peer refused to send (no track added for that).
    ASSERT_EQ(mrsTransceiverOptDirection::kSendOnly, dir_negotiated1);
    ASSERT_EQ(mrsTransceiverDirection::kSendRecv, dir_desired1);
  }

  // Create the external source for the local video track of the local peer (#1)
  mrsExternalVideoTrackSourceHandle source_handle1 = nullptr;
  ASSERT_EQ(mrsResult::kSuccess,
            mrsExternalVideoTrackSourceCreateFromI420ACallback(
                &VideoTestUtils::MakeTestFrame, nullptr, &source_handle1));
  ASSERT_NE(nullptr, source_handle1);
  mrsExternalVideoTrackSourceFinishCreation(source_handle1);

  // Create the local video track (#1)
  mrsLocalVideoTrackHandle track_handle1{};
  {
    LocalVideoTrackFromExternalSourceInitConfig config{};
    ASSERT_EQ(
        mrsResult::kSuccess,
        mrsLocalVideoTrackCreateFromExternalSource(
            source_handle1, &config, "simulated_video_track1", &track_handle1));
    ASSERT_NE(nullptr, track_handle1);
    ASSERT_NE(mrsBool::kFalse, mrsLocalVideoTrackIsEnabled(track_handle1));
  }

  // Add track to transceiver #1
  ASSERT_EQ(Result::kSuccess, mrsVideoTransceiverSetLocalTrack(
                                  transceiver_handle1, track_handle1));

  // Check video transceiver #1 consistency
  {
    // Desired state is Send+Receive, negotiated is still Send only.
    // SetLocalTrack() doesn't change the transceiver directions.
    ASSERT_EQ(mrsTransceiverOptDirection::kSendOnly, dir_negotiated1);
    ASSERT_EQ(mrsTransceiverDirection::kSendRecv, dir_desired1);

    // Local video track is track_handle1
    mrsLocalVideoTrackHandle track_handle_local{};
    ASSERT_EQ(Result::kSuccess, mrsVideoTransceiverGetLocalTrack(
                                    transceiver_handle1, &track_handle_local));
    ASSERT_EQ(track_handle1, track_handle_local);
    mrsLocalVideoTrackRemoveRef(track_handle_local);

    // Remote video track is NULL
    mrsRemoteVideoTrackHandle track_handle_remote{};
    ASSERT_EQ(Result::kSuccess, mrsVideoTransceiverGetRemoteTrack(
                                    transceiver_handle1, &track_handle_remote));
    ASSERT_EQ(nullptr, track_handle_remote);
  }

  // Remove track from transceiver #1 with non-null track
  ASSERT_EQ(Result::kSuccess,
            mrsVideoTransceiverSetLocalTrack(transceiver_handle1, nullptr));
  mrsLocalVideoTrackRemoveRef(track_handle1);
  mrsExternalVideoTrackSourceRemoveRef(source_handle1);

  // Check video transceiver #1 consistency
  {
    // Desired state is Send+Receive, negotiated is still Send only.
    // SetLocalTrack() doesn't change the transceiver directions.
    ASSERT_EQ(mrsTransceiverOptDirection::kSendOnly, dir_negotiated1);
    ASSERT_EQ(mrsTransceiverDirection::kSendRecv, dir_desired1);

    // Local video track is track_handle1
    mrsLocalVideoTrackHandle track_handle_local{};
    ASSERT_EQ(Result::kSuccess, mrsVideoTransceiverGetLocalTrack(
                                    transceiver_handle1, &track_handle_local));
    ASSERT_EQ(nullptr, track_handle_local);

    // Remote video track is NULL
    mrsRemoteVideoTrackHandle track_handle_remote{};
    ASSERT_EQ(Result::kSuccess, mrsVideoTransceiverGetRemoteTrack(
                                    transceiver_handle1, &track_handle_remote));
    ASSERT_EQ(nullptr, track_handle_remote);
  }

  // Renegotiate
  pair.ConnectAndWait();

  // Check video transceiver #1 consistency
  {
    // Again, nothing changed
    ASSERT_EQ(mrsTransceiverOptDirection::kSendOnly, dir_negotiated1);
    ASSERT_EQ(mrsTransceiverDirection::kSendRecv, dir_desired1);
  }

  // Wait until the SDP session exchange completed before cleaning-up
  ASSERT_TRUE(pair.WaitExchangeCompletedFor(10s));

  // Clean-up
  mrsVideoTransceiverRemoveRef(transceiver_handle1);
}

TEST_P(VideoTransceiverTests, SetLocalTrackRecvOnly) {
  PeerConnectionConfiguration pc_config{};
  pc_config.sdp_semantic = GetParam();
  LocalPeerPairRaii pair(pc_config);
  FakeInteropRaii interop({pair.pc1(), pair.pc2()});

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
  mrsVideoTransceiverHandle transceiver_handle1{};
  {
    VideoTransceiverInitConfig transceiver_config{};
    transceiver_config.name = "video_transceiver_1";
    transceiver_config.transceiver_interop_handle =
        kFakeInteropVideoTransceiverHandle;
    renegotiation_needed1_ev.Reset();
    ASSERT_EQ(Result::kSuccess,
              mrsPeerConnectionAddVideoTransceiver(
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
  mrsVideoTransceiverRegisterStateUpdatedCallback(transceiver_handle1,
                                                  CB(state_updated1_cb));

  // Start in receive mode for this test
  state_updated1_ev_setdir.Reset();
  ASSERT_EQ(Result::kSuccess,
            mrsVideoTransceiverSetDirection(
                transceiver_handle1, mrsTransceiverDirection::kRecvOnly));
  ASSERT_TRUE(state_updated1_ev_setdir.WaitFor(10s));
  state_updated1_ev_setdir.Reset();

  // Check video transceiver #1 consistency
  {
    // Default values inchanged (callback was just registered)
    ASSERT_EQ(mrsTransceiverOptDirection::kNotSet, dir_negotiated1);
    ASSERT_EQ(mrsTransceiverDirection::kRecvOnly, dir_desired1);

    // Local video track is NULL
    mrsLocalVideoTrackHandle track_handle_local{};
    ASSERT_EQ(Result::kSuccess, mrsVideoTransceiverGetLocalTrack(
                                    transceiver_handle1, &track_handle_local));
    ASSERT_EQ(nullptr, track_handle_local);

    // Remote video track is NULL
    mrsRemoteVideoTrackHandle track_handle_remote{};
    ASSERT_EQ(Result::kSuccess, mrsVideoTransceiverGetRemoteTrack(
                                    transceiver_handle1, &track_handle_remote));
    ASSERT_EQ(nullptr, track_handle_remote);
  }

  // Connect #1 and #2
  pair.ConnectAndWait();

  // Wait for transceiver to be updated; this happens *after* connect,
  // during SetRemoteDescription().
  ASSERT_TRUE(state_updated1_ev_remote.WaitFor(10s));
  state_updated1_ev_remote.Reset();

  // Check video transceiver #1 consistency
  {
    // Desired state is Receive, negotiated is Inactive only because the remote
    // peer refused to send (no track added for that).
    ASSERT_EQ(mrsTransceiverOptDirection::kInactive, dir_negotiated1);
    ASSERT_EQ(mrsTransceiverDirection::kRecvOnly, dir_desired1);
  }

  // Create the external source for the local video track of the local peer (#1)
  mrsExternalVideoTrackSourceHandle source_handle1 = nullptr;
  ASSERT_EQ(mrsResult::kSuccess,
            mrsExternalVideoTrackSourceCreateFromI420ACallback(
                &VideoTestUtils::MakeTestFrame, nullptr, &source_handle1));
  ASSERT_NE(nullptr, source_handle1);
  mrsExternalVideoTrackSourceFinishCreation(source_handle1);

  // Create the local video track (#1)
  mrsLocalVideoTrackHandle track_handle1{};
  {
    LocalVideoTrackFromExternalSourceInitConfig config{};
    ASSERT_EQ(
        mrsResult::kSuccess,
        mrsLocalVideoTrackCreateFromExternalSource(
            source_handle1, &config, "simulated_video_track1", &track_handle1));
    ASSERT_NE(nullptr, track_handle1);
    ASSERT_NE(mrsBool::kFalse, mrsLocalVideoTrackIsEnabled(track_handle1));
  }

  // Add track to transceiver #1
  ASSERT_EQ(Result::kSuccess, mrsVideoTransceiverSetLocalTrack(
                                  transceiver_handle1, track_handle1));

  // Check video transceiver #1 consistency
  {
    // Desired state is Receive, negotiated is still Inactive
    ASSERT_EQ(mrsTransceiverOptDirection::kInactive, dir_negotiated1);
    ASSERT_EQ(mrsTransceiverDirection::kRecvOnly, dir_desired1);

    // Local video track is track_handle1
    mrsLocalVideoTrackHandle track_handle_local{};
    ASSERT_EQ(Result::kSuccess, mrsVideoTransceiverGetLocalTrack(
                                    transceiver_handle1, &track_handle_local));
    ASSERT_EQ(track_handle1, track_handle_local);
    mrsLocalVideoTrackRemoveRef(track_handle_local);

    // Remote video track is NULL
    mrsRemoteVideoTrackHandle track_handle_remote{};
    ASSERT_EQ(Result::kSuccess, mrsVideoTransceiverGetRemoteTrack(
                                    transceiver_handle1, &track_handle_remote));
    ASSERT_EQ(nullptr, track_handle_remote);
  }

  // Remote track from transceiver #1 with non-null track
  ASSERT_EQ(Result::kSuccess,
            mrsVideoTransceiverSetLocalTrack(transceiver_handle1, nullptr));
  mrsLocalVideoTrackRemoveRef(track_handle1);
  mrsExternalVideoTrackSourceRemoveRef(source_handle1);

  // Check video transceiver #1 consistency
  {
    // Desired state is Receive, negotiated is still Inactive
    ASSERT_EQ(mrsTransceiverOptDirection::kInactive, dir_negotiated1);
    ASSERT_EQ(mrsTransceiverDirection::kRecvOnly, dir_desired1);

    // Local video track is NULL
    mrsLocalVideoTrackHandle track_handle_local{};
    ASSERT_EQ(Result::kSuccess, mrsVideoTransceiverGetLocalTrack(
                                    transceiver_handle1, &track_handle_local));
    ASSERT_EQ(nullptr, track_handle_local);

    // Remote video track is NULL
    mrsRemoteVideoTrackHandle track_handle_remote{};
    ASSERT_EQ(Result::kSuccess, mrsVideoTransceiverGetRemoteTrack(
                                    transceiver_handle1, &track_handle_remote));
    ASSERT_EQ(nullptr, track_handle_remote);
  }

  // Renegotiate
  pair.ConnectAndWait();

  // Check video transceiver #1 consistency
  {
    // Note how nothing changed, because SetLocalTrack() does not change the
    // desired direction of Receive, and the remote peer #2 still doesn't have a
    // track to send us.
    ASSERT_EQ(mrsTransceiverOptDirection::kInactive, dir_negotiated1);
    ASSERT_EQ(mrsTransceiverDirection::kRecvOnly, dir_desired1);
  }

  // Wait until the SDP session exchange completed before cleaning-up
  ASSERT_TRUE(pair.WaitExchangeCompletedFor(10s));

  // Clean-up
  mrsVideoTransceiverRemoveRef(transceiver_handle1);
}

TEST_F(VideoTransceiverTests, SetLocalTrack_InvalidHandle) {
  mrsLocalVideoTrackHandle dummy = (void*)0x1;  // looks legit
  ASSERT_EQ(Result::kInvalidNativeHandle,
            mrsVideoTransceiverSetLocalTrack(nullptr, dummy));
}
