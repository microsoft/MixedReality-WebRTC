// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#pragma once

#include "pch.h"

namespace mrtk {
namespace net {
namespace webrtc_impl {

/// Wrapper for a static callback with user data.
template <typename... Args>
struct Callback {
  using callback_type = void (*)(void*, Args...);
  callback_type callback_{nullptr};
  void* user_data_{nullptr};
  inline operator bool() const noexcept { return (callback_ != nullptr); }
  inline void operator()(Args... args) const noexcept {
    if (callback_ != nullptr) {
      (*callback_)(user_data_, std::forward<Args>(args)...);
    }
  }
};

}  // namespace webrtc_impl
}  // namespace net
}  // namespace mrtk
