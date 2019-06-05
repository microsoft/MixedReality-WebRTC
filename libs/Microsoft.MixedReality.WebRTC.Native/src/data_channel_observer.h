// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#pragma once

#include <mutex>

#include "api/datachannelinterface.h"

#include "callback.h"

namespace mrtk {
namespace net {
namespace webrtc_impl {

/// Data channel state as marshaled through the public API.
enum class DataChannelState : int {
  kConnecting = 0,
  kOpen = 1,
  kClosing = 2,
  kClosed = 3
};

/// Callback fired on newly available data channel data.
using DataChannelMessageCallback = Callback<const void*, const uint64_t>;

/// Callback fired when data buffering changed.
/// The first parameter indicates the old buffering amount in bytes, the second
/// one the new value, and the last one indicates the limit in bytes (buffer
/// capacity). This is important because if the send buffer is full then any
/// attempt to send data will abruptly close the data channel. See comment in
/// webrtc::DataChannelInterface::Send() for details. Current WebRTC
/// implementation has a limit of 16MB for the buffer capacity.
using DataChannelBufferingCallback =
    Callback<const uint64_t, const uint64_t, const uint64_t>;

/// Callback fired when the data channel state changed.
using DataChannelStateCallback = Callback</*DataChannelState*/ int, int>;

/// Data channel observer to get notified of newly available data.
class DataChannelObserver : public webrtc::DataChannelObserver {
 public:
  DataChannelObserver(
      rtc::scoped_refptr<webrtc::DataChannelInterface> data_channel) noexcept;
  webrtc::DataChannelInterface* data_channel() const {
    return data_channel_.get();
  }
  void SetMessageCallback(DataChannelMessageCallback callback) noexcept {
    auto lock = std::lock_guard{mutex_};
    message_callback_ = callback;
  }
  void SetBufferingCallback(
      DataChannelBufferingCallback callback) noexcept {
    auto lock = std::lock_guard{mutex_};
    buffering_callback_ = callback;
  }
  void SetStateCallback(DataChannelStateCallback callback) noexcept {
    auto lock = std::lock_guard{mutex_};
    state_callback_ = callback;
  }

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
  DataChannelMessageCallback message_callback_;
  DataChannelBufferingCallback buffering_callback_;
  DataChannelStateCallback state_callback_;
  std::mutex mutex_;
};

}  // namespace webrtc_impl
}  // namespace net
}  // namespace mrtk
