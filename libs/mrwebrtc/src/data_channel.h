// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#pragma once

#include <mutex>

#include "api/datachannelinterface.h"

#include "callback.h"
#include "data_channel.h"
#include "data_channel_interop.h"
#include "interop_api.h"

namespace Microsoft {
namespace MixedReality {
namespace WebRTC {

class PeerConnection;

/// A data channel is a bidirectional pipe established between the local and
/// remote peer to carry random blobs of data.
/// The data channel API does not specify the content of the data; instead the
/// user can transmit any data in the form of a raw stream of bytes. All data
/// channels are managed and transported with DTLS-SCTP, and therefore are
/// encrypted. A data channel can be configured on creation to be either or both
/// of:
/// - reliable: data is guaranteed to be transmitted to the remote peer, by
/// re-sending lost packets as many times as needed.
/// - ordered: data is received by the remote peer in the same order as it is
/// sent by the local peer.
class DataChannel : public webrtc::DataChannelObserver {
 public:
  /// Callback fired on newly available data channel data.
  using MessageCallback = Callback<const void*, const uint64_t>;

  /// Callback fired on newly available data channel data.
  using MessageExCallback = Callback<const mrsMessageKind, const void*, const uint64_t>;

  /// Callback fired when data buffering changed.
  /// The first parameter indicates the old buffering amount in bytes, the
  /// second one the new value, and the last one indicates the limit in bytes
  /// (buffer capacity). This is important because if the send buffer is full
  /// then any attempt to send data will abruptly close the data channel. See
  /// comment in webrtc::DataChannelInterface::Send() for details. Current
  /// WebRTC implementation has a limit of 16MB for the buffer capacity.
  using BufferingCallback =
      Callback<const uint64_t, const uint64_t, const uint64_t>;

  /// Callback fired when the data channel state changed.
  using StateCallback = Callback<mrsDataChannelState, int>;

  DataChannel(
      PeerConnection* owner,
      rtc::scoped_refptr<webrtc::DataChannelInterface> data_channel) noexcept;

  /// Remove the data channel from its parent PeerConnection and close it.
  ~DataChannel() override;

  MRS_NODISCARD constexpr void* GetUserData() const noexcept {
    return user_data_;
  }

  constexpr void SetUserData(void* user_data) noexcept {
    user_data_ = user_data;
  }

  /// Get the unique channel identifier.
  MRS_NODISCARD int id() const { return data_channel_->id(); }

  MRS_NODISCARD mrsDataChannelConfigFlags flags() const noexcept {
    mrsDataChannelConfigFlags flags{mrsDataChannelConfigFlags::kNone};
    if (data_channel_->ordered()) {
      flags = flags | mrsDataChannelConfigFlags::kOrdered;
    }
    if (data_channel_->reliable()) {
      flags = flags | mrsDataChannelConfigFlags::kReliable;
    }
    return flags;
  }

  /// Get the friendly channel name.
  MRS_NODISCARD std::string label() const;

  void SetMessageCallback(MessageCallback callback) noexcept;
  void SetMessageExCallback(MessageExCallback callback) noexcept;
  void SetBufferingCallback(BufferingCallback callback) noexcept;
  void SetStateCallback(StateCallback callback) noexcept;

  /// Get the maximum buffering size, in bytes, before |Send()| stops accepting
  /// data.
  MRS_NODISCARD size_t GetMaxBufferingSize() const noexcept;

  /// Send a blob of data through the data channel.
  bool Send(const void* data, size_t size) noexcept;

  /// Send a message through the data channel with the specified message kind.
  bool SendEx(mrsMessageKind messageKind, const void* data, size_t size) noexcept;

  //
  // Advanced use
  //

  MRS_NODISCARD webrtc::DataChannelInterface* impl() const {
    return data_channel_.get();
  }

  /// This is invoked automatically by PeerConnection::RemoveDataChannel().
  /// Do not call it manually.
  void OnRemovedFromPeerConnection() noexcept { owner_ = nullptr; }

  /// Fire the event now.
  void InvokeOnStateChange() const noexcept;

 protected:
  // DataChannelObserver interface

  // The data channel state have changed.
  void OnStateChange() noexcept override;
  //  A data buffer was successfully received.
  void OnMessage(const webrtc::DataBuffer& buffer) noexcept override;
  // The data channel's buffered_amount has changed.
  void OnBufferedAmountChange(uint64_t previous_amount) noexcept override;

 private:
  /// PeerConnection object owning this data channel. This is only valid from
  /// creation until the data channel is removed from the peer connection with
  /// RemoveDataChannel(), at which point the data channel is removed from its
  /// parent's collection and |owner_| is set to nullptr.
  PeerConnection* owner_{};

  /// Underlying core implementation.
  rtc::scoped_refptr<webrtc::DataChannelInterface> data_channel_;

  MessageCallback message_callback_ RTC_GUARDED_BY(mutex_);
  MessageExCallback message_ex_callback_ RTC_GUARDED_BY(mutex_);
  BufferingCallback buffering_callback_ RTC_GUARDED_BY(mutex_);
  StateCallback state_callback_ RTC_GUARDED_BY(mutex_);
  mutable std::mutex mutex_;

  /// Opaque user data.
  void* user_data_{nullptr};
};

}  // namespace WebRTC
}  // namespace MixedReality
}  // namespace Microsoft
