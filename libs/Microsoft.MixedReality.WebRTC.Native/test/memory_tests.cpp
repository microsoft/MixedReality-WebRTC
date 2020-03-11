// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include "interop_api.h"

// Test fast path of mrsMemCpyStride() when data is packed.
TEST(MemoryUtils, MemCpyStride_Fast) {
  std::vector<uint8_t> s, d;
  constexpr int kWidth = 32;
  constexpr int kStride = kWidth;
  constexpr int kHeight = 13;
  s.resize(kStride * kHeight);
  d.resize(kStride * kHeight);
  {
    uint8_t* src = s.data();
    for (int j = 0; j < kHeight; ++j) {
      for (int i = 0; i < kWidth; ++i) {
        *src++ = (rand() & 0xFF);
      }
    }
  }
  {
    const void* const src = s.data();
    void* const dst = d.data();
    mrsMemCpyStride(dst, kStride, src, kStride, kWidth, kHeight);
    // Data is contiguous
    ASSERT_EQ(0, memcmp(src, dst, kStride * kHeight));
  }
}

// Test slow path of mrsMemCpyStride() with stride, without changing the
// packing.
TEST(MemoryUtils, MemCpyStride_Stride) {
  std::vector<uint8_t> s, d;
  constexpr int kWidth = 29;
  constexpr int kStride = 32;
  constexpr int kHeight = 13;
  s.resize(kStride * kHeight);
  d.resize(kStride * kHeight);
  {
    uint8_t* src = s.data();
    for (int j = 0; j < kHeight; ++j) {
      for (int i = 0; i < kWidth; ++i) {
        *src++ = (rand() & 0xFF);
      }
      for (int i = kWidth; i < kStride; ++i) {
        *src++ = 0xCF;
      }
    }
  }
  {
    const void* const src = s.data();
    void* const dst = d.data();
    mrsMemCpyStride(dst, kStride, src, kStride, kWidth, kHeight);
  }
  {
    const uint8_t* src = s.data();
    const uint8_t* dst = d.data();
    for (int j = 0; j < kHeight; ++j) {
      // Test row
      bool row_equal = true;
      for (int i = 0; i < kWidth; ++i) {
        row_equal = row_equal && (*src == *dst);
        ++src;
        ++dst;
      }
      ASSERT_TRUE(row_equal);
      // Skip row padding
      for (int i = kWidth; i < kStride; ++i) {
        ++src;
        ++dst;
      }
    }
  }
}

// Test slow path of mrsMemCpyStride() with stride, expanding the one existing
// in the source buffer.
TEST(MemoryUtils, MemCpyStride_ExpandStride) {
  std::vector<uint8_t> s, d;
  constexpr int kWidth = 29;
  constexpr int kSrcStride = 32;
  constexpr int kDstStride = 48;
  constexpr int kHeight = 13;
  s.resize(kSrcStride * kHeight);
  d.resize(kDstStride * kHeight);
  {
    uint8_t* src = s.data();
    for (int j = 0; j < kHeight; ++j) {
      for (int i = 0; i < kWidth; ++i) {
        *src++ = (rand() & 0xFF);
      }
      for (int i = kWidth; i < kSrcStride; ++i) {
        *src++ = 0xCF;
      }
    }
  }
  {
    const void* const src = s.data();
    void* const dst = d.data();
    mrsMemCpyStride(dst, kDstStride, src, kSrcStride, kWidth, kHeight);
  }
  {
    const uint8_t* src = s.data();
    const uint8_t* dst = d.data();
    for (int j = 0; j < kHeight; ++j) {
      // Test row
      bool row_equal = true;
      for (int i = 0; i < kWidth; ++i) {
        row_equal = row_equal && (*src == *dst);
        ++src;
        ++dst;
      }
      ASSERT_TRUE(row_equal);
      // Skip row padding
      for (int i = kWidth; i < kSrcStride; ++i) {
        ++src;
      }
      for (int i = kWidth; i < kDstStride; ++i) {
        ++dst;
      }
    }
  }
}

// Test slow path of mrsMemCpyStride() with stride, packing the data on output.
TEST(MemoryUtils, MemCpyStride_StrideToPack) {
  std::vector<uint8_t> s, d;
  constexpr int kWidth = 29;
  constexpr int kSrcStride = 32;
  constexpr int kDstStride = kWidth;
  constexpr int kHeight = 13;
  s.resize(kSrcStride * kHeight);
  d.resize(kDstStride * kHeight);
  {
    uint8_t* src = s.data();
    for (int j = 0; j < kHeight; ++j) {
      for (int i = 0; i < kWidth; ++i) {
        *src++ = (rand() & 0xFF);
      }
      for (int i = kWidth; i < kSrcStride; ++i) {
        *src++ = 0xCF;
      }
    }
  }
  {
    const void* const src = s.data();
    void* const dst = d.data();
    mrsMemCpyStride(dst, kDstStride, src, kSrcStride, kWidth, kHeight);
  }
  {
    const uint8_t* src = s.data();
    const uint8_t* dst = d.data();
    for (int j = 0; j < kHeight; ++j) {
      // Test row
      bool row_equal = true;
      for (int i = 0; i < kWidth; ++i) {
        row_equal = row_equal && (*src == *dst);
        ++src;
        ++dst;
      }
      ASSERT_TRUE(row_equal);
      // Skip row padding
      for (int i = kWidth; i < kSrcStride; ++i) {
        ++src;
      }
    }
  }
}
