// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#pragma once

#include "pch.h"

namespace Microsoft::MixedReality::WebRTC {

/// Wrapper for a static callback with user data.
template <typename... Args>
struct Callback {
  using callback_type = void (MRS_CALL *)(void*, Args...);
  callback_type callback_{nullptr};
  void* user_data_{nullptr};
  explicit operator bool() const noexcept { return (callback_ != nullptr); }
  void operator()(Args... args) const noexcept {
    if (callback_ != nullptr) {
      (*callback_)(user_data_, std::forward<Args>(args)...);
    }
  }
};

}  // namespace Microsoft::MixedReality::WebRTC
