// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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

}  // namespace VideoTestUtils
