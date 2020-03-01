// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include "callback.h"
#include "interop_api.h"
#include "tracked_object.h"

namespace rtc {
template <typename T>
class scoped_refptr;
}

namespace webrtc {
class RtpTransceiverInterface;
}

namespace Microsoft::MixedReality::WebRTC {

class PeerConnection;

/// Media kind for tracks and transceivers.
enum class MediaKind : uint32_t { kAudio, kVideo };

/// Base class for audio and video transceivers.
class Transceiver : public TrackedObject {
 public:
  using Direction = mrsTransceiverDirection;
  using OptDirection = mrsTransceiverOptDirection;

  /// Construct a Plan B transceiver abstraction which tries to mimic a
  /// transceiver for Plan B despite the fact that this semantic doesn't have
  /// any concept of transceiver.
  Transceiver(RefPtr<GlobalFactory> global_factory,
              MediaKind kind,
              PeerConnection& owner,
              Direction desired_direction) noexcept;

  /// Construct a Unified Plan transceiver wrapper referencing an actual WebRTC
  /// transceiver implementation object as defined in Unified Plan.
  Transceiver(RefPtr<GlobalFactory> global_factory,
              MediaKind kind,
              PeerConnection& owner,
              rtc::scoped_refptr<webrtc::RtpTransceiverInterface> transceiver,
              Direction desired_direction) noexcept;

  ~Transceiver() override;

  [[nodiscard]] std::string GetName() const override;

  /// Get the kind of transceiver. This is generally used for determining what
  /// type to static_cast<> a |Transceiver| pointer to. If this is |kAudio| then
  /// the object is an |AudioTransceiver| instance, and if this is |kVideo| then
  /// the object is a |VideoTransceiver| instance.
  [[nodiscard]] MediaKind GetMediaKind() const noexcept { return kind_; }

  /// Get the desired transceiver direction.
  [[nodiscard]] Direction GetDesiredDirection() const noexcept {
    return desired_direction_;
  }

  /// Get the current transceiver direction.
  [[nodiscard]] OptDirection GetDirection() const noexcept {
    return direction_;
  }

  [[nodiscard]] bool IsUnifiedPlan() const {
    RTC_DCHECK(!plan_b_ != !transceiver_);
    return (transceiver_ != nullptr);
  }

  [[nodiscard]] bool IsPlanB() const { return !IsUnifiedPlan(); }

  [[nodiscard]] bool HasSender(webrtc::RtpSenderInterface* sender) const;
  [[nodiscard]] bool HasReceiver(webrtc::RtpReceiverInterface* receiver) const;

  //
  // Interop callbacks
  //

  /// Callback invoked when the internal state of the transceiver has
  /// been updated.
  using StateUpdatedCallback =
      Callback<mrsTransceiverStateUpdatedReason, OptDirection, Direction>;

  void RegisterStateUpdatedCallback(StateUpdatedCallback&& callback) noexcept {
    auto lock = std::scoped_lock{cb_mutex_};
    state_updated_callback_ = std::move(callback);
  }

  //
  // Advanced
  //

  [[nodiscard]] rtc::scoped_refptr<webrtc::RtpTransceiverInterface> impl()
      const;

  /// Synchronize the RTP sender with the desired direction when using Plan B.
  /// |needed| indicate whether an RTP sender is needed or not. |peer| is passed
  /// as argument for convenience, as |owner_| cannot access it. |media_kind| is
  /// the Cricket value, so "audio" or "video".
  void SyncSenderPlanB(bool needed,
                       webrtc::PeerConnectionInterface* peer,
                       const char* media_kind,
                       const char* stream_id);

  /// Set the RTP receiver created for Plan B emulation when the desired
  /// direction allows receiving.
  void SetReceiverPlanB(
      rtc::scoped_refptr<webrtc::RtpReceiverInterface> receiver);

  /// Hot-swap the local sender track on this transceiver, without changing the
  /// transceiver direction. This emulates the RTP transceiver's SetTrack
  /// function. Because the RTP sender in Plan B only exists when the track is
  /// sending, and therefore acts as a marker for the sender direction, this
  /// does not create an RTP sender if none exists, but instead stores a
  /// reference to it for later. This will however assign the track if an RTP
  /// sender already exists at the time of the call.
  void SetTrackPlanB(webrtc::MediaStreamTrackInterface* new_track);

  /// Callback on local description updated, to check for any change in the
  /// transceiver direction and update its state.
  void OnSessionDescUpdated(bool remote, bool forced = false);

  /// Fire the StateUpdated event, invoking the |state_updated_callback_| if
  /// any is registered.
  void FireStateUpdatedEvent(mrsTransceiverStateUpdatedReason reason);

  [[nodiscard]] static webrtc::RtpTransceiverDirection ToRtp(
      Direction direction);
  [[nodiscard]] static Direction FromRtp(
      webrtc::RtpTransceiverDirection rtp_direction);
  [[nodiscard]] static OptDirection FromRtp(
      std::optional<webrtc::RtpTransceiverDirection> rtp_direction);
  [[nodiscard]] static Direction FromSendRecv(bool send, bool recv);
  [[nodiscard]] static OptDirection OptFromSendRecv(bool send, bool recv);

  [[nodiscard]] static std::vector<std::string> DecodeStreamIDs(
      const char* encoded_stream_ids);
  [[nodiscard]] static std::string EncodeStreamIDs(
      const std::vector<std::string>& stream_ids);

 protected:
  struct PlanBEmulation;

  /// Weak reference to the PeerConnection object owning this transceiver.
  PeerConnection* owner_{};

  /// Transceiver media kind.
  MediaKind kind_;

  /// Current transceiver direction, as last negotiated.
  /// This does not map 1:1 with the presence/absence of the local and remote
  /// tracks in |AudioTransceiver| and |VideoTransceiver|, which represent the
  /// state for the next negotiation, and will differ after changing tracks but
  /// before renegotiating them. This does map however with the internal concept
  /// of "preferred direction" |webrtc::RtpTransceiverInterface::direction()|.
  OptDirection direction_ = OptDirection::kNotSet;

  /// Next desired direction, as set by user via |SetDirection()|.
  Direction desired_direction_ = Direction::kInactive;

  /// If the owner PeerConnection uses Unified Plan, pointer to the actual
  /// transceiver implementation object. Otherwise NULL for Plan B.
  /// This is also used as a cache of which Plan is in use, to avoid querying
  /// the peer connection.
  rtc::scoped_refptr<webrtc::RtpTransceiverInterface> transceiver_;

  /// Emulation layer for transceiver-over-PlanB. Used when SDP semantic is set
  /// Plan B to emulate the behavior of a transceiver using the track-based API.
  /// This is NULL if using Unified Plan.
  /// This is also used as a cache of which Plan is in use, to avoid querying
  /// the peer connection.
  std::unique_ptr<PlanBEmulation> plan_b_;

  /// Interop callback invoked when the internal state of the transceiver has
  /// been updated.
  StateUpdatedCallback state_updated_callback_ RTC_GUARDED_BY(cb_mutex_);

  std::mutex cb_mutex_;
};

}  // namespace Microsoft::MixedReality::WebRTC
