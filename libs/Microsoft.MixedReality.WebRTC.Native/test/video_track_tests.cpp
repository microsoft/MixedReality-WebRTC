// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include "interop/interop_api.h"
#include "interop/local_video_track_interop.h"

#if !defined(MRSW_EXCLUDE_DEVICE_TESTS)

namespace {

// PeerConnectionI420VideoFrameCallback
using I420VideoFrameCallback = InteropCallback<const void*,
                                               const void*,
                                               const void*,
                                               const void*,
                                               const int,
                                               const int,
                                               const int,
                                               const int,
                                               const int,
                                               const int>;

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
  I420VideoFrameCallback i420cb =
      [&frame_count](const void* yptr, const void* uptr, const void* vptr,
                     const void* /*aptr*/, const int /*ystride*/,
                     const int /*ustride*/, const int /*vstride*/,
                     const int /*astride*/, const int frame_width,
                     const int frame_height) {
        ASSERT_NE(nullptr, yptr);
        ASSERT_NE(nullptr, uptr);
        ASSERT_NE(nullptr, vptr);
        ASSERT_LT(0, frame_width);
        ASSERT_LT(0, frame_height);
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
  I420VideoFrameCallback i420cb =
      [&frame_count](const void* yptr, const void* uptr, const void* vptr,
                     const void* /*aptr*/, const int ystride,
                     const int /*ustride*/, const int /*vstride*/,
                     const int /*astride*/, const int frame_width,
                     const int frame_height) {
        ASSERT_NE(nullptr, yptr);
        ASSERT_NE(nullptr, uptr);
        ASSERT_NE(nullptr, vptr);
        ASSERT_LT(0, frame_width);
        ASSERT_LT(0, frame_height);
        const uint8_t* s = (const uint8_t*)yptr;
        const uint8_t* e = s + ((size_t)ystride * frame_height);
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

#endif  // MRSW_EXCLUDE_DEVICE_TESTS
