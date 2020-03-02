// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include "interop_api.h"

extern "C" {

/// Configuration for opening a local video capture device and creating a local
/// video track.
struct mrsLocalVideoTrackInitConfig {
  /// Handle of the local video track interop wrapper, if any, which will be
  /// associated with the native local video track object.
  mrsLocalVideoTrackInteropHandle track_interop_handle{};

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

/// Configuration for creating a local video track from an external source.
struct mrsLocalVideoTrackFromExternalSourceInitConfig {
  /// Handle of the local video track interop wrapper, if any, which will be
  /// associated with the native local video track object.
  mrsLocalVideoTrackInteropHandle track_interop_handle{};
};

/// Add a reference to the native object associated with the given handle.
MRS_API void MRS_CALL
mrsLocalVideoTrackAddRef(mrsLocalVideoTrackHandle handle) noexcept;

/// Remove a reference from the native object associated with the given handle.
MRS_API void MRS_CALL
mrsLocalVideoTrackRemoveRef(mrsLocalVideoTrackHandle handle) noexcept;

/// Create a new local video track by opening a local video capture device
/// (webcam).
/// [UWP] This must be invoked from another thread than the main UI thread.
MRS_API mrsResult MRS_CALL mrsLocalVideoTrackCreateFromDevice(
    const mrsLocalVideoTrackInitConfig* config,
    const char* track_name,
    mrsLocalVideoTrackHandle* track_handle_out) noexcept;

/// Create a new local video track by using an existing external video source.
MRS_API mrsResult MRS_CALL mrsLocalVideoTrackCreateFromExternalSource(
    mrsExternalVideoTrackSourceHandle source_handle,
    const mrsLocalVideoTrackFromExternalSourceInitConfig* config,
    const char* track_name,
    mrsLocalVideoTrackHandle* track_handle_out) noexcept;

/// Register a custom callback to be called when the local video track captured
/// a frame. The captured frames is passed to the registered callback in I420
/// encoding.
MRS_API void MRS_CALL mrsLocalVideoTrackRegisterI420AFrameCallback(
    mrsLocalVideoTrackHandle trackHandle,
    mrsI420AVideoFrameCallback callback,
    void* user_data) noexcept;

/// Register a custom callback to be called when the local video track captured
/// a frame. The captured frames is passed to the registered callback in ARGB32
/// encoding.
MRS_API void MRS_CALL mrsLocalVideoTrackRegisterArgb32FrameCallback(
    mrsLocalVideoTrackHandle trackHandle,
    mrsArgb32VideoFrameCallback callback,
    void* user_data) noexcept;

/// Enable or disable a local video track. Enabled tracks output their media
/// content as usual. Disabled track output some void media content (black video
/// frames, silent audio frames). Enabling/disabling a track is a lightweight
/// concept similar to "mute", which does not require an SDP renegotiation.
MRS_API mrsResult MRS_CALL
mrsLocalVideoTrackSetEnabled(mrsLocalVideoTrackHandle track_handle,
                             mrsBool enabled) noexcept;

/// Query a local video track for its enabled status.
MRS_API mrsBool MRS_CALL
mrsLocalVideoTrackIsEnabled(mrsLocalVideoTrackHandle track_handle) noexcept;

}  // extern "C"
