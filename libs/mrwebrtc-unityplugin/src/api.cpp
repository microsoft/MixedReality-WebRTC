// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// This is a precompiled header, it must be on its own, followed by a blank
// line, to prevent clang-format from reordering it with other headers.
#include "pch.h"

#include "PlatformBase.h"
#include "../include/api.h"
#include "./Unity/IUnityGraphics.h"
#include "./Unity/IUnityInterface.h"
#include "native_renderer.h"

//
// Unity
//

static IUnityInterfaces* s_UnityInterfaces = nullptr;
static IUnityGraphics* s_Graphics = nullptr;


extern "C" UNITY_INTERFACE_EXPORT void UNITY_INTERFACE_API OnGraphicsDeviceEvent(UnityGfxDeviceEventType eventType) {
  if (s_Graphics && s_UnityInterfaces) {
    NativeRenderer::OnGraphicsDeviceEvent(eventType, s_Graphics->GetRenderer(), s_UnityInterfaces);
  }
}

extern "C" UNITY_INTERFACE_EXPORT void UNITY_INTERFACE_API UnityPluginLoad(IUnityInterfaces* unityInterfaces) {
  s_UnityInterfaces = unityInterfaces;
  s_Graphics = s_UnityInterfaces->Get<IUnityGraphics>();
  s_Graphics->RegisterDeviceEventCallback(OnGraphicsDeviceEvent);
  OnGraphicsDeviceEvent(kUnityGfxDeviceEventInitialize);
}

extern "C" UNITY_INTERFACE_EXPORT void UNITY_INTERFACE_API UnityPluginUnload() {
  if (s_Graphics) {
    s_Graphics->UnregisterDeviceEventCallback(OnGraphicsDeviceEvent);
    s_Graphics = nullptr;
  }
}

void MRS_CALL
mrsNativeRenderer_SetLoggingFunctions(UnityLogger::LogFunction logDebugFunc,
                                      UnityLogger::LogFunction logErrorFunc,
                                      UnityLogger::LogFunction logWarningFunc) {
  logDebugFunc("mrsNativeRenderer_SetLoggingFunctions");

#if SUPPORT_D3D11
  logDebugFunc("SUPPORT_D3D11");
#endif

#if SUPPORT_D3D12
  logDebugFunc("SUPPORT_D3D12");
#endif

#if SUPPORT_OPENGL_UNIFIED
  logDebugFunc("SUPPORT_OPENGL_UNIFIED");
#endif

#if SUPPORT_METAL
  logDebugFunc("SUPPORT_METAL");
#endif

  UnityLogger::SetLoggingFunctions(logDebugFunc, logErrorFunc, logWarningFunc);
}

//
// NativeRenderer API
//

using namespace Microsoft::MixedReality::WebRTC;

mrsResult MRS_CALL
mrsNativeRenderer_Create(mrsRemoteVideoTrackHandle videoHandle) noexcept {
  NativeRenderer::Create(videoHandle);
  return Result::kSuccess;
}

mrsResult MRS_CALL
mrsNativeRenderer_Destroy(mrsRemoteVideoTrackHandle videoHandle) noexcept {
  NativeRenderer::Destroy(videoHandle);
  return Result::kSuccess;
}

mrsResult MRS_CALL
mrsNativeRenderer_EnableRemoteVideo(mrsRemoteVideoTrackHandle videoHandle,
                                    VideoKind format,
                                    TextureDesc textures[],
                                    int textureCount) noexcept {
  UnityLogger::LogDebug("mrsNativeRenderer_EnableRemoteVideo");
  if (auto renderer = NativeRenderer::Get(videoHandle)) {
    renderer->EnableRemoteVideo(videoHandle, format, textures, textureCount);
  }
  return Result::kSuccess;
}

mrsResult MRS_CALL mrsNativeRenderer_DisableRemoteVideo(mrsRemoteVideoTrackHandle videoHandle) noexcept {
  if (auto renderer = NativeRenderer::Get(videoHandle)) {
    renderer->DisableRemoteVideo();
  }
  return Result::kSuccess;
}

VideoRenderMethod MRS_CALL mrsNativeRenderer_GetVideoUpdateMethod() noexcept {
  return NativeRenderer::DoVideoUpdate;
}
