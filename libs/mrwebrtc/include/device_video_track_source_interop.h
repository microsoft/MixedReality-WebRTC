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


/// Create a video track source by opening a local video capture device
/// (webcam).
///
/// [UWP] This must be invoked from another thread than the main UI thread.
MRS_API mrsResult MRS_CALL mrsDeviceVideoTrackSourceCreate(
    const mrsLocalVideoDeviceInitConfig* init_config,
    mrsDeviceVideoTrackSourceHandle* source_handle_out) noexcept;

}  // extern "C"
