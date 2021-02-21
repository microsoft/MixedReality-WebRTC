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

void MR_UNITYPLUGIN__CALL
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

mrsNativeVideoHandle MR_UNITYPLUGIN__CALL
mrsNativeRenderer_Create(mrsRemoteVideoTrackHandle videoTrackHandle) noexcept {
  NativeRenderer* nativeVideo = NativeRenderer::Create(videoTrackHandle);
  return nativeVideo;
}

mrsResult MR_UNITYPLUGIN__CALL
mrsNativeRenderer_Destroy(mrsNativeVideoHandle nativeVideoHandle) noexcept {
  NativeRenderer::Destroy(nativeVideoHandle);
  return Result::kSuccess;
}

mrsResult MR_UNITYPLUGIN__CALL
mrsNativeRenderer_EnableRemoteVideo(mrsNativeVideoHandle nativeVideoHandle,
                                    VideoKind format,
                                    TextureDesc textures[],
                                    int textureCount) noexcept {
  UnityLogger::LogDebug("mrsNativeRenderer_EnableRemoteVideo");
  NativeRenderer* nativeVideo = static_cast<NativeRenderer*>(nativeVideoHandle);
  nativeVideo->SetRemoteVideoTextures(format, textures, textureCount);
  return Result::kSuccess;
}

mrsResult MR_UNITYPLUGIN__CALL
mrsNativeRenderer_UpdateTextures(mrsNativeVideoHandle nativeVideoHandle,
                                 TextureDesc textures[]) noexcept {
  UnityLogger::LogDebug("mrsNativeRenderer_UpdateTextures");
  NativeRenderer* nativeVideo = static_cast<NativeRenderer*>(nativeVideoHandle);
  nativeVideo->UpdateTextures(textures);
  return Result::kSuccess;
}

mrsResult MR_UNITYPLUGIN__CALL 
mrsNativeRenderer_DisableRemoteVideo(mrsNativeVideoHandle nativeVideoHandle) noexcept {
  NativeRenderer* nativeVideo = (NativeRenderer*)nativeVideoHandle;
  nativeVideo->DisableRemoteVideo();
  return Result::kSuccess;
}

VideoRenderMethod MR_UNITYPLUGIN__CALL 
mrsNativeRenderer_GetVideoUpdateMethod() noexcept {
  return NativeRenderer::DoVideoUpdate;
}

void MR_UNITYPLUGIN__CALL
mrsNativeRenderer_SetTextureSizeChangeCallback(NativeRenderer::TextureSizeChangeCallback callback) noexcept {
  UnityLogger::LogDebug("mrsNativeRenderer_SetTextureSizeChangeCallback");
  NativeRenderer::SetTextureSizeChangeCallback(callback);
}

