// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include "audio_frame.h"
#include "audio_track_source_interop.h"
#include "interop_api.h"
#include "object_interop.h"

#include "test_utils.h"

namespace {

class ObjectTests : public TestUtils::TestBase {};

}  // namespace

TEST_F(ObjectTests, Name) {
  // Use a peer connection object as a placeholder for any object.
  mrsPeerConnectionConfiguration config{};
  mrsPeerConnectionHandle handle{};
  ASSERT_EQ(mrsResult::kSuccess, mrsPeerConnectionCreate(&config, &handle));
  ASSERT_NE(nullptr, handle);

  // Exact-fit buffer
  {
    constexpr const char kTestName[] = "test_name_exact_fit_buffer";
    constexpr const size_t kTestNameSize =
        sizeof(kTestName);  // incl. zero-terminator
    mrsObjectSetName(handle, kTestName);
    constexpr const size_t kBufferSize = kTestNameSize;  // exact-fit
    char buffer[kBufferSize];
    uint64_t size = kBufferSize;
    ASSERT_EQ(mrsResult::kSuccess, mrsObjectGetName(handle, buffer, &size));
    ASSERT_EQ(kTestNameSize, size);
    ASSERT_EQ(0, memcmp(buffer, kTestName, kTestNameSize));
  }

  // Larger buffer
  {
    constexpr const char kTestName[] = "test_name_larger_buffer";
    constexpr const size_t kTestNameSize =
        sizeof(kTestName);  // incl. zero-terminator
    mrsObjectSetName(handle, kTestName);
    constexpr const size_t kBufferSize = kTestNameSize + 1;  // larger
    char buffer[kBufferSize];
    uint64_t size = kBufferSize;
    ASSERT_EQ(mrsResult::kSuccess, mrsObjectGetName(handle, buffer, &size));
    ASSERT_EQ(kTestNameSize, size);
    ASSERT_EQ(0, memcmp(buffer, kTestName, kTestNameSize));
  }

  // Buffer too small
  {
    constexpr const char kTestName[] = "test_name_buffer_too_small";
    constexpr const size_t kTestNameSize =
        sizeof(kTestName);  // incl. zero-terminator
    mrsObjectSetName(handle, kTestName);
    constexpr const size_t kBufferSize = kTestNameSize - 1;  // too small
    char buffer[kBufferSize];
    uint64_t size = kBufferSize;
    ASSERT_EQ(mrsResult::kBufferTooSmall,
              mrsObjectGetName(handle, buffer, &size));
    ASSERT_EQ(kTestNameSize, size);
  }

  // Invalid buffer
  {
    uint64_t size = 0;
    ASSERT_EQ(mrsResult::kInvalidParameter,
              mrsObjectGetName(handle, nullptr, &size));
  }

  // Invalid size
  {
    char buffer[5];
    ASSERT_EQ(mrsResult::kInvalidParameter,
              mrsObjectGetName(handle, buffer, nullptr));
  }

  mrsRefCountedObjectRemoveRef(handle);
}

TEST_F(ObjectTests, UserData) {
  // Use a peer connection object as a placeholder for any object.
  mrsPeerConnectionConfiguration config{};
  mrsPeerConnectionHandle handle{};
  ASSERT_EQ(mrsResult::kSuccess, mrsPeerConnectionCreate(&config, &handle));
  ASSERT_NE(nullptr, handle);

  void* const value = (void*)0x600DC4FE;
  ASSERT_EQ(nullptr, mrsObjectGetUserData(handle));
  mrsObjectSetUserData(handle, value);
  ASSERT_EQ(value, mrsObjectGetUserData(handle));

  mrsRefCountedObjectRemoveRef(handle);
}
