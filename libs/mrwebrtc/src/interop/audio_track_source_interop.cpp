// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// This is a precompiled header, it must be on its own, followed by a blank
// line, to prevent clang-format from reordering it with other headers.
#include "pch.h"

#include "audio_track_source_interop.h"
#include "callback.h"
#include "global_factory.h"
#include "media/audio_track_source.h"
#include "utils.h"

using namespace Microsoft::MixedReality::WebRTC;

void MRS_CALL
mrsAudioTrackSourceAddRef(mrsAudioTrackSourceHandle handle) noexcept {
  if (auto source = static_cast<AudioTrackSource*>(handle)) {
    source->AddRef();
  } else {
    RTC_LOG(LS_WARNING)
        << "Trying to add reference to NULL AudioTrackSource object.";
  }
}

void MRS_CALL
mrsAudioTrackSourceRemoveRef(mrsAudioTrackSourceHandle handle) noexcept {
  if (auto source = static_cast<AudioTrackSource*>(handle)) {
    source->RemoveRef();
  } else {
    RTC_LOG(LS_WARNING) << "Trying to remove reference from NULL "
                           "AudioTrackSource object.";
  }
}

void MRS_CALL mrsAudioTrackSourceSetName(mrsAudioTrackSourceHandle handle,
                                         const char* name) noexcept {
  if (auto source = static_cast<AudioTrackSource*>(handle)) {
    source->SetName(name);
  }
}

mrsResult MRS_CALL mrsAudioTrackSourceGetName(mrsAudioTrackSourceHandle handle,
                                              char* buffer,
                                              uint64_t* buffer_size) noexcept {
  auto source = static_cast<AudioTrackSource*>(handle);
  if (!source) {
    RTC_LOG(LS_ERROR) << "Invalid handle to audio track source.";
    return mrsResult::kInvalidNativeHandle;
  }
  if (!buffer) {
    RTC_LOG(LS_ERROR) << "Invalid NULL string buffer.";
    return mrsResult::kInvalidParameter;
  }
  if (!buffer_size) {
    RTC_LOG(LS_ERROR) << "Invalid NULL string buffer size reference.";
    return mrsResult::kInvalidParameter;
  }
  const std::string name = source->GetName();
  const size_t capacity = *buffer_size;
  const size_t size_with_terminator = name.size() + 1;
  // Always assign size, even if buffer too small
  *buffer_size = size_with_terminator;
  if (size_with_terminator <= capacity) {
    memcpy(buffer, name.c_str(), size_with_terminator);
    return mrsResult::kSuccess;
  }
  return mrsResult::kBufferTooSmall;
}

void MRS_CALL mrsAudioTrackSourceSetUserData(mrsAudioTrackSourceHandle handle,
                                             void* user_data) noexcept {
  if (auto source = static_cast<AudioTrackSource*>(handle)) {
    source->SetUserData(user_data);
  }
}

void* MRS_CALL
mrsAudioTrackSourceGetUserData(mrsAudioTrackSourceHandle handle) noexcept {
  if (auto source = static_cast<AudioTrackSource*>(handle)) {
    return source->GetUserData();
  }
  return nullptr;
}

mrsResult MRS_CALL mrsAudioTrackSourceCreateFromDevice(
    const mrsLocalAudioDeviceInitConfig* init_config,
    mrsAudioTrackSourceHandle* source_handle_out) noexcept {
  if (!source_handle_out) {
    RTC_LOG(LS_ERROR) << "Invalid NULL audio track source handle.";
    return Result::kInvalidParameter;
  }
  *source_handle_out = nullptr;

  RefPtr<GlobalFactory> global_factory(GlobalFactory::InstancePtr());
  auto pc_factory = global_factory->GetPeerConnectionFactory();
  if (!pc_factory) {
    return Result::kInvalidOperation;
  }

  // Create the audio track source
  cricket::AudioOptions options{};
  options.auto_gain_control = ToOptional(init_config->auto_gain_control_);
  rtc::scoped_refptr<webrtc::AudioSourceInterface> audio_source =
      pc_factory->CreateAudioSource(options);
  if (!audio_source) {
    RTC_LOG(LS_ERROR)
        << "Failed to create audio source from local audio capture device.";
    return Result::kUnknownError;
  }

  // Create the wrapper
  RefPtr<AudioTrackSource> wrapper =
      new AudioTrackSource(global_factory, std::move(audio_source));
  if (!wrapper) {
    RTC_LOG(LS_ERROR) << "Failed to create audio track source.";
    return Result::kUnknownError;
  }
  *source_handle_out = wrapper.release();
  return Result::kSuccess;
}

void MRS_CALL mrsAudioTrackSourceRegisterFrameCallback(
    mrsAudioTrackSourceHandle source_handle,
    mrsAudioFrameCallback callback,
    void* user_data) noexcept {
  if (auto source = static_cast<AudioTrackSource*>(source_handle)) {
    source->SetCallback(AudioFrameReadyCallback{callback, user_data});
  }
}
