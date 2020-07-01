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

// void MRS_CALL mrsAudioTrackSourceRegisterFrameCallback(
//    mrsAudioTrackSourceHandle source_handle,
//    mrsAudioFrameCallback callback,
//    void* user_data) noexcept {
//  if (auto source = static_cast<AudioTrackSource*>(source_handle)) {
//    source->SetCallback(AudioFrameReadyCallback{callback, user_data});
//  }
//}
