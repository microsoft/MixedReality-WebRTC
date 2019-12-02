// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#include "pch.h"

#include "data_channel.h"
#include "peer_connection.h"

namespace {

using RtcDataState = webrtc::DataChannelInterface::DataState;
using ApiDataState = Microsoft::MixedReality::WebRTC::DataChannel::State;

inline ApiDataState apiStateFromRtcState(RtcDataState rtcState) {
  // API values have been chosen to match the current WebRTC values. If the
  // later change, this helper must be updated, as API values cannot change.
  static_assert((int)RtcDataState::kOpen == (int)ApiDataState::kOpen);
  static_assert((int)RtcDataState::kConnecting ==
                (int)ApiDataState::kConnecting);
  static_assert((int)RtcDataState::kClosing == (int)ApiDataState::kClosing);
  static_assert((int)RtcDataState::kClosed == (int)ApiDataState::kClosed);
  return (ApiDataState)rtcState;
}

}  // namespace

namespace Microsoft::MixedReality::WebRTC {

DataChannel::DataChannel(
    PeerConnection* owner,
    rtc::scoped_refptr<webrtc::DataChannelInterface> data_channel,
    mrsDataChannelInteropHandle interop_handle) noexcept
    : owner_(owner),
      data_channel_(std::move(data_channel)),
      interop_handle_(interop_handle) {
  RTC_CHECK(owner_);
  data_channel_->RegisterObserver(this);
}

DataChannel::~DataChannel() {
  data_channel_->UnregisterObserver();
  if (owner_) {
    owner_->RemoveDataChannel(*this);
  }
  RTC_CHECK(!owner_);
}

str DataChannel::label() const {
  return str{data_channel_->label()};
}

void DataChannel::SetMessageCallback(MessageCallback callback) noexcept {
  auto lock = std::scoped_lock{mutex_};
  message_callback_ = callback;
}

void DataChannel::SetBufferingCallback(BufferingCallback callback) noexcept {
  auto lock = std::scoped_lock{mutex_};
  buffering_callback_ = callback;
}

void DataChannel::SetStateCallback(StateCallback callback) noexcept {
  auto lock = std::scoped_lock{mutex_};
  state_callback_ = callback;
}

size_t DataChannel::GetMaxBufferingSize() const noexcept {
  // See BufferingCallback; current WebRTC implementation has a limit of 16MB
  // for the internal data track buffer capacity.
  static constexpr size_t kMaxBufferingSize = 0x1000000uLL;  // 16 MB
  return kMaxBufferingSize;
}

bool DataChannel::Send(const void* data, size_t size) noexcept {
  if (data_channel_->buffered_amount() + size > GetMaxBufferingSize()) {
    return false;
  }
  rtc::CopyOnWriteBuffer bufferStorage((const char*)data, size);
  webrtc::DataBuffer buffer(bufferStorage, /* binary = */ true);
  return data_channel_->Send(buffer);
}

void DataChannel::OnStateChange() noexcept {
  const webrtc::DataChannelInterface::DataState state = data_channel_->state();
  switch (state) {
    case webrtc::DataChannelInterface::DataState::kOpen:
      // Negotiated (out-of-band) data channels never generate an
      // OnDataChannel() message, so simulate it for the DataChannelAdded event
      // to be consistent.
      if (data_channel_->negotiated()) {
        owner_->OnDataChannelAdded(*this);
      }
      break;
  }

  // Invoke the StateChanged event
  {
    auto lock = std::scoped_lock{mutex_};
    if (state_callback_) {
      auto apiState = apiStateFromRtcState(state);
      state_callback_((int)apiState, data_channel_->id());
    }
  }
}

void DataChannel::OnMessage(const webrtc::DataBuffer& buffer) noexcept {
  auto lock = std::scoped_lock{mutex_};
  if (message_callback_) {
    message_callback_(buffer.data.data(), buffer.data.size());
  }
}

void DataChannel::OnBufferedAmountChange(uint64_t previous_amount) noexcept {
  auto lock = std::scoped_lock{mutex_};
  if (buffering_callback_) {
    uint64_t current_amount = data_channel_->buffered_amount();
    constexpr uint64_t max_capacity =
        0x1000000;  // 16MB, see DataChannelInterface
    buffering_callback_(previous_amount, current_amount, max_capacity);
  }
}

}  // namespace Microsoft::MixedReality::WebRTC
