// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include "device_video_track_source_interop.h"
#include "external_video_track_source_interop.h"
#include "interop_api.h"
#include "local_video_track_interop.h"
#include "remote_video_track_interop.h"
#include "transceiver_interop.h"

#include "simple_interop.h"
#include "test_utils.h"
#include "video_test_utils.h"

namespace {

class VideoTrackTests : public TestUtils::TestBase,
                        public testing::WithParamInterface<mrsSdpSemantic> {};

// PeerConnectionVideoTrackAddedCallback
using VideoTrackAddedCallback =
    InteropCallback<const mrsRemoteVideoTrackAddedInfo*>;

// PeerConnectionI420VideoFrameCallback
using I420VideoFrameCallback = InteropCallback<const I420AVideoFrame&>;

}  // namespace

INSTANTIATE_TEST_CASE_P(,
                        VideoTrackTests,
                        testing::ValuesIn(TestUtils::TestSemantics),
                        TestUtils::SdpSemanticToString);

#if !defined(MRSW_EXCLUDE_DEVICE_TESTS)

TEST_P(VideoTrackTests, Simple) {
  mrsPeerConnectionConfiguration pc_config{};
  pc_config.sdp_semantic = GetParam();
  LocalPeerPairRaii pair(pc_config);

  // Register event for renegotiation needed
  Event renegotiation_needed1_ev;
  InteropCallback renegotiation_needed1_cb = [&renegotiation_needed1_ev]() {
    renegotiation_needed1_ev.Set();
  };
  mrsPeerConnectionRegisterRenegotiationNeededCallback(
      pair.pc1(), CB(renegotiation_needed1_cb));

  // Grab the handle of the remote track from the remote peer (#2) via the
  // VideoTrackAdded callback.
  mrsRemoteVideoTrackHandle track_handle2{};
  mrsTransceiverHandle transceiver_handle2{};
  Event track_added2_ev;
  VideoTrackAddedCallback track_added2_cb =
      [&track_handle2, &transceiver_handle2,
       &track_added2_ev](const mrsRemoteVideoTrackAddedInfo* info) {
        track_handle2 = info->track_handle;
        transceiver_handle2 = info->audio_transceiver_handle;
        track_added2_ev.Set();
      };
  mrsPeerConnectionRegisterVideoTrackAddedCallback(pair.pc2(),
                                                   CB(track_added2_cb));

  // Create the video transceiver #1
  mrsTransceiverHandle transceiver_handle1{};
  {
    renegotiation_needed1_ev.Reset();
    mrsTransceiverInitConfig transceiver_config{};
    transceiver_config.name = "video_transceiver_1";
    transceiver_config.media_kind = mrsMediaKind::kVideo;
    ASSERT_EQ(Result::kSuccess,
              mrsPeerConnectionAddTransceiver(pair.pc1(), &transceiver_config,
                                              &transceiver_handle1));
    ASSERT_NE(nullptr, transceiver_handle1);
    ASSERT_TRUE(renegotiation_needed1_ev.WaitFor(1s));
    renegotiation_needed1_ev.Reset();
  }

  // Check video transceiver #1 consistency
  {
    // Local track is NULL
    mrsLocalVideoTrackHandle track_handle_local{};
    ASSERT_EQ(Result::kSuccess, mrsTransceiverGetLocalVideoTrack(
                                    transceiver_handle1, &track_handle_local));
    ASSERT_EQ(nullptr, track_handle_local);

    // Remote track is NULL
    mrsRemoteVideoTrackHandle track_handle_remote{};
    ASSERT_EQ(Result::kSuccess, mrsTransceiverGetRemoteVideoTrack(
                                    transceiver_handle1, &track_handle_remote));
    ASSERT_EQ(nullptr, track_handle_remote);
  }

  // Create the video source #1
  mrsVideoTrackSourceHandle source_handle1{};
  {
    mrsLocalVideoDeviceInitConfig device_config{};
    ASSERT_EQ(Result::kSuccess,
              mrsDeviceVideoTrackSourceCreate(&device_config, &source_handle1));
    ASSERT_NE(nullptr, source_handle1);
  }

  // Create the local video track #1
  mrsLocalVideoTrackHandle track_handle1{};
  {
    mrsLocalVideoTrackInitSettings config{};
    config.track_name = "local_video_track";
    ASSERT_EQ(Result::kSuccess, mrsLocalVideoTrackCreateFromSource(
                                    &config, source_handle1, &track_handle1));
    ASSERT_NE(nullptr, track_handle1);
  }

  // New tracks are enabled by default
  ASSERT_NE(mrsBool::kFalse, mrsLocalVideoTrackIsEnabled(track_handle1));

  // Add the local track #1 on the transceiver #1.
  ASSERT_FALSE(renegotiation_needed1_ev.IsSignaled());
  ASSERT_EQ(Result::kSuccess, mrsTransceiverSetLocalVideoTrack(
                                  transceiver_handle1, track_handle1));
  ASSERT_FALSE(
      renegotiation_needed1_ev
          .IsSignaled());  // TODO: why? because transceiver starts in SendRecv
                           // mode, so didn't change direction?

  // Check video transceiver #1 consistency
  {
    // Local track is track_handle1
    mrsLocalVideoTrackHandle track_handle_local{};
    ASSERT_EQ(Result::kSuccess, mrsTransceiverGetLocalVideoTrack(
                                    transceiver_handle1, &track_handle_local));
    ASSERT_EQ(track_handle1, track_handle_local);

    // Remote track is NULL
    mrsRemoteVideoTrackHandle track_handle_remote{};
    ASSERT_EQ(Result::kSuccess, mrsTransceiverGetRemoteVideoTrack(
                                    transceiver_handle1, &track_handle_remote));
    ASSERT_EQ(nullptr, track_handle_remote);
  }

  // Connect #1 and #2
  pair.ConnectAndWait();

  // Wait for remote track to be added on #2
  ASSERT_TRUE(track_added2_ev.WaitFor(5s));
  ASSERT_NE(nullptr, track_handle2);

  // Check video transceiver #2 consistency
  {
    // Local track is NULL
    mrsLocalVideoTrackHandle track_handle_local{};
    ASSERT_EQ(Result::kSuccess, mrsTransceiverGetLocalVideoTrack(
                                    transceiver_handle2, &track_handle_local));
    ASSERT_EQ(nullptr, track_handle_local);

    // Remote track is track_handle2
    mrsRemoteVideoTrackHandle track_handle_remote{};
    ASSERT_EQ(Result::kSuccess, mrsTransceiverGetRemoteVideoTrack(
                                    transceiver_handle2, &track_handle_remote));
    ASSERT_EQ(track_handle2, track_handle_remote);
  }

  // Register a frame callback for the remote video of #2
  uint32_t frame_count = 0;
  I420VideoFrameCallback i420cb = [&frame_count](const I420AVideoFrame& frame) {
    ASSERT_NE(nullptr, frame.ydata_);
    ASSERT_NE(nullptr, frame.udata_);
    ASSERT_NE(nullptr, frame.vdata_);
    ASSERT_LT(0u, frame.width_);
    ASSERT_LT(0u, frame.height_);
    ++frame_count;
  };
  mrsRemoteVideoTrackRegisterI420AFrameCallback(track_handle2, CB(i420cb));

  // Wait 3 seconds and check the frame callback is called
  Event ev;
  ev.WaitFor(3s);
  ASSERT_LT(30u, frame_count) << "Expected at least 10 FPS";

  ASSERT_TRUE(pair.WaitExchangeCompletedFor(5s));

  // Clean-up
  mrsRemoteVideoTrackRegisterI420AFrameCallback(track_handle2, nullptr,
                                                nullptr);
  mrsRefCountedObjectRemoveRef(track_handle1);
  mrsRefCountedObjectRemoveRef(source_handle1);
}

TEST_P(VideoTrackTests, Muted) {
  mrsPeerConnectionConfiguration pc_config{};
  pc_config.sdp_semantic = GetParam();
  LocalPeerPairRaii pair(pc_config);

  // Grab the handle of the remote track from the remote peer (#2) via the
  // VideoTrackAdded callback.
  mrsRemoteVideoTrackHandle track_handle2{};
  mrsTransceiverHandle transceiver_handle2{};
  Event track_added2_ev;
  VideoTrackAddedCallback track_added2_cb =
      [&track_handle2, &transceiver_handle2,
       &track_added2_ev](const mrsRemoteVideoTrackAddedInfo* info) {
        track_handle2 = info->track_handle;
        transceiver_handle2 = info->audio_transceiver_handle;
        track_added2_ev.Set();
      };
  mrsPeerConnectionRegisterVideoTrackAddedCallback(pair.pc2(),
                                                   CB(track_added2_cb));

  // Create the video transceiver #1
  mrsTransceiverHandle transceiver_handle1{};
  {
    mrsTransceiverInitConfig transceiver_config{};
    transceiver_config.name = "video_transceiver_1";
    transceiver_config.media_kind = mrsMediaKind::kVideo;
    ASSERT_EQ(Result::kSuccess,
              mrsPeerConnectionAddTransceiver(pair.pc1(), &transceiver_config,
                                              &transceiver_handle1));
    ASSERT_NE(nullptr, transceiver_handle1);
  }

  // Create the video source #1
  mrsVideoTrackSourceHandle source_handle1{};
  {
    mrsLocalVideoDeviceInitConfig device_config{};
    ASSERT_EQ(Result::kSuccess,
              mrsDeviceVideoTrackSourceCreate(&device_config, &source_handle1));
    ASSERT_NE(nullptr, source_handle1);
  }

  // Create the local video track #1
  mrsLocalVideoTrackHandle track_handle1{};
  {
    mrsLocalVideoTrackInitSettings config{};
    config.track_name = "local_video_track";
    ASSERT_EQ(Result::kSuccess, mrsLocalVideoTrackCreateFromSource(
                                    &config, source_handle1, &track_handle1));
    ASSERT_NE(nullptr, track_handle1);
  }

  // New tracks are enabled by default
  ASSERT_NE(mrsBool::kFalse, mrsLocalVideoTrackIsEnabled(track_handle1));

  // Disable the video track; it should output only black frames
  ASSERT_EQ(Result::kSuccess,
            mrsLocalVideoTrackSetEnabled(track_handle1, mrsBool::kFalse));
  ASSERT_EQ(mrsBool::kFalse, mrsLocalVideoTrackIsEnabled(track_handle1));

  // Add the local track #1 on the transceiver #1
  ASSERT_EQ(Result::kSuccess, mrsTransceiverSetLocalVideoTrack(
                                  transceiver_handle1, track_handle1));

  // Connect #1 and #2
  pair.ConnectAndWait();

  // Wait for remote track to be added on #2
  ASSERT_TRUE(track_added2_ev.WaitFor(5s));
  ASSERT_NE(nullptr, track_handle2);
  ASSERT_NE(nullptr, transceiver_handle2);

  // Register a frame callback for the remote video of #2
  uint32_t frame_count = 0;
  I420VideoFrameCallback i420cb = [&frame_count](const I420AVideoFrame& frame) {
    ASSERT_NE(nullptr, frame.ydata_);
    ASSERT_NE(nullptr, frame.udata_);
    ASSERT_NE(nullptr, frame.vdata_);
    ASSERT_LT(0u, frame.width_);
    ASSERT_LT(0u, frame.height_);
    const uint8_t* s = (const uint8_t*)frame.ydata_;
    const uint8_t* e = s + ((size_t)frame.ystride_ * frame.height_);
    bool all_black = true;
    for (const uint8_t* p = s; p < e; ++p) {
      all_black = all_black && (*p == 0);
    }
    // Note: U and V can be anything, so don't test them.
    ASSERT_TRUE(all_black);
    ++frame_count;
  };
  mrsRemoteVideoTrackRegisterI420AFrameCallback(track_handle2, CB(i420cb));

  // Wait 3 seconds and check the frame callback is called
  Event ev;
  ev.WaitFor(3s);
  ASSERT_LT(30u, frame_count) << "Expected at least 10 FPS";

  ASSERT_TRUE(pair.WaitExchangeCompletedFor(5s));

  // Clean-up
  mrsRemoteVideoTrackRegisterI420AFrameCallback(track_handle2, nullptr,
                                                nullptr);
  mrsRefCountedObjectRemoveRef(track_handle1);
  mrsRefCountedObjectRemoveRef(source_handle1);
}

void MRS_CALL enumDeviceCallback(const char* id,
                                 const char* /*name*/,
                                 void* user_data) {
  auto device_ids = (std::vector<std::string>*)user_data;
  device_ids->push_back(id);
}

void MRS_CALL enumDeviceCallbackCompleted(void* user_data) {
  auto ev = (Event*)user_data;
  ev->Set();
}

// FIXME - PeerConnection currently doesn't support multiple local video tracks
// TEST_F(VideoTrackTests, DeviceIdAll) {
//  LocalPeerPairRaii pair;
//
//  Event ev;
//  std::vector<std::string> device_ids;
//  mrsEnumVideoCaptureDevicesAsync(enumDeviceCallback, &device_ids,
//                                  enumDeviceCallbackCompleted, &ev);
//  ev.Wait();
//
//  for (auto&& id : device_ids) {
//    mrsLocalVideoTrackInitConfig config{};
//    config.video_device_id = id.c_str();
//    ASSERT_EQ(Result::kSuccess,
//              mrsPeerConnectionAddLocalVideoTrack(pair.pc1(), config));
//  }
//}

#endif  // MRSW_EXCLUDE_DEVICE_TESTS

TEST_P(VideoTrackTests, Multi) {
  SimpleInterop simple_interop1;
  SimpleInterop simple_interop2;

  mrsPeerConnectionConfiguration pc_config{};
  pc_config.sdp_semantic = GetParam();
  LocalPeerPairRaii pair(pc_config);

  // void* h1 = simple_interop1.CreateObject(ObjectType::kPeerConnection);
  // void* h2 = simple_interop2.CreateObject(ObjectType::kPeerConnection);
  // mrsPeerConnectionSetUserData(pair.pc1(), h1);
  // mrsPeerConnectionSetUserData(pair.pc1(), h1);

  constexpr const int kNumTracks = 5;
  struct TestTrack {
    int id{0};
    int frame_count{0};
    I420VideoFrameCallback frame_cb{};
    mrsLocalVideoTrackHandle local_handle{};
    mrsRemoteVideoTrackHandle remote_handle{};
    mrsTransceiverHandle local_transceiver_handle{};
    mrsTransceiverHandle remote_transceiver_handle{};
  };
  TestTrack tracks[kNumTracks];

  // Grab the handle of the remote track from the remote peer (#2) via the
  // VideoTrackAdded callback.
  Semaphore track_added2_sem;
  std::atomic_int32_t track_id{0};
  VideoTrackAddedCallback track_added2_cb =
      [&track_added2_sem, &track_id, &tracks,
       kNumTracks](const mrsRemoteVideoTrackAddedInfo* info) {
        int id = track_id.fetch_add(1);
        ASSERT_LT(id, kNumTracks);
        tracks[id].remote_handle = info->track_handle;
        tracks[id].remote_transceiver_handle = info->audio_transceiver_handle;
        track_added2_sem.Release();
      };
  mrsPeerConnectionRegisterVideoTrackAddedCallback(pair.pc2(),
                                                   CB(track_added2_cb));

  // Create the external source for the local tracks of the local peer (#1)
  mrsExternalVideoTrackSourceHandle source_handle1 = nullptr;
  ASSERT_EQ(mrsResult::kSuccess,
            mrsExternalVideoTrackSourceCreateFromI420ACallback(
                &VideoTestUtils::MakeTestFrame, nullptr, &source_handle1));
  ASSERT_NE(nullptr, source_handle1);
  mrsExternalVideoTrackSourceFinishCreation(source_handle1);

  // Create local video tracks on the local peer (#1)
  int idx = 0;
  for (auto&& track : tracks) {
    std::stringstream strstr;
    std::string str;
    mrsTransceiverInitConfig transceiver_config{};
    strstr << "transceiver_1_" << idx;
    str = strstr.str();  // keep alive
    transceiver_config.name = str.c_str();
    transceiver_config.media_kind = mrsMediaKind::kVideo;
    ASSERT_EQ(Result::kSuccess,
              mrsPeerConnectionAddTransceiver(pair.pc1(), &transceiver_config,
                                              &track.local_transceiver_handle));
    ASSERT_NE(nullptr, track.local_transceiver_handle);
    strstr.clear();
    strstr << "track_1_" << idx;
    str = strstr.str();  // keep alive
    mrsLocalVideoTrackInitSettings track_settings{};
    track_settings.track_name = str.c_str();
    ASSERT_EQ(Result::kSuccess,
              mrsLocalVideoTrackCreateFromSource(
                  &track_settings, source_handle1, &track.local_handle));
    ASSERT_NE(nullptr, track.local_handle);
    ASSERT_EQ(Result::kSuccess,
              mrsTransceiverSetLocalVideoTrack(track.local_transceiver_handle,
                                               track.local_handle));
    ASSERT_NE(mrsBool::kFalse, mrsLocalVideoTrackIsEnabled(track.local_handle));

    // Check video transceiver consistency
    {
      mrsLocalVideoTrackHandle track_handle_local{};
      ASSERT_EQ(Result::kSuccess,
                mrsTransceiverGetLocalVideoTrack(track.local_transceiver_handle,
                                                 &track_handle_local));
      ASSERT_EQ(track.local_handle, track_handle_local);

      mrsRemoteVideoTrackHandle track_handle_remote{};
      ASSERT_EQ(Result::kSuccess,
                mrsTransceiverGetRemoteVideoTrack(
                    track.local_transceiver_handle, &track_handle_remote));
      ASSERT_EQ(nullptr, track_handle_remote);
    }

    ++idx;
  }

  // Connect #1 and #2
  pair.ConnectAndWait();

  // Wait for all remote tracks to be added on #2
  ASSERT_TRUE(track_added2_sem.TryAcquireFor(5s, kNumTracks));
  for (auto&& track : tracks) {
    ASSERT_NE(nullptr, track.remote_handle);
  }

  // Register a frame callback for the remote video of #2
  for (auto&& track : tracks) {
    track.frame_cb = [&track](const I420AVideoFrame& frame) {
      ASSERT_NE(nullptr, frame.ydata_);
      ASSERT_NE(nullptr, frame.udata_);
      ASSERT_NE(nullptr, frame.vdata_);
      ASSERT_LT(0u, frame.width_);
      ASSERT_LT(0u, frame.height_);
      ++track.frame_count;
    };
    mrsRemoteVideoTrackRegisterI420AFrameCallback(track.remote_handle,
                                                  CB(track.frame_cb));
  }

  Event ev;
  ev.WaitFor(3s);
  for (auto&& track : tracks) {
    ASSERT_LT(30, track.frame_count) << "Expected at least 10 FPS";
  }

  ASSERT_TRUE(pair.WaitExchangeCompletedFor(5s));

  // Clean-up
  for (auto&& track : tracks) {
    mrsRemoteVideoTrackRegisterI420AFrameCallback(track.remote_handle, nullptr,
                                                  nullptr);
    mrsRefCountedObjectRemoveRef(track.local_handle);
  }
  mrsRefCountedObjectRemoveRef(source_handle1);
}

TEST_P(VideoTrackTests, ExternalI420) {
  mrsPeerConnectionConfiguration pc_config{};
  pc_config.sdp_semantic = GetParam();
  LocalPeerPairRaii pair(pc_config);

  // Grab the handle of the remote track from the remote peer (#2) via the
  // VideoTrackAdded callback.
  mrsRemoteVideoTrackHandle track_handle2{};
  mrsTransceiverHandle transceiver_handle2{};
  Event track_added2_ev;
  VideoTrackAddedCallback track_added2_cb =
      [&track_handle2, &transceiver_handle2,
       &track_added2_ev](const mrsRemoteVideoTrackAddedInfo* info) {
        track_handle2 = info->track_handle;
        transceiver_handle2 = info->audio_transceiver_handle;
        track_added2_ev.Set();
      };
  mrsPeerConnectionRegisterVideoTrackAddedCallback(pair.pc2(),
                                                   CB(track_added2_cb));

  // Create the video transceiver #1
  mrsTransceiverHandle transceiver_handle1{};
  {
    mrsTransceiverInitConfig transceiver_config{};
    transceiver_config.name = "video_transceiver_1";
    transceiver_config.media_kind = mrsMediaKind::kVideo;
    ASSERT_EQ(Result::kSuccess,
              mrsPeerConnectionAddTransceiver(pair.pc1(), &transceiver_config,
                                              &transceiver_handle1));
    ASSERT_NE(nullptr, transceiver_handle1);
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
    settings.track_name = "simulated_video_track";
    ASSERT_EQ(mrsResult::kSuccess,
              mrsLocalVideoTrackCreateFromSource(&settings, source_handle1,
                                                 &track_handle1));
    ASSERT_NE(nullptr, track_handle1);
    ASSERT_NE(mrsBool::kFalse, mrsLocalVideoTrackIsEnabled(track_handle1));
  }

  // Add the local track #1 on the transceiver #1
  ASSERT_EQ(Result::kSuccess, mrsTransceiverSetLocalVideoTrack(
                                  transceiver_handle1, track_handle1));

  // Check video transceiver #1 consistency
  {
    // Local track is track_handle1
    mrsLocalVideoTrackHandle track_handle_local{};
    ASSERT_EQ(Result::kSuccess, mrsTransceiverGetLocalVideoTrack(
                                    transceiver_handle1, &track_handle_local));
    ASSERT_EQ(track_handle1, track_handle_local);

    // Remote track is NULL
    mrsRemoteVideoTrackHandle track_handle_remote{};
    ASSERT_EQ(Result::kSuccess, mrsTransceiverGetRemoteVideoTrack(
                                    transceiver_handle1, &track_handle_remote));
    ASSERT_EQ(nullptr, track_handle_remote);
  }

  // Connect #1 and #2
  pair.ConnectAndWait();

  // Wait for remote track to be added on #2
  ASSERT_TRUE(track_added2_ev.WaitFor(5s));
  ASSERT_NE(nullptr, track_handle2);
  ASSERT_NE(nullptr, transceiver_handle2);

  // Register a frame callback for the remote video of #2
  uint32_t frame_count = 0;
  I420VideoFrameCallback i420cb = [&frame_count](const I420AVideoFrame& frame) {
    VideoTestUtils::CheckIsTestFrame(frame);
    ++frame_count;
  };
  mrsRemoteVideoTrackRegisterI420AFrameCallback(track_handle2, CB(i420cb));

  Event ev;
  ev.WaitFor(3s);
  ASSERT_LT(30u, frame_count) << "Expected at least 10 FPS";

  ASSERT_TRUE(pair.WaitExchangeCompletedFor(5s));

  mrsRemoteVideoTrackRegisterI420AFrameCallback(track_handle2, nullptr,
                                                nullptr);
  mrsRefCountedObjectRemoveRef(track_handle1);
  mrsExternalVideoTrackSourceShutdown(source_handle1);
  mrsRefCountedObjectRemoveRef(source_handle1);
}
