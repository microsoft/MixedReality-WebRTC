// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include "audio_frame_observer.h"
#include "audio_track_read_buffer.h"
#include "callback.h"
#include "interop_api.h"
#include "media_track.h"
#include "refptr.h"
#include "toggle_audio_mixer.h"
#include "tracked_object.h"

namespace rtc {
template <typename T>
class scoped_refptr;
}

namespace webrtc {
class RtpReceiverInterface;
class AudioTrackInterface;
}  // namespace webrtc

namespace Microsoft {
namespace MixedReality {
namespace WebRTC {

class PeerConnection;
class Transceiver;

/// A remote audio track is a media track for a peer connection backed by a
/// remote audio stream received from the remote peer.
///
/// The remote nature of the track implies that the remote peer has control on
/// it, including enabling or disabling the track, and removing it from the peer
/// connection. The local peer only has limited control over the track.
class RemoteAudioTrack : public MediaTrack, public AudioFrameObserver {
 public:
  RemoteAudioTrack(
      RefPtr<GlobalFactory> global_factory,
      PeerConnection& owner,
      Transceiver* transceiver,
      rtc::scoped_refptr<webrtc::AudioTrackInterface> track,
      rtc::scoped_refptr<webrtc::RtpReceiverInterface> receiver) noexcept;
  ~RemoteAudioTrack() override;

  /// Enable or disable the audio track. An enabled track streams its content
  /// from its source to the remote peer. A disabled audio track only sends
  /// empty audio data (silence).
  void SetEnabled(bool enabled) const noexcept { track_->set_enabled(enabled); }

  /// Check if the track is enabled.
  /// See |SetEnabled(bool)|.
  MRS_NODISCARD bool IsEnabled() const noexcept { return track_->enabled(); }

  /// See |mrsRemoteAudioOutputToDevice|.
  void OutputToDevice(bool output) noexcept;

  /// See |mrsRemoteAudioTrackIsOutputToDevice|.
  MRS_NODISCARD bool IsOutputToDevice() const noexcept {
    return output_to_device_;
  }

  /// See |mrsAudioTrackReadBufferCreate|.
  std::unique_ptr<AudioTrackReadBuffer> CreateReadBuffer() const noexcept;

  //
  // Advanced use
  //

  /// Get a handle to the remote audio track. This handle is valid until the
  /// remote track is removed from the peer connection and destroyed, which is
  /// signaled by the |TrackRemoved| event on the peer connection.
  MRS_NODISCARD constexpr mrsRemoteAudioTrackHandle GetHandle() const noexcept {
    return (mrsRemoteAudioTrackHandle)this;
  }

  MRS_NODISCARD webrtc::AudioTrackInterface* impl() const;
  MRS_NODISCARD webrtc::RtpReceiverInterface* receiver() const;

  MRS_NODISCARD constexpr Transceiver* GetTransceiver() const {
    return transceiver_;
  }

  MRS_NODISCARD webrtc::MediaStreamTrackInterface* GetMediaImpl()
      const override {
    return impl();
  }

  // Automatically called - do not use.
  void OnTrackRemoved(PeerConnection& owner);

  /// Automatically called - do not use.
  void InitSsrc(int ssrc);

 private:
  /// Underlying core implementation.
  rtc::scoped_refptr<webrtc::AudioTrackInterface> track_;

  /// RTP sender this track is associated with.
  rtc::scoped_refptr<webrtc::RtpReceiverInterface> receiver_;

  /// Weak back-pointer to the Transceiver this track is associated with. This
  /// avoids a circular reference with the transceiver itself.
  /// Note that unlike local tracks, this is never NULL since the remote track
  /// gets destroyed when detached from the transceiver.
  Transceiver* transceiver_{nullptr};

  /// SSRC id of the corresponding RtpReceiver.
  absl::optional<int> ssrc_;

  /// Indicates whether or not this track is output automatically to the
  /// system audio device.
  bool output_to_device_{true};
};

}  // namespace WebRTC
}  // namespace MixedReality
}  // namespace Microsoft
