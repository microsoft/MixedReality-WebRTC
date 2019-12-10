// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include "interop/external_video_track_source_interop.h"
#include "interop/interop_api.h"
#include "interop/local_video_track_interop.h"

#if !defined(MRSW_EXCLUDE_DEVICE_TESTS)

namespace {

// PeerConnectionI420VideoFrameCallback
using I420VideoFrameCallback = InteropCallback<const I420AVideoFrame&>;

/// Generate a test frame to simualte an external video track source.
mrsResult MRS_CALL MakeTestFrame(void* /*user_data*/,
                                 ExternalVideoTrackSourceHandle handle,
                                 uint32_t request_id,
                                 int64_t timestamp_ms) {
  // Generate a frame
  uint8_t buffer_y[256];
  uint8_t buffer_u[64];
  uint8_t buffer_v[64];
  memset(buffer_y, 0x7F, 256);
  memset(buffer_u, 0x7F, 64);
  memset(buffer_v, 0x7F, 64);

  // Complete the frame request with the generated frame
  mrsI420AVideoFrame frame{};
  frame.width_ = 16;
  frame.height_ = 16;
  frame.ydata_ = buffer_y;
  frame.udata_ = buffer_u;
  frame.vdata_ = buffer_v;
  frame.ystride_ = 16;
  frame.ustride_ = 8;
  frame.vstride_ = 8;
  return mrsExternalVideoTrackSourceCompleteI420AFrameRequest(
      handle, request_id, timestamp_ms, &frame);
}

void CheckIsTestFrame(const I420AVideoFrame& frame) {
  ASSERT_EQ(16u, frame.width_);
  ASSERT_EQ(16u, frame.height_);
  ASSERT_NE(nullptr, frame.ydata_);
  ASSERT_NE(nullptr, frame.udata_);
  ASSERT_NE(nullptr, frame.vdata_);
  ASSERT_EQ(16, frame.ystride_);
  ASSERT_EQ(8, frame.ustride_);
  ASSERT_EQ(8, frame.vstride_);
  {
    const uint8_t* s = (const uint8_t*)frame.ydata_;
    const uint8_t* e = s + ((size_t)frame.ystride_ * frame.height_);
    bool all_7f = true;
    for (const uint8_t* p = s; p < e; ++p) {
      all_7f = all_7f && (*p == 0x7F);
    }
    ASSERT_TRUE(all_7f);
  }
  {
    const uint8_t* s = (const uint8_t*)frame.udata_;
    const uint8_t* e = s + ((size_t)frame.ustride_ * frame.height_ / 2);
    bool all_7f = true;
    for (const uint8_t* p = s; p < e; ++p) {
      all_7f = all_7f && (*p == 0x7F);
    }
    ASSERT_TRUE(all_7f);
  }
  {
    const uint8_t* s = (const uint8_t*)frame.vdata_;
    const uint8_t* e = s + ((size_t)frame.vstride_ * frame.height_ / 2);
    bool all_7f = true;
    for (const uint8_t* p = s; p < e; ++p) {
      all_7f = all_7f && (*p == 0x7F);
    }
    ASSERT_TRUE(all_7f);
  }
}

}  // namespace

TEST(VideoTrack, Simple) {
  LocalPeerPairRaii pair;

  VideoDeviceConfiguration config{};
  LocalVideoTrackHandle track_handle{};
  ASSERT_EQ(Result::kSuccess,
            mrsPeerConnectionAddLocalVideoTrack(pair.pc1(), "local_video_track",
                                                config, &track_handle));
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

TEST(VideoTrack, Muted) {
  LocalPeerPairRaii pair;

  VideoDeviceConfiguration config{};
  LocalVideoTrackHandle track_handle{};
  ASSERT_EQ(Result::kSuccess,
            mrsPeerConnectionAddLocalVideoTrack(pair.pc1(), "local_video_track",
                                                config, &track_handle));

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
//    VideoDeviceConfiguration config{};
//    config.video_device_id = id.c_str();
//    ASSERT_EQ(Result::kSuccess,
//              mrsPeerConnectionAddLocalVideoTrack(pair.pc1(), config));
//  }
//}

TEST(VideoTrack, DeviceIdInvalid) {
  LocalPeerPairRaii pair;

  VideoDeviceConfiguration config{};
  LocalVideoTrackHandle track_handle{};
  config.video_device_id = "[[INVALID DEVICE ID]]";
  ASSERT_EQ(Result::kNotFound,
            mrsPeerConnectionAddLocalVideoTrack(pair.pc1(), "invalid_track",
                                                config, &track_handle));
  ASSERT_EQ(nullptr, track_handle);
}

TEST(VideoTrack, ExternalI420) {
  LocalPeerPairRaii pair;

  ExternalVideoTrackSourceHandle source_handle = nullptr;
  ASSERT_EQ(mrsResult::kSuccess,
            mrsExternalVideoTrackSourceCreateFromI420ACallback(
                &MakeTestFrame, nullptr, &source_handle));
  ASSERT_NE(nullptr, source_handle);

  LocalVideoTrackHandle track_handle = nullptr;
  ASSERT_EQ(
      mrsResult::kSuccess,
      mrsPeerConnectionAddLocalVideoTrackFromExternalSource(
          pair.pc1(), "simulated_video_track", source_handle, &track_handle));
  ASSERT_NE(nullptr, track_handle);
  ASSERT_NE(mrsBool::kFalse, mrsLocalVideoTrackIsEnabled(track_handle));

  uint32_t frame_count = 0;
  I420VideoFrameCallback i420cb = [&frame_count](const I420AVideoFrame& frame) {
    CheckIsTestFrame(frame);
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
