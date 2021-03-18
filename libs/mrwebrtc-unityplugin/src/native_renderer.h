// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include <map>
#include <mutex>
#include <set>

#include "render_api.h"

using mrsI420AVideoFrame = Microsoft::MixedReality::WebRTC::I420AVideoFrame;
using mrsArgb32VideoFrame = Microsoft::MixedReality::WebRTC::Argb32VideoFrame;

struct I420VideoFrame {
  int width{0};
  int height{0};
  int ystride{0};
  int ustride{0};
  int vstride{0};
  std::vector<uint8_t> ybuffer;
  std::vector<uint8_t> ubuffer;
  std::vector<uint8_t> vbuffer;

  bool CopyFrame(const mrsI420AVideoFrame& frame) noexcept;

  const std::vector<uint8_t>& GetBuffer(int i) {
    switch (i) {
      case 0:
        return ybuffer;
      case 1:
        return ubuffer;
      case 2:
        return vbuffer;
      default:
        // TODO: Fix.
        throw "aaah!";
    }
  }
};

struct ArgbVideoFrame {
    // TODO
};

class NativeRenderer {
 public:
  static NativeRenderer* Create(mrsRemoteVideoTrackHandle videoTrackHandle);
  static void Destroy(mrsNativeVideoHandle nativeVideoHandle);
  static std::shared_ptr<NativeRenderer> Get(mrsRemoteVideoTrackHandle videoHandle);

  NativeRenderer(mrsRemoteVideoTrackHandle videoHandle);
  ~NativeRenderer();

  mrsRemoteVideoTrackHandle Handle() const { return m_handle; }

  void EnableRemoteVideo(VideoKind format) noexcept;
  void UpdateRemoteTextures(VideoKind format,
                            TextureDesc textureDescs[],
                            int textureDescCount) noexcept;
  void DisableRemoteVideo();

  static void MRS_CALL DoVideoUpdate();
  static void OnGraphicsDeviceEvent(UnityGfxDeviceEventType eventType,
                                    UnityGfxRenderer deviceType,
                                    IUnityInterfaces* unityInterfaces);

  typedef void (*mrsTextureSizeChangedCallback)(int width, int height, mrsRemoteVideoTrackHandle handle);
  static void SetTextureSizeChangeCallback(mrsTextureSizeChangedCallback textureSizeChangeCallback) noexcept;

private:
  static std::set<mrsNativeVideoHandle> g_nativeVideos;

  mrsRemoteVideoTrackHandle m_handle{nullptr};
  std::mutex m_lock;
  std::vector<TextureDesc> m_remoteTextures;
  VideoKind m_remoteVideoFormat{VideoKind::kNone};
  std::shared_ptr<I420VideoFrame> m_nextI420RemoteVideoFrame;
  std::shared_ptr<ArgbVideoFrame> m_nextArgbRemoteVideoFrame;

  void Shutdown();

  static void MRS_CALL I420ARemoteVideoFrameCallback(void* user_data, const mrsI420AVideoFrame& frame);
  static void MRS_CALL ArgbRemotevideoFrameCallback(void* user_data, const mrsArgb32VideoFrame& frame);

  static mrsTextureSizeChangedCallback g_textureSizeChangeCallback;

  static uint64_t m_frameId;
};
