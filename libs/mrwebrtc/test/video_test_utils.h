// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include "../include/interop_api.h"
#include "../include/peer_connection_interop.h"
#include "../src/interop/global_factory.h"
#include "../src/mrs_errors.h"

using namespace Microsoft::MixedReality::WebRTC;

namespace VideoTestUtils {

mrsResult MRS_CALL MakeTestFrame(void* /*user_data*/,
                                 mrsExternalVideoTrackSourceHandle handle,
                                 uint32_t request_id,
                                 int64_t timestamp_ms);

void CheckIsTestFrame(const I420AVideoFrame& frame);

}  // namespace VideoTestUtils
