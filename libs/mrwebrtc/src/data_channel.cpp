// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#include "pch.h"

#include "data_channel.h"
#include "peer_connection.h"

namespace {

using RtcDataState = webrtc::DataChannelInterface::DataState;
using ApiDataState = mrsDataChannelState;

inline ApiDataState apiStateFromRtcState(RtcDataState rtcState) {
  // API values have been chosen to match the current WebRTC values. If the
  // later change, this helper must be updated, as API values cannot change.
  static_assert((int)RtcDataState::kOpen == (int)ApiDataState::kOpen, "");
  static_assert((int)RtcDataState::kConnecting == (int)ApiDataState::kConnecting, "");
  static_assert((int)RtcDataState::kClosing == (int)ApiDataState::kClosing, "");
  static_assert((int)RtcDataState::kClosed == (int)ApiDataState::kClosed, "");
  return (ApiDataState)rtcState;
}

}  // namespace

namespace Microsoft {
namespace MixedReality {
namespace WebRTC {

DataChannel::DataChannel(
    PeerConnection* owner,
    rtc::scoped_refptr<webrtc::DataChannelInterface> data_channel) noexcept
    : owner_(owner), data_channel_(std::move(data_channel)) {
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

std::string DataChannel::label() const {
  return data_channel_->label();
}

void DataChannel::SetMessageCallback(MessageCallback callback) noexcept {
  std::lock_guard<std::mutex> lock(mutex_);
  message_callback_ = callback;
}

void DataChannel::SetMessageExCallback(MessageExCallback callback) noexcept {
  std::lock_guard<std::mutex> lock(mutex_);
  message_ex_callback_ = callback;
}

void DataChannel::SetBufferingCallback(BufferingCallback callback) noexcept {
  std::lock_guard<std::mutex> lock(mutex_);
  buffering_callback_ = callback;
}

void DataChannel::SetStateCallback(StateCallback callback) noexcept {
  std::lock_guard<std::mutex> lock(mutex_);
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

bool DataChannel::SendEx(mrsMessageKind messageKind, const void* data, size_t size) noexcept {
  if (data_channel_->buffered_amount() + size > GetMaxBufferingSize()) {
    return false;
  }

  rtc::CopyOnWriteBuffer bufferStorage((const char*)data, size);
  webrtc::DataBuffer buffer(bufferStorage, messageKind == mrsMessageKind::kBinary);
  return data_channel_->Send(buffer);
}

void DataChannel::InvokeOnStateChange() const noexcept {
  std::lock_guard<std::mutex> lock(mutex_);
  if (state_callback_) {
    const webrtc::DataChannelInterface::DataState state =
        data_channel_->state();
    auto apiState = apiStateFromRtcState(state);
    state_callback_(apiState, data_channel_->id());
  }
}

void DataChannel::OnStateChange() noexcept {
  InvokeOnStateChange();
}

void DataChannel::OnMessage(const webrtc::DataBuffer& buffer) noexcept {
  std::lock_guard<std::mutex> lock(mutex_);
  if (message_callback_) {
    message_callback_(buffer.data.data(), buffer.data.size());
  }
  if (message_ex_callback_) {
    mrsMessageKind kind = buffer.binary ? mrsMessageKind::kBinary : mrsMessageKind::kText;
    message_ex_callback_(kind, buffer.data.data(), buffer.data.size());
  }
}

void DataChannel::OnBufferedAmountChange(uint64_t previous_amount) noexcept {
  std::lock_guard<std::mutex> lock(mutex_);
  if (buffering_callback_) {
    uint64_t current_amount = data_channel_->buffered_amount();
    constexpr uint64_t max_capacity =
        0x1000000;  // 16MB, see DataChannelInterface
    buffering_callback_(previous_amount, current_amount, max_capacity);
  }
}

}  // namespace WebRTC
}  // namespace MixedReality
}  // namespace Microsoft
