// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// This is a precompiled header, it must be on its own, followed by a blank
// line, to prevent clang-format from reordering it with other headers.
#include "pch.h"

#include "global_factory.h"
#include "local_video_track_interop.h"
#include "media/external_video_track_source_impl.h"
#include "media/local_video_track.h"
#include "utils.h"

using namespace Microsoft::MixedReality::WebRTC;

void MRS_CALL
mrsLocalVideoTrackAddRef(mrsLocalVideoTrackHandle handle) noexcept {
  if (auto track = static_cast<LocalVideoTrack*>(handle)) {
    track->AddRef();
  } else {
    RTC_LOG(LS_WARNING)
        << "Trying to add reference to NULL LocalVideoTrack object.";
  }
}

void MRS_CALL
mrsLocalVideoTrackRemoveRef(mrsLocalVideoTrackHandle handle) noexcept {
  if (auto track = static_cast<LocalVideoTrack*>(handle)) {
    track->RemoveRef();
  } else {
    RTC_LOG(LS_WARNING) << "Trying to remove reference from NULL "
                           "LocalVideoTrack object.";
  }
}

// mrsLocalVideoTrackCreateFromDevice -> interop_api.cpp

mrsResult MRS_CALL mrsLocalVideoTrackCreateFromExternalSource(
    mrsExternalVideoTrackSourceHandle source_handle,
    const mrsLocalVideoTrackFromExternalSourceInitConfig* config,
    const char* track_name,
    mrsLocalVideoTrackHandle* track_handle_out) noexcept {
  if (!track_handle_out) {
    return Result::kInvalidParameter;
  }
  *track_handle_out = nullptr;

  auto track_source =
      static_cast<detail::ExternalVideoTrackSourceImpl*>(source_handle);
  if (!track_source) {
    return Result::kInvalidNativeHandle;
  }

  std::string track_name_str;
  if (!IsStringNullOrEmpty(track_name)) {
    track_name_str = track_name;
  } else {
    track_name_str = "external_track";
  }

  RefPtr<GlobalFactory> global_factory(GlobalFactory::InstancePtr());
  auto pc_factory = global_factory->GetPeerConnectionFactory();
  if (!pc_factory) {
    return Result::kUnknownError;
  }

  // The video track keeps a reference to the video source; let's hope this
  // does not change, because this is not explicitly mentioned in the docs,
  // and the video track is the only one keeping the video source alive.
  rtc::scoped_refptr<webrtc::VideoTrackInterface> video_track =
      pc_factory->CreateVideoTrack(track_name_str, track_source->impl());
  if (!video_track) {
    return Result::kUnknownError;
  }

  // Create the video track wrapper
  RefPtr<LocalVideoTrack> track =
      new LocalVideoTrack(std::move(global_factory), std::move(video_track),
                          config->track_interop_handle);
  *track_handle_out = track.release();
  return Result::kSuccess;
}

void MRS_CALL mrsLocalVideoTrackRegisterI420AFrameCallback(
    mrsLocalVideoTrackHandle trackHandle,
    mrsI420AVideoFrameCallback callback,
    void* user_data) noexcept {
  if (auto track = static_cast<LocalVideoTrack*>(trackHandle)) {
    track->SetCallback(I420AFrameReadyCallback{callback, user_data});
  }
}

void MRS_CALL mrsLocalVideoTrackRegisterArgb32FrameCallback(
    mrsLocalVideoTrackHandle trackHandle,
    mrsArgb32VideoFrameCallback callback,
    void* user_data) noexcept {
  if (auto track = static_cast<LocalVideoTrack*>(trackHandle)) {
    track->SetCallback(Argb32FrameReadyCallback{callback, user_data});
  }
}

mrsResult MRS_CALL
mrsLocalVideoTrackSetEnabled(mrsLocalVideoTrackHandle track_handle,
                             mrsBool enabled) noexcept {
  auto track = static_cast<LocalVideoTrack*>(track_handle);
  if (!track) {
    return Result::kInvalidParameter;
  }
  track->SetEnabled(enabled != mrsBool::kFalse);
  return Result::kSuccess;
}

mrsBool MRS_CALL
mrsLocalVideoTrackIsEnabled(mrsLocalVideoTrackHandle track_handle) noexcept {
  auto track = static_cast<LocalVideoTrack*>(track_handle);
  if (!track) {
    return mrsBool::kFalse;
  }
  return (track->IsEnabled() ? mrsBool::kTrue : mrsBool::kFalse);
}
