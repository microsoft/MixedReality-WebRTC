// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include "external_video_track_source_interop.h"
#include "interop_api.h"
#include "local_video_track_interop.h"
#include "remote_video_track_interop.h"
#include "transceiver_interop.h"

#include "simple_interop.h"
#include "video_test_utils.h"

// Named types for readability of auto-generated test names.
struct AudioTest {};
struct VideoTest {};
struct SdpUnifiedPlan {};
struct SdpPlanB {};

/// Test parameters templated on type above.
template <typename MEDIA_KIND, typename SDP_SEMANTIC>
struct TestParams;

template <>
struct TestParams<AudioTest, SdpUnifiedPlan> {
  using MediaType = AudioTest;
  constexpr static const mrsMediaKind kMediaKind = mrsMediaKind::kAudio;
  constexpr static const mrsSdpSemantic kSdpSemantic =
      mrsSdpSemantic::kUnifiedPlan;
};
template <>
struct TestParams<AudioTest, SdpPlanB> {
  using MediaType = AudioTest;
  constexpr static const mrsMediaKind kMediaKind = mrsMediaKind::kAudio;
  constexpr static const mrsSdpSemantic kSdpSemantic = mrsSdpSemantic::kPlanB;
};
template <>
struct TestParams<VideoTest, SdpUnifiedPlan> {
  using MediaType = VideoTest;
  constexpr static const mrsMediaKind kMediaKind = mrsMediaKind::kVideo;
  constexpr static const mrsSdpSemantic kSdpSemantic =
      mrsSdpSemantic::kUnifiedPlan;
};
template <>
struct TestParams<VideoTest, SdpPlanB> {
  using MediaType = VideoTest;
  constexpr static const mrsMediaKind kMediaKind = mrsMediaKind::kVideo;
  constexpr static const mrsSdpSemantic kSdpSemantic = mrsSdpSemantic::kPlanB;
};

namespace {

template <typename T>
class TransceiverTests : public TestUtils::TestBase {};

/// Media kind trait for audio vs. video tests.
template <typename MEDIA_KIND>
struct MediaTrait;

/// Specialization for audio tests.
template <>
struct MediaTrait<AudioTest> {
  static void CheckTransceiverTracksAreNull(mrsTransceiverHandle handle) {
    mrsLocalAudioTrackHandle local_handle{};
    ASSERT_EQ(Result::kSuccess,
              mrsTransceiverGetLocalAudioTrack(handle, &local_handle));
    ASSERT_EQ(nullptr, local_handle);

    mrsRemoteAudioTrackHandle remote_handle{};
    ASSERT_EQ(Result::kSuccess,
              mrsTransceiverGetRemoteAudioTrack(handle, &remote_handle));
    ASSERT_EQ(nullptr, remote_handle);
  }

  static void Test_SetLocalTrack_InvalidHandle() {
    mrsLocalAudioTrackHandle dummy = (void*)0x1;  // looks legit
    ASSERT_EQ(Result::kInvalidNativeHandle,
              mrsTransceiverSetLocalAudioTrack(nullptr, dummy));
  }
};

/// Specialization for video tests.
template <>
struct MediaTrait<VideoTest> {
  static void CheckTransceiverTracksAreNull(mrsTransceiverHandle handle) {
    mrsLocalVideoTrackHandle local_handle{};
    ASSERT_EQ(Result::kSuccess,
              mrsTransceiverGetLocalVideoTrack(handle, &local_handle));
    ASSERT_EQ(nullptr, local_handle);

    mrsRemoteVideoTrackHandle remote_handle{};
    ASSERT_EQ(Result::kSuccess,
              mrsTransceiverGetRemoteVideoTrack(handle, &remote_handle));
    ASSERT_EQ(nullptr, remote_handle);
  }

  static void Test_SetLocalTrack_InvalidHandle() {
    mrsLocalVideoTrackHandle dummy = (void*)0x1;  // looks legit
    ASSERT_EQ(Result::kInvalidNativeHandle,
              mrsTransceiverSetLocalVideoTrack(nullptr, dummy));
  }
};

/// Test that SetLocalTrack() on a transceiver does not change its desired or
/// negotiated directions. This is currently only available for video, because
/// there is no external tracks for audio.
void Test_SetLocalTrack(mrsSdpSemantic sdp_semantic,
                        mrsTransceiverDirection start_dir,
                        mrsTransceiverOptDirection neg_dir) {
  mrsPeerConnectionConfiguration pc_config{};
  pc_config.sdp_semantic = sdp_semantic;
  LocalPeerPairRaii pair(pc_config);

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

  // Add an inactive transceiver to the local peer (#1)
  const mrsTransceiverDirection created_dir1 =
      mrsTransceiverDirection::kInactive;
  mrsTransceiverHandle transceiver_handle1{};
  {
    mrsTransceiverInitConfig transceiver_config{};
    transceiver_config.name = "video_transceiver_1";
    transceiver_config.media_kind = mrsMediaKind::kVideo;
    transceiver_config.desired_direction = created_dir1;
    renegotiation_needed1_ev.Reset();
    ASSERT_EQ(Result::kSuccess,
              mrsPeerConnectionAddTransceiver(pair.pc1(), &transceiver_config,
                                              &transceiver_handle1));
    ASSERT_NE(nullptr, transceiver_handle1);
    ASSERT_TRUE(renegotiation_needed1_ev.IsSignaled());
    renegotiation_needed1_ev.Reset();
  }

  // Register event for transceiver state update
  Event state_updated1_ev_local;
  Event state_updated1_ev_remote;
  Event state_updated1_ev_setdir;
  mrsTransceiverDirection dir_desired1 = created_dir1;
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
  mrsTransceiverRegisterStateUpdatedCallback(transceiver_handle1,
                                             CB(state_updated1_cb));

  // Start in desired mode for this test
  state_updated1_ev_setdir.Reset();
  ASSERT_EQ(Result::kSuccess,
            mrsTransceiverSetDirection(transceiver_handle1, start_dir));
  ASSERT_TRUE(state_updated1_ev_setdir.WaitFor(10s));
  state_updated1_ev_setdir.Reset();

  // Check video transceiver #1 consistency
  {
    // Default values inchanged (callback was just registered)
    ASSERT_EQ(mrsTransceiverOptDirection::kNotSet, dir_negotiated1);
    ASSERT_EQ(start_dir, dir_desired1);

    // Local video track is NULL
    mrsLocalVideoTrackHandle track_handle_local{};
    ASSERT_EQ(Result::kSuccess, mrsTransceiverGetLocalVideoTrack(
                                    transceiver_handle1, &track_handle_local));
    ASSERT_EQ(nullptr, track_handle_local);

    // Remote video track is NULL
    mrsRemoteVideoTrackHandle track_handle_remote{};
    ASSERT_EQ(Result::kSuccess, mrsTransceiverGetRemoteVideoTrack(
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
    // Desired state is inchanged, negotiated is the intersection of the desired
    // state and the ReceiveOnly state from the remote peer who refused to send
    // (no track added for that).
    ASSERT_EQ(neg_dir, dir_negotiated1);
    ASSERT_EQ(start_dir, dir_desired1);
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
    mrsLocalVideoTrackInitSettings settings{};
    settings.track_name = "simulated_video_track1";
    ASSERT_EQ(mrsResult::kSuccess,
              mrsLocalVideoTrackCreateFromSource(&settings, source_handle1,
                                                 &track_handle1));
    ASSERT_NE(nullptr, track_handle1);
    ASSERT_NE(mrsBool::kFalse, mrsLocalVideoTrackIsEnabled(track_handle1));
  }

  // Add track to transceiver #1
  ASSERT_EQ(Result::kSuccess, mrsTransceiverSetLocalVideoTrack(
                                  transceiver_handle1, track_handle1));

  // Check video transceiver #1 consistency
  {
    // Desired and negotiated state are still unchanged
    ASSERT_EQ(neg_dir, dir_negotiated1);
    ASSERT_EQ(start_dir, dir_desired1);

    // Local video track is track_handle1
    mrsLocalVideoTrackHandle track_handle_local{};
    ASSERT_EQ(Result::kSuccess, mrsTransceiverGetLocalVideoTrack(
                                    transceiver_handle1, &track_handle_local));
    ASSERT_EQ(track_handle1, track_handle_local);

    // Remote video track is NULL
    mrsRemoteVideoTrackHandle track_handle_remote{};
    ASSERT_EQ(Result::kSuccess, mrsTransceiverGetRemoteVideoTrack(
                                    transceiver_handle1, &track_handle_remote));
    ASSERT_EQ(nullptr, track_handle_remote);
  }

  // Remote track from transceiver #1 with non-null track
  ASSERT_EQ(Result::kSuccess,
            mrsTransceiverSetLocalVideoTrack(transceiver_handle1, nullptr));
  mrsRefCountedObjectRemoveRef(track_handle1);
  mrsRefCountedObjectRemoveRef(source_handle1);

  // Check video transceiver #1 consistency
  {
    // Desired and negotiated state are still unchanged
    ASSERT_EQ(neg_dir, dir_negotiated1);
    ASSERT_EQ(start_dir, dir_desired1);

    // Local video track is NULL
    mrsLocalVideoTrackHandle track_handle_local{};
    ASSERT_EQ(Result::kSuccess, mrsTransceiverGetLocalVideoTrack(
                                    transceiver_handle1, &track_handle_local));
    ASSERT_EQ(nullptr, track_handle_local);

    // Remote video track is NULL
    mrsRemoteVideoTrackHandle track_handle_remote{};
    ASSERT_EQ(Result::kSuccess, mrsTransceiverGetRemoteVideoTrack(
                                    transceiver_handle1, &track_handle_remote));
    ASSERT_EQ(nullptr, track_handle_remote);
  }

  // Renegotiate
  pair.ConnectAndWait();

  // Check video transceiver #1 consistency
  {
    // Desired and negotiated state are still unchanged
    ASSERT_EQ(neg_dir, dir_negotiated1);
    ASSERT_EQ(start_dir, dir_desired1);
  }

  // Wait until the SDP session exchange completed before cleaning-up
  ASSERT_TRUE(pair.WaitExchangeCompletedFor(10s));
}

}  // namespace

TYPED_TEST_CASE_P(TransceiverTests);

TYPED_TEST_P(TransceiverTests, InvalidName) {
  using Media = MediaTrait<TypeParam::MediaType>;
  mrsPeerConnectionConfiguration pc_config{};
  pc_config.sdp_semantic = TypeParam::kSdpSemantic;
  LocalPeerPairRaii pair(pc_config);
  mrsTransceiverHandle transceiver_handle1{};
  mrsTransceiverInitConfig transceiver_config{};
  transceiver_config.name = "invalid name with space";
  transceiver_config.media_kind = TypeParam::kMediaKind;
  ASSERT_EQ(Result::kInvalidParameter,
            mrsPeerConnectionAddTransceiver(pair.pc1(), &transceiver_config,
                                            &transceiver_handle1));
  ASSERT_EQ(nullptr, transceiver_handle1);
}

TYPED_TEST_P(TransceiverTests, SetDirection) {
  using Media = MediaTrait<TypeParam::MediaType>;

  mrsPeerConnectionConfiguration pc_config{};
  pc_config.sdp_semantic = TypeParam::kSdpSemantic;
  LocalPeerPairRaii pair(pc_config);

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
  mrsTransceiverHandle transceiver_handle1{};
  {
    mrsTransceiverInitConfig transceiver_config{};
    transceiver_config.name =
        (TypeParam::kMediaKind == mrsMediaKind::kAudio ? "audio_transceiver_1"
                                                       : "video_transceiver_1");
    transceiver_config.media_kind = TypeParam::kMediaKind;
    renegotiation_needed1_ev.Reset();
    ASSERT_EQ(Result::kSuccess,
              mrsPeerConnectionAddTransceiver(pair.pc1(), &transceiver_config,
                                              &transceiver_handle1));
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
  mrsTransceiverRegisterStateUpdatedCallback(transceiver_handle1,
                                             CB(state_updated1_cb));

  // Check transceiver #1 consistency
  {
    // Default values inchanged (callback was just registered)
    ASSERT_EQ(mrsTransceiverOptDirection::kNotSet, dir_negotiated1);
    ASSERT_EQ(mrsTransceiverDirection::kInactive, dir_desired1);

    Media::CheckTransceiverTracksAreNull(transceiver_handle1);
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

  // Check transceiver #1 consistency
  {
    // Desired state is Send+Receive, negotiated is Send only because the remote
    // peer refused to send (no track added for that).
    ASSERT_EQ(mrsTransceiverOptDirection::kSendOnly, dir_negotiated1);
    ASSERT_EQ(mrsTransceiverDirection::kSendRecv, dir_desired1);
  }

  // Set transceiver #1 direction to Receive
  ASSERT_EQ(Result::kSuccess,
            mrsTransceiverSetDirection(transceiver_handle1,
                                       mrsTransceiverDirection::kRecvOnly));
  ASSERT_TRUE(state_updated1_ev_setdir.IsSignaled());
  state_updated1_ev_setdir.Reset();

  // Check transceiver #1 consistency
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

  // Check transceiver #1 consistency
  {
    // Desired state is Receive, negotiated is Inactive because remote peer
    // refused to send (no track added for that).
    ASSERT_EQ(mrsTransceiverOptDirection::kInactive, dir_negotiated1);
    ASSERT_EQ(mrsTransceiverDirection::kRecvOnly, dir_desired1);
  }
}

TYPED_TEST_P(TransceiverTests, SetDirection_InvalidHandle) {
  ASSERT_EQ(
      Result::kInvalidNativeHandle,
      mrsTransceiverSetDirection(nullptr, mrsTransceiverDirection::kRecvOnly));
}

TYPED_TEST_P(TransceiverTests, SetLocalTrackSendRecv) {
  const mrsSdpSemantic sdp_semantic = TypeParam::kSdpSemantic;
  Test_SetLocalTrack(sdp_semantic, mrsTransceiverDirection::kSendRecv,
                     mrsTransceiverOptDirection::kSendOnly);
}

TYPED_TEST_P(TransceiverTests, SetLocalTrackRecvOnly) {
  const mrsSdpSemantic sdp_semantic = TypeParam::kSdpSemantic;
  Test_SetLocalTrack(sdp_semantic, mrsTransceiverDirection::kRecvOnly,
                     mrsTransceiverOptDirection::kInactive);
}

TYPED_TEST_P(TransceiverTests, SetLocalTrack_InvalidHandle) {
  MediaTrait<TypeParam::MediaType>::Test_SetLocalTrack_InvalidHandle();
}

TYPED_TEST_P(TransceiverTests, StreamIDs) {
  using Media = MediaTrait<TypeParam::MediaType>;

  mrsPeerConnectionConfiguration pc_config{};
  pc_config.sdp_semantic = TypeParam::kSdpSemantic;
  LocalPeerPairRaii pair(pc_config);

  const char* const transceiver_name =
      (TypeParam::kMediaKind == mrsMediaKind::kAudio ? "audio_transceiver_1"
                                                     : "video_transceiver_1");
  const char* const encoded_stream_ids = "id1;id2;id3";

  // Register event handler for transceiver added
  Event transceiver_added1_ev;
  InteropCallback<const mrsTransceiverAddedInfo*> transceiver_added1_cb =
      [&](const mrsTransceiverAddedInfo* info) {
        ASSERT_EQ(TypeParam::kMediaKind, info->media_kind);
        // Name is equal because the transceiver was created locally
        ASSERT_STREQ(info->transceiver_name, transceiver_name);
        ASSERT_STREQ(info->encoded_stream_ids_, encoded_stream_ids);
        transceiver_added1_ev.Set();
      };
  mrsPeerConnectionRegisterTransceiverAddedCallback(pair.pc1(),
                                                    CB(transceiver_added1_cb));
  Event transceiver_added2_ev;
  mrsTransceiverHandle transceiver_handle2{};
  InteropCallback<const mrsTransceiverAddedInfo*> transceiver_added2_cb =
      [&](const mrsTransceiverAddedInfo* info) {
        ASSERT_EQ(TypeParam::kMediaKind, info->media_kind);
        // Here the name of the transceiver is unknown because it was generated
        // by the implementation; the name is not synchronized over SDP.
        ASSERT_STREQ(info->encoded_stream_ids_, encoded_stream_ids);
        transceiver_handle2 = info->transceiver_handle;
        transceiver_added2_ev.Set();
      };
  mrsPeerConnectionRegisterTransceiverAddedCallback(pair.pc2(),
                                                    CB(transceiver_added2_cb));

  // Add a transceiver to the local peer (#1)
  mrsTransceiverHandle transceiver_handle1{};
  {
    mrsTransceiverInitConfig transceiver_config{};
    transceiver_config.name = transceiver_name;
    transceiver_config.media_kind = TypeParam::kMediaKind;
    transceiver_config.stream_ids = encoded_stream_ids;
    ASSERT_EQ(Result::kSuccess,
              mrsPeerConnectionAddTransceiver(pair.pc1(), &transceiver_config,
                                              &transceiver_handle1));
    ASSERT_NE(nullptr, transceiver_handle1);
    ASSERT_TRUE(transceiver_added1_ev.IsSignaled());
    transceiver_added1_ev.Reset();
  }

  // Connect #1 and #2
  pair.ConnectAndWait();
  pair.WaitExchangeCompletedFor(60s);
}

// Note: All tests must be listed in this macro
REGISTER_TYPED_TEST_CASE_P(TransceiverTests,
                           InvalidName,
                           SetDirection,
                           SetDirection_InvalidHandle,
                           SetLocalTrack_InvalidHandle,
                           SetLocalTrackSendRecv,
                           SetLocalTrackRecvOnly,
                           StreamIDs);

using TestTypes = ::testing::Types<TestParams<AudioTest, SdpPlanB>,
                                   TestParams<AudioTest, SdpUnifiedPlan>,
                                   TestParams<VideoTest, SdpPlanB>,
                                   TestParams<VideoTest, SdpUnifiedPlan>>;
INSTANTIATE_TYPED_TEST_CASE_P(, TransceiverTests, TestTypes);
