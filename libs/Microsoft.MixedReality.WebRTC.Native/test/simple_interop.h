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

  void Register(mrsPeerConnectionHandle pc) noexcept;
  void Unregister(mrsPeerConnectionHandle pc) noexcept;

  void* CreateObject(ObjectType type) noexcept;
  void DestroyObject(void* obj) noexcept;
  bool ObjectExists(ObjectType type, void* obj) noexcept;

  mrsRemoteAudioTrackInteropHandle RemoteAudioTrackCreate(
      mrsPeerConnectionInteropHandle parent,
      const mrsRemoteAudioTrackConfig& config) noexcept;
  mrsRemoteVideoTrackInteropHandle RemoteVideoTrackCreate(
      mrsPeerConnectionInteropHandle parent,
      const mrsRemoteVideoTrackConfig& config) noexcept;
  mrsDataChannelInteropHandle DataChannelCreate(
      mrsPeerConnectionInteropHandle parent,
      const mrsDataChannelConfig& config,
      mrsDataChannelCallbacks* callbacks) noexcept;

  static mrsRemoteAudioTrackInteropHandle MRS_CALL RemoteAudioTrackCreateStatic(
      mrsPeerConnectionInteropHandle parent,
      const mrsRemoteAudioTrackConfig& config) noexcept;
  static mrsRemoteVideoTrackInteropHandle MRS_CALL RemoteVideoTrackCreateStatic(
      mrsPeerConnectionInteropHandle parent,
      const mrsRemoteVideoTrackConfig& config) noexcept;
  static mrsDataChannelInteropHandle MRS_CALL
  DataChannelCreateStatic(mrsPeerConnectionInteropHandle parent,
                          const mrsDataChannelConfig& config,
                          mrsDataChannelCallbacks* callbacks) noexcept;

  std::mutex objects_map_mutex_;
  std::unordered_map<void*, std::unique_ptr<Handle>> objects_map_;
  std::atomic_uint32_t free_id_;
};
