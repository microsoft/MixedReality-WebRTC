// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include <algorithm>
#include <cstdint>

enum class VideoFormat {
  R8,   // 1 plane: r. 8bits per pixel. Probably shouldn't be used directly as a
        // requested video format. Meant to help with multi-planar formats.
  RG8,  // 1 plane: rg. 16 bits per pixel. Probably shouldn't be used directly
        // as a requested video format. Meant to help with multi-planar formats.
  RGBA8,    // 1 plane: rgba. 32 bits per pixel.
  BGRA8,    // 1 plane: bgra. 32 bits per pixel.
  YUV420P,  // 3 planes: y, u, and v. 12 bits per pixel.
  NV12,     // 2 planes: y and uv. 12 bits per pixel.
};

inline uint32_t GetBytesPerPixel(VideoFormat format) {
  switch (format) {
    case VideoFormat::R8:
      return 1;
    case VideoFormat::RG8:
      return 2;
    case VideoFormat::RGBA8:
      return 4;
    case VideoFormat::BGRA8:
      return 4;
    default:
      // Log_Error("GetBytesPerPixel called on unsupported format.");
      return 0;
  }
}

struct VideoRect {
  int32_t x{0};
  int32_t y{0};
  int32_t width{0};
  int32_t height{0};

  bool HasZeroArea() const { return width == 0 || height == 0; }

  bool IsValid() const { return width > 0 && height > 0; }

  void UnionRect(const VideoRect& other) {
    int32_t myX2 = x + width;
    int32_t myY2 = y + height;
    x = (std::min)(x, other.x);
    y = (std::min)(y, other.y);
    width = (std::max)(myX2, other.x + other.width) - x;
    height = (std::max)(myY2, other.y + other.height) - y;
  }

  void Intersect(const VideoRect& other) {
    int32_t x1 = (std::max)(x, other.x);
    int32_t y1 = (std::max)(y, other.y);
    int32_t x2 = (std::min)(x + width, other.x + other.width);
    int32_t y2 = (std::min)(y + height, other.y + other.height);
    x = x1;
    y = y1;
    width = x2 - x1;
    height = y2 - y1;
  }

  bool Contains(const VideoRect& other) {
    return x <= other.x && y <= other.y && x + width >= other.x + other.width &&
           y + height >= other.y + other.height;
  }

  inline bool operator!=(const VideoRect& rhs) const {
    return x != rhs.x || y != rhs.y || width != rhs.width ||
           height != rhs.height;
  }

  inline bool operator==(const VideoRect& rhs) const {
    return !(operator!=(rhs));
  }
};

struct VideoDesc {
  VideoFormat format{VideoFormat::RGBA8};
  uint32_t width{0};
  uint32_t height{0};

  inline bool operator!=(const VideoDesc& rhs) const {
    return format != rhs.format || width != rhs.width || height != rhs.height;
  }

  inline bool operator==(const VideoDesc& rhs) const {
    return !(operator!=(rhs));
  }

  // Used to participate in maps.
  inline bool operator<(const VideoDesc& rhs) const {
    if (format != rhs.format) {
      return static_cast<int>(format) < static_cast<int>(rhs.format);
    }

    if (width != rhs.width) {
      return width < rhs.width;
    }

    return height < rhs.height;
  }
};
