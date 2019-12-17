// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include <string>

#include "export.h"

namespace Microsoft::MixedReality::WebRTC {

/// Result code from an operation, typically used through the interop layer
/// instead of a full-featured Error object.
///
/// Somewhat similar to webrtc::RTCErrorType to avoid pulling it as a dependency in the
/// public API. This also has extra values not found in webrtc::RTCErrorType.
enum class Result : std::uint32_t {
  /// The operation was successful.
  kSuccess = 0,

  //
  // Generic errors
  //

  /// Unknown internal error.
  /// This is generally the fallback value when no other error code applies.
  kUnknownError = 0x80000000,

  /// A parameter passed to the API function was invalid.
  kInvalidParameter = 0x80000001,

  /// The operation cannot be performed in the current state.
  kInvalidOperation = 0x80000002,

  /// A call was made to an API function on the wrong thread.
  /// This is generally related to platforms with thread affinity like UWP.
  kWrongThread = 0x80000003,

  /// An object was not found.
  kNotFound = 0x80000004,

  /// An interop handle referencing a native object instance is invalid,
  /// although the API function was expecting a valid object.
  kInvalidNativeHandle = 0x80000005,

  /// The API object is not initialized, and cannot as a result perform the
  /// given operation.
  kNotInitialized = 0x80000006,

  /// The current operation is not supported by the implementation.
  kUnsupported = 0x80000007,

  /// An argument was passed to the API function with a value out of the expected range.
  kOutOfRange = 0x80000008,

  //
  // Peer connection (0x1xx)
  //

  /// The peer connection is closed, but the current operation requires an open
  /// peer connection.
  kPeerConnectionClosed = 0x80000101,

  //
  // Data (0x3xx)
  //

  /// The SCTP handshake for data channels encryption was not performed, because
  /// the connection was established before any data channel was added to it.
  /// Due to limitations in the implementation, without SCTP handshake data
  /// channels cannot be used, and therefor applications expecting to use data
  /// channels must open at least a single channel before establishing a peer
  /// connection (calling |CreateOffer()|).
  kSctpNotNegotiated = 0x80000301,

  /// The specified data channel ID is invalid.
  kInvalidDataChannelId = 0x80000302,
};

/// Full-featured error object, containing an error code and a message.
///
/// Adapted from webrtc::RTCError to avoid pulling it as a dependency in the
/// public API.
class Error {
 public:
  /// Create an empty "error" wrapping a non-error result.
  /// Preferred over the default constructor for readability.
  static MRS_API Error OK();

  /// Create an empty "error" wrapping a non-error result.
  Error() = default;

  /// Create an error object from a result code. This represents an actual error
  /// only if |result| is not kSuccess.
  explicit Error(Result result) : result_(result) {}

  /// Create an error object from a result code, with an additional
  /// informational message associated with the error. Generally it makes no
  /// sense to use this with the kSuccess result.
  Error(Result result, std::string message)
      : result_(result), message_(std::move(message)) {}

  Error(const Error& other) = delete;
  Error& operator=(const Error& other) = delete;

  MRS_API Error(Error&& other);
  MRS_API Error& operator=(Error&& other);

  Result result() const { return result_; }
  void set_result(Result result) { result_ = result; }

  /// Human-readable informational message, for display only.
  /// The message is susceptible to change in future revisions.
  MRS_API const char* message() const;

  /// Explicitly set the informational message associated with the error, often
  /// to provide more context than the default one.
  MRS_API void set_message(std::string message);

  /// Return |true| if the Error instance does not currently represent an error.
  bool ok() const { return result_ == Result::kSuccess; }

 private:
  Result result_ = Result::kSuccess;
  std::string message_;
};

/// Container holding either an Error or a value of the given type.
/// Typically used as return value for methods creating new instances of
/// objects, to report an error if the object cannot be created.
///
/// Adapted from webrtc::RTCError to avoid pulling it as a dependency in the
/// public API.
template <typename T>
class ErrorOr {
  template <typename U>
  friend class ErrorOr;

 public:
  /// Marked as explicit to avoid errors like 'return {}' expecting to return an
  /// empty object instead of an error.
  explicit ErrorOr() : error_(Result::kUnknownError) {}

  ErrorOr(Error&& error) : error_(std::move(error)) { assert(!error.ok()); }

  /// Build a non-error instance from a valid value.
  ErrorOr(T&& value) : value_(std::move(value)) {}

  ErrorOr(const ErrorOr& other) = delete;
  ErrorOr& operator=(const ErrorOr& other) = delete;

  ErrorOr(ErrorOr&& other) = default;
  ErrorOr& operator=(ErrorOr&& other) = default;

  template <typename U>
  ErrorOr(ErrorOr<U> other)
      : error_(std::move(other.error_)), value_(std::move(other.value_)) {}

  template <typename U>
  ErrorOr& operator=(ErrorOr<U> other) {
    error_ = std::move(other.error_);
    value_ = std::move(other.value_);
    return *this;
  }

  /// Return a reference to the Error object held by this instance, even if
  /// |ok()| is true.
  const Error& error() const { return error_; }

  /// Move the error out of this object to forward it somewhere else (like the
  /// caller through a return value). After this call the current instance
  /// contains a default-constructed no-error instance.
  Error MoveError() { return std::move(error_); }

  /// Check whether this instance contains a valid value, and not an error.
  bool ok() const { return error_.ok(); }

  /// Assuming |ok()| is true, get a reference to the value held inside this
  /// instance. This will assert if |ok()| is false.
  const T& value() const {
    assert(ok());
    return value_;
  }

  /// Assuming |ok()| is true, get a reference to the value held inside this
  /// instance. This will assert if |ok()| is false.
  T& value() {
    assert(ok());
    return value_;
  }

  /// Assuming |ok()| is true, move the value out of this instance.
  T MoveValue() {
    assert(ok());
    return std::move(value_);
  }

 private:
  Error error_;
  T value_;
};

}  // namespace Microsoft::MixedReality::WebRTC
