// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include <cstdint>
#include <memory>
#include <mutex>
#include <vector>

#include "Unity/IUnityGraphics.h"
#include "video_types.h"

struct IUnityInterfaces;

class RenderApi {
 public:
  struct TextureUpdate {
    void* handle{nullptr};
    uint8_t* data{nullptr};
    uint32_t rowPitch{0};
    uint32_t slicePitch{0};
  };

  virtual void ProcessEndOfFrame(uint64_t frameId) = 0;
  virtual void ProcessDeviceEvent(UnityGfxDeviceEventType type,
                                  IUnityInterfaces* interfaces) = 0;
  virtual bool BeginModifyTexture(const VideoDesc& desc,
                                  TextureUpdate* update) = 0;
  virtual void EndModifyTexture(void* dstTexture,
                                const TextureUpdate& update,
                                const VideoDesc& desc,
                                const std::vector<VideoRect>& rects = {}) = 0;
};

// Create a graphics API implementation instance for the given API type.
std::shared_ptr<RenderApi> CreateRenderApi(UnityGfxRenderer apiType);
