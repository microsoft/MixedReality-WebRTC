// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// This is a precompiled header, it must be on its own, followed by a blank
// line, to prevent clang-format from reordering it with other headers.
#include "pch.h"

#include "callback.h"
#include "external_video_track_source_interop.h"
#include "interop/global_factory.h"
#include "media/external_video_track_source.h"

using namespace Microsoft::MixedReality::WebRTC;

mrsResult MRS_CALL mrsExternalVideoTrackSourceCreateFromI420ACallback(
    mrsRequestExternalI420AVideoFrameCallback callback,
    void* user_data,
    mrsExternalVideoTrackSourceHandle* source_handle_out) noexcept {
  if (!source_handle_out) {
    return Result::kInvalidParameter;
  }
  *source_handle_out = nullptr;
  RefPtr<ExternalVideoTrackSource> track_source =
      detail::ExternalVideoTrackSourceCreateFromI420A(
          GlobalFactory::InstancePtr(), callback, user_data);
  if (!track_source) {
    return Result::kUnknownError;
  }
  *source_handle_out = track_source.release();
  return Result::kSuccess;
}

mrsResult MRS_CALL mrsExternalVideoTrackSourceCreateFromArgb32Callback(
    mrsRequestExternalArgb32VideoFrameCallback callback,
    void* user_data,
    mrsExternalVideoTrackSourceHandle* source_handle_out) noexcept {
  if (!source_handle_out) {
    return Result::kInvalidParameter;
  }
  *source_handle_out = nullptr;
  RefPtr<ExternalVideoTrackSource> track_source =
      detail::ExternalVideoTrackSourceCreateFromArgb32(
          GlobalFactory::InstancePtr(), callback, user_data);
  if (!track_source) {
    return Result::kUnknownError;
  }
  *source_handle_out = track_source.release();
  return Result::kSuccess;
}

void MRS_CALL mrsExternalVideoTrackSourceFinishCreation(
    mrsExternalVideoTrackSourceHandle source_handle) noexcept {
  if (auto source = static_cast<ExternalVideoTrackSource*>(source_handle)) {
    source->FinishCreation();
  }
}

mrsResult MRS_CALL mrsExternalVideoTrackSourceCompleteI420AFrameRequest(
    mrsExternalVideoTrackSourceHandle handle,
    uint32_t request_id,
    int64_t timestamp_ms,
    const mrsI420AVideoFrame* frame_view) noexcept {
  if (!frame_view) {
    return Result::kInvalidParameter;
  }
  if (auto track = static_cast<ExternalVideoTrackSource*>(handle)) {
    return track->CompleteRequest(request_id, timestamp_ms, *frame_view);
  }
  return mrsResult::kInvalidNativeHandle;
}

mrsResult MRS_CALL mrsExternalVideoTrackSourceCompleteArgb32FrameRequest(
    mrsExternalVideoTrackSourceHandle handle,
    uint32_t request_id,
    int64_t timestamp_ms,
    const mrsArgb32VideoFrame* frame_view) noexcept {
  if (!frame_view) {
    return Result::kInvalidParameter;
  }
  if (auto track = static_cast<ExternalVideoTrackSource*>(handle)) {
    return track->CompleteRequest(request_id, timestamp_ms, *frame_view);
  }
  return mrsResult::kInvalidNativeHandle;
}

void MRS_CALL mrsExternalVideoTrackSourceShutdown(
    mrsExternalVideoTrackSourceHandle handle) noexcept {
  if (auto track = static_cast<ExternalVideoTrackSource*>(handle)) {
    track->Shutdown();
  }
}

namespace {

/// Adapter for a an interop-based I420A custom video source.
struct I420AInteropVideoSource : I420AExternalVideoSource {
  using callback_type = RetCallback<mrsResult,
                                    mrsExternalVideoTrackSourceHandle,
                                    uint32_t,
                                    int64_t>;

  /// Interop callback to generate frames.
  callback_type callback_;

  /// External video track source to deliver the frames to.
  /// Note that this is a "weak" pointer to avoid a circular reference to the
  /// video track source owning it.
  ExternalVideoTrackSource* track_source_{};

  I420AInteropVideoSource(mrsRequestExternalI420AVideoFrameCallback callback,
                          void* user_data)
      : callback_({callback, user_data}) {}

  Result FrameRequested(I420AVideoFrameRequest& frame_request) override {
    assert(track_source_);
    return callback_(track_source_, frame_request.request_id_,
                     frame_request.timestamp_ms_);
  }
};

/// Adapter for a an interop-based ARGB32 custom video source.
struct Argb32InteropVideoSource : Argb32ExternalVideoSource {
  using callback_type = RetCallback<mrsResult,
                                    mrsExternalVideoTrackSourceHandle,
                                    uint32_t,
                                    int64_t>;

  /// Interop callback to generate frames.
  callback_type callback_;

  /// External video track source to deliver the frames to.
  /// Note that this is a "weak" pointer to avoid a circular reference to the
  /// video track source owning it.
  ExternalVideoTrackSource* track_source_{};

  Argb32InteropVideoSource(mrsRequestExternalArgb32VideoFrameCallback callback,
                           void* user_data)
      : callback_({callback, user_data}) {}

  Result FrameRequested(Argb32VideoFrameRequest& frame_request) override {
    assert(track_source_);
    return callback_(track_source_, frame_request.request_id_,
                     frame_request.timestamp_ms_);
  }
};

}  // namespace

namespace Microsoft {
namespace MixedReality {
namespace WebRTC {
namespace detail {

RefPtr<ExternalVideoTrackSource> ExternalVideoTrackSourceCreateFromI420A(
    RefPtr<GlobalFactory> global_factory,
    mrsRequestExternalI420AVideoFrameCallback callback,
    void* user_data) {
  RefPtr<I420AInteropVideoSource> custom_source =
      new I420AInteropVideoSource(callback, user_data);
  if (!custom_source) {
    return {};
  }
  // Tracks need to be created from the worker thread
  rtc::Thread* const worker_thread = global_factory->GetWorkerThread();
  auto track_source = worker_thread->Invoke<RefPtr<ExternalVideoTrackSource>>(
      RTC_FROM_HERE, rtc::Bind(&ExternalVideoTrackSource::createFromI420A,
                               std::move(global_factory), custom_source));
  if (!track_source) {
    return {};
  }
  custom_source->track_source_ = track_source.get();
  return track_source;
}

RefPtr<ExternalVideoTrackSource> ExternalVideoTrackSourceCreateFromArgb32(
    RefPtr<GlobalFactory> global_factory,
    mrsRequestExternalArgb32VideoFrameCallback callback,
    void* user_data) {
  RefPtr<Argb32InteropVideoSource> custom_source =
      new Argb32InteropVideoSource(callback, user_data);
  if (!custom_source) {
    return {};
  }
  // Tracks need to be created from the worker thread
  rtc::Thread* const worker_thread = global_factory->GetWorkerThread();
  auto track_source = worker_thread->Invoke<RefPtr<ExternalVideoTrackSource>>(
      RTC_FROM_HERE, rtc::Bind(&ExternalVideoTrackSource::createFromArgb32,
                               std::move(global_factory), custom_source));
  if (!track_source) {
    return {};
  }
  custom_source->track_source_ = track_source.get();
  return track_source;
}

}  // namespace detail
}  // namespace WebRTC
}  // namespace MixedReality
}  // namespace Microsoft
