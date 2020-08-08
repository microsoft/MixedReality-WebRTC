// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include "audio_frame.h"
#include "device_audio_track_source_interop.h"
#include "interop_api.h"

#include "test_utils.h"

namespace {

class DeviceAudioTrackSourceTests
    : public TestUtils::TestBase,
      public testing::WithParamInterface<mrsSdpSemantic> {};

}  // namespace

#if !defined(MRSW_EXCLUDE_DEVICE_TESTS)

INSTANTIATE_TEST_CASE_P(,
                        DeviceAudioTrackSourceTests,
                        testing::ValuesIn(TestUtils::TestSemantics),
                        TestUtils::SdpSemanticToString);

TEST_P(DeviceAudioTrackSourceTests, Create) {
  mrsLocalAudioDeviceInitConfig config{};
  mrsDeviceAudioTrackSourceHandle source_handle{};
  ASSERT_EQ(mrsResult::kSuccess,
            mrsDeviceAudioTrackSourceCreate(&config, &source_handle));
  ASSERT_NE(nullptr, source_handle);
  mrsRefCountedObjectRemoveRef(source_handle);
}

#endif  // MRSW_EXCLUDE_DEVICE_TESTS
