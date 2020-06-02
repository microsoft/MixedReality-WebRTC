// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// This is a precompiled header, it must be on its own, followed by a blank
// line, to prevent clang-format from reordering it with other headers.
#include "pch.h"

#include "interop_api.h"
#include "ref_counted_object_interop.h"
#include "tracked_object.h"
#include "utils.h"

using namespace Microsoft::MixedReality::WebRTC;

void MRS_CALL
mrsRefCountedObjectAddRef(mrsRefCountedObjectHandle handle) noexcept {
  if (auto obj = static_cast<TrackedObject*>(handle)) {
    obj->AddRef();
  } else {
    RTC_LOG(LS_WARNING) << "Trying to add reference to NULL "
                        << ObjectTypeToString(obj->GetObjectType())
                        << " object.";
  }
}

void MRS_CALL
mrsRefCountedObjectRemoveRef(mrsRefCountedObjectHandle handle) noexcept {
  if (auto obj = static_cast<TrackedObject*>(handle)) {
    const ObjectType objType = obj->GetObjectType();
    const std::string name = obj->GetName();
    if (obj->RemoveRef() == 0) {
      RTC_LOG(LS_VERBOSE) << "Destroyed " << ObjectTypeToString(objType) << " "
                          << name.c_str() << "\" (0 ref).";
    }
  } else {
    RTC_LOG(LS_WARNING) << "Trying to remove reference from NULL object.";
  }
}
