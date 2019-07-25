// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#include "pch.h"

#include "../include/api.h"

#if !defined(MRSW_EXCLUDE_DEVICE_TESTS)

// Fail to add channel before the PeerConnection is initialized
TEST(DataChannel, AddChannelBeforeInit) {
  PCRaii pc;
  ASSERT_NE(nullptr, pc.handle());
  ASSERT_EQ(MRS_SUCCESS, mrsPeerConnectionAddDataChannel(
                             pc.handle(), -1, "data", true, true, nullptr,
                             nullptr, nullptr, nullptr, nullptr, nullptr));
}

#endif  // MRSW_EXCLUDE_DEVICE_TESTS
