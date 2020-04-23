// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include <atomic>

namespace Microsoft::MixedReality::WebRTC {

/// Base class for ref-counted objects.
class RefCountedBase {
 public:
  RefCountedBase() noexcept = default;

  void AddRef() const noexcept {
    ref_count_.fetch_add(1, std::memory_order_relaxed);
  }

  void RemoveRef() const noexcept {
    if (ref_count_.fetch_sub(1, std::memory_order_acq_rel) == 1)
      delete this;
  }

  /// Get an approximate reference count at the time of the call. This value can
  /// be invalid as soon as the call return, and shall be used only for
  /// approximate informational message while debugging.
  std::uint32_t GetApproxRefCount() const noexcept {
    return ref_count_.load(std::memory_order_relaxed);
  }

 protected:
  virtual ~RefCountedBase() noexcept = default;

 private:
  RefCountedBase(const RefCountedBase&) = delete;
  RefCountedBase& operator=(const RefCountedBase&) = delete;

  mutable std::atomic_uint32_t ref_count_{0};
};

}  // namespace Microsoft::MixedReality::WebRTC
