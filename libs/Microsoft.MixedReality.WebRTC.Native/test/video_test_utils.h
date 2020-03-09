// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "../include/interop_api.h"
#include "../include/peer_connection_interop.h"
#include "../src/mrs_errors.h"

using namespace Microsoft::MixedReality::WebRTC;

namespace VideoTestUtils {

mrsResult MRS_CALL MakeTestFrame(void* /*user_data*/,
                                 ExternalVideoTrackSourceHandle handle,
                                 uint32_t request_id,
                                 int64_t timestamp_ms);

void CheckIsTestFrame(const I420AVideoFrame& frame);

}  // namespace VideoTestUtils
