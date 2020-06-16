// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include "device_video_track_source_interop.h"
#include "interop_api.h"
#include "video_frame.h"

#include "test_utils.h"

namespace {

class DeviceVideoTrackSourceTests
    : public TestUtils::TestBase,
      public testing::WithParamInterface<mrsSdpSemantic> {};

}  // namespace

#if !defined(MRSW_EXCLUDE_DEVICE_TESTS)

INSTANTIATE_TEST_CASE_P(,
                        DeviceVideoTrackSourceTests,
                        testing::ValuesIn(TestUtils::TestSemantics),
                        TestUtils::SdpSemanticToString);

TEST_P(DeviceVideoTrackSourceTests, Create) {
  mrsLocalVideoDeviceInitConfig config{};
  mrsVideoTrackSourceHandle source_handle{};
  ASSERT_EQ(mrsResult::kSuccess,
            mrsDeviceVideoTrackSourceCreate(&config, &source_handle));
  ASSERT_NE(nullptr, source_handle);
  mrsRefCountedObjectRemoveRef(source_handle);
}

TEST_P(DeviceVideoTrackSourceTests, DeviceIdInvalid) {
  mrsLocalVideoDeviceInitConfig device_config{};
  device_config.video_device_id = "[[INVALID DEVICE ID]]";
  mrsVideoTrackSourceHandle source_handle{};
  ASSERT_EQ(Result::kNotFound,
            mrsDeviceVideoTrackSourceCreate(&device_config, &source_handle));
  ASSERT_EQ(nullptr, source_handle);
}

#endif  // MRSW_EXCLUDE_DEVICE_TESTS
