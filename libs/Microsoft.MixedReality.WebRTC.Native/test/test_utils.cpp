// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include "test_utils.h"

namespace TestUtils {

void MRS_CALL SetEventOnCompleted(void* user_data) {
  Event* ev = (Event*)user_data;
  ev->Set();
}

std::string SdpSemanticToString(
    const testing::TestParamInfo<SdpSemantic>& info) {
  switch (info.param) {
    case SdpSemantic::kPlanB:
      return "PlanB";
    case SdpSemantic::kUnifiedPlan:
      return "UnifiedPlan";
  }
}

}  // namespace TestUtils
