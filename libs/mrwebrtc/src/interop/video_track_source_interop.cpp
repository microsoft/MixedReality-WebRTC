// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// This is a precompiled header, it must be on its own, followed by a blank
// line, to prevent clang-format from reordering it with other headers.
#include "pch.h"

#include "media/video_track_source.h"
#include "video_track_source_interop.h"

using namespace Microsoft::MixedReality::WebRTC;

void MRS_CALL mrsVideoTrackSourceRegisterFrameCallback(
    mrsVideoTrackSourceHandle source_handle,
    mrsI420AVideoFrameCallback callback,
    void* user_data) noexcept {
  if (auto source = static_cast<VideoTrackSource*>(source_handle)) {
    RTC_DCHECK(
        (source->GetObjectType() == ObjectType::kDeviceVideoTrackSource) ||
        (source->GetObjectType() == ObjectType::kExternalVideoTrackSource));
    source->SetCallback(I420AFrameReadyCallback{callback, user_data});
  }
}

void MRS_CALL mrsVideoTrackSourceRegisterArgb32FrameCallback(
    mrsVideoTrackSourceHandle source_handle,
    mrsArgb32VideoFrameCallback callback,
    void* user_data) noexcept {
  // TODO - Remove ARGB callbacks, use I420 callbacks only and expose some
  // conversion utility to convert from ARGB to I420 when needed (to be called
  // by the user).
  if (auto source = static_cast<VideoTrackSource*>(source_handle)) {
    RTC_DCHECK(
        (source->GetObjectType() == ObjectType::kDeviceVideoTrackSource) ||
        (source->GetObjectType() == ObjectType::kExternalVideoTrackSource));
    source->SetCallback(Argb32FrameReadyCallback{callback, user_data});
  }
}
