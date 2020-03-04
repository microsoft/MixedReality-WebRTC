// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

namespace Microsoft::MixedReality::WebRTC {

enum class DontAddRef {};

/// Smart pointer for reference-counted objects.
/// The pointer type is typically of a class deriving from RefCountedBase,
/// although there is no restriction and other classes implementing some
/// AddRef() and RemovedRef() methods can be used with it too.
template <typename T>
class RefPtr {
 public:
  constexpr RefPtr() noexcept = default;

  RefPtr(T* ptr) noexcept : ptr_{ptr} {
    if (ptr_)
      ptr_->AddRef();
  }

  constexpr RefPtr(T* ptr, DontAddRef) noexcept : ptr_{ptr} {}

  constexpr RefPtr(RefPtr<T>&& other) noexcept : ptr_{other.ptr_} {
    other.ptr_ = nullptr;
  }

  template <typename U>
  constexpr RefPtr(RefPtr<U>&& other) noexcept : ptr_{other.release()} {}

  RefPtr(const RefPtr<T>& other) noexcept : ptr_{other.ptr_} {
    if (ptr_)
      ptr_->AddRef();
  }

  template <typename U>
  RefPtr(const RefPtr<U>& other) noexcept : ptr_{other.get()} {
    if (ptr_)
      ptr_->AddRef();
  }

  ~RefPtr() noexcept {
    if (ptr_)
      ptr_->RemoveRef();
  }

  RefPtr& operator=(RefPtr&& other) noexcept {
    const auto new_ptr = other.ptr_;
    other.ptr_ = nullptr;
    const auto old_ptr = ptr_;
    ptr_ = new_ptr;
    if (old_ptr)
      old_ptr->RemoveRef();
    return *this;
  }

  RefPtr& operator=(const RefPtr& other) noexcept {
    if (other.ptr_)
      other.ptr_->AddRef();
    auto old_ptr = ptr_;
    ptr_ = other.ptr_;
    if (old_ptr)
      old_ptr->RemoveRef();
    return *this;
  }

  constexpr explicit operator bool() const noexcept { return ptr_ != nullptr; }
  constexpr T* get() const noexcept { return ptr_; }
  constexpr T* operator->() const noexcept { return ptr_; }
  constexpr T& operator*() const noexcept { return *ptr_; }

  void swap(RefPtr& other) noexcept {
    T* tmp = ptr_;
    ptr_ = other.ptr_;
    other.ptr_ = tmp;
  }

  void reset() noexcept {
    if (ptr_) {
      ptr_->RemoveRef();
      ptr_ = nullptr;
    }
  }

  /// Returns a pointer to the object and releases the ownership (reference
  /// count won't decrease after this operation).
  constexpr T* release() {
    T* result = ptr_;
    ptr_ = nullptr;
    return result;
  }

 private:
  T* ptr_{nullptr};
};

template <class T, class U>
inline bool operator==(const RefPtr<T>& a, const RefPtr<U>& b) noexcept {
  return a.get() == b.get();
}

template <class T, class U>
inline bool operator!=(const RefPtr<T>& a, const RefPtr<U>& b) noexcept {
  return a.get() != b.get();
}

template <class T, class U>
inline bool operator<(const RefPtr<T>& a, const RefPtr<U>& b) noexcept {
  return a.get() < b.get();
}

template <class T, class U>
inline bool operator>(const RefPtr<T>& a, const RefPtr<U>& b) noexcept {
  return a.get() > b.get();
}

template <class T, class U>
inline bool operator<=(const RefPtr<T>& a, const RefPtr<U>& b) noexcept {
  return a.get() <= b.get();
}

template <class T, class U>
inline bool operator>=(const RefPtr<T>& a, const RefPtr<U>& b) noexcept {
  return a.get() >= b.get();
}

}  // namespace Microsoft::MixedReality::WebRTC
