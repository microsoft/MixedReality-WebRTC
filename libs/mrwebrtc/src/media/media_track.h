// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include "callback.h"
#include "interop_api.h"
#include "str.h"
#include "tracked_object.h"
#include "video_frame_observer.h"

namespace rtc {
template <typename T>
class scoped_refptr;
}

namespace webrtc {
class RtpSenderInterface;
class VideoTrackInterface;
}  // namespace webrtc

namespace Microsoft::MixedReality::WebRTC {

class PeerConnection;

/// Base class for all audio and video tracks.
class MediaTrack : public TrackedObject {
 public:
  MediaTrack(RefPtr<GlobalFactory> global_factory,
             ObjectType object_type) noexcept;
  MediaTrack(RefPtr<GlobalFactory> global_factory,
             ObjectType object_type,
             PeerConnection& owner) noexcept;
  ~MediaTrack() override;

  /// Get the kind of track.
  [[nodiscard]] mrsTrackKind GetKind() const noexcept { return kind_; }

  [[nodiscard]] virtual webrtc::MediaStreamTrackInterface* GetMediaImpl()
      const = 0;

 protected:
  /// Weak reference to the PeerConnection object owning this track.
  PeerConnection* owner_{};

  /// Track kind.
  mrsTrackKind kind_ = mrsTrackKind::kUnknownTrack;
};

}  // namespace Microsoft::MixedReality::WebRTC
