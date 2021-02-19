// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include "interop_api.h"

extern "C" {
/// Data channel state as marshaled through the public API.
enum class mrsDataChannelState : int32_t {
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

/// Data channel message types. Messages can be sent and receieved with these
/// kinds.
enum class mrsMessageKind : int32_t {
    /// The message is a binary representation.
    kBinary = 1,

    /// The message is a text representation.
    kText = 2, 
};

/// Assign some opaque user data to the data channel. The implementation will
/// store the pointer in the data channel object and not touch it. It can be
/// retrieved with |mrsDataChannelGetUserData()| at any point during the data
/// channel lifetime. This is not multithread-safe.
MRS_API void MRS_CALL mrsDataChannelSetUserData(mrsDataChannelHandle handle,
                                                void* user_data) noexcept;

/// Get the opaque user data pointer previously assigned to the data channel
/// with |mrsDataChannelSetUserData()|. If no value was previously assigned,
/// return |nullptr|. This is not multithread-safe.
MRS_API void* MRS_CALL
mrsDataChannelGetUserData(mrsDataChannelHandle handle) noexcept;

/// Callback fired when a message |data| of byte size |size| is received on a
/// data channel.
///
/// The code responding to this callback should unwind the stack before
/// using any other MR-WebRTC APIs; re-entrancy is not supported.
using mrsDataChannelMessageCallback = void(
    MRS_CALL*)(void* user_data, const void* data, const uint64_t size);

/// Callback fired when a message |data| of byte size |size| with kind |messageKind|
/// is received on a data channel.
///
/// The code responding to this callback should unwind the stack before
/// using any other MR-WebRTC APIs; re-entrancy is not supported.
using mrsDataChannelMessageExCallback = void(
    MRS_CALL*)(void* user_data, const mrsMessageKind messageKind, const void* data, const uint64_t size);

/// Callback invoked when a data channel internal buffering changes.
/// The |previous| and |current| values are the old and new sizes in bytes of
/// the buffering buffer. The |limit| is the capacity of the buffer. Note that
/// when the buffer is full, any attempt to send data will result is an abrupt
/// closing of the data channel. So monitoring the buffering state is critical.
///
/// The code responding to this callback should unwind the stack before
/// using any other MR-WebRTC APIs; re-entrancy is not supported.
using mrsDataChannelBufferingCallback =
    void(MRS_CALL*)(void* user_data,
                    const uint64_t previous,
                    const uint64_t current,
                    const uint64_t limit);

/// Callback fired when the state of a data channel changed.
///
/// The code responding to this callback should unwind the stack before
/// using any other MR-WebRTC APIs; re-entrancy is not supported.
using mrsDataChannelStateCallback = void(MRS_CALL*)(void* user_data,
                                                    mrsDataChannelState state,
                                                    int32_t id);

/// Helper to register a group of data channel callbacks.
struct mrsDataChannelCallbacks {
  mrsDataChannelMessageCallback message_callback{};
  void* message_user_data{};
  mrsDataChannelMessageExCallback message_ex_callback{};
  void* message_ex_user_data{};
  mrsDataChannelBufferingCallback buffering_callback{};
  void* buffering_user_data{};
  mrsDataChannelStateCallback state_callback{};
  void* state_user_data{};
};

/// Register callbacks for managing a data channel.
///
/// Should be called in a |mrsPeerConnectionDataChannelAddedCallback| to start
/// listening for state changes/messages. Registered callbacks will be called
/// after the end of the DataChannelAdded callback. Note that if this is not
/// called during the DataChannelAdded callback some state changes/messages
/// might be lost.
///
/// Callbacks can be replaced later as long as the channel is alive.
MRS_API void MRS_CALL mrsDataChannelRegisterCallbacks(
    mrsDataChannelHandle handle,
    const mrsDataChannelCallbacks* callbacks) noexcept;

/// Send through the given data channel a raw message |data| of byte length
/// |size|. The message may be buffered internally, and the caller should
/// monitor the buffering event to avoid overflowing the internal buffer.
///
/// This returns an error if the data channel is not open. The caller should
/// monitor the state change event to know when it is safe to send a message.
MRS_API mrsResult MRS_CALL
mrsDataChannelSendMessage(mrsDataChannelHandle data_channel_handle,
                          const void* data,
                          uint64_t size) noexcept;

/// Send through the given data channel a message |data| of byte length
/// |size| with kind |messageKind|. The message may be buffered internally,
/// and the caller should monitor the buffering event to avoid overflowing
/// the internal buffer.
///
/// This returns an error if the data channel is not open. The caller should
/// monitor the state change event to know when it is safe to send a message.
MRS_API mrsResult MRS_CALL
mrsDataChannelSendMessageEx(mrsDataChannelHandle data_channel_handle,
                          mrsMessageKind messageKind,
                          const void* data,
                          uint64_t size) noexcept;

}  // extern "C"
