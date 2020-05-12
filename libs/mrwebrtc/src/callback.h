// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#pragma once

#include "export.h"

namespace Microsoft {
namespace MixedReality {
namespace WebRTC {

/// Wrapper for a static callback with user data.
/// Usage:
///   void* user_data = [...]
///   Callback<int> cb = {func_ptr, user_data};
///   cb(42); // -> func_ptr(user_data, 42)
///   Callback<float> cb2;
///   cb2(3.4f); // -> safe, does nothing
template <typename... Args>
struct Callback {
  /// Type of the raw callback function.
  /// The first parameter is the opaque user data pointer.
  using callback_type = void(MRS_CALL*)(void*, Args...);

  /// Pointer to the raw function to invoke.
  callback_type callback_{};

  /// User-provided opaque pointer passed as first argument to the raw function.
  void* user_data_{};

  /// Check if the callback has a valid function pointer.
  constexpr explicit operator bool() const noexcept {
    return (callback_ != nullptr);
  }

  /// Invoke the callback with the given arguments |args|.
  constexpr void operator()(Args... args) const noexcept {
    if (callback_ != nullptr) {
      (*callback_)(user_data_, std::forward<Args>(args)...);
    }
  }
};

/// Same as |Callback|, with a return value.
template <typename Ret, typename... Args>
struct RetCallback {
  /// Type of the return value.
  using return_type = Ret;

  /// Type of the raw callback function.
  /// The first parameter is the opaque user data pointer.
  using callback_type = return_type(MRS_CALL*)(void*, Args...);

  /// Pointer to the raw function to invoke.
  callback_type callback_{};

  /// User-provided opaque pointer passed as first argument to the raw function.
  void* user_data_{};

  /// Check if the callback has a valid function pointer.
  constexpr explicit operator bool() const noexcept {
    return (callback_ != nullptr);
  }

  /// Invoke the callback with the given arguments |args|.
  constexpr return_type operator()(Args... args) const noexcept {
    if (callback_ != nullptr) {
      return (*callback_)(user_data_, std::forward<Args>(args)...);
    }
    return return_type{};
  }
};

}  // namespace WebRTC
}  // namespace MixedReality
}  // namespace Microsoft
