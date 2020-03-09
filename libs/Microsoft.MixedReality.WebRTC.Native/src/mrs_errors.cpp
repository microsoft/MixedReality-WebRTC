// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include "interop_api.h"
#include "mrs_errors.h"

namespace Microsoft::MixedReality::WebRTC {

Error::Error(Error&& other) = default;
Error& Error::operator=(Error&& other) = default;

Error Error::OK() {
  return Error();
}

const char* Error::message() const {
  return message_.c_str();
}

void Error::set_message(std::string message) {
  message_ = std::move(message);
}

std::string_view ToString(Result code) {
  switch (code) {
    case Result::kSuccess:
      return "Success";
    case Result::kUnknownError:
    default:
      return "Unknown error";
    case Result::kInvalidParameter:
      return "Invalid parameter";
    case Result::kInvalidOperation:
      return "Invalid operation";
    case Result::kWrongThread:
      return "Wrong thread";
    case Result::kNotFound:
      return "Object not found";
    case Result::kInvalidNativeHandle:
      return "Invalid native handle";
    case Result::kNotInitialized:
      return "Object not initialized";
    case Result::kSctpNotNegotiated:
      return "SCTP not negotiated";
    case Result::kInvalidDataChannelId:
      return "Invalid DataChannel ID";
  }
}

}  // namespace Microsoft::MixedReality::WebRTC
