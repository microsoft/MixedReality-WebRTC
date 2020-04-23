// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include "interop/global_factory.h"
#include "tracked_object.h"

namespace Microsoft::MixedReality::WebRTC {

TrackedObject::TrackedObject(RefPtr<GlobalFactory> global_factory,
                             ObjectType object_type)
    : global_factory_(std::move(global_factory)), object_type_(object_type) {
  global_factory_->AddObject(this);
}

TrackedObject::~TrackedObject() noexcept {
  global_factory_->RemoveObject(this);
}

}  // namespace Microsoft::MixedReality::WebRTC
