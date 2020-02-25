// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include "../include/external_video_track_source_interop.h"

#include "video_test_utils.h"

namespace VideoTestUtils {

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

}  // namespace VideoTestUtils
