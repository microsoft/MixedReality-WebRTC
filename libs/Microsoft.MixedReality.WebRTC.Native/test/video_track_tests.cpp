// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include "external_video_track_source_interop.h"
#include "interop_api.h"
#include "local_video_track_interop.h"

#include "test_utils.h"
#include "video_test_utils.h"

#if !defined(MRSW_EXCLUDE_DEVICE_TESTS)

namespace {

// PeerConnectionI420VideoFrameCallback
using I420VideoFrameCallback = InteropCallback<const I420AVideoFrame&>;

class VideoTrackTests : public TestUtils::TestBase {};

}  // namespace

TEST_F(VideoTrackTests, Simple) {
  LocalPeerPairRaii pair;

  LocalVideoTrackInitConfig config{};
  LocalVideoTrackHandle track_handle{};
  ASSERT_EQ(Result::kSuccess,
            mrsPeerConnectionAddLocalVideoTrack(pair.pc1(), "local_video_track",
                                                &config, &track_handle));
  ASSERT_NE(mrsBool::kFalse, mrsLocalVideoTrackIsEnabled(track_handle));

  uint32_t frame_count = 0;
  I420VideoFrameCallback i420cb = [&frame_count](const I420AVideoFrame& frame) {
    ASSERT_NE(nullptr, frame.ydata_);
    ASSERT_NE(nullptr, frame.udata_);
    ASSERT_NE(nullptr, frame.vdata_);
    ASSERT_LT(0u, frame.width_);
    ASSERT_LT(0u, frame.height_);
    ++frame_count;
  };
  mrsPeerConnectionRegisterI420ARemoteVideoFrameCallback(pair.pc2(),
                                                         CB(i420cb));

  pair.ConnectAndWait();

  Event ev;
  ev.WaitFor(5s);
  ASSERT_LT(50u, frame_count);  // at least 10 FPS

  mrsPeerConnectionRegisterI420ARemoteVideoFrameCallback(pair.pc2(), nullptr,
                                                         nullptr);
  mrsLocalVideoTrackRemoveRef(track_handle);
}

TEST_F(VideoTrackTests, Muted) {
  LocalPeerPairRaii pair;

  LocalVideoTrackInitConfig config{};
  LocalVideoTrackHandle track_handle{};
  ASSERT_EQ(Result::kSuccess,
            mrsPeerConnectionAddLocalVideoTrack(pair.pc1(), "local_video_track",
                                                &config, &track_handle));

  // New tracks are enabled by default
  ASSERT_NE(mrsBool::kFalse, mrsLocalVideoTrackIsEnabled(track_handle));

  // Disable the video track; it should output only black frames
  ASSERT_EQ(Result::kSuccess,
            mrsLocalVideoTrackSetEnabled(track_handle, mrsBool::kFalse));
  ASSERT_EQ(mrsBool::kFalse, mrsLocalVideoTrackIsEnabled(track_handle));

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
  mrsPeerConnectionRegisterI420ARemoteVideoFrameCallback(pair.pc2(),
                                                         CB(i420cb));

  pair.ConnectAndWait();

  Event ev;
  ev.WaitFor(5s);
  ASSERT_LT(50u, frame_count);  // at least 10 FPS

  mrsPeerConnectionRegisterI420ARemoteVideoFrameCallback(pair.pc2(), nullptr,
                                                         nullptr);
  mrsLocalVideoTrackRemoveRef(track_handle);
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
// TEST(VideoTrack, DeviceIdAll) {
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
  LocalPeerPairRaii pair;

  LocalVideoTrackInitConfig config{};
  LocalVideoTrackHandle track_handle{};
  config.video_device_id = "[[INVALID DEVICE ID]]";
  ASSERT_EQ(Result::kNotFound,
            mrsPeerConnectionAddLocalVideoTrack(pair.pc1(), "invalid_track",
                                                &config, &track_handle));
  ASSERT_EQ(nullptr, track_handle);
}

TEST_F(VideoTrackTests, ExternalI420) {
  LocalPeerPairRaii pair;

  ExternalVideoTrackSourceHandle source_handle = nullptr;
  ASSERT_EQ(mrsResult::kSuccess,
            mrsExternalVideoTrackSourceCreateFromI420ACallback(
                &VideoTestUtils::MakeTestFrame, nullptr, &source_handle));
  ASSERT_NE(nullptr, source_handle);
  mrsExternalVideoTrackSourceFinishCreation(source_handle);

  LocalVideoTrackHandle track_handle = nullptr;
  LocalVideoTrackFromExternalSourceInitConfig source_config{};
  ASSERT_EQ(mrsResult::kSuccess,
            mrsPeerConnectionAddLocalVideoTrackFromExternalSource(
                pair.pc1(), "simulated_video_track", source_handle,
                &source_config, &track_handle));
  ASSERT_NE(nullptr, track_handle);
  ASSERT_NE(mrsBool::kFalse, mrsLocalVideoTrackIsEnabled(track_handle));

  uint32_t frame_count = 0;
  I420VideoFrameCallback i420cb = [&frame_count](const I420AVideoFrame& frame) {
    VideoTestUtils::CheckIsTestFrame(frame);
    ++frame_count;
  };
  mrsPeerConnectionRegisterI420ARemoteVideoFrameCallback(pair.pc2(),
                                                         CB(i420cb));

  pair.ConnectAndWait();

  Event ev;
  ev.WaitFor(5s);
  ASSERT_LT(50u, frame_count);  // at least 10 FPS

  mrsPeerConnectionRegisterI420ARemoteVideoFrameCallback(pair.pc2(), nullptr,
                                                         nullptr);
  mrsPeerConnectionRemoveLocalVideoTrack(pair.pc1(), track_handle);
  mrsLocalVideoTrackRemoveRef(track_handle);
  mrsExternalVideoTrackSourceShutdown(source_handle);
  mrsExternalVideoTrackSourceRemoveRef(source_handle);
}

#endif  // MRSW_EXCLUDE_DEVICE_TESTS
