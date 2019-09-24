// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#include "pch.h"

#include "api.h"

#if !defined(MRSW_EXCLUDE_DEVICE_TESTS)

namespace {

// PeerConnectionI420VideoFrameCallback
using I420VideoFrameCallback = Callback<const void*,
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
  ASSERT_EQ(MRS_SUCCESS,
            mrsPeerConnectionAddLocalVideoTrack(pair.pc1(), config));
  ASSERT_NE(0, mrsPeerConnectionIsLocalVideoTrackEnabled(pair.pc1()));

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
  mrsPeerConnectionRegisterI420RemoteVideoFrameCallback(pair.pc2(), CB(i420cb));

  pair.ConnectAndWait();

  Event ev;
  ev.WaitFor(5s);
  ASSERT_LT(50u, frame_count);  // at least 10 FPS

  mrsPeerConnectionRegisterI420RemoteVideoFrameCallback(pair.pc2(), nullptr,
                                                        nullptr);
}

TEST(VideoTrack, Muted) {
  LocalPeerPairRaii pair;

  VideoDeviceConfiguration config{};
  ASSERT_EQ(MRS_SUCCESS,
            mrsPeerConnectionAddLocalVideoTrack(pair.pc1(), config));

  // Disable the video track; it should output only black frames
  ASSERT_EQ(MRS_SUCCESS,
            mrsPeerConnectionSetLocalVideoTrackEnabled(pair.pc1(), false));
  ASSERT_EQ(0, mrsPeerConnectionIsLocalVideoTrackEnabled(pair.pc1()));

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
        const uint8_t* e = s + (ystride * frame_height);
        bool all_black = true;
        for (const uint8_t* p = s; p < e; ++p) {
          all_black = all_black && (*p == 0);
        }
        // Note: U and V can be anything, so don't test them.
        ASSERT_TRUE(all_black);
        ++frame_count;
      };
  mrsPeerConnectionRegisterI420RemoteVideoFrameCallback(pair.pc2(), CB(i420cb));

  pair.ConnectAndWait();

  Event ev;
  ev.WaitFor(5s);
  ASSERT_LT(50u, frame_count);  // at least 10 FPS

  mrsPeerConnectionRegisterI420RemoteVideoFrameCallback(pair.pc2(), nullptr,
                                                        nullptr);
}

#endif  // MRSW_EXCLUDE_DEVICE_TESTS
