// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include "interop/global_factory.h"
#include "media_track.h"
#include "peer_connection.h"

namespace Microsoft {
namespace MixedReality {
namespace WebRTC {

MediaTrack::MediaTrack(RefPtr<GlobalFactory> global_factory,
                       ObjectType object_type) noexcept
    : TrackedObject(std::move(global_factory), object_type) {}

MediaTrack::MediaTrack(RefPtr<GlobalFactory> global_factory,
                       ObjectType object_type,
                       PeerConnection& owner) noexcept
    : TrackedObject(std::move(global_factory), object_type), owner_(&owner) {
  RTC_CHECK(owner_);
}

MediaTrack::~MediaTrack() {
  RTC_CHECK(!owner_);
}

}  // namespace WebRTC
}  // namespace MixedReality
}  // namespace Microsoft
