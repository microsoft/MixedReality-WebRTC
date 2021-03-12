// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include "../include/api.h"
#include "handle_pool.h"
#include "interop_api.h"
#include "remote_video_track_interop.h"
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

uint64_t NativeRenderer::m_frameId{0};
static std::mutex g_lock;
static std::shared_ptr<RenderApi> g_renderApi;
static std::set<mrsNativeVideoHandle> g_nativeVideoUpdateQueue;
static std::vector<std::shared_ptr<I420VideoFrame>> g_freeI420VideoFrames;
static std::vector<std::shared_ptr<ArgbVideoFrame>> g_freeArgbVideoFrames;
std::set<mrsNativeVideoHandle> NativeRenderer::g_nativeVideos;
NativeRenderer::mrsTextureSizeChangedCallback NativeRenderer::g_textureSizeChangeCallback = nullptr;

bool I420VideoFrame::CopyFrame(const mrsI420AVideoFrame& frame) noexcept {
  width = frame.width_;
  height = frame.height_;
  ystride = frame.ystride_;
  ustride = frame.ustride_;
  vstride = frame.vstride_;
  size_t ysize = (size_t)ystride * height;
  size_t usize = (size_t)ustride * (height + 1) / 2;
  size_t vsize = (size_t)vstride * (height + 1) / 2;
  try { 
    ybuffer.resize(ysize);
    ubuffer.resize(usize);
    vbuffer.resize(vsize);
  }
  catch (std::bad_alloc const&) {
    Log_Warning("Copy Frame Resize Failed.");
    return false;
  }
  memcpy(ybuffer.data(), frame.ydata_, ysize);
  memcpy(ubuffer.data(), frame.udata_, usize);
  memcpy(vbuffer.data(), frame.vdata_, vsize);
  return true;
}

NativeRenderer* NativeRenderer::Create(mrsRemoteVideoTrackHandle videoTrackHandle) {
  {
    // Global lock
    std::lock_guard guard(g_lock);
    auto nativeVideo = new NativeRenderer(videoTrackHandle);
    g_nativeVideos.insert(nativeVideo);
    return nativeVideo;
  }
}

void NativeRenderer::Destroy(mrsNativeVideoHandle nativeVideoHandle) {
  {
    // Global lock
    std::lock_guard guard(g_lock);
    NativeRenderer* nativeVideo = (NativeRenderer*)nativeVideoHandle;
    nativeVideo->Shutdown();
    g_nativeVideos.erase(nativeVideoHandle);
  }
}

NativeRenderer::NativeRenderer(mrsRemoteVideoTrackHandle videoTrackHandle) : m_handle(videoTrackHandle) {
  Log_Debug("NativeRenderer::NativeRenderer");
  if (!g_renderApi) {
    Log_Warning("NativeRenderer: Unity plugin not initialized.");
  }
}

NativeRenderer::~NativeRenderer() {
  /// This is unsafe to log in Unity Editor.
  // Log_Debug("NativeRenderer::~NativeRenderer");
}

void NativeRenderer::SetTextureSizeChangeCallback(mrsTextureSizeChangedCallback textureSizeChangeCallback) noexcept {
  g_textureSizeChangeCallback = textureSizeChangeCallback;
}

void NativeRenderer::Shutdown() {
  Log_Debug("NativeRenderer::Shutdown");
  DisableRemoteVideo();
}

void NativeRenderer::EnableRemoteVideo(VideoKind format) noexcept {
  if (!g_renderApi) {
    Log_Warning("NativeRenderer: Unity plugin not initialized.");
  }

  switch (format) {
    case VideoKind::kI420:
        mrsRemoteVideoTrackRegisterI420AFrameCallback(m_handle, NativeRenderer::I420ARemoteVideoFrameCallback, this);
      break;
    case VideoKind::kARGB:
      Log_Warning("NativeRenderer: kARGB not currently supported.");
      // TODO
      break;
    case VideoKind::kNone:
      Log_Warning("NativeRenderer: No VideoKind specified.");
      // TODO
      break;
  }
}

void NativeRenderer::UpdateRemoteTextures(VideoKind format,
                                          TextureDesc textureDescs[],
                                          int textureDescCount) noexcept {
  {
    if (!g_renderApi) {
      Log_Warning("NativeRenderer: Unity plugin not initialized.");
    }

    {
      // Instance lock
      std::lock_guard guard(m_lock);
      m_remoteVideoFormat = format;
      switch (m_remoteVideoFormat) {
        case VideoKind::kI420:
          if (textureDescCount == 3) {

            m_remoteTextures.clear();
            try {
              m_remoteTextures.resize(3);
            } catch (std::bad_alloc const&) {
              Log_Warning("Resize fail in UpdateRemoteTextures.");
              return;
            }

            m_remoteTextures[0] = textureDescs[0];
            m_remoteTextures[1] = textureDescs[1];
            m_remoteTextures[2] = textureDescs[2];
          }
          break;
        case VideoKind::kARGB:
          Log_Warning("NativeRenderer: kARGB not currently supported.");
          // TODO
          break;

        case VideoKind::kNone:
          Log_Warning("NativeRenderer: No VideoKind specified.");
          // TODO
          break;
      }
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
    mrsRemoteVideoTrackRegisterI420AFrameCallback(m_handle, nullptr, nullptr);
  }
}

void NativeRenderer::I420ARemoteVideoFrameCallback(
  void* user_data,
  const mrsI420AVideoFrame& frame) {

  // It is possible for one buffer to be empty, each buffer must be checked.
  if (frame.ydata_ == nullptr || frame.udata_ == nullptr || frame.vdata_ == nullptr) {
    return;
  }

  NativeRenderer* nativeVideo = static_cast<NativeRenderer*>(user_data);
 
  {
    // Instance lock in case they get updated from UpdateRemoteTextures
    std::lock_guard guard(nativeVideo->m_lock);
    if (nativeVideo->m_remoteTextures.size() == 0 || (int)frame.width_ != nativeVideo->m_remoteTextures[0].width || (int)frame.height_ != nativeVideo->m_remoteTextures[0].height) {
      if (g_textureSizeChangeCallback != nullptr) {
        g_textureSizeChangeCallback(frame.width_, frame.height_, nativeVideo->m_handle);
      }
      return;
    }
  }

  // Acquire a video frame buffer from the free list with the global lock.
  std::shared_ptr<I420VideoFrame> newRemoteI420Frame = nullptr;
  {
    // Global lock
    std::lock_guard guard(g_lock);
    if (g_freeI420VideoFrames.size()) {
        newRemoteI420Frame = std::move(g_freeI420VideoFrames.back());
        g_freeI420VideoFrames.pop_back();
    } 
  }

  // If no free frame could be acquired, allocate new frame.
  if (newRemoteI420Frame == nullptr) {
    try {
      newRemoteI420Frame = std::make_shared<I420VideoFrame>();
    } catch (std::bad_alloc const&) {
      return;
    }
  }

  if (newRemoteI420Frame->CopyFrame(frame)) {
    std::shared_ptr<I420VideoFrame> staleRemoteI420Frame = nullptr;

    {
      // Set new copied frame on nativeVideo while in the instance lock of the
      // NativeVideo otherwise the render update loop may grab it pre-emptively.
      std::lock_guard guard(nativeVideo->m_lock);
      staleRemoteI420Frame = nativeVideo->m_nextI420RemoteVideoFrame;
      nativeVideo->m_nextI420RemoteVideoFrame = newRemoteI420Frame;
    }

    {
      std::lock_guard guard(g_lock);
      g_nativeVideoUpdateQueue.emplace(nativeVideo);

      // If there was a frame already on nativeVideo that means it was unprocessed
      // and was replaced with newer frame, so recycle it back.
      if (staleRemoteI420Frame != nullptr) {
        g_freeI420VideoFrames.push_back(std::move(staleRemoteI420Frame));
      }
    }
  } else {
    {
      // If frame copy fails for any reason, recycle frame.
      std::lock_guard guard(g_lock);
      g_freeI420VideoFrames.push_back(newRemoteI420Frame);
    }
  }
}

/// Renders current frame of all queued NativeRenderers.
void MRS_CALL NativeRenderer::DoVideoUpdate() {
  if (!g_renderApi)
    return;

  /// RESEARCH: Can all native renderers be handled in a single draw call?
  for (auto nativeVideoHandle : g_nativeVideoUpdateQueue) {
    if (!nativeVideoHandle)
      continue;

    NativeRenderer* nativeVideo = static_cast<NativeRenderer*>(nativeVideoHandle);

    // TODO: Support ARGB format.

    std::vector<TextureDesc> textures;
    std::shared_ptr<I420VideoFrame> remoteI420Frame;
    std::shared_ptr<ArgbVideoFrame> remoteArgbFrame;
    {
      // Instance lock
      std::lock_guard guard(nativeVideo->m_lock);
      // Copy the remote textures and current video frame.
      textures = nativeVideo->m_remoteTextures;
      remoteI420Frame = nativeVideo->m_nextI420RemoteVideoFrame;
      remoteArgbFrame = nativeVideo->m_nextArgbRemoteVideoFrame;
      nativeVideo->m_nextI420RemoteVideoFrame = nullptr;
      nativeVideo->m_nextArgbRemoteVideoFrame = nullptr;
    }

    if (remoteI420Frame) {

      int index = 0;
      for (const TextureDesc& textureDesc : textures) {
        VideoDesc videoDesc = {VideoFormat::R8, (uint32_t)textureDesc.width, (uint32_t)textureDesc.height};
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

        ++index;
      }

      g_renderApi->ProcessEndOfFrame(m_frameId++);

      // Recycle the frame
      {
        // Global lock
        std::lock_guard guard(g_lock);
        g_freeI420VideoFrames.push_back(std::move(remoteI420Frame));
      }
    }

    if (remoteArgbFrame) {
      // TODO

      // Recycle the frame
      {
        // Global lock
        std::lock_guard guard(g_lock);
        g_freeArgbVideoFrames.push_back(std::move(remoteArgbFrame));
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
