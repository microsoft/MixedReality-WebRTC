// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include "../include/api.h"
#include "handle_pool.h"
#include "include/interop_api.h"
#include "include/remote_video_track_interop.h"
#include "log_helpers.h"
#include "native_renderer.h"

#include <set>

//#pragma warning(disable : 4302 4311 4312)

// Mutex locking hierarchy. You may nest locks in this order only. Never go the
// other way. You don't necessarily have to  have a higher-order guard in place
// to lock a lower one, but once a lower one is locked, a higher one must not be
// subsequently locked.
//  1. g_lock -- Global lock (file-level)
//  2. s_lock -- Static lock (class-level)
//  3. m_lock -- Local lock (instance-level)

static std::mutex g_lock;
static std::shared_ptr<RenderApi> g_renderApi;
static std::set<PeerConnectionHandle> g_videoUpdateQueue;
static std::vector<std::shared_ptr<I420VideoFrame>> g_freeI420VideoFrames;
static std::vector<std::shared_ptr<ArgbVideoFrame>> g_freeArgbVideoFrames;
std::map<PeerConnectionHandle, std::shared_ptr<NativeRenderer>>
    NativeRenderer::g_renderers;

void I420VideoFrame::CopyFrame(const mrsI420AVideoFrame& frame) {
  width = frame.width_;
  height = frame.height_;
  ystride = frame.ystride_;
  ustride = frame.ustride_;
  vstride = frame.vstride_;
  size_t ysize = (size_t)ystride * height;
  size_t usize = (size_t)ustride * height / 2;
  size_t vsize = (size_t)vstride * height / 2;
  ybuffer.resize(ysize);
  ubuffer.resize(usize);
  vbuffer.resize(vsize);
  memcpy(ybuffer.data(), frame.ydata_, ysize);
  memcpy(ubuffer.data(), frame.udata_, usize);
  memcpy(vbuffer.data(), frame.vdata_, vsize);
}

void NativeRenderer::Create(PeerConnectionHandle peerHandle) {
  {
    // Global lock
    std::lock_guard guard(g_lock);
    auto existing = NativeRenderer::GetUnsafe(peerHandle);
    if (existing) {
      NativeRenderer::DestroyUnsafe(peerHandle);
    }
    g_renderers.emplace(peerHandle,
                        std::make_shared<NativeRenderer>(peerHandle));
  }
}

void NativeRenderer::Destroy(PeerConnectionHandle peerHandle) {
  {
    // Global lock
    std::lock_guard guard(g_lock);
    DestroyUnsafe(peerHandle);
  }
}

void NativeRenderer::DestroyUnsafe(PeerConnectionHandle peerHandle) {
  auto existing = NativeRenderer::GetUnsafe(peerHandle);
  if (existing) {
    existing->Shutdown();
    g_renderers.erase(peerHandle);
  }
}

std::shared_ptr<NativeRenderer> NativeRenderer::Get(
    PeerConnectionHandle peerHandle) {
  {
    // Global lock
    std::lock_guard guard(g_lock);
    return GetUnsafe(peerHandle);
  }
}

std::shared_ptr<NativeRenderer> NativeRenderer::GetUnsafe(
    PeerConnectionHandle peerHandle) {
  auto iter = g_renderers.find(peerHandle);
  if (iter == g_renderers.end())
    return nullptr;
  else
    return iter->second;
}

std::vector<std::shared_ptr<NativeRenderer>> NativeRenderer::MultiGetUnsafe(
    const std::set<PeerConnectionHandle>& peerHandles) {
  std::vector<std::shared_ptr<NativeRenderer>> renderers;
  for (auto peerHandle : peerHandles) {
    auto renderer = GetUnsafe(peerHandle);
    if (renderer) {
      renderers.push_back(renderer);
    }
  }
  return std::move(renderers);
}

NativeRenderer::NativeRenderer(PeerConnectionHandle peerHandle)
    : m_handle(peerHandle) {
  Log_Debug("NativeRenderer::NativeRenderer");
  if (!g_renderApi) {
    Log_Warning("NativeRenderer: Unity plugin not initialized.");
  }
}

NativeRenderer::~NativeRenderer() {
  /// This is unsafe to log in Unity Editor.
  // Log_Debug("NativeRenderer::~NativeRenderer");
}

void NativeRenderer::Shutdown() {
  Log_Debug("NativeRenderer::Shutdown");
  DisableRemoteVideo();
}

void NativeRenderer::EnableRemoteVideo(mrsRemoteVideoTrackHandle videoTrackHandle,
                                        VideoKind format,
                                        TextureDesc textureDescs[],
                                        int textureDescCount) {
  Log_Debug("NativeRenderer::EnableRemoteVideo");
  if (!g_renderApi) {
    Log_Warning("NativeRenderer: Unity plugin not initialized.");
  }
  {
    // Instance lock
    std::lock_guard guard(m_lock);
    m_remoteVideoFormat = format;
    switch (format) {
      case VideoKind::kI420:
        if (textureDescCount == 3) {
          m_remoteTextures.resize(3);
          m_remoteTextures[0] = textureDescs[0];
          m_remoteTextures[1] = textureDescs[1];
          m_remoteTextures[2] = textureDescs[2];
          mrsRemoteVideoTrackRegisterI420AFrameCallback(
              videoTrackHandle, NativeRenderer::I420ARemoteVideoFrameCallback,
              m_handle);
        }
        break;

      case VideoKind::kARGB:
        // TODO
        break;

      case VideoKind::kNone:
        // TODO
        break;
    }
  }
}

void NativeRenderer::DisableRemoteVideo() {
  Log_Debug("NativeRenderer::DisableRemoteVideo");
  {
    // Instance lock
    std::lock_guard guard(m_lock);
    m_remoteTextures.clear();
    m_remoteVideoFormat = VideoKind::kNone;
  }
}

void NativeRenderer::I420ARemoteVideoFrameCallback(
    void* user_data,
    const mrsI420AVideoFrame& frame) {
  UnityLogger::LogDebug("I420ARemoteVideoFrameCallback");

  if (auto renderer = NativeRenderer::Get(user_data)) {
    // RESEARCH: Do we need to keep a frame queue or is it fine to just render
    // the most recent frame?

    // The performance trade-off being made here is to lock g_lock two times,
    // preferring to copy the frame buffer outside any lock. Alternatively, the
    // copy operation could be done inside g_lock scope. This would result in a
    // single g_lock, but the lock would be held for a longer period of time
    // every video frame, for every video stream. RESEARCH: Which is better?

    // Acquire a video frame buffer from the free list.
    {
      // Global lock
      std::lock_guard guard(g_lock);
      if (renderer->m_nextI420RemoteVideoFrame == nullptr) {
        // TODO: Encapsulate this logic.
        if (g_freeI420VideoFrames.size()) {
          renderer->m_nextI420RemoteVideoFrame = g_freeI420VideoFrames.back();
          g_freeI420VideoFrames.pop_back();
        } else {
          renderer->m_nextI420RemoteVideoFrame =
              std::make_shared<I420VideoFrame>();
        }
      }
    }

    // Copy the incoming video frame to the buffer.
    renderer->m_nextI420RemoteVideoFrame->CopyFrame(frame);

    // Queue the renderer for the next video update.
    {
      // Global lock
      std::lock_guard guard(g_lock);
      // Register this renderer for the next video update.
      g_videoUpdateQueue.emplace(renderer->m_handle);
    }
  }
}

/// Renders current frame of all queued NativeRenderers.
void MRS_CALL NativeRenderer::DoVideoUpdate() {
  if (!g_renderApi)
    return;

  // Copy and zero out the video update queue.
  std::vector<std::shared_ptr<NativeRenderer>> renderers;
  {
    // Global lock
    std::lock_guard guard(g_lock);
    // Gather queued native renderers.
    renderers = NativeRenderer::MultiGetUnsafe(g_videoUpdateQueue);
    // Clear the queue.
    g_videoUpdateQueue.clear();
  }

  /// RESEARCH: Can all native renderers be handled in a single draw call?
  for (auto renderer : renderers) {
    UnityLogger::LogDebug("DoVideoUpdate renderer");
    if (!renderer)
      continue;

    // TODO: Support ARGB format.

    std::vector<TextureDesc> textures;
    std::shared_ptr<I420VideoFrame> remoteI420Frame;
    std::shared_ptr<ArgbVideoFrame> remoteArgbFrame;
    {
      // Instance lock
      std::lock_guard guard(renderer->m_lock);
      // Copy the remote textures and current video frame.
      textures = renderer->m_remoteTextures;
      remoteI420Frame = renderer->m_nextI420RemoteVideoFrame;
      remoteArgbFrame = renderer->m_nextArgbRemoteVideoFrame;
      renderer->m_nextI420RemoteVideoFrame = nullptr;
      renderer->m_nextArgbRemoteVideoFrame = nullptr;
    }

    if (remoteI420Frame) {
      // Render the frame to the textures.
      if (textures.size() < 3)
        continue;

      int index = 0;
      for (const TextureDesc& textureDesc : textures) {
#if 1
        const std::vector<uint8_t>& src = remoteI420Frame->GetBuffer(index);
        g_renderApi->SimpleUpdateTexture(textureDesc.texture, textureDesc.width,
                                         textureDesc.height, src.data(),
                                         src.size());
#else
        // WARNING!!! There is an egregious memory leak somewhere in
        // this code block. I suspect it is in the render API.
        VideoDesc videoDesc = {VideoFormat::R8, (uint32_t)textureDesc.width,
                               (uint32_t)textureDesc.height};
        RenderApi::TextureUpdate update;
        if (g_renderApi->BeginModifyTexture(videoDesc, &update)) {
          int copyPitch = std::min<int>(videoDesc.width, update.rowPitch);
          uint8_t* dst = static_cast<uint8_t*>(update.data);
          const uint8_t* src = remoteI420Frame->GetBuffer(index).data();
          for (int32_t r = 0; r < textureDesc.height; ++r) {
            memcpy(dst, src, copyPitch);
            dst += update.rowPitch;
            src += textureDesc.width;
          }
          g_renderApi->EndModifyTexture(textureDesc.texture, update, videoDesc);
        }
#endif
        ++index;
      }

      // Recycle the frame
      {
        // Global lock
        std::lock_guard guard(g_lock);
        g_freeI420VideoFrames.push_back(remoteI420Frame);
      }
    }

    if (remoteArgbFrame) {
      // TODO

      // Recycle the frame
      {
        // Global lock
        std::lock_guard guard(g_lock);
        g_freeArgbVideoFrames.push_back(remoteArgbFrame);
      }
    }
  }
}

void NativeRenderer::OnGraphicsDeviceEvent(UnityGfxDeviceEventType eventType,
                                           UnityGfxRenderer deviceType,
                                           IUnityInterfaces* unityInterfaces) {
  if (eventType == kUnityGfxDeviceEventInitialize) {
    g_renderApi = CreateRenderApi(deviceType);
  } else if (eventType == kUnityGfxDeviceEventShutdown) {
    g_renderApi = nullptr;
  }

  if (g_renderApi) {
    g_renderApi->ProcessDeviceEvent(eventType, unityInterfaces);
  }
}
