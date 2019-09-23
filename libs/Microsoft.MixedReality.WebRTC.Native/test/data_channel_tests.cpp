// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#include "pch.h"

#include "api.h"
#include "data_channel.h"

#if !defined(MRSW_EXCLUDE_DEVICE_TESTS)

TEST(DataChannel, AddChannelBeforeInit) {
  PCRaii pc;
  ASSERT_NE(nullptr, pc.handle());
  mrsDataChannelConfig config{};
  config.label = "data";
  config.flags = mrsDataChannelConfigFlags::kOrdered |
                 mrsDataChannelConfigFlags::kReliable;
  mrsDataChannelCallbacks callbacks{};
  DataChannelHandle handle;
  mrsDataChannelInteropHandle interopHandle = (void*)0x2;  // fake
  ASSERT_EQ(MRS_SUCCESS,
            mrsPeerConnectionAddDataChannel(pc.handle(), interopHandle, config,
                                            callbacks, &handle));
}

#endif  // MRSW_EXCLUDE_DEVICE_TESTS
