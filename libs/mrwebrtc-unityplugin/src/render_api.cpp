// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include "../include/api.h"
#include "Unity/IUnityGraphics.h"
#include "render_api.h"
#include "PlatformBase.h"

std::shared_ptr<RenderApi> CreateRenderApi(UnityGfxRenderer apiType) {
#if SUPPORT_D3D11
  if (apiType == kUnityGfxRendererD3D11) {
    extern std::shared_ptr<RenderApi> CreateRenderApi_D3D11();
    return CreateRenderApi_D3D11();
  }
#endif

#if SUPPORT_D3D12
  if (apiType == kUnityGfxRendererD3D12) {
    extern std::shared_ptr<RenderApi> CreateRenderApi_D3D12();
    return CreateRenderApi_D3D12();
  }
#endif

#if SUPPORT_OPENGL_UNIFIED
  if (apiType == kUnityGfxRendererOpenGLCore ||
      apiType == kUnityGfxRendererOpenGLES20 ||
      apiType == kUnityGfxRendererOpenGLES30) {
    extern std::shared_ptr<RenderApi> CreateRenderApi_OpenGLCoreES(
        UnityGfxRenderer apiType);
    return CreateRenderApi_OpenGLCoreES(apiType);
  }
#endif

#if SUPPORT_METAL
  if (apiType == kUnityGfxRendererMetal) {
    extern std::shared_ptr<RenderApi> CreateRenderApi_Metal();
    return CreateRenderApi_Metal();
  }
#endif

  // Unknown or unsupported graphics API
  return nullptr;
}
