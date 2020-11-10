// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include "mrs_errors.h"
#include "interop_api.h"
#include "../src/log_helpers.h"

extern "C" {

using mrsResult = Microsoft::MixedReality::WebRTC::Result;

//
// Native rendering
//

enum class VideoKind : int32_t {
  kNone = 0,
  kI420 = 1,
  kARGB = 2,
};

struct TextureDesc {
  void* texture{nullptr};
  int width{0};
  int height{0};
};

/// Signature of rendering method called by Unity.
typedef void(MRS_CALL* VideoRenderMethod)();

/// Create a native renderer and return a handle to it.
MRS_API mrsResult MRS_CALL
mrsNativeRenderer_Create(mrsRemoteVideoTrackHandle videoHandle) noexcept;

/// Destroy a native renderer.
MRS_API mrsResult MRS_CALL
mrsNativeRenderer_Destroy(mrsRemoteVideoTrackHandle videoHandle) noexcept;

//// Register textures for remote video and start rendering it.
MRS_API mrsResult MRS_CALL
mrsNativeRenderer_EnableRemoteVideo(mrsRemoteVideoTrackHandle videoHandle,
                                    VideoKind format,
                                    TextureDesc textures[],
                                    int textureCount) noexcept;

/// Clear remote textures and stop rendering remote video.
MRS_API mrsResult MRS_CALL mrsNativeRenderer_DisableRemoteVideo(mrsRemoteVideoTrackHandle videoHandle) noexcept;

/// Returns the rendering method called by Unity.
MRS_API VideoRenderMethod MRS_CALL
mrsNativeRenderer_GetVideoUpdateMethod() noexcept;

//
// Utils
//

/// Pipe log entries to Unity's log.
MRS_API void MRS_CALL
mrsNativeRenderer_SetLoggingFunctions(UnityLogger::LogFunction logDebugFunc,
                                      UnityLogger::UnityLogger::LogFunction logErrorFunc,
                                      UnityLogger::LogFunction logWarningFunc);
}
