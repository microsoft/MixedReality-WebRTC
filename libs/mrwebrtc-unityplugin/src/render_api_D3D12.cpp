// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"
#include "render_api.h"
#include "video_types.h"
#include "PlatformBase.h"

// Direct3D 12 implementation of RenderApi.

#if SUPPORT_D3D12

#include <assert.h>
#include <d3d12.h>
#include "Unity/IUnityGraphicsD3D12.h"

class RenderApi_D3D12 : public RenderApi {
 public:
  RenderApi_D3D12() {}
  virtual ~RenderApi_D3D12() {}

  virtual void ProcessEndOfFrame(uint64_t frameId);
  virtual void ProcessDeviceEvent(UnityGfxDeviceEventType type,
                                  IUnityInterfaces* interfaces);
  virtual bool BeginModifyTexture(const VideoDesc& desc, TextureUpdate* update);
  virtual void EndModifyTexture(void* dstTexture,
                                const TextureUpdate& update,
                                const VideoDesc& desc,
                                const std::vector<VideoRect>& rects = {});

 private:
  ID3D12Resource* GetUploadResource(UINT64 size);
  void CreateResources();
  void ReleaseResources();

 private:
  IUnityGraphicsD3D12v2* s_D3D12{nullptr};
  ID3D12Resource* s_D3D12Upload{nullptr};
  ID3D12CommandAllocator* s_D3D12CmdAlloc{nullptr};
  ID3D12GraphicsCommandList* s_D3D12CmdList{nullptr};
  UINT64 s_D3D12FenceValue{0};
  HANDLE s_D3D12Event{nullptr};
};

std::shared_ptr<RenderApi> CreateRenderApi_D3D12() {
  return std::make_shared<RenderApi_D3D12>();
}

const UINT kNodeMask = 0;

ID3D12Resource* RenderApi_D3D12::GetUploadResource(UINT64 size) {
  if (s_D3D12Upload) {
    D3D12_RESOURCE_DESC desc = s_D3D12Upload->GetDesc();
    if (desc.Width == size)
      return s_D3D12Upload;
    else
      s_D3D12Upload->Release();
  }

  // Texture upload buffer
  D3D12_HEAP_PROPERTIES heapProps = {};
  heapProps.Type = D3D12_HEAP_TYPE_UPLOAD;
  heapProps.CPUPageProperty = D3D12_CPU_PAGE_PROPERTY_UNKNOWN;
  heapProps.MemoryPoolPreference = D3D12_MEMORY_POOL_UNKNOWN;
  heapProps.CreationNodeMask = kNodeMask;
  heapProps.VisibleNodeMask = kNodeMask;

  D3D12_RESOURCE_DESC heapDesc = {};
  heapDesc.Dimension = D3D12_RESOURCE_DIMENSION_BUFFER;
  heapDesc.Alignment = 0;
  heapDesc.Width = size;
  heapDesc.Height = 1;
  heapDesc.DepthOrArraySize = 1;
  heapDesc.MipLevels = 1;
  heapDesc.Format = DXGI_FORMAT_UNKNOWN;
  heapDesc.SampleDesc.Count = 1;
  heapDesc.SampleDesc.Quality = 0;
  heapDesc.Layout = D3D12_TEXTURE_LAYOUT_ROW_MAJOR;
  heapDesc.Flags = D3D12_RESOURCE_FLAG_NONE;

  ID3D12Device* device = s_D3D12->GetDevice();
  HRESULT hr = device->CreateCommittedResource(
      &heapProps, D3D12_HEAP_FLAG_NONE, &heapDesc,
      D3D12_RESOURCE_STATE_GENERIC_READ, nullptr, IID_PPV_ARGS(&s_D3D12Upload));
  if (FAILED(hr)) {
    OutputDebugStringA("Failed to CreateCommittedResource.\n");
  }

  return s_D3D12Upload;
}

void RenderApi_D3D12::CreateResources() {
  ID3D12Device* device = s_D3D12->GetDevice();

  HRESULT hr = E_FAIL;

  // Command list
  hr = device->CreateCommandAllocator(D3D12_COMMAND_LIST_TYPE_DIRECT,
                                      IID_PPV_ARGS(&s_D3D12CmdAlloc));
  if (FAILED(hr))
    OutputDebugStringA("Failed to CreateCommandAllocator.\n");
  hr = device->CreateCommandList(kNodeMask, D3D12_COMMAND_LIST_TYPE_DIRECT,
                                 s_D3D12CmdAlloc, nullptr,
                                 IID_PPV_ARGS(&s_D3D12CmdList));
  if (FAILED(hr))
    OutputDebugStringA("Failed to CreateCommandList.\n");
  s_D3D12CmdList->Close();

  // Fence
  s_D3D12FenceValue = 0;
  s_D3D12Event = CreateEvent(nullptr, FALSE, FALSE, nullptr);
}

void RenderApi_D3D12::ReleaseResources() {
  SAFE_RELEASE(s_D3D12Upload);
  if (s_D3D12Event)
    CloseHandle(s_D3D12Event);
  SAFE_RELEASE(s_D3D12CmdList);
  SAFE_RELEASE(s_D3D12CmdAlloc);
}

void RenderApi_D3D12::ProcessEndOfFrame(uint64_t frameId) {}

void RenderApi_D3D12::ProcessDeviceEvent(UnityGfxDeviceEventType type,
                                         IUnityInterfaces* interfaces) {
  switch (type) {
    case kUnityGfxDeviceEventInitialize:
      s_D3D12 = interfaces->Get<IUnityGraphicsD3D12v2>();
      CreateResources();
      break;
    case kUnityGfxDeviceEventShutdown:
      ReleaseResources();
      break;
  }
}

bool RenderApi_D3D12::BeginModifyTexture(const VideoDesc& desc,
                                         TextureUpdate* update) {
  ID3D12Fence* fence = s_D3D12->GetFrameFence();

  // Wait on the previous job (example only - simplifies resource management)
  if (fence->GetCompletedValue() < s_D3D12FenceValue) {
    fence->SetEventOnCompletion(s_D3D12FenceValue, s_D3D12Event);
    WaitForSingleObject(s_D3D12Event, INFINITE);
  }

  // Begin a command list
  s_D3D12CmdAlloc->Reset();
  s_D3D12CmdList->Reset(s_D3D12CmdAlloc, nullptr);

  // Fill data
  const UINT64 kDataSize =
      desc.width * desc.height * GetBytesPerPixel(desc.format);
  ID3D12Resource* upload = GetUploadResource(kDataSize);
  void* mapped = nullptr;
  upload->Map(0, nullptr, &mapped);

  update->handle = upload;
  update->rowPitch = textureWidth * GetBytesPerPixel(desc.format);
  update->data = static_cast<uint8_t*>(mapped);
  return true;
}

void RenderApi_D3D12::EndModifyTexture(void* dstTexture,
                                       const TextureUpdate& update,
                                       const VideoDesc& desc,
                                       const std::vector<VideoRect>& rects) {
  ID3D12Device* device = s_D3D12->GetDevice();

  auto upload = (ID3D12Resource*)update->handle;
  upload->Unmap(0, nullptr);

  auto resource = (ID3D12Resource*)dstTexture;
  D3D12_RESOURCE_DESC desc = resource->GetDesc();

  D3D12_TEXTURE_COPY_LOCATION srcLoc = {};
  srcLoc.pResource = upload;
  srcLoc.Type = D3D12_TEXTURE_COPY_TYPE_PLACED_FOOTPRINT;
  device->GetCopyableFootprints(&desc, 0, 1, 0, &srcLoc.PlacedFootprint,
                                nullptr, nullptr, nullptr);

  D3D12_TEXTURE_COPY_LOCATION dstLoc = {};
  dstLoc.pResource = resource;
  dstLoc.Type = D3D12_TEXTURE_COPY_TYPE_SUBRESOURCE_INDEX;
  dstLoc.SubresourceIndex = 0;

  // We inform Unity that we expect this resource to be in
  // D3D12_RESOURCE_STATE_COPY_DEST state, and because we do not barrier it
  // ourselves, we tell Unity that no changes are done on our command list.
  UnityGraphicsD3D12ResourceState resourceState = {};
  resourceState.resource = resource;
  resourceState.expected = D3D12_RESOURCE_STATE_COPY_DEST;
  resourceState.current = D3D12_RESOURCE_STATE_COPY_DEST;

  // Queue data upload
  const VideoRect* rectPtr = (rects.size() == 0) ? &dstRect : rects.data();
  size_t rectCount = (rects.size() == 0) ? 1 : rects.size();
  for (const auto& rect : rects) {
    D3D12_BOX srcBox{
        rect.x, rect.y, 0, rect.x + rect.width, rect.y + rect.height, 0};
    s_D3D12CmdList->CopyTextureRegion(&dstLoc, rect.x, rect.y, 0, &srcLoc,
                                      &srcBox);
  }

  // Execute the command list
  s_D3D12CmdList->Close();
  s_D3D12FenceValue =
      s_D3D12->ExecuteCommandList(s_D3D12CmdList, 1, &resourceState);
}

#endif  // #if SUPPORT_D3D12
