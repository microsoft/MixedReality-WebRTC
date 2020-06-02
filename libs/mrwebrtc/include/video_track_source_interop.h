// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include "interop_api.h"

extern "C" {

/// Configuration for opening a local video capture device (webcam) as a video
/// track source.
struct mrsLocalVideoDeviceInitConfig {
  /// Unique identifier of the video capture device to select, as returned by
  /// |mrsEnumVideoCaptureDevicesAsync|, or a null or empty string to select the
  /// default device.
  const char* video_device_id = nullptr;

  /// Optional name of a video profile, if the platform supports it, or null to
  /// no use video profiles.
  const char* video_profile_id = nullptr;

  /// Optional kind of video profile to select, if the platform supports it.
  /// If a video profile ID is specified with |video_profile_id| it is
  /// recommended to leave this as kUnspecified to avoid over-constraining the
  /// video capture format selection.
  mrsVideoProfileKind video_profile_kind = mrsVideoProfileKind::kUnspecified;

  /// Optional preferred capture resolution width, in pixels, or zero for
  /// unconstrained.
  uint32_t width = 0;

  /// Optional preferred capture resolution height, in pixels, or zero for
  /// unconstrained.
  uint32_t height = 0;

  /// Optional preferred capture framerate, in frame per second (FPS), or zero
  /// for unconstrained.
  /// This framerate is compared exactly to the one reported by the video
  /// capture device (webcam), so should be queried rather than hard-coded to
  /// avoid mismatches with video formats reporting e.g. 29.99 instead of 30.0.
  double framerate = 0;

  /// On platforms supporting Mixed Reality Capture (MRC) like HoloLens, enable
  /// this feature. This produces a video track where the holograms rendering is
  /// overlaid over the webcam frame. This parameter is ignored on platforms not
  /// supporting MRC.
  /// Note that MRC is only available in exclusive-mode applications, or in
  /// shared apps with the restricted capability "rescap:screenDuplication". In
  /// any other case the capability will not be granted and MRC will silently
  /// fail, falling back to a simple webcam video feed without holograms.
  mrsBool enable_mrc = mrsBool::kTrue;

  /// When Mixed Reality Capture is enabled, enable or disable the recording
  /// indicator shown on screen.
  mrsBool enable_mrc_recording_indicator = mrsBool::kTrue;
};

/// Add a reference to the native object associated with the given handle.
MRS_API void MRS_CALL
mrsVideoTrackSourceAddRef(mrsVideoTrackSourceHandle handle) noexcept;

/// Remove a reference from the native object associated with the given handle.
MRS_API void MRS_CALL
mrsVideoTrackSourceRemoveRef(mrsVideoTrackSourceHandle handle) noexcept;

/// Assign some name to the track source, for logging and debugging.
MRS_API void MRS_CALL
mrsVideoTrackSourceSetName(mrsVideoTrackSourceHandle handle,
                           const char* name) noexcept;

/// Get the name to the track source. The caller must provide a buffer with a
/// sufficent size to copy the name to, including a null terminator character.
/// The |buffer| argument points to the raw buffer, and the |buffer_size| to the
/// capacity of the buffer, in bytes. On return, if the buffer has enough
/// capacity for the name and its null terminator, the name is copied to the
/// buffer, and the actual buffer size consumed (including null terminator) is
/// returned in |buffer_size|. If not, then the function returns
/// |mrsResult::kBufferTooSmall|, and |buffer_size| contains the total size that
/// the buffer would need for the call to succeed, such that the caller can
/// retry with a buffer with that capacity.
MRS_API mrsResult MRS_CALL
mrsVideoTrackSourceGetName(mrsVideoTrackSourceHandle handle,
                           char* buffer,
                           uint64_t* buffer_size) noexcept;

/// Assign some opaque user data to the video track source. The implementation
/// will store the pointer in the video track source object and not touch it. It
/// can be retrieved with |mrsVideoTrackSourceGetUserData()| at any point during
/// the object lifetime. This is not multithread-safe.
MRS_API void MRS_CALL
mrsVideoTrackSourceSetUserData(mrsVideoTrackSourceHandle handle,
                               void* user_data) noexcept;

/// Get the opaque user data pointer previously assigned to the video track
/// source with |mrsVideoTrackSourceSetUserData()|. If no value was previously
/// assigned, return |nullptr|. This is not multithread-safe.
MRS_API void* MRS_CALL
mrsVideoTrackSourceGetUserData(mrsVideoTrackSourceHandle handle) noexcept;

/// Create a video track source by opening a local video capture device
/// (webcam).
///
/// [UWP] This must be invoked from another thread than the main UI thread.
MRS_API mrsResult MRS_CALL mrsVideoTrackSourceCreateFromDevice(
    const mrsLocalVideoDeviceInitConfig* init_config,
    mrsVideoTrackSourceHandle* source_handle_out) noexcept;

/// Register a custom callback to be called when the video track source produced
/// a frame. The produced frame is passed to the registered callback in I420
/// encoding.
MRS_API void MRS_CALL mrsVideoTrackSourceRegisterFrameCallback(
    mrsVideoTrackSourceHandle source_handle,
    mrsI420AVideoFrameCallback callback,
    void* user_data) noexcept;

}  // extern "C"
