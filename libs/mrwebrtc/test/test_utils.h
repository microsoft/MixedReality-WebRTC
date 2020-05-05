// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include "../include/interop_api.h"

namespace TestUtils {

class TestBase : public ::testing::Test {
 public:
  void SetUp() override {
    // Ensure there is no object alive before the test
    ASSERT_EQ(0u, mrsReportLiveObjects()) << "Alive objects before test.";
  }
  void TearDown() override {
    // Ensure there is no object alive after the test
    ASSERT_EQ(0u, mrsReportLiveObjects()) << "Alive objects after test.";
  }
};

/// Helper callback accepting an Event object as parameter and calling |Set()|
/// on it when invoked.
void MRS_CALL SetEventOnCompleted(void* user_data,
                                  mrsResult result,
                                  const char* error_message);

constexpr const mrsSdpSemantic TestSemantics[] = {mrsSdpSemantic::kUnifiedPlan,
                                                  mrsSdpSemantic::kPlanB};

std::string SdpSemanticToString(
    const testing::TestParamInfo<mrsSdpSemantic>& info);

}  // namespace TestUtils
