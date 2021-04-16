// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// This is a precompiled header, it must be on its own, followed by a blank
// line, to prevent clang-format from reordering it with other headers.
#include "pch.h"

#include "callback.h"
#include "data_channel.h"
#include "data_channel_interop.h"

using namespace Microsoft::MixedReality::WebRTC;

MRS_API void MRS_CALL
mrsDataChannelSetUserData(mrsDataChannelHandle handle,
                               void* user_data) noexcept {
  if (auto data_channel = static_cast<DataChannel*>(handle)) {
    data_channel->SetUserData(user_data);
  }
}

MRS_API void* MRS_CALL
mrsDataChannelGetUserData(mrsDataChannelHandle handle) noexcept {
  if (auto data_channel = static_cast<DataChannel*>(handle)) {
    return data_channel->GetUserData();
  }
  return nullptr;
}

void MRS_CALL mrsDataChannelRegisterCallbacks(
    mrsDataChannelHandle handle,
    const mrsDataChannelCallbacks* callbacks) noexcept {
  if (auto data_channel = static_cast<DataChannel*>(handle)) {
    data_channel->SetMessageCallback(
        {callbacks->message_callback, callbacks->message_user_data});
    data_channel->SetMessageExCallback(
        {callbacks->message_ex_callback, callbacks->message_ex_user_data});
    data_channel->SetBufferingCallback(
        {callbacks->buffering_callback, callbacks->buffering_user_data});
    data_channel->SetStateCallback(
        {callbacks->state_callback, callbacks->state_user_data});
  }
}

mrsResult MRS_CALL
mrsDataChannelSendMessage(mrsDataChannelHandle dataChannelHandle,
                          const void* data,
                          uint64_t size) noexcept {
  auto data_channel = static_cast<DataChannel*>(dataChannelHandle);
  if (!data_channel) {
    return Result::kInvalidNativeHandle;
  }
  return (data_channel->Send(data, (size_t)size) ? Result::kSuccess
                                                 : Result::kUnknownError);
}

mrsResult MRS_CALL
mrsDataChannelSendMessageEx(mrsDataChannelHandle dataChannelHandle,
                          mrsMessageKind messageKind,
                          const void* data,
                          uint64_t size) noexcept {
  auto data_channel = static_cast<DataChannel*>(dataChannelHandle);
  if (!data_channel) {
    return Result::kInvalidNativeHandle;
  }
  return (data_channel->SendEx(messageKind, data, (size_t)size) ? Result::kSuccess
                                                                : Result::kUnknownError);
}
