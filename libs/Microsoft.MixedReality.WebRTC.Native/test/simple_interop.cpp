// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include "simple_interop.h"

void SimpleInterop::Register(mrsPeerConnectionHandle pc) noexcept {
  mrsPeerConnectionInteropCallbacks interop{};
  interop.remote_audio_track_create_object = &RemoteAudioTrackCreateStatic;
  interop.remote_video_track_create_object = &RemoteVideoTrackCreateStatic;
  interop.data_channel_create_object = &DataChannelCreateStatic;
  ASSERT_EQ(Result::kSuccess,
            mrsPeerConnectionRegisterInteropCallbacks(pc, &interop));
}

void SimpleInterop::Unregister(mrsPeerConnectionHandle pc) noexcept {
  mrsPeerConnectionInteropCallbacks interop{};
  ASSERT_EQ(Result::kSuccess,
            mrsPeerConnectionRegisterInteropCallbacks(pc, &interop));
}

void* SimpleInterop::CreateObject(ObjectType type) noexcept {
  const uint32_t id = free_id_.fetch_add(1);
  auto handle = std::make_unique<Handle>(Handle{this, type, id});
  void* interop_handle = handle.get();
  try {
    auto lock = std::scoped_lock{objects_map_mutex_};
    objects_map_.emplace(interop_handle, std::move(handle));
  } catch (...) {
  }
  return interop_handle;
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
  }
  return false;
}

mrsRemoteAudioTrackInteropHandle SimpleInterop::RemoteAudioTrackCreate(
    mrsPeerConnectionInteropHandle parent,
    const mrsRemoteAudioTrackConfig& /*config*/) noexcept {
  EXPECT_TRUE(ObjectExists(ObjectType::kPeerConnection, parent));
  return CreateObject(ObjectType::kRemoteAudioTrack);
}

mrsRemoteVideoTrackInteropHandle SimpleInterop::RemoteVideoTrackCreate(
    mrsPeerConnectionInteropHandle parent,
    const mrsRemoteVideoTrackConfig& /*config*/) noexcept {
  EXPECT_TRUE(ObjectExists(ObjectType::kPeerConnection, parent));
  return CreateObject(ObjectType::kRemoteVideoTrack);
}

mrsDataChannelInteropHandle SimpleInterop::DataChannelCreate(
    mrsPeerConnectionInteropHandle parent,
    const mrsDataChannelConfig& /*config*/,
    mrsDataChannelCallbacks* /*callbacks*/) noexcept {
  EXPECT_TRUE(ObjectExists(ObjectType::kPeerConnection, parent));
  return CreateObject(ObjectType::kDataChannel);
}

mrsRemoteAudioTrackInteropHandle MRS_CALL
SimpleInterop::RemoteAudioTrackCreateStatic(
    mrsPeerConnectionInteropHandle parent,
    const mrsRemoteAudioTrackConfig& config) noexcept {
  auto parent_handle = (Handle*)parent;
  EXPECT_NE(nullptr, parent_handle);
  return parent_handle->interop_->RemoteAudioTrackCreate(parent, config);
}

mrsRemoteVideoTrackInteropHandle MRS_CALL
SimpleInterop::RemoteVideoTrackCreateStatic(
    mrsPeerConnectionInteropHandle parent,
    const mrsRemoteVideoTrackConfig& config) noexcept {
  auto parent_handle = (Handle*)parent;
  EXPECT_NE(nullptr, parent_handle);
  return parent_handle->interop_->RemoteVideoTrackCreate(parent, config);
}

mrsDataChannelInteropHandle MRS_CALL SimpleInterop::DataChannelCreateStatic(
    mrsPeerConnectionInteropHandle parent,
    const mrsDataChannelConfig& config,
    mrsDataChannelCallbacks* callbacks) noexcept {
  auto parent_handle = (Handle*)parent;
  EXPECT_NE(nullptr, parent_handle);
  EXPECT_NE(nullptr, callbacks);
  return parent_handle->interop_->DataChannelCreate(parent, config, callbacks);
}
