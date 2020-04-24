// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

//< FIXME - Internal symbols not exported, need static linking
#if 0

#include "video_frame_observer.h"

using namespace Microsoft::MixedReality::WebRTC;

namespace {

class MockVideoFrameObserver : public VideoFrameObserver {
 public:
  // Expose publicly for testing.
  ArgbBuffer* mock_GetArgbScratchBuffer(int width, int height) {
    return GetArgbScratchBuffer(width, height);
  }
};

}  // namespace

//< FIXME - Internal symbols not exported, need static linking
//TEST(VideoFrameObserver, CreateArgbBuffer) {
//  auto buffer = ArgbBuffer::Create(12, 15);
//  ASSERT_NE(nullptr, buffer);
//  ASSERT_NE(nullptr, buffer->Data());
//  ASSERT_EQ(12 * 4, buffer->Stride());
//  ASSERT_EQ(15 * 12 * 4, buffer->Size());
//}

//< FIXME - Internal symbols not exported, need static linking
//TEST(VideoFrameObserver, CreateArgbBufferWithStride) {
//  auto buffer = ArgbBuffer::Create(12, 15, 16);
//  ASSERT_NE(nullptr, buffer);
//  ASSERT_NE(nullptr, buffer->Data());
//  ASSERT_EQ(16 * 4, buffer->Stride());
//  ASSERT_EQ(15 * 16 * 4, buffer->Size());
//}

TEST(VideoFrameObserver, GetArgbScratchBuffer) {
  MockVideoFrameObserver observer;
  ArgbBuffer* const buffer = observer.mock_GetArgbScratchBuffer(16, 16);
  ASSERT_NE(nullptr, buffer);
  ASSERT_NE(nullptr, buffer->Data());
  ASSERT_EQ(16 * 4, buffer->Stride());
  ASSERT_EQ(16 * 16 * 4, buffer->Size());
}

TEST(VideoFrameObserver, ReuseArgbScratchBuffer) {
  MockVideoFrameObserver observer;
  ArgbBuffer* const buffer0 = observer.mock_GetArgbScratchBuffer(16, 16);
  ArgbBuffer* const buffer1 = observer.mock_GetArgbScratchBuffer(15, 16);
  ASSERT_EQ(buffer0, buffer1);
  ArgbBuffer* const buffer2 = observer.mock_GetArgbScratchBuffer(16, 15);
  ASSERT_EQ(buffer0, buffer2);
  ArgbBuffer* const buffer3 = observer.mock_GetArgbScratchBuffer(16, 16);
  ASSERT_EQ(buffer0, buffer3);
  ArgbBuffer* const buffer4 = observer.mock_GetArgbScratchBuffer(17, 16);
  ASSERT_NE(buffer0, buffer4);
  ArgbBuffer* const buffer5 = observer.mock_GetArgbScratchBuffer(16, 17);
  ASSERT_NE(buffer0, buffer5);
  ASSERT_EQ(buffer4, buffer5);
  ArgbBuffer* const buffer6 = observer.mock_GetArgbScratchBuffer(16, 18);
  ASSERT_NE(buffer4, buffer6);
}

#endif  // #if 0
