// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#pragma once

#include <mutex>

#include "api/datachannelinterface.h"

#include "callback.h"
#include "data_channel.h"

namespace Microsoft::MixedReality::WebRTC {

class DataChannel : public webrtc::DataChannelObserver {
 public:
  /// Data channel state as marshaled through the public API.
  enum class State : int {
    kConnecting = 0,
    kOpen = 1,
    kClosing = 2,
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

  DataChannel(
      rtc::scoped_refptr<webrtc::DataChannelInterface> data_channel) noexcept;

  ~DataChannel() override;

  int id() const { return data_channel_->id(); }
  std::string label() const { return data_channel_->label(); }

  void SetMessageCallback(MessageCallback callback) noexcept;
  void SetBufferingCallback(BufferingCallback callback) noexcept;
  void SetStateCallback(StateCallback callback) noexcept;

  /// Get the maximum buffering size, in bytes, before |Send()| stops accepting
  /// data.
  size_t GetMaxBufferingSize() const noexcept;

  /// Send a blob of data through the data channel.
  bool Send(const void* data, size_t size) noexcept;

  //
  // Advanced use
  //

  webrtc::DataChannelInterface* impl() const { return data_channel_.get(); }

 protected:
  // DataChannelObserver interface

  // The data channel state have changed.
  void OnStateChange() noexcept override;
  //  A data buffer was successfully received.
  void OnMessage(const webrtc::DataBuffer& buffer) noexcept override;
  // The data channel's buffered_amount has changed.
  void OnBufferedAmountChange(uint64_t previous_amount) noexcept override;

 private:
  rtc::scoped_refptr<webrtc::DataChannelInterface> data_channel_;
  MessageCallback message_callback_ RTC_GUARDED_BY(mutex_);
  BufferingCallback buffering_callback_ RTC_GUARDED_BY(mutex_);
  StateCallback state_callback_ RTC_GUARDED_BY(mutex_);
  std::mutex mutex_;
};

}  // namespace Microsoft::MixedReality::WebRTC
