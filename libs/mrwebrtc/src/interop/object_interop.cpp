// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// This is a precompiled header, it must be on its own, followed by a blank
// line, to prevent clang-format from reordering it with other headers.
#include "pch.h"

#include "interop_api.h"
#include "object_interop.h"
#include "tracked_object.h"
#include "utils.h"

using namespace Microsoft::MixedReality::WebRTC;

void MRS_CALL mrsObjectSetName(mrsObjectHandle handle,
                               const char* name) noexcept {
  if (auto obj = static_cast<TrackedObject*>(handle)) {
    obj->SetName(name);
  }
}

mrsResult MRS_CALL mrsObjectGetName(mrsObjectHandle handle,
                                    char* buffer,
                                    uint64_t* buffer_size) noexcept {
  auto obj = static_cast<TrackedObject*>(handle);
  if (!obj) {
    RTC_LOG(LS_ERROR) << "Invalid handle to object.";
    return mrsResult::kInvalidNativeHandle;
  }
  if (!buffer) {
    RTC_LOG(LS_ERROR) << "Invalid NULL string buffer.";
    return mrsResult::kInvalidParameter;
  }
  if (!buffer_size) {
    RTC_LOG(LS_ERROR) << "Invalid NULL string buffer size reference.";
    return mrsResult::kInvalidParameter;
  }
  const std::string name = obj->GetName();
  constexpr uint64_t max_capacity =
      (uint64_t)std::numeric_limits<size_t>::max();
  const size_t capacity =
      (size_t)std::min<uint64_t>(*buffer_size, max_capacity);
  const size_t size_with_terminator = name.size() + 1;
  // Always assign size, even if buffer too small
  *buffer_size = size_with_terminator;
  if (size_with_terminator <= capacity) {
    memcpy(buffer, name.c_str(), size_with_terminator);
    return mrsResult::kSuccess;
  }
  return mrsResult::kBufferTooSmall;
}

void MRS_CALL mrsObjectSetUserData(mrsObjectHandle handle,
                                   void* user_data) noexcept {
  if (auto obj = static_cast<TrackedObject*>(handle)) {
    obj->SetUserData(user_data);
  }
}

void* MRS_CALL mrsObjectGetUserData(mrsObjectHandle handle) noexcept {
  if (auto obj = static_cast<TrackedObject*>(handle)) {
    return obj->GetUserData();
  }
  return nullptr;
}
