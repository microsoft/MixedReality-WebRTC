// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include "external_video_track_source_interop.h"
#include "interop_api.h"
#include "video_test_utils.h"

TEST(LibraryTests, SetShutdownOptions) {
  ASSERT_EQ(0u, mrsReportLiveObjects());
  auto const initial_options = mrsGetShutdownOptions();
  mrsSetShutdownOptions(mrsShutdownOptions::kNone);
  ASSERT_EQ(mrsShutdownOptions::kNone, mrsGetShutdownOptions());
  mrsSetShutdownOptions(mrsShutdownOptions::kLogLiveObjects);
  ASSERT_EQ(mrsShutdownOptions::kLogLiveObjects, mrsGetShutdownOptions());
  mrsSetShutdownOptions(initial_options);
}

TEST(LibraryTests, ReportLiveObjects) {
  ASSERT_EQ(0u, mrsReportLiveObjects());
  mrsExternalVideoTrackSourceHandle source_handle = nullptr;
  ASSERT_EQ(mrsResult::kSuccess,
            mrsExternalVideoTrackSourceCreateFromI420ACallback(
                &VideoTestUtils::MakeTestFrame, nullptr, &source_handle));
  ASSERT_NE(nullptr, source_handle);
  mrsExternalVideoTrackSourceFinishCreation(source_handle);
  ASSERT_EQ(1u, mrsReportLiveObjects());
  mrsRefCountedObjectRemoveRef(source_handle);
  ASSERT_EQ(0u, mrsReportLiveObjects());
}

TEST(LibraryTests, ForceShutdown) {
  // Disable kDebugBreakOnForceShutdown; debug break makes the test fail
  mrsSetShutdownOptions(mrsShutdownOptions::kNone);
  ASSERT_EQ(0u, mrsReportLiveObjects());
  mrsExternalVideoTrackSourceHandle source_handle = nullptr;
  ASSERT_EQ(mrsResult::kSuccess,
            mrsExternalVideoTrackSourceCreateFromI420ACallback(
                &VideoTestUtils::MakeTestFrame, nullptr, &source_handle));
  ASSERT_NE(nullptr, source_handle);
  mrsExternalVideoTrackSourceFinishCreation(source_handle);
  ASSERT_EQ(1u, mrsReportLiveObjects());
  mrsForceShutdown();
  ASSERT_EQ(0u, mrsReportLiveObjects());
}
