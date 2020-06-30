// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include "interop/global_factory.h"
#include "media/audio_track_source.h"

namespace Microsoft {
namespace MixedReality {
namespace WebRTC {

AudioSourceAdapter::AudioSourceAdapter(
    rtc::scoped_refptr<webrtc::AudioSourceInterface> source)
    : source_(std::move(source)), state_(source->state()) {}

void AudioSourceAdapter::RegisterObserver(webrtc::ObserverInterface* observer) {
  observer_ = observer;
}

void AudioSourceAdapter::UnregisterObserver(
    webrtc::ObserverInterface* observer) {
  RTC_DCHECK_EQ(observer_, observer);
  observer_ = nullptr;
}

void AudioSourceAdapter::AddSink(webrtc::AudioTrackSinkInterface* sink) {
  sinks_.push_back(sink);
}

void AudioSourceAdapter::RemoveSink(webrtc::AudioTrackSinkInterface* sink) {
  auto it = std::find(sinks_.begin(), sinks_.end(), sink);
  if (it != sinks_.end()) {
    sinks_.erase(it);
  }
}

AudioTrackSource::AudioTrackSource(
    RefPtr<GlobalFactory> global_factory,
    ObjectType audio_track_source_type,
    rtc::scoped_refptr<webrtc::AudioSourceInterface> source) noexcept
    : TrackedObject(std::move(global_factory), audio_track_source_type),
      source_(std::move(source)) {
  RTC_CHECK(source_);
  RTC_CHECK(audio_track_source_type == ObjectType::kDeviceAudioTrackSource);
}

AudioTrackSource::~AudioTrackSource() {
  if (observer_) {
    source_->RemoveSink(observer_.get());
  }
}

void AudioTrackSource::SetCallback(AudioFrameReadyCallback callback) noexcept {
  std::lock_guard<std::mutex> lock(observer_mutex_);
  if (callback) {
    // When assigning a new callback, create and register an observer.
    if (!observer_) {
      observer_ = std::make_unique<AudioFrameObserver>();
      source_->AddSink(observer_.get());
    }
    observer_->SetCallback(callback);
  } else {
    // When clearing the existing callback, unregister and destroy the observer.
    // This ensures the native source knows when there is no more observer, and
    // can potentially optimize its behavior.
    if (observer_) {
      source_->RemoveSink(observer_.get());
      observer_.reset();
    }
  }
}

}  // namespace WebRTC
}  // namespace MixedReality
}  // namespace Microsoft
