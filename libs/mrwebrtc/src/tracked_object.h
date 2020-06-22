// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include <string>

#include "ref_counted_base.h"
#include "refptr.h"

namespace Microsoft {
namespace MixedReality {
namespace WebRTC {

class GlobalFactory;

/// Enumeration of all object types that the global factory keeps track of for
/// the purpose of keeping itself alive. Each value correspond to a type of
/// wrapper object. Wrapper objects must call |GlobalFactory::AddObject()| and
/// |GlobalFactory::RemoveObject()| to register themselves with the global
/// factory while alive.
enum class ObjectType : int {
  kPeerConnection,
  kLocalAudioTrack,
  kLocalVideoTrack,
  kRemoteAudioTrack,
  kRemoteVideoTrack,
  kDataChannel,
  kAudioTransceiver,
  kVideoTransceiver,
  kDeviceAudioTrackSource,
  kDeviceVideoTrackSource,
  kExternalVideoTrackSource,
  kAudioTrackReadBuffer,
};

/// Object tracked for interop, exposing helper methods for debugging purpose.
/// This is the base class for both mrsObject and mrsRefCountedObject, as
/// internally all objects are reference-counted for historical reasons, but as
/// exposed through the API only standalone objects created by the user are
/// reference counted.
class TrackedObject : public RefCountedBase {
 public:
  TrackedObject(RefPtr<GlobalFactory> global_factory, ObjectType object_type);
  ~TrackedObject() noexcept override;

  MRS_NODISCARD constexpr ObjectType GetObjectType() const noexcept {
    return object_type_;
  }

  void SetName(absl::string_view name) {
    name_.assign(name.data(), name.size());
  }

  MRS_NODISCARD std::string GetName() const { return name_; }

  MRS_NODISCARD constexpr void* GetUserData() const noexcept {
    return user_data_;
  }

  constexpr void SetUserData(void* user_data) noexcept {
    user_data_ = user_data;
  }

 protected:
  RefPtr<GlobalFactory> global_factory_;
  const ObjectType object_type_;
  void* user_data_{nullptr};
  std::string name_;
};

}  // namespace WebRTC
}  // namespace MixedReality
}  // namespace Microsoft
