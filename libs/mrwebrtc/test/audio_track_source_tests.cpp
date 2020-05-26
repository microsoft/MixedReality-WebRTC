// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include "audio_frame.h"
#include "audio_track_source_interop.h"
#include "interop_api.h"

#include "test_utils.h"

namespace {

class AudioTrackSourceTests
    : public TestUtils::TestBase,
      public testing::WithParamInterface<mrsSdpSemantic> {};

}  // namespace

#if !defined(MRSW_EXCLUDE_DEVICE_TESTS)

INSTANTIATE_TEST_CASE_P(,
                        AudioTrackSourceTests,
                        testing::ValuesIn(TestUtils::TestSemantics),
                        TestUtils::SdpSemanticToString);

TEST_P(AudioTrackSourceTests, CreateFromDevice) {
  mrsLocalAudioDeviceInitConfig config{};
  mrsAudioTrackSourceHandle source_handle{};
  ASSERT_EQ(mrsResult::kSuccess,
            mrsAudioTrackSourceCreateFromDevice(&config, &source_handle));
  ASSERT_NE(nullptr, source_handle);
  mrsAudioTrackSourceRemoveRef(source_handle);
}

// TODO - Don't use device, run outside MRSW_EXCLUDE_DEVICE_TESTS
TEST_P(AudioTrackSourceTests, Name) {
  mrsLocalAudioDeviceInitConfig config{};
  mrsAudioTrackSourceHandle source_handle{};
  ASSERT_EQ(mrsResult::kSuccess,
            mrsAudioTrackSourceCreateFromDevice(&config, &source_handle));
  ASSERT_NE(nullptr, source_handle);

  // Exact-fit buffer
  {
    constexpr const char kTestName[] = "test_name_exact_fit_buffer";
    constexpr const size_t kTestNameSize =
        sizeof(kTestName);  // incl. zero-terminator
    mrsAudioTrackSourceSetName(source_handle, kTestName);
    constexpr const size_t kBufferSize = kTestNameSize;  // exact-fit
    char buffer[kBufferSize];
    size_t size = kBufferSize;
    ASSERT_EQ(mrsResult::kSuccess,
              mrsAudioTrackSourceGetName(source_handle, buffer, &size));
    ASSERT_EQ(kTestNameSize, size);
    ASSERT_EQ(0, memcmp(buffer, kTestName, kTestNameSize));
  }

  // Larger buffer
  {
    constexpr const char kTestName[] = "test_name_larger_buffer";
    constexpr const size_t kTestNameSize =
        sizeof(kTestName);  // incl. zero-terminator
    mrsAudioTrackSourceSetName(source_handle, kTestName);
    constexpr const size_t kBufferSize = kTestNameSize + 1;  // larger
    char buffer[kBufferSize];
    size_t size = kBufferSize;
    ASSERT_EQ(mrsResult::kSuccess,
              mrsAudioTrackSourceGetName(source_handle, buffer, &size));
    ASSERT_EQ(kTestNameSize, size);
    ASSERT_EQ(0, memcmp(buffer, kTestName, kTestNameSize));
  }

  // Buffer too small
  {
    constexpr const char kTestName[] = "test_name_buffer_too_small";
    constexpr const size_t kTestNameSize =
        sizeof(kTestName);  // incl. zero-terminator
    mrsAudioTrackSourceSetName(source_handle, kTestName);
    constexpr const size_t kBufferSize = kTestNameSize - 1;  // too small
    char buffer[kBufferSize];
    size_t size = kBufferSize;
    ASSERT_EQ(mrsResult::kBufferTooSmall,
              mrsAudioTrackSourceGetName(source_handle, buffer, &size));
    ASSERT_EQ(kTestNameSize, size);
  }

  // Invalid buffer
  {
    size_t size = 0;
    ASSERT_EQ(mrsResult::kInvalidParameter,
              mrsAudioTrackSourceGetName(source_handle, nullptr, &size));
  }

  // Invalid size
  {
    char buffer[5];
    ASSERT_EQ(mrsResult::kInvalidParameter,
              mrsAudioTrackSourceGetName(source_handle, buffer, nullptr));
  }

  mrsAudioTrackSourceRemoveRef(source_handle);
}

#endif  // MRSW_EXCLUDE_DEVICE_TESTS
