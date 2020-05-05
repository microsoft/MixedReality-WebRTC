// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include "test_utils.h"

namespace TestUtils {

void MRS_CALL SetEventOnCompleted(void* user_data,
                                  mrsResult result,
                                  const char* error_message) {
  ASSERT_EQ(mrsResult::kSuccess, result) << error_message;
  Event* ev = (Event*)user_data;
  ev->Set();
}

std::string SdpSemanticToString(
    const testing::TestParamInfo<mrsSdpSemantic>& info) {
  switch (info.param) {
    case mrsSdpSemantic::kPlanB:
      return "PlanB";
    case mrsSdpSemantic::kUnifiedPlan:
      return "UnifiedPlan";
    default:
      return "Invalid mrsSdpSemantic !";
  }
}

}  // namespace TestUtils
