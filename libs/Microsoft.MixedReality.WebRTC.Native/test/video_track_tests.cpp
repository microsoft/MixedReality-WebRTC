// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include "external_video_track_source_interop.h"
#include "interop/remote_video_track_interop.h"
#include "interop/video_transceiver_interop.h"
#include "interop_api.h"
#include "local_video_track_interop.h"

#include "simple_interop.h"
#include "test_utils.h"
#include "video_test_utils.h"

#if !defined(MRSW_EXCLUDE_DEVICE_TESTS)

namespace {

const mrsPeerConnectionInteropHandle kFakeInteropPeerConnectionHandle =
    (void*)0x1;

const mrsRemoteVideoTrackInteropHandle kFakeInteropRemoteVideoTrackHandle =
    (void*)0x2;

/// Fake interop callback always returning the same fake remote video track
/// interop handle, for tests which do not care about it.
mrsRemoteVideoTrackInteropHandle MRS_CALL FakeIterop_RemoteVideoTrackCreate(
    mrsPeerConnectionInteropHandle /*parent*/,
    const mrsRemoteVideoTrackConfig& /*config*/) noexcept {
  return kFakeInteropRemoteVideoTrackHandle;
}

// PeerConnectionVideoTrackAddedCallback
using VideoTrackAddedCallback =
    InteropCallback<mrsRemoteVideoTrackInteropHandle,
                    RemoteVideoTrackHandle,
                    mrsVideoTransceiverInteropHandle,
                    VideoTransceiverHandle>;

// PeerConnectionI420VideoFrameCallback
using I420VideoFrameCallback = InteropCallback<const I420AVideoFrame&>;

class VideoTrackTests : public TestUtils::TestBase {};

}  // namespace

TEST_F(VideoTrackTests, Simple) {
  LocalPeerPairRaii pair;

  // In order to allow creating interop wrappers from native code, register the
  // necessary interop callbacks.
  mrsPeerConnectionInteropCallbacks interop{};
  interop.remote_video_track_create_object = &FakeIterop_RemoteVideoTrackCreate;
  ASSERT_EQ(Result::kSuccess,
            mrsPeerConnectionRegisterInteropCallbacks(pair.pc2(), &interop));

  // Register event for renegotiation needed
  Event renegotiation_needed1_ev;
  InteropCallback renegotiation_needed1_cb = [&renegotiation_needed1_ev]() {
    renegotiation_needed1_ev.Set();
  };
  mrsPeerConnectionRegisterRenegotiationNeededCallback(
      pair.pc1(), CB(renegotiation_needed1_cb));

  // Grab the handle of the remote track from the remote peer (#2) via the
  // VideoTrackAdded callback.
  RemoteVideoTrackHandle track_handle2{};
  VideoTransceiverHandle transceiver_handle2{};
  Event track_added2_ev;
  VideoTrackAddedCallback track_added2_cb =
      [&track_handle2, &transceiver_handle2, &track_added2_ev](
          mrsRemoteVideoTrackInteropHandle /*interop_handle*/,
          RemoteVideoTrackHandle track_native_handle,
          mrsVideoTransceiverInteropHandle /*interop_handle*/,
          VideoTransceiverHandle transceiver_native_handle) {
        track_handle2 = track_native_handle;
        transceiver_handle2 = transceiver_native_handle;
        track_added2_ev.Set();
      };
  mrsPeerConnectionRegisterVideoTrackAddedCallback(pair.pc2(),
                                                   CB(track_added2_cb));

  // Create the video transceiver #1
  VideoTransceiverHandle transceiver_handle1{};
  {
    renegotiation_needed1_ev.Reset();
    VideoTransceiverInitConfig config{};
    config.name = "transceiver_1";
    ASSERT_EQ(Result::kSuccess, mrsPeerConnectionAddVideoTransceiver(
                                    pair.pc1(), &config, &transceiver_handle1));
    ASSERT_NE(nullptr, transceiver_handle1);
    ASSERT_TRUE(renegotiation_needed1_ev.WaitFor(1s));
    renegotiation_needed1_ev.Reset();
  }

  // Check video transceiver #1 consistency
  {
    // Local track is NULL
    LocalVideoTrackHandle track_handle_local{};
    ASSERT_EQ(Result::kSuccess, mrsVideoTransceiverGetLocalTrack(
                                    transceiver_handle1, &track_handle_local));
    ASSERT_EQ(nullptr, track_handle_local);

    // Remote track is NULL
    RemoteVideoTrackHandle track_handle_remote{};
    ASSERT_EQ(Result::kSuccess, mrsVideoTransceiverGetRemoteTrack(
                                    transceiver_handle1, &track_handle_remote));
    ASSERT_EQ(nullptr, track_handle_remote);
  }

  // Create the local video track #1
  LocalVideoTrackHandle track_handle1{};
  {
    LocalVideoTrackInitConfig config{};
    ASSERT_EQ(Result::kSuccess,
              mrsLocalVideoTrackCreateFromDevice(&config, "local_video_track",
                                                 &track_handle1));
    ASSERT_NE(nullptr, track_handle1);
  }

  // New tracks are enabled by default
  ASSERT_NE(mrsBool::kFalse, mrsLocalVideoTrackIsEnabled(track_handle1));

  // Add the local track #1 on the transceiver #1.
  ASSERT_FALSE(renegotiation_needed1_ev.IsSignaled());
  ASSERT_EQ(Result::kSuccess, mrsVideoTransceiverSetLocalTrack(
                                  transceiver_handle1, track_handle1));
  ASSERT_FALSE(
      renegotiation_needed1_ev
          .IsSignaled());  // TODO: why? because transceiver starts in SendRecv
                           // mode, so didn't change direction?

  // Check video transceiver #1 consistency
  {
    // Local track is track_handle1
    LocalVideoTrackHandle track_handle_local{};
    ASSERT_EQ(Result::kSuccess, mrsVideoTransceiverGetLocalTrack(
                                    transceiver_handle1, &track_handle_local));
    ASSERT_EQ(track_handle1, track_handle_local);
    mrsLocalVideoTrackRemoveRef(track_handle_local);

    // Remote track is NULL
    RemoteVideoTrackHandle track_handle_remote{};
    ASSERT_EQ(Result::kSuccess, mrsVideoTransceiverGetRemoteTrack(
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
    LocalVideoTrackHandle track_handle_local{};
    ASSERT_EQ(Result::kSuccess, mrsVideoTransceiverGetLocalTrack(
                                    transceiver_handle2, &track_handle_local));
    ASSERT_EQ(nullptr, track_handle_local);

    // Remote track is track_handle2
    RemoteVideoTrackHandle track_handle_remote{};
    ASSERT_EQ(Result::kSuccess, mrsVideoTransceiverGetRemoteTrack(
                                    transceiver_handle2, &track_handle_remote));
    ASSERT_EQ(track_handle2, track_handle_remote);
    mrsRemoteVideoTrackRemoveRef(track_handle_remote);
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

  // Wait 5 seconds and check the frame callback is called
  Event ev;
  ev.WaitFor(5s);
  ASSERT_LT(50u, frame_count) << "Expected at least 10 FPS";

  // Clean-up
  mrsRemoteVideoTrackRegisterI420AFrameCallback(track_handle2, nullptr,
                                                nullptr);
  mrsRemoteVideoTrackRemoveRef(track_handle2);
  mrsVideoTransceiverRemoveRef(transceiver_handle2);
  mrsLocalVideoTrackRemoveRef(track_handle1);
  mrsVideoTransceiverRemoveRef(transceiver_handle1);
}

TEST_F(VideoTrackTests, Muted) {
  LocalPeerPairRaii pair;

  // In order to allow creating interop wrappers from native code, register the
  // necessary interop callbacks.
  mrsPeerConnectionInteropCallbacks interop{};
  interop.remote_video_track_create_object = &FakeIterop_RemoteVideoTrackCreate;
  ASSERT_EQ(Result::kSuccess,
            mrsPeerConnectionRegisterInteropCallbacks(pair.pc2(), &interop));

  // Grab the handle of the remote track from the remote peer (#2) via the
  // VideoTrackAdded callback.
  RemoteVideoTrackHandle track_handle2{};
  VideoTransceiverHandle transceiver_handle2{};
  Event track_added2_ev;
  VideoTrackAddedCallback track_added2_cb =
      [&track_handle2, &transceiver_handle2, &track_added2_ev](
          mrsRemoteVideoTrackInteropHandle /*interop_handle*/,
          RemoteVideoTrackHandle track_native_handle,
          mrsVideoTransceiverInteropHandle /*interop_handle*/,
          VideoTransceiverHandle transceiver_native_handle) {
        track_handle2 = track_native_handle;
        transceiver_handle2 = transceiver_native_handle;
        track_added2_ev.Set();
      };
  mrsPeerConnectionRegisterVideoTrackAddedCallback(pair.pc2(),
                                                   CB(track_added2_cb));

  // Create the video transceiver #1
  VideoTransceiverHandle transceiver_handle1{};
  {
    VideoTransceiverInitConfig config{};
    config.name = "transceiver_1";
    ASSERT_EQ(Result::kSuccess, mrsPeerConnectionAddVideoTransceiver(
                                    pair.pc1(), &config, &transceiver_handle1));
    ASSERT_NE(nullptr, transceiver_handle1);
  }

  // Create the local video track #1
  LocalVideoTrackHandle track_handle1{};
  {
    LocalVideoTrackInitConfig config{};
    ASSERT_EQ(Result::kSuccess,
              mrsLocalVideoTrackCreateFromDevice(&config, "local_video_track",
                                                 &track_handle1));
    ASSERT_NE(nullptr, track_handle1);
  }

  // New tracks are enabled by default
  ASSERT_NE(mrsBool::kFalse, mrsLocalVideoTrackIsEnabled(track_handle1));

  // Disable the video track; it should output only black frames
  ASSERT_EQ(Result::kSuccess,
            mrsLocalVideoTrackSetEnabled(track_handle1, mrsBool::kFalse));
  ASSERT_EQ(mrsBool::kFalse, mrsLocalVideoTrackIsEnabled(track_handle1));

  // Add the local track #1 on the transceiver #1
  ASSERT_EQ(Result::kSuccess, mrsVideoTransceiverSetLocalTrack(
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

  // Wait 5 seconds and check the frame callback is called
  Event ev;
  ev.WaitFor(5s);
  ASSERT_LT(50u, frame_count) << "Expected at least 10 FPS";

  // Clean-up
  mrsRemoteVideoTrackRegisterI420AFrameCallback(track_handle2, nullptr,
                                                nullptr);
  mrsRemoteVideoTrackRemoveRef(track_handle2);
  mrsVideoTransceiverRemoveRef(transceiver_handle2);
  mrsLocalVideoTrackRemoveRef(track_handle1);
  mrsVideoTransceiverRemoveRef(transceiver_handle1);
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
//    LocalVideoTrackInitConfig config{};
//    config.video_device_id = id.c_str();
//    ASSERT_EQ(Result::kSuccess,
//              mrsPeerConnectionAddLocalVideoTrack(pair.pc1(), config));
//  }
//}

TEST_F(VideoTrackTests, DeviceIdInvalid) {
  LocalVideoTrackInitConfig config{};
  LocalVideoTrackHandle track_handle{};
  config.video_device_id = "[[INVALID DEVICE ID]]";
  ASSERT_EQ(Result::kNotFound, mrsLocalVideoTrackCreateFromDevice(
                                   &config, "invalid_track", &track_handle));
  ASSERT_EQ(nullptr, track_handle);
}

TEST_F(VideoTrackTests, Multi) {
  SimpleInterop simple_interop1;
  SimpleInterop simple_interop2;

  mrsPeerConnectionInteropHandle h1 =
      simple_interop1.CreateObject(ObjectType::kPeerConnection);
  mrsPeerConnectionInteropHandle h2 =
      simple_interop2.CreateObject(ObjectType::kPeerConnection);

  PeerConnectionConfiguration pc_config{};
  LocalPeerPairRaii pair(pc_config, h1, h2);

  constexpr const int kNumTracks = 5;
  struct TestTrack {
    int id{0};
    int frame_count{0};
    I420VideoFrameCallback frame_cb{};
    LocalVideoTrackHandle local_handle{};
    RemoteVideoTrackHandle remote_handle{};
    VideoTransceiverHandle local_transceiver_handle{};
    VideoTransceiverHandle remote_transceiver_handle{};
  };
  TestTrack tracks[kNumTracks];

  // In order to allow creating interop wrappers from native code, register the
  // necessary interop callbacks.
  simple_interop1.Register(pair.pc1());
  simple_interop2.Register(pair.pc2());

  // Grab the handle of the remote track from the remote peer (#2) via the
  // VideoTrackAdded callback.
  Semaphore track_added2_sem;
  std::atomic_int32_t track_id{0};
  VideoTrackAddedCallback track_added2_cb =
      [&track_added2_sem, &track_id, &tracks, kNumTracks](
          mrsRemoteVideoTrackInteropHandle /*interop_handle*/,
          RemoteVideoTrackHandle track_handle,
          mrsVideoTransceiverInteropHandle /*interop_handle*/,
          VideoTransceiverHandle transceiver_handle) {
        int id = track_id.fetch_add(1);
        ASSERT_LT(id, kNumTracks);
        tracks[id].remote_handle = track_handle;
        tracks[id].remote_transceiver_handle = transceiver_handle;
        track_added2_sem.Release();
      };
  mrsPeerConnectionRegisterVideoTrackAddedCallback(pair.pc2(),
                                                   CB(track_added2_cb));

  // Create the external source for the local tracks of the local peer (#1)
  ExternalVideoTrackSourceHandle source_handle1 = nullptr;
  ASSERT_EQ(mrsResult::kSuccess,
            mrsExternalVideoTrackSourceCreateFromI420ACallback(
                &VideoTestUtils::MakeTestFrame, nullptr, &source_handle1));
  ASSERT_NE(nullptr, source_handle1);
  mrsExternalVideoTrackSourceFinishCreation(source_handle1);

  // Create local video tracks on the local peer (#1)
  LocalVideoTrackFromExternalSourceInitConfig track_config{};
  int idx = 0;
  for (auto&& track : tracks) {
    std::stringstream strstr;
    std::string str;
    VideoTransceiverInitConfig tranceiver_config{};
    strstr << "transceiver_1_" << idx;
    str = strstr.str();  // keep alive
    tranceiver_config.name = str.c_str();
    ASSERT_EQ(Result::kSuccess, mrsPeerConnectionAddVideoTransceiver(
                                    pair.pc1(), &tranceiver_config,
                                    &track.local_transceiver_handle));
    ASSERT_NE(nullptr, track.local_transceiver_handle);
    strstr.clear();
    strstr << "track_1_" << idx;
    str = strstr.str();  // keep alive
    ASSERT_EQ(Result::kSuccess, mrsLocalVideoTrackCreateFromExternalSource(
                                    source_handle1, &track_config, str.c_str(),
                                    &track.local_handle));
    ASSERT_NE(nullptr, track.local_handle);
    ASSERT_EQ(Result::kSuccess,
              mrsVideoTransceiverSetLocalTrack(track.local_transceiver_handle,
                                               track.local_handle));
    ASSERT_NE(mrsBool::kFalse, mrsLocalVideoTrackIsEnabled(track.local_handle));

    // Check video transceiver consistency
    {
      LocalVideoTrackHandle track_handle_local{};
      ASSERT_EQ(Result::kSuccess,
                mrsVideoTransceiverGetLocalTrack(track.local_transceiver_handle,
                                                 &track_handle_local));
      ASSERT_EQ(track.local_handle, track_handle_local);
      mrsLocalVideoTrackRemoveRef(track_handle_local);

      RemoteVideoTrackHandle track_handle_remote{};
      ASSERT_EQ(Result::kSuccess,
                mrsVideoTransceiverGetRemoteTrack(
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
  ev.WaitFor(5s);
  for (auto&& track : tracks) {
    ASSERT_LT(50, track.frame_count) << "Expected at least 10 FPS";
  }

  // Clean-up
  for (auto&& track : tracks) {
    mrsRemoteVideoTrackRegisterI420AFrameCallback(track.remote_handle, nullptr,
                                                  nullptr);
    mrsRemoteVideoTrackRemoveRef(track.remote_handle);
    mrsVideoTransceiverRemoveRef(track.remote_transceiver_handle);
    mrsLocalVideoTrackRemoveRef(track.local_handle);
    mrsVideoTransceiverRemoveRef(track.local_transceiver_handle);
  }

  simple_interop1.Unregister(pair.pc1());
  simple_interop2.Unregister(pair.pc2());
}

TEST_F(VideoTrackTests, ExternalI420) {
  LocalPeerPairRaii pair;

  // In order to allow creating interop wrappers from native code, register the
  // necessary interop callbacks.
  mrsPeerConnectionInteropCallbacks interop{};
  interop.remote_video_track_create_object = &FakeIterop_RemoteVideoTrackCreate;
  ASSERT_EQ(Result::kSuccess,
            mrsPeerConnectionRegisterInteropCallbacks(pair.pc2(), &interop));

  // Grab the handle of the remote track from the remote peer (#2) via the
  // VideoTrackAdded callback.
  RemoteVideoTrackHandle track_handle2{};
  VideoTransceiverHandle transceiver_handle2{};
  Event track_added2_ev;
  VideoTrackAddedCallback track_added2_cb =
      [&track_handle2, &transceiver_handle2, &track_added2_ev](
          mrsRemoteVideoTrackInteropHandle /*interop_handle*/,
          RemoteVideoTrackHandle track_native_handle,
          mrsVideoTransceiverInteropHandle /*interop_handle*/,
          VideoTransceiverHandle transceiver_native_handle) {
        track_handle2 = track_native_handle;
        transceiver_handle2 = transceiver_native_handle;
        track_added2_ev.Set();
      };
  mrsPeerConnectionRegisterVideoTrackAddedCallback(pair.pc2(),
                                                   CB(track_added2_cb));

  // Create the video transceiver #1
  VideoTransceiverHandle transceiver_handle1{};
  {
    VideoTransceiverInitConfig config{};
    config.name = "transceiver_1";
    ASSERT_EQ(Result::kSuccess, mrsPeerConnectionAddVideoTransceiver(
                                    pair.pc1(), &config, &transceiver_handle1));
    ASSERT_NE(nullptr, transceiver_handle1);
  }

  // Create the external source for the local video track of the local peer (#1)
  ExternalVideoTrackSourceHandle source_handle1 = nullptr;
  ASSERT_EQ(mrsResult::kSuccess,
            mrsExternalVideoTrackSourceCreateFromI420ACallback(
                &VideoTestUtils::MakeTestFrame, nullptr, &source_handle1));
  ASSERT_NE(nullptr, source_handle1);
  mrsExternalVideoTrackSourceFinishCreation(source_handle1);

  // Create the local video track (#1)
  LocalVideoTrackHandle track_handle1{};
  {
    LocalVideoTrackFromExternalSourceInitConfig config{};
    ASSERT_EQ(
        mrsResult::kSuccess,
        mrsLocalVideoTrackCreateFromExternalSource(
            source_handle1, &config, "simulated_video_track", &track_handle1));
    ASSERT_NE(nullptr, track_handle1);
    ASSERT_NE(mrsBool::kFalse, mrsLocalVideoTrackIsEnabled(track_handle1));
  }

  // Add the local track #1 on the transceiver #1
  ASSERT_EQ(Result::kSuccess, mrsVideoTransceiverSetLocalTrack(
                                  transceiver_handle1, track_handle1));

  // Check video transceiver #1 consistency
  {
    // Local track is track_handle1
    LocalVideoTrackHandle track_handle_local{};
    ASSERT_EQ(Result::kSuccess, mrsVideoTransceiverGetLocalTrack(
                                    transceiver_handle1, &track_handle_local));
    ASSERT_EQ(track_handle1, track_handle_local);
    mrsLocalVideoTrackRemoveRef(track_handle_local);

    // Remote track is NULL
    RemoteVideoTrackHandle track_handle_remote{};
    ASSERT_EQ(Result::kSuccess, mrsVideoTransceiverGetRemoteTrack(
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
  ev.WaitFor(5s);
  ASSERT_LT(50u, frame_count) << "Expected at least 10 FPS";

  mrsRemoteVideoTrackRegisterI420AFrameCallback(track_handle2, nullptr,
                                                nullptr);
  mrsLocalVideoTrackRemoveRef(track_handle1);
  mrsVideoTransceiverRemoveRef(transceiver_handle1);
  mrsRemoteVideoTrackRemoveRef(track_handle2);
  mrsVideoTransceiverRemoveRef(transceiver_handle2);
  mrsExternalVideoTrackSourceShutdown(source_handle1);
  mrsExternalVideoTrackSourceRemoveRef(source_handle1);
}

#endif  // MRSW_EXCLUDE_DEVICE_TESTS
