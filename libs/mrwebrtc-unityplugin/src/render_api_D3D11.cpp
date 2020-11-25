// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"
#include "render_api.h"
#include "video_types.h"
#include "PlatformBase.h"

// Direct3D 11 implementation of RenderApi.

#if SUPPORT_D3D11

#include <assert.h>
#include <d3d11.h>
#include <wrl.h>
#include <algorithm>
#include <deque>
#include <map>
#include <queue>
#include <vector>
#include "Unity/IUnityGraphicsD3D11.h"

// https://graphics.stanford.edu/~seander/bithacks.html#RoundUpPowerOf2
static uint32_t RoundUpPowerOf2(uint32_t v) {
  v--;
  v |= v >> 1;
  v |= v >> 2;
  v |= v >> 4;
  v |= v >> 8;
  v |= v >> 16;
  return v + 1;
}

class StagingBufferPool {
 public:
  ~StagingBufferPool() {
    m_freeTextures.clear();
    m_outstandingTextures.clear();
    while (m_unsafeTextures.size()) {
      m_unsafeTextures.pop();
    }
  }

  ID3D11Texture2D* GetFreeStagingTexture(ID3D11Device* device,
                                         const VideoDesc& desc) {
    // I typically do a round-up to the next power of two with square textures
    // for a generic pool, but since we're generally working with video
    // textures, I do expect a certain amount of regularity in size, and we want
    // to avoid the aggressive rounding we might see for certain resolutions
    // (e.g. 720p => 2048x2048), so we use the VideoDesc directly.
    auto itr = m_freeTextures.find(desc);
    if (itr == std::end(m_freeTextures)) {
      auto pair = m_freeTextures.emplace(desc, std::deque<FreeStruct>());
      if (!pair.second) {
        // Log_Warning("Could not get staging texture.");
        return nullptr;
      }
      itr = pair.first;
    }
    std::deque<FreeStruct>& textureArray = itr->second;

    Microsoft::WRL::ComPtr<ID3D11Texture2D> texture;
    if (textureArray.size()) {
      texture = textureArray.back().Texture;
      textureArray.pop_back();

      if (texture == nullptr) {
        // Log_Warning("Failed to get staging texture. Invalid entry in
        // free-texture array.");
      }
    } else {
      D3D11_TEXTURE2D_DESC stagingDesc{};
      stagingDesc.Width = desc.width;
      stagingDesc.Height = desc.height;
      stagingDesc.MipLevels = 0;
      stagingDesc.ArraySize = 1;
      switch (desc.format) {
        case VideoFormat::R8:
          stagingDesc.Format = DXGI_FORMAT_R8_UNORM;
          break;
        case VideoFormat::RG8:
          stagingDesc.Format = DXGI_FORMAT_R8G8_UNORM;
          break;
        case VideoFormat::RGBA8:
          stagingDesc.Format = DXGI_FORMAT_R8G8B8A8_UNORM_SRGB;
          break;
        case VideoFormat::BGRA8:
          stagingDesc.Format = DXGI_FORMAT_B8G8R8A8_UNORM_SRGB;
          break;
      }
      stagingDesc.SampleDesc.Count = 1;
      stagingDesc.SampleDesc.Quality = 0;
      stagingDesc.Usage = D3D11_USAGE_STAGING;
      stagingDesc.BindFlags = 0;
      stagingDesc.CPUAccessFlags = D3D11_CPU_ACCESS_WRITE;
      stagingDesc.MiscFlags = 0;

      HRESULT hr = device->CreateTexture2D(&stagingDesc, nullptr, &texture);
      if (FAILED(hr) || texture == nullptr) {
        // Log_Warning("Failed to create texture: 0x%x", hr);
      }
    }

    if (texture) {
      m_outstandingTextures.push_back(texture);
    }

    return texture.Get();
  }

  void ReleaseStagingTexture(ID3D11Texture2D* texture) {
    for (auto itr = std::begin(m_outstandingTextures);
         itr != std::end(m_outstandingTextures); ++itr) {
      if (itr->Get() == texture) {
        uint64_t thisFrameId = m_lastFrameId + 1;
        m_unsafeTextures.push({thisFrameId + FramesUntilSafe, texture});
        m_outstandingTextures.erase(itr);
        return;
      }
    }

    // Log_Warning("Attempted to release an untracked texture.");
  }

  void ProcessEndOfFrame(uint64_t frameId) {
    if (m_outstandingTextures.size() > 0) {
      // Log_Warning("There should be no outstanding textures at the end of a
      // frame.");
    }

    // Promote previously used textures from unsafe to safe, if enough time has
    // passed.
    D3D11_TEXTURE2D_DESC d3dDesc{};
    while (m_unsafeTextures.size() &&
           m_unsafeTextures.front().SafeOnFrameId <= frameId) {
      Microsoft::WRL::ComPtr<ID3D11Texture2D> texture =
          m_unsafeTextures.front().Texture;
      m_unsafeTextures.pop();

      texture->GetDesc(&d3dDesc);

      VideoDesc keyDesc{};
      keyDesc.width = d3dDesc.Width;
      keyDesc.height = d3dDesc.Height;
      switch (d3dDesc.Format) {
        case DXGI_FORMAT_R8_UNORM:
          keyDesc.format = VideoFormat::R8;
          break;
        case DXGI_FORMAT_R8G8_UNORM:
          keyDesc.format = VideoFormat::RG8;
          break;
        case DXGI_FORMAT_R8G8B8A8_UNORM_SRGB:
          keyDesc.format = VideoFormat::RGBA8;
          break;
        case DXGI_FORMAT_B8G8R8A8_UNORM_SRGB:
          keyDesc.format = VideoFormat::BGRA8;
          break;
      }

      auto itr = m_freeTextures.find(keyDesc);
      if (itr == std::end(m_freeTextures)) {
        // Log_Warning("Invalid texture found in delay queue. Refusing to place
        // it back in the free list.");
      } else {
        itr->second.push_back({frameId, texture});
      }
    }

    // Remove textures that are very old. We use the free array like a stack, so
    // the bottom textures are the ones way look at for removal.
    for (auto& freeTexturePair : m_freeTextures) {
      std::deque<FreeStruct>& freeTextureArray = freeTexturePair.second;
      while (freeTextureArray.size() &&
             freeTextureArray.front().LastUsedFrameId + FramesUntilDelete <=
                 frameId) {
        freeTextureArray.pop_front();
      }
    }

    m_lastFrameId = frameId;
  }

 private:
  // The number of frames we wait before we assume a used staging buffer is free
  // again. Three is larger than typical frame queues.
  static const uint64_t FramesUntilSafe = 3;

  // The number of frames we wait before we delete textures. This is pretty
  // lazy. We could improve this by using configurable size limits.
  static const uint64_t FramesUntilDelete = 3600;

  struct UnsafeStruct {
    uint64_t SafeOnFrameId{0};
    Microsoft::WRL::ComPtr<ID3D11Texture2D> Texture;
  };

  struct FreeStruct {
    uint64_t LastUsedFrameId{0};
    Microsoft::WRL::ComPtr<ID3D11Texture2D> Texture;
  };

  // Textures that are currently free for use.
  std::map<VideoDesc, std::deque<FreeStruct>> m_freeTextures;

  // Textures that have been used as staging textures but not enough frames have
  // passed for us to consider them safe for re-use.
  std::queue<UnsafeStruct> m_unsafeTextures;

  // Textures that have been requested as staging textures and have not yet been
  // reclaimed.
  std::list<Microsoft::WRL::ComPtr<ID3D11Texture2D>> m_outstandingTextures;

  uint64_t m_lastFrameId{0};
};

class RenderApi_D3D11 : public RenderApi {
 public:
  RenderApi_D3D11() {}
  virtual ~RenderApi_D3D11() {}

  virtual void ProcessEndOfFrame(uint64_t frameId);
  virtual void ProcessDeviceEvent(UnityGfxDeviceEventType type,
                                  IUnityInterfaces* interfaces);
  virtual bool BeginModifyTexture(const VideoDesc& desc, TextureUpdate* update);
  virtual void EndModifyTexture(void* dstTexture,
                                const TextureUpdate& update,
                                const VideoDesc& desc,
                                const std::vector<VideoRect>& rects = {});

 private:
  void CreateResources();
  void ReleaseResources();

 private:
  ID3D11Device* m_device{nullptr};
  std::shared_ptr<StagingBufferPool> m_pool;
};

std::shared_ptr<RenderApi> CreateRenderApi_D3D11() {
  return std::make_shared<RenderApi_D3D11>();
}

void RenderApi_D3D11::ProcessEndOfFrame(uint64_t frameId) {
  if (m_pool) {
    m_pool->ProcessEndOfFrame(frameId);
  }
}

void RenderApi_D3D11::ProcessDeviceEvent(UnityGfxDeviceEventType type,
                                         IUnityInterfaces* interfaces) {
  switch (type) {
    case kUnityGfxDeviceEventInitialize: {
      IUnityGraphicsD3D11* d3d = interfaces->Get<IUnityGraphicsD3D11>();
      m_device = d3d->GetDevice();
      CreateResources();
      break;
    }
    case kUnityGfxDeviceEventShutdown:
      ReleaseResources();
      m_device = nullptr;
      break;
  }
}

void RenderApi_D3D11::CreateResources() {
  m_pool = std::make_shared<StagingBufferPool>();
}

void RenderApi_D3D11::ReleaseResources() {
  m_pool = nullptr;
}

bool RenderApi_D3D11::BeginModifyTexture(const VideoDesc& desc,
                                         TextureUpdate* update) {
  // Validate our preconditions.
  if (update == nullptr || desc.width == 0 || desc.height == 0) {
    return false;
  }

  ID3D11DeviceContext* ctx = NULL;
  m_device->GetImmediateContext(&ctx);

  ID3D11Texture2D* stagingTexture =
      m_pool->GetFreeStagingTexture(m_device, desc);
  if (stagingTexture == nullptr) {
    ctx->Release();
    return false;
  }

  // Once we map, we either need to return true (tell the external caller to
  // unmap) or we need to manually unmap before we return.
  D3D11_MAPPED_SUBRESOURCE mappedRes{};
  HRESULT hr = ctx->Map(stagingTexture, 0, D3D11_MAP_WRITE, 0, &mappedRes);
  if (FAILED(hr)) {
    ctx->Release();
    return false;
  }

  update->handle = stagingTexture;
  update->rowPitch = mappedRes.RowPitch;
  update->slicePitch = mappedRes.DepthPitch;
  update->data = static_cast<uint8_t*>(mappedRes.pData);
  ctx->Release();
  return true;
}

void RenderApi_D3D11::EndModifyTexture(void* dstTexture,
                                       const TextureUpdate& update,
                                       const VideoDesc& desc_,
                                       const std::vector<VideoRect>& rects) {
  UNREFERENCED_PARAMETER(desc_);

  if (update.data == nullptr || update.handle == nullptr) {
    return;
  }

  ID3D11DeviceContext* ctx = nullptr;
  m_device->GetImmediateContext(&ctx);

  auto dstD3DTexture = (ID3D11Texture2D*)dstTexture;
  auto srcD3DTexture = (ID3D11Texture2D*)update.handle;

#if 0
    // Debugging.
    ctx->UpdateSubresource(dstD3DTexture, 0, nullptr, update.data, update.rowPitch, 0);
    ctx->Unmap(srcD3DTexture, 0);
#else
  // Immediately unmap, to make sure we don't leak resources and to make sure
  // our copies below are valid.
  ctx->Unmap(srcD3DTexture, 0);

  if (dstD3DTexture != nullptr) {
    D3D11_TEXTURE2D_DESC desc;
    srcD3DTexture->GetDesc(&desc);
    VideoRect srcRect{0, 0, static_cast<int32_t>(desc.Width),
                      static_cast<int32_t>(desc.Height)};

    dstD3DTexture->GetDesc(&desc);
    VideoRect dstRect{0, 0, static_cast<int32_t>(desc.Width),
                      static_cast<int32_t>(desc.Height)};

    // Copy all of the individual rects. We validate that our buffer sizes are
    // the same (we don't adjust for otherwise in the copy), we ensure that only
    // dirty regions are copied (non-dirty regions may not have valid data), and
    // we ensure we don't copy outside of the
    //  source or dest regions (avoids device removal).
    if (srcRect == dstRect) {
      const VideoRect* rectPtr = (rects.size() == 0) ? &dstRect : rects.data();
      size_t rectCount = (rects.size() == 0) ? 1 : rects.size();

      for (size_t i = 0; i < rectCount; ++i) {
        if (srcRect.Contains(rectPtr[i])) {
          D3D11_BOX srcBox{
              static_cast<uint32_t>(rectPtr[i].x),
              static_cast<uint32_t>(rectPtr[i].y),
              0,
              static_cast<uint32_t>(rectPtr[i].x + rectPtr[i].width),
              static_cast<uint32_t>(rectPtr[i].y + rectPtr[i].height),
              1};

          ctx->CopySubresourceRegion(dstD3DTexture,  // Destination resource.
                                     0,  // Destination subresource index.
                                     rectPtr[i].x,   // Destination X offset.
                                     rectPtr[i].y,   // Destination Y offset.
                                     0,              // Destination Z offset.
                                     srcD3DTexture,  // Source resource.
                                     0,         // Source subresource index.
                                     &srcBox);  // Source region.
        }
      }
    }
  }
#endif
  m_pool->ReleaseStagingTexture(srcD3DTexture);
  ctx->Release();
}

#endif  // #if SUPPORT_D3D11
