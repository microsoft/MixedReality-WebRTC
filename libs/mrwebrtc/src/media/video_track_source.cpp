// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include "interop/global_factory.h"
#include "media/video_track_source.h"

namespace Microsoft {
namespace MixedReality {
namespace WebRTC {

VideoSourceAdapter::VideoSourceAdapter(
    rtc::scoped_refptr<webrtc::VideoTrackSourceInterface> source)
    : source_(std::move(source)), state_(source->state()) {}

void VideoSourceAdapter::RegisterObserver(webrtc::ObserverInterface* observer) {
  observer_ = observer;
}

void VideoSourceAdapter::UnregisterObserver(
    webrtc::ObserverInterface* observer) {
  RTC_DCHECK_EQ(observer_, observer);
  observer_ = nullptr;
}

// void VideoSourceAdapter::AddSink(webrtc::AudioTrackSinkInterface* sink) {
//  sinks_.push_back(sink);
//}
//
// void VideoSourceAdapter::RemoveSink(webrtc::AudioTrackSinkInterface* sink) {
//  auto it = std::find(sinks_.begin(), sinks_.end(), sink);
//  if (it != sinks_.end()) {
//    sinks_.erase(it);
//  }
//}

VideoTrackSource::VideoTrackSource(
    RefPtr<GlobalFactory> global_factory,
    ObjectType video_track_source_type,
    rtc::scoped_refptr<webrtc::VideoTrackSourceInterface> source) noexcept
    : TrackedObject(std::move(global_factory), video_track_source_type),
      source_(std::move(source)) {
  RTC_CHECK(source_);
  RTC_CHECK((video_track_source_type == ObjectType::kDeviceVideoTrackSource) ||
            (video_track_source_type == ObjectType::kExternalVideoTrackSource));
}

VideoTrackSource::~VideoTrackSource() {
  if (observer_) {
    // Track sources need to be manipulated from the worker thread
    rtc::Thread* const worker_thread =
        GlobalFactory::InstancePtr()->GetWorkerThread();
    worker_thread->Invoke<void>(
        RTC_FROM_HERE, [&]() { source_->RemoveSink(observer_.get()); });
  }
}

template<class T>
void VideoTrackSource::SetCallbackImpl(T callback) noexcept {
  std::lock_guard<std::mutex> lock(observer_mutex_);
  if (callback) {
    // When assigning a new callback, create and register an observer.
    if (!observer_) {
      observer_ = std::make_unique<VideoFrameObserver>();
      // Track sources need to be manipulated from the worker thread
      rtc::Thread* const worker_thread =
          GlobalFactory::InstancePtr()->GetWorkerThread();
      worker_thread->Invoke<void>(RTC_FROM_HERE, [&]() {
        rtc::VideoSinkWants sink_settings{};
        sink_settings.rotation_applied = true;
        source_->AddOrUpdateSink(observer_.get(), sink_settings);
      });
    }
    observer_->SetCallback(callback);
  } else {
    // When clearing the existing callback, unregister and destroy the observer.
    // This ensures the native source knows when there is no more observer, and
    // can potentially optimize its behavior.
    if (observer_) {
      // Reset the callback.
      observer_->SetCallback(callback);
      if (!observer_->HasAnyCallbacks()) {
        // Detach the observer.
        // Track sources need to be manipulated from the worker thread
        rtc::Thread* const worker_thread =
            GlobalFactory::InstancePtr()->GetWorkerThread();
        worker_thread->Invoke<void>(
            RTC_FROM_HERE, [&]() { source_->RemoveSink(observer_.get()); });
        observer_.reset();
      }
    }
  }
}

void VideoTrackSource::SetCallback(I420AFrameReadyCallback callback) noexcept {
  return SetCallbackImpl(callback);
}

void VideoTrackSource::SetCallback(Argb32FrameReadyCallback callback) noexcept {
  return SetCallbackImpl(callback);
}

}  // namespace WebRTC
}  // namespace MixedReality
}  // namespace Microsoft
