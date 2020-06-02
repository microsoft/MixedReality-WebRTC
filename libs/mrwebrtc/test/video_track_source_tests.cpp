// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include "interop_api.h"
#include "video_frame.h"
#include "video_track_source_interop.h"

#include "test_utils.h"

namespace {

class VideoTrackSourceTests
    : public TestUtils::TestBase,
      public testing::WithParamInterface<mrsSdpSemantic> {};

}  // namespace

#if !defined(MRSW_EXCLUDE_DEVICE_TESTS)

INSTANTIATE_TEST_CASE_P(,
                        VideoTrackSourceTests,
                        testing::ValuesIn(TestUtils::TestSemantics),
                        TestUtils::SdpSemanticToString);

TEST_P(VideoTrackSourceTests, CreateFromDevice) {
  mrsLocalVideoDeviceInitConfig config{};
  mrsVideoTrackSourceHandle source_handle{};
  ASSERT_EQ(mrsResult::kSuccess,
            mrsVideoTrackSourceCreateFromDevice(&config, &source_handle));
  ASSERT_NE(nullptr, source_handle);
  mrsVideoTrackSourceRemoveRef(source_handle);
}

// TODO - Don't use device, run outside MRSW_EXCLUDE_DEVICE_TESTS
TEST_P(VideoTrackSourceTests, Name) {
  mrsLocalVideoDeviceInitConfig config{};
  mrsVideoTrackSourceHandle source_handle{};
  ASSERT_EQ(mrsResult::kSuccess,
            mrsVideoTrackSourceCreateFromDevice(&config, &source_handle));
  ASSERT_NE(nullptr, source_handle);

  // Exact-fit buffer
  {
    constexpr const char kTestName[] = "test_name_exact_fit_buffer";
    constexpr const size_t kTestNameSize =
        sizeof(kTestName);  // incl. zero-terminator
    mrsVideoTrackSourceSetName(source_handle, kTestName);
    constexpr const size_t kBufferSize = kTestNameSize;  // exact-fit
    char buffer[kBufferSize];
    size_t size = kBufferSize;
    ASSERT_EQ(mrsResult::kSuccess,
              mrsVideoTrackSourceGetName(source_handle, buffer, &size));
    ASSERT_EQ(kTestNameSize, size);
    ASSERT_EQ(0, memcmp(buffer, kTestName, kTestNameSize));
  }

  // Larger buffer
  {
    constexpr const char kTestName[] = "test_name_larger_buffer";
    constexpr const size_t kTestNameSize =
        sizeof(kTestName);  // incl. zero-terminator
    mrsVideoTrackSourceSetName(source_handle, kTestName);
    constexpr const size_t kBufferSize = kTestNameSize + 1;  // larger
    char buffer[kBufferSize];
    size_t size = kBufferSize;
    ASSERT_EQ(mrsResult::kSuccess,
              mrsVideoTrackSourceGetName(source_handle, buffer, &size));
    ASSERT_EQ(kTestNameSize, size);
    ASSERT_EQ(0, memcmp(buffer, kTestName, kTestNameSize));
  }

  // Buffer too small
  {
    constexpr const char kTestName[] = "test_name_buffer_too_small";
    constexpr const size_t kTestNameSize =
        sizeof(kTestName);  // incl. zero-terminator
    mrsVideoTrackSourceSetName(source_handle, kTestName);
    constexpr const size_t kBufferSize = kTestNameSize - 1;  // too small
    char buffer[kBufferSize];
    size_t size = kBufferSize;
    ASSERT_EQ(mrsResult::kBufferTooSmall,
              mrsVideoTrackSourceGetName(source_handle, buffer, &size));
    ASSERT_EQ(kTestNameSize, size);
  }

  // Invalid buffer
  {
    size_t size = 0;
    ASSERT_EQ(mrsResult::kInvalidParameter,
              mrsVideoTrackSourceGetName(source_handle, nullptr, &size));
  }

  // Invalid size
  {
    char buffer[5];
    ASSERT_EQ(mrsResult::kInvalidParameter,
              mrsVideoTrackSourceGetName(source_handle, buffer, nullptr));
  }

  mrsVideoTrackSourceRemoveRef(source_handle);
}

TEST_P(VideoTrackSourceTests, DeviceIdInvalid) {
  mrsLocalVideoDeviceInitConfig device_config{};
  device_config.video_device_id = "[[INVALID DEVICE ID]]";
  mrsVideoTrackSourceHandle source_handle{};
  ASSERT_EQ(Result::kNotFound, mrsVideoTrackSourceCreateFromDevice(
                                   &device_config, &source_handle));
  ASSERT_EQ(nullptr, source_handle);
}

#endif  // MRSW_EXCLUDE_DEVICE_TESTS
