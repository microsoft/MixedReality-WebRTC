// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include <string>

#include "ref_counted_base.h"

namespace Microsoft::MixedReality::WebRTC {

/// Object tracked for interop, exposing helper methods for debugging purpose.
class TrackedObject : public RefCountedBase {
 public:
  /// Retrieve the name of the object. The exact meaning depends on the actual
  /// object, and may be user-set or reused from another field of the object,
  /// but will generally allow the user to identify the object instance during
  /// debugging. There is no other meaning to this.
  virtual std::string GetName() const = 0;
};

}  // namespace Microsoft::MixedReality::WebRTC
