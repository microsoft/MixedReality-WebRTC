// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#include "pch.h"

#include "data_channel_observer.h"

namespace {
using RtcDataState = webrtc::DataChannelInterface::DataState;
using ApiDataState = Microsoft::MixedReality::WebRTC::DataChannelState;
inline ApiDataState apiStateFromRtcState(RtcDataState rtcState) {
  // API values have been chosen to match the current WebRTC values. If the
  // later change, this helper must be updated, as API values cannot change.
  static_assert((int)RtcDataState::kOpen == (int)ApiDataState::kOpen, "");
  static_assert(
      (int)RtcDataState::kConnecting == (int)ApiDataState::kConnecting, "");
  static_assert((int)RtcDataState::kClosing == (int)ApiDataState::kClosing, "");
  static_assert((int)RtcDataState::kClosed == (int)ApiDataState::kClosed, "");
  return (ApiDataState)rtcState;
}
}  // namespace

namespace Microsoft::MixedReality::WebRTC {

DataChannelObserver::DataChannelObserver(
    rtc::scoped_refptr<webrtc::DataChannelInterface> data_channel) noexcept
    : data_channel_(std::move(data_channel)) {}

void DataChannelObserver::OnStateChange() noexcept {
  auto lock = std::lock_guard{mutex_};
  if (!state_callback_)
    return;
  auto apiState = apiStateFromRtcState(data_channel_->state());
  state_callback_((int)apiState, data_channel_->id());
}

void DataChannelObserver::OnMessage(const webrtc::DataBuffer& buffer) noexcept {
  auto lock = std::lock_guard{mutex_};
  if (!message_callback_)
    return;
  message_callback_(buffer.data.data(), buffer.data.size());
}

void DataChannelObserver::OnBufferedAmountChange(
    uint64_t previous_amount) noexcept {
  auto lock = std::lock_guard{mutex_};
  if (!buffering_callback_)
    return;
  uint64_t current_amount = data_channel_->buffered_amount();
  constexpr uint64_t max_capacity = 0x1000000; // 16MB, see DataChannelInterface
  buffering_callback_(previous_amount, current_amount, max_capacity);
}

}  // namespace Microsoft::MixedReality::WebRTC
