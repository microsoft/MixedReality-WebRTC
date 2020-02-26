// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include "data_channel.h"
#include "external_video_track_source_interop.h"
#include "interop_api.h"
#include "local_video_track_interop.h"

#include "test_utils.h"

#include "libyuv.h"

namespace {

class ExternalVideoTrackSourceTests : public TestUtils::TestBase {};

}  // namespace

#if !defined(MRSW_EXCLUDE_DEVICE_TESTS)

namespace {

uint32_t FrameBuffer[256];

void FillSquareArgb32(uint32_t* buffer,
                      int x,
                      int y,
                      int w,
                      int h,
                      int stride,
                      uint32_t color) {
  assert(stride % 4 == 0);
  char* row = ((char*)buffer + (y * stride));
  for (int j = 0; j < h; ++j) {
    uint32_t* ptr = (uint32_t*)row + x;
    for (int i = 0; i < w; ++i) {
      *ptr++ = color;
    }
    row += stride;
  }
}

constexpr uint32_t kRed = 0xFF2250F2u;
constexpr uint32_t kGreen = 0xFF00BA7Fu;
constexpr uint32_t kBlue = 0xFFEFA400u;
constexpr uint32_t kYellow = 0xFF00B9FFu;

/// Generate a 16px by 16px test frame.
mrsResult MRS_CALL
GenerateQuadTestFrame(void* /*user_data*/,
                      ExternalVideoTrackSourceHandle source_handle,
                      uint32_t request_id,
                      int64_t timestamp_ms) {
  memset(FrameBuffer, 0, 256 * 4);
  FillSquareArgb32(FrameBuffer, 0, 0, 8, 8, 64, kRed);
  FillSquareArgb32(FrameBuffer, 8, 0, 8, 8, 64, kGreen);
  FillSquareArgb32(FrameBuffer, 0, 8, 8, 8, 64, kBlue);
  FillSquareArgb32(FrameBuffer, 8, 8, 8, 8, 64, kYellow);
  mrsArgb32VideoFrame frame_view{};
  frame_view.width_ = 16;
  frame_view.height_ = 16;
  frame_view.argb32_data_ = FrameBuffer;
  frame_view.stride_ = 16 * 4;
  return mrsExternalVideoTrackSourceCompleteArgb32FrameRequest(
      source_handle, request_id, timestamp_ms, &frame_view);
}

inline double ArgbColorError(uint32_t ref, uint32_t val) {
  return ((double)(ref & 0xFFu) - (double)(val & 0xFFu)) +
         ((double)((ref & 0xFF00u) >> 8u) - (double)((val & 0xFF00u) >> 8u)) +
         ((double)((ref & 0xFF0000u) >> 16u) -
          (double)((val & 0xFF0000u) >> 16u)) +
         ((double)((ref & 0xFF000000u) >> 24u) -
          (double)((val & 0xFF000000u) >> 24u));
}

void ValidateQuadTestFrame(const void* data,
                           const int stride,
                           const int frame_width,
                           const int frame_height) {
  ASSERT_NE(nullptr, data);
  ASSERT_EQ(16, frame_width);
  ASSERT_EQ(16, frame_height);
  double err = 0.0;
  const uint8_t* row = (const uint8_t*)data;
  for (int j = 0; j < 8; ++j) {
    const uint32_t* argb = (const uint32_t*)row;
    // Red
    for (int i = 0; i < 8; ++i) {
      err += ArgbColorError(kRed, *argb++);
    }
    // Green
    for (int i = 0; i < 8; ++i) {
      err += ArgbColorError(kGreen, *argb++);
    }
    row += stride;
  }
  for (int j = 0; j < 8; ++j) {
    const uint32_t* argb = (const uint32_t*)row;
    // Blue
    for (int i = 0; i < 8; ++i) {
      err += ArgbColorError(kBlue, *argb++);
    }
    // Yellow
    for (int i = 0; i < 8; ++i) {
      err += ArgbColorError(kYellow, *argb++);
    }
    row += stride;
  }
  ASSERT_LE(std::fabs(err), 768.0);  // +/-1 per component over 256 pixels
}

// mrsArgb32VideoFrameCallback
using Argb32VideoFrameCallback = InteropCallback<const mrsArgb32VideoFrame&>;

}  // namespace

TEST_F(ExternalVideoTrackSourceTests, Simple) {
  LocalPeerPairRaii pair;

  ExternalVideoTrackSourceHandle source_handle = nullptr;
  ASSERT_EQ(mrsResult::kSuccess,
            mrsExternalVideoTrackSourceCreateFromArgb32Callback(
                &GenerateQuadTestFrame, nullptr, &source_handle));
  ASSERT_NE(nullptr, source_handle);
  mrsExternalVideoTrackSourceFinishCreation(source_handle);

  LocalVideoTrackHandle track_handle = nullptr;
  LocalVideoTrackFromExternalSourceInitConfig source_config{};
  ASSERT_EQ(mrsResult::kSuccess,
            mrsPeerConnectionAddLocalVideoTrackFromExternalSource(
                pair.pc1(), "gen_track", source_handle, &source_config,
                &track_handle));
  ASSERT_NE(nullptr, track_handle);
  ASSERT_NE(mrsBool::kFalse, mrsLocalVideoTrackIsEnabled(track_handle));

  uint32_t frame_count = 0;
  Argb32VideoFrameCallback argb_cb =
      [&frame_count](const mrsArgb32VideoFrame& frame) {
        ASSERT_NE(nullptr, frame.argb32_data_);
        ASSERT_LT(0u, frame.width_);
        ASSERT_LT(0u, frame.height_);
        ValidateQuadTestFrame(frame.argb32_data_, frame.stride_, frame.width_,
                              frame.height_);
        ++frame_count;
      };
  mrsPeerConnectionRegisterArgb32RemoteVideoFrameCallback(pair.pc2(),
                                                          CB(argb_cb));

  pair.ConnectAndWait();

  // Simple timer
  Event ev;
  ev.WaitFor(5s);
  ASSERT_LT(50u, frame_count);  // at least 10 FPS

  mrsPeerConnectionRegisterArgb32RemoteVideoFrameCallback(pair.pc2(), nullptr,
                                                          nullptr);
  mrsPeerConnectionRemoveLocalVideoTracksFromSource(pair.pc1(), source_handle);
  mrsLocalVideoTrackRemoveRef(track_handle);
  mrsExternalVideoTrackSourceShutdown(source_handle);
  mrsExternalVideoTrackSourceRemoveRef(source_handle);
}

#endif  // MRSW_EXCLUDE_DEVICE_TESTS
