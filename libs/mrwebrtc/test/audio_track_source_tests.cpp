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
  mrsRefCountedObjectRemoveRef(source_handle);
}

#endif  // MRSW_EXCLUDE_DEVICE_TESTS
