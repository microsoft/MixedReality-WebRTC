// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"
#include "render_api.h"
#include "video_types.h"
#include "PlatformBase.h"
#include "../src/log_helpers.h"
#include <map>
#include <deque>
#include <queue>
#include <list>

// OpenGL Core profile (desktop) or OpenGL ES (mobile) implementation of
// RenderApi. Supports several flavors: Core, ES2, ES3

#if SUPPORT_OPENGL_UNIFIED

#include <assert.h>

#if SUPPORT_OPENGL_CORE
#include "GLEW/glew.h"
#elif UNITY_IPHONE
#include <OpenGLES/ES3/gl.h>
#elif UNITY_ANDROID || UNITY_WEBGL
#include <GLES3/gl3.h>
#endif

class PixelBufferPool {
 public:
  ~PixelBufferPool() {
    for (OutstandingStruct& os : m_outstandingBuffers) {
      glDeleteBuffers(1, &os.BufferId);
    }
    m_outstandingBuffers.clear();

    for (auto& bufferIdArray : m_freeBuffers) {
      for (FreeStruct& fs : bufferIdArray.second) {
        glDeleteBuffers(1, &fs.BufferId);
      }
    }
    m_freeBuffers.clear();

    while (m_unsafeBuffers.size()) {
      glDeleteBuffers(1, &m_unsafeBuffers.front().BufferId);
      m_unsafeBuffers.pop();
    }
  }

  ///
  /// Returns GL_INVALID_VALUE on failure
  ///
  GLuint GetFreePixelBuffer(const VideoDesc& desc) {
    // I typically do a round-up to the next power of two with square textures
    // for a generic pool, but since we're generally working with video
    // textures, I do expect a certain amount of regularity in size, and we want
    // to avoid the aggressive rounding we might see for certain resolutions
    // (e.g. 720p => 2048x2048), so we use the VideoDesc directly.
    auto itr = m_freeBuffers.find(desc);
    if (itr == std::end(m_freeBuffers)) {
      auto pair = m_freeBuffers.emplace(desc, std::deque<FreeStruct>());
      if (!pair.second) {
        Log_Warning("Could not get pixel buffer.");
        return GL_INVALID_VALUE;
      }
      itr = pair.first;
    }
    std::deque<FreeStruct>& bufferIdArray = itr->second;

    GLuint bufferId = GL_INVALID_VALUE;
    if (bufferIdArray.size()) {
      bufferId = bufferIdArray.back().BufferId;
      bufferIdArray.pop_back();

      if (bufferId == GL_INVALID_VALUE) {
        Log_Warning(
            "Failed to get staging texture. Invalid entry in free-texture "
            "array.");
      }
    } else  // create a new one
    {
      glGenBuffers(1, &bufferId);

      if (bufferId != GL_INVALID_VALUE) {
        glBindBuffer(GL_PIXEL_UNPACK_BUFFER, bufferId);
        GLsizeiptr size =
            desc.height * desc.width * GetBytesPerPixel(desc.format);
        glBufferData(GL_PIXEL_UNPACK_BUFFER, size, nullptr, GL_STREAM_DRAW);
        glBindBuffer(GL_PIXEL_UNPACK_BUFFER, 0);
      }
    }

    if (bufferId != GL_INVALID_VALUE) {
      m_outstandingBuffers.push_back({bufferId, desc});
    }

    return bufferId;
  }

  void ReleasePixelBuffer(GLuint bufferId) {
    for (auto itr = std::begin(m_outstandingBuffers);
         itr != std::end(m_outstandingBuffers); ++itr) {
      if ((*itr).BufferId == bufferId) {
        uint64_t thisFrameId = m_lastFrameId + 1;
        m_unsafeBuffers.push(
            {thisFrameId + FramesUntilSafe, bufferId, (*itr).Desc});
        m_outstandingBuffers.erase(itr);
        return;
      }
    }

    Log_Warning("Attempted to release an untracked texture.");
  }

  void ProcessEndOfFrame(uint64_t frameId) {
    if (m_outstandingBuffers.size() > 0) {
      Log_Warning(
          "There should be no outstanding textures at the end of a frame.");
    }

    // Promote previously used buffers from unsafe to safe, if enough time has
    // passed.
    while (m_unsafeBuffers.size() &&
           m_unsafeBuffers.front().SafeOnFrameId <= frameId) {
      GLuint bufferId = m_unsafeBuffers.front().BufferId;
      VideoDesc keyDesc = m_unsafeBuffers.front().Desc;
      m_unsafeBuffers.pop();

      auto itr = m_freeBuffers.find(keyDesc);
      if (itr == std::end(m_freeBuffers)) {
        Log_Warning(
            "Invalid texture found in delay queue. Refusing to place it back "
            "in the free list.");
      } else {
        itr->second.push_back({frameId, bufferId, keyDesc});
      }
    }

    // Remove textures that are very old. We use the free array like a stack, so
    // the bottom textures are the ones way look at for removal.
    for (auto& freeTexturePair : m_freeBuffers) {
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
    GLuint BufferId;
    VideoDesc Desc;
  };

  struct FreeStruct {
    uint64_t LastUsedFrameId{0};
    GLuint BufferId;
    VideoDesc Desc;
  };

  struct OutstandingStruct  // really its the best one
  {
    GLuint BufferId;
    VideoDesc Desc;
  };

  // Textures that are currently free for use.
  std::map<VideoDesc, std::deque<FreeStruct>> m_freeBuffers;

  // Textures that have been used as staging textures but not enough frames have
  // passed for us to consider them safe for re-use.
  std::queue<UnsafeStruct> m_unsafeBuffers;

  // Textures that have been requested as staging textures and have not yet been
  // reclaimed.
  std::list<OutstandingStruct> m_outstandingBuffers;

  uint64_t m_lastFrameId{0};
};

class RenderApi_OpenGLCoreES : public RenderApi {
 public:
  RenderApi_OpenGLCoreES(UnityGfxRenderer apiType);
  virtual ~RenderApi_OpenGLCoreES() {}

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
  UnityGfxRenderer m_APIType;
  std::shared_ptr<PixelBufferPool> m_pool;
};

std::shared_ptr<RenderApi> CreateRenderApi_OpenGLCoreES(
    UnityGfxRenderer apiType) {
  return std::make_shared<RenderApi_OpenGLCoreES>(apiType);
}

RenderApi_OpenGLCoreES::RenderApi_OpenGLCoreES(UnityGfxRenderer apiType)
    : m_APIType(apiType) {}

void RenderApi_OpenGLCoreES::ProcessEndOfFrame(uint64_t frameId) {
  if (m_pool) {
    m_pool->ProcessEndOfFrame(frameId);
  }
}

void RenderApi_OpenGLCoreES::ProcessDeviceEvent(UnityGfxDeviceEventType type,
                                                IUnityInterfaces* interfaces) {
  if (type == kUnityGfxDeviceEventInitialize) {
#if defined(SUPPORT_OPENGL_CORE)
    glewInit();
    GLenum err = glewInit();
    if (err != GLEW_OK) {
      Log_Error("glewInit failed something is seriously wrong. error:{0}", err);
      exit(1);
    }
#endif
    CreateResources();
  } else if (type == kUnityGfxDeviceEventShutdown) {
    // Todo: This event doesn't seem to be called (at least in play mode)
    ReleaseResources();
  }
}

void RenderApi_OpenGLCoreES::CreateResources() {
  m_pool = std::make_shared<PixelBufferPool>();
}

void RenderApi_OpenGLCoreES::ReleaseResources() {
  m_pool = nullptr;
}

bool RenderApi_OpenGLCoreES::BeginModifyTexture(const VideoDesc& desc,
                                                TextureUpdate* update) {
  // Validate our preconditions.
  if (update == nullptr || desc.width == 0 || desc.height == 0) {
    return false;
  }

  if (!m_pool) {
    Log_Error("Render API not properly set up!");
    return false;
  }

  GLuint bufferId = m_pool->GetFreePixelBuffer(desc);
  if (bufferId == GL_INVALID_VALUE) {
    return false;
  }

  // bind PBO to update texture source
  glBindBuffer(GL_PIXEL_UNPACK_BUFFER, bufferId);

  // map the buffer object into client's memory
#if SUPPORT_OPENGL_CORE
  GLubyte* ptr = (GLubyte*)glMapBuffer(GL_PIXEL_UNPACK_BUFFER, GL_WRITE_ONLY);
#else
  GLsizeiptr size = desc.height * desc.width * GetBytesPerPixel(desc.format);
  GLubyte* ptr = (GLubyte*)glMapBufferRange(
      GL_PIXEL_UNPACK_BUFFER, 0, size,
      GL_MAP_WRITE_BIT | GL_MAP_INVALIDATE_BUFFER_BIT);
#endif

  if (!ptr) {
    glBindBuffer(GL_PIXEL_UNPACK_BUFFER, 0);
    return false;
  }

  update->rowPitch = desc.width * GetBytesPerPixel(desc.format);
  update->slicePitch = desc.width * update->rowPitch;
  update->data = ptr;
  update->handle = (void*)(size_t)bufferId;
  return true;
}

void RenderApi_OpenGLCoreES::EndModifyTexture(
    void* dstTexture,
    const TextureUpdate& update,
    const VideoDesc& desc,
    const std::vector<VideoRect>& rects) {
  glUnmapBuffer(GL_PIXEL_UNPACK_BUFFER);  // release the mapped buffer

  GLuint gltex = (GLuint)(size_t)(dstTexture);

  // Todo: ensure that the pixel buffer is actually the same size as the texture

  // Update texture data, and free the memory buffer
  glBindTexture(GL_TEXTURE_2D, gltex);

  VideoRect textRect = {0, 0, (int)desc.width, (int)desc.height};

  const VideoRect* rectPtr = (rects.size() == 0) ? &textRect : rects.data();
  size_t rectCount = (rects.size() == 0) ? 1 : rects.size();

  GLenum glFormat = 0;
  switch (desc.format) {
    case VideoFormat::R8:
      glFormat = GL_RED;
      break;
    case VideoFormat::RG8:
      glFormat = GL_RG;
      break;
    case VideoFormat::BGRA8:
    case VideoFormat::RGBA8:
      glFormat = GL_RGBA;
      break;
    default:
      Log_Error("Invalid EndModifyTextureParameters!");
  }

  // Only copy if the format is valid.
  if (glFormat > 0) {
    for (size_t i = 0; i < rectCount; ++i) {
      glTexSubImage2D(GL_TEXTURE_2D,      // Target
                      0,                  // Mip Level
                      rectPtr[i].x,       // xOffset
                      rectPtr[i].y,       // yOffset
                      rectPtr[i].width,   // Copy Width
                      rectPtr[i].height,  // Copy Height
                      glFormat, GL_UNSIGNED_BYTE,
                      0 /*update.Data*/);  // Use PBO
    }
  }
  glBindTexture(GL_TEXTURE_2D, 0);
  glBindBuffer(GL_PIXEL_UNPACK_BUFFER, 0);

  auto bufferId = (GLuint)(size_t)update.handle;
  m_pool->ReleasePixelBuffer(bufferId);
}

#endif  // #if SUPPORT_OPENGL_UNIFIED
