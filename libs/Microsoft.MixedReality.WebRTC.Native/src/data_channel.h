// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#pragma once

#include <mutex>

#include "api/datachannelinterface.h"

#include "callback.h"
#include "data_channel.h"
#include "str.h"

// Internal
#include "interop_api.h"

namespace Microsoft::MixedReality::WebRTC {

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
  /// Data channel state as marshaled through the public API.
  enum class State : int {
    /// The data channel is being connected, but is not yet ready to send nor
    /// received any data.
    kConnecting = 0,

    /// The data channel is ready for read and write operations.
    kOpen = 1,

    /// The data channel is being closed, and cannot send any more data.
    kClosing = 2,

    /// The data channel is closed, and cannot be used again anymore.
    kClosed = 3
  };

  /// Callback fired on newly available data channel data.
  using MessageCallback = Callback<const void*, const uint64_t>;

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
  using StateCallback = Callback</*DataChannelState*/ int, int>;

  DataChannel(PeerConnection* owner,
              rtc::scoped_refptr<webrtc::DataChannelInterface> data_channel,
              mrsDataChannelInteropHandle interop_handle = nullptr) noexcept;

  /// Remove the data channel from its parent PeerConnection and close it.
  ~DataChannel() override;

  /// Get the unique channel identifier.
  [[nodiscard]] int id() const { return data_channel_->id(); }

  /// Get the friendly channel name.
  [[nodiscard]] str label() const;

  void SetMessageCallback(MessageCallback callback) noexcept;
  void SetBufferingCallback(BufferingCallback callback) noexcept;
  void SetStateCallback(StateCallback callback) noexcept;

  /// Get the maximum buffering size, in bytes, before |Send()| stops accepting
  /// data.
  [[nodiscard]] size_t GetMaxBufferingSize() const noexcept;

  /// Send a blob of data through the data channel.
  bool Send(const void* data, size_t size) noexcept;

  //
  // Advanced use
  //

  [[nodiscard]] webrtc::DataChannelInterface* impl() const {
    return data_channel_.get();
  }

  mrsDataChannelInteropHandle GetInteropHandle() const noexcept {
    return interop_handle_;
  }

  /// This is invoked automatically by PeerConnection::RemoveDataChannel().
  /// Do not call it manually.
  void OnRemovedFromPeerConnection() noexcept { owner_ = nullptr; }

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
  BufferingCallback buffering_callback_ RTC_GUARDED_BY(mutex_);
  StateCallback state_callback_ RTC_GUARDED_BY(mutex_);
  std::mutex mutex_;

  /// Optional interop handle, if associated with an interop wrapper.
  mrsDataChannelInteropHandle interop_handle_{};
};

}  // namespace Microsoft::MixedReality::WebRTC
