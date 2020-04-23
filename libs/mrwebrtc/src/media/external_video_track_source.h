// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include "external_video_track_source_interop.h"
#include "mrs_errors.h"
#include "refptr.h"
#include "tracked_object.h"
#include "video_frame.h"

namespace Microsoft::MixedReality::WebRTC {

class ExternalVideoTrackSource;

/// Frame request for an external video source producing video frames encoded in
/// I420 format, with optional Alpha (opacity) plane.
struct I420AVideoFrameRequest {
  /// Video track source the request is related to.
  ExternalVideoTrackSource& track_source_;

  /// Video frame timestamp, in milliseconds.
  std::int64_t timestamp_ms_;

  /// Unique identifier of the request.
  const std::uint32_t request_id_;

  /// Complete the request by making the track source consume the given video
  /// frame and have it deliver the frame to all its video tracks.
  Result CompleteRequest(const I420AVideoFrame& frame_view);
};

/// Custom video source producing video frames encoded in I420 format, with
/// optional Alpha (opacity) plane.
class I420AExternalVideoSource : public RefCountedBase {
 public:
  /// Produce a video frame for a request initiated by an external track source.
  ///
  /// This callback is invoked automatically by the track source whenever a new
  /// video frame is needed (pull model). The custom video source implementation
  /// must either return an error, or produce a new video frame and call the
  /// |CompleteRequest()| request on the |frame_request| object.
  virtual Result FrameRequested(I420AVideoFrameRequest& frame_request) = 0;
};

/// Frame request for an external video source producing video frames encoded in
/// ARGB 32-bit-per-pixel format.
struct Argb32VideoFrameRequest {
  /// Video track source the request is related to.
  ExternalVideoTrackSource& track_source_;

  /// Video frame timestamp, in milliseconds.
  std::int64_t timestamp_ms_;

  /// Unique identifier of the request.
  const std::uint32_t request_id_;

  /// Complete the request by making the track source consume the given video
  /// frame and have it deliver the frame to all its video tracks.
  Result CompleteRequest(const Argb32VideoFrame& frame_view);
};

/// Custom video source producing vidoe frames encoded in ARGB 32-bit-per-pixel
/// format.
class Argb32ExternalVideoSource : public RefCountedBase {
 public:
  /// Produce a video frame for a request initiated by an external track source.
  ///
  /// This callback is invoked automatically by the track source whenever a new
  /// video frame is needed (pull model). The custom video source implementation
  /// must either return an error, or produce a new video frame and call the
  /// |CompleteRequest()| request on the |track_source| object, passing the
  /// |request_id| of the current request being completed.
  virtual Result FrameRequested(Argb32VideoFrameRequest& frame_request) = 0;
};

/// Video track source acting as an adapter for an external source of raw
/// frames.
class ExternalVideoTrackSource : public TrackedObject {
 public:
  /// Helper to create an external video track source from a custom I420A video
  /// frame request callback.
  static RefPtr<ExternalVideoTrackSource> createFromI420A(
      RefPtr<GlobalFactory> global_factory,
      RefPtr<I420AExternalVideoSource> video_source);

  /// Helper to create an external video track source from a custom ARGB32 video
  /// frame request callback.
  static RefPtr<ExternalVideoTrackSource> createFromArgb32(
      RefPtr<GlobalFactory> global_factory,
      RefPtr<Argb32ExternalVideoSource> video_source);

  /// Finish the creation of the video track source, and start capturing.
  /// See |mrsExternalVideoTrackSourceFinishCreation()| for details.
  virtual void FinishCreation() = 0;

  /// Start the video capture. This will begin to produce video frames and start
  /// calling the video frame callback.
  virtual void StartCapture() = 0;

  /// Complete a given video frame request with the provided I420A frame.
  /// The caller must know the source expects an I420A frame; there is no check
  /// to confirm the source is I420A-based or ARGB32-based.
  virtual Result CompleteRequest(uint32_t request_id,
                                 int64_t timestamp_ms,
                                 const I420AVideoFrame& frame) = 0;

  /// Complete a given video frame request with the provided ARGB32 frame.
  /// The caller must know the source expects an ARGB32 frame; there is no check
  /// to confirm the source is I420A-based or ARGB32-based.
  virtual Result CompleteRequest(uint32_t request_id,
                                 int64_t timestamp_ms,
                                 const Argb32VideoFrame& frame) = 0;

  /// Stop the video capture. This will stop producing video frames.
  virtual void StopCapture() = 0;

  /// Shutdown the source and release the buffer adapter and its callback.
  virtual void Shutdown() noexcept = 0;

 protected:
  ExternalVideoTrackSource(RefPtr<GlobalFactory> global_factory);
};

namespace detail {

//
// Helpers
//

/// Create an I420A external video track source wrapping the given interop
/// callback.
RefPtr<ExternalVideoTrackSource> ExternalVideoTrackSourceCreateFromI420A(
    RefPtr<GlobalFactory> global_factory,
    mrsRequestExternalI420AVideoFrameCallback callback,
    void* user_data);

/// Create an ARGB32 external video track source wrapping the given interop
/// callback.
RefPtr<ExternalVideoTrackSource> ExternalVideoTrackSourceCreateFromArgb32(
    RefPtr<GlobalFactory> global_factory,
    mrsRequestExternalArgb32VideoFrameCallback callback,
    void* user_data);

}  // namespace detail

}  // namespace Microsoft::MixedReality::WebRTC
