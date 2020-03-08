// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include "simple_interop.h"

namespace {

void ForceTestFailure() {
  ASSERT_TRUE(false);
}

}  // namespace

void* SimpleInterop::CreateObject(ObjectType type) noexcept {
  const uint32_t id = free_id_.fetch_add(1);
  try {
    auto handle = std::make_unique<Handle>(Handle{this, type, id});
    void* interop_handle = handle.get();
    auto lock = std::scoped_lock{objects_map_mutex_};
    objects_map_.emplace(interop_handle, std::move(handle));
    return interop_handle;
  } catch (...) {
    ForceTestFailure();
  }
  return nullptr;
}

void SimpleInterop::DestroyObject(void* obj) noexcept {
  const uint32_t id = (uint32_t) reinterpret_cast<uint64_t>(obj);
  try {
    auto lock = std::scoped_lock{objects_map_mutex_};
    auto it = objects_map_.find(obj);
    EXPECT_NE(objects_map_.end(), it);
    if (it != objects_map_.end()) {
      EXPECT_EQ(this, it->second->interop_);
      EXPECT_EQ(id, it->second->id_);
      objects_map_.erase(it);
    }
  } catch (...) {
    ForceTestFailure();
  }
}

bool SimpleInterop::ObjectExists(ObjectType type, void* obj) noexcept {
  try {
    auto lock = std::scoped_lock{objects_map_mutex_};
    auto it = objects_map_.find(obj);
    if (it != objects_map_.end()) {
      // If an object address is found, it must be registered with the current
      // interop and with the right object type.
      EXPECT_EQ(this, it->second->interop_);
      EXPECT_EQ(type, it->second->object_type_);
      return true;
    }
  } catch (...) {
    ForceTestFailure();
  }
  return false;
}
