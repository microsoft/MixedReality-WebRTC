// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include "api/mediastreaminterface.h"
#include "api/peerconnectioninterface.h"
#include "api/rtpsenderinterface.h"

#include "callback.h"
#include "interop/interop_api.h"
#include "str.h"
#include "video_frame_observer.h"

namespace Microsoft::MixedReality::WebRTC {

class PeerConnection;

class LocalVideoTrack : public VideoFrameObserver,
                        public rtc::RefCountInterface {
 public:
  LocalVideoTrack(PeerConnection& owner,
                  rtc::scoped_refptr<webrtc::VideoTrackInterface> track,
                  rtc::scoped_refptr<webrtc::RtpSenderInterface> sender,
                  mrsLocalVideoTrackInteropHandle interop_handle) noexcept;
  MRS_API ~LocalVideoTrack() override;

  MRS_API [[nodiscard]] bool IsEnabled() const noexcept;
  MRS_API void SetEnabled(bool enabled) const noexcept;

  //
  // Advanced use
  //

  [[nodiscard]] webrtc::VideoTrackInterface* impl() const {
    return track_.get();
  }

  [[nodiscard]] webrtc::RtpSenderInterface* sender() const {
    return sender_.get();
  }

  [[nodiscard]] mrsLocalVideoTrackInteropHandle GetInteropHandle() const
      noexcept {
    return interop_handle_;
  }

  void RemoveFromPeerConnection(webrtc::PeerConnectionInterface& peer);

 private:
  /// PeerConnection object owning this track.
  PeerConnection* owner_{};

  /// Underlying core implementation.
  rtc::scoped_refptr<webrtc::VideoTrackInterface> track_;

  /// RTP sender this track is associated with.
  rtc::scoped_refptr<webrtc::RtpSenderInterface> sender_;

  /// Optional interop handle, if associated with an interop wrapper.
  mrsLocalVideoTrackInteropHandle interop_handle_{};
};

}  // namespace Microsoft::MixedReality::WebRTC
