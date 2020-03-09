// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include <string>

#include "ref_counted_base.h"
#include "refptr.h"

namespace Microsoft::MixedReality::WebRTC {

class GlobalFactory;

/// Enumeration of all object types that the global factory keeps track of for
/// the purpose of keeping itself alive. Each value correspond to a type of
/// wrapper object. Wrapper objects must call |GlobalFactory::AddObject()| and
/// |GlobalFactory::RemoveObject()| to register themselves with the global
/// factory while alive.
enum class ObjectType : int {
  kPeerConnection,
  kLocalVideoTrack,
  kExternalVideoTrackSource,
};

/// Object tracked for interop, exposing helper methods for debugging purpose.
class TrackedObject : public RefCountedBase {
 public:
  TrackedObject(RefPtr<GlobalFactory> global_factory, ObjectType object_type);
  ~TrackedObject() noexcept override;

  constexpr ObjectType GetObjectType() const noexcept { return object_type_; }

  /// Retrieve the name of the object. The exact meaning depends on the actual
  /// object, and may be user-set or reused from another field of the object,
  /// but will generally allow the user to identify the object instance during
  /// debugging. There is no other meaning to this.
  /// Note that this may be called while the library is not initialized,
  /// including during static deinitializing of the process, therefore the
  /// implementation must not rely on WebRTC objects. Generally the
  /// implementation should locally cache a string value to comply with this
  /// limitation.
  virtual std::string GetName() const = 0;

 protected:
  RefPtr<GlobalFactory> global_factory_;
  const ObjectType object_type_;
};

}  // namespace Microsoft::MixedReality::WebRTC
