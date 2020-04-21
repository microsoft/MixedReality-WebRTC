// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include "../include/interop_api.h"
#include "../include/peer_connection_interop.h"
#include "../src/interop/global_factory.h"
#include "../src/mrs_errors.h"

using namespace Microsoft::MixedReality::WebRTC;

/// A simple interop layer maintaining a hash map of all objects, with assigned
/// unique identifiers. Use this to keep track of multiple interop objects in
/// tests and ensure consistency of interop handle types.
class SimpleInterop {
 public:
  using ObjectType = Microsoft::MixedReality::WebRTC::ObjectType;

  struct Handle {
    SimpleInterop* interop_;
    ObjectType object_type_;
    uint32_t id_;
  };

  void* CreateObject(ObjectType type) noexcept;
  void DestroyObject(void* obj) noexcept;
  bool ObjectExists(ObjectType type, void* obj) noexcept;

  std::mutex objects_map_mutex_;
  std::unordered_map<void*, std::unique_ptr<Handle>> objects_map_;
  std::atomic_uint32_t free_id_;
};
