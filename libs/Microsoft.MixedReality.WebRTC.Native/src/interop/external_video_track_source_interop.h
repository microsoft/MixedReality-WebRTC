// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include "export.h"
#include "external_video_track_source.h"
#include "interop/interop_api.h"
#include "refptr.h"

extern "C" {

//
// Wrapper
//

/// Add a reference to the native object associated with the given handle.
MRS_API void MRS_CALL mrsExternalVideoTrackSourceAddRef(
    ExternalVideoTrackSourceHandle handle) noexcept;

/// Remove a reference from the native object associated with the given handle.
MRS_API void MRS_CALL mrsExternalVideoTrackSourceRemoveRef(
    ExternalVideoTrackSourceHandle handle) noexcept;

/// Create a custom video track source external to the implementation. This
/// allows feeding into WebRTC frames from any source, including generated or
/// synthetic frames, for example for testing. The frame is provided from a
/// callback as an I420-encoded buffer. This returns a handle to a newly
/// allocated object, which must be released once not used anymore with
/// |mrsExternalVideoTrackSourceRemoveRef()|.
MRS_API mrsResult MRS_CALL mrsExternalVideoTrackSourceCreateFromI420ACallback(
    mrsRequestExternalI420AVideoFrameCallback callback,
    void* user_data,
    ExternalVideoTrackSourceHandle* source_handle_out) noexcept;

/// Create a custom video track source external to the implementation. This
/// allows feeding into WebRTC frames from any source, including generated or
/// synthetic frames, for example for testing. The frame is provided from a
/// callback as an ARGB32-encoded buffer. This returns a handle to a newly
/// allocated object, which must be released once not used anymore with
/// |mrsExternalVideoTrackSourceRemoveRef()|.
MRS_API mrsResult MRS_CALL mrsExternalVideoTrackSourceCreateFromArgb32Callback(
    mrsRequestExternalArgb32VideoFrameCallback callback,
    void* user_data,
    ExternalVideoTrackSourceHandle* source_handle_out) noexcept;

/// Complete a video frame request with a provided I420A video frame.
MRS_API mrsResult MRS_CALL mrsExternalVideoTrackSourceCompleteI420AFrameRequest(
    ExternalVideoTrackSourceHandle handle,
    uint32_t request_id,
    int64_t timestamp_ms,
    const mrsI420AVideoFrame* frame_view) noexcept;

/// Complete a video frame request with a provided ARGB32 video frame.
MRS_API mrsResult MRS_CALL
mrsExternalVideoTrackSourceCompleteArgb32FrameRequest(
    ExternalVideoTrackSourceHandle handle,
    uint32_t request_id,
    int64_t timestamp_ms,
    const mrsArgb32VideoFrame* frame_view) noexcept;

/// Irreversibly stop the video source frame production and shutdown the video
/// source.
MRS_API void MRS_CALL mrsExternalVideoTrackSourceShutdown(
    ExternalVideoTrackSourceHandle handle) noexcept;

}  // extern "C"

namespace Microsoft::MixedReality::WebRTC::detail {

//
// Helpers
//

/// Create an I420A external video track source wrapping the given interop
/// callback.
RefPtr<ExternalVideoTrackSource> ExternalVideoTrackSourceCreateFromI420A(
    mrsRequestExternalI420AVideoFrameCallback callback,
    void* user_data);

/// Create an ARGB32 external video track source wrapping the given interop
/// callback.
RefPtr<ExternalVideoTrackSource> ExternalVideoTrackSourceCreateFromArgb32(
    mrsRequestExternalArgb32VideoFrameCallback callback,
    void* user_data);

}  // namespace Microsoft::MixedReality::WebRTC::detail
