// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#include "../include/api.h"

/// Simple wait event, similar to rtc::Event.
struct Event {
  void Reset() {
    std::unique_lock<std::mutex> lk(m_);
    signaled_ = false;
  }
  void Set() {
    std::unique_lock<std::mutex> lk(m_);
    signaled_ = true;
    cv_.notify_one();
  }
  void Wait() {
    std::unique_lock<std::mutex> lk(m_);
    if (!signaled_) {
      cv_.wait(lk);
    }
  }
  bool WaitFor(std::chrono::seconds seconds) {
    std::unique_lock<std::mutex> lk(m_);
    if (!signaled_) {
      return (cv_.wait_for(lk, seconds) == std::cv_status::no_timeout);
    }
    return true;
  }
  std::mutex m_;
  std::condition_variable cv_;
  bool signaled_{false};
};

/// Wrapper around an interop callback taking an extra raw pointer argument, to
/// trampoline its call to a generic std::function for convenience (including
/// lambda functions).
/// Use the CB() macro to register the callback with the interop layer.
/// Note that the Callback<> variable must stay in scope for the duration of the
/// use, so that its static callback function is kept alive while registered.
/// Usage:
///   {
///     Callback<int> cb([](int i) { ... });
///     mrsRegisterXxxCallback(h, CB(cb));
///     [...]
///     mrsRegisterXxxCallback(h, nullptr, nullptr);
///   }
template <typename... Args>
struct Callback {
  /// Callback function signature type.
  using function_type = void(Args...);

  /// Type of the static interop callback, which prepends a void* user data
  /// opaque argument.
  using static_type = void(MRS_CALL*)(void*, Args...);

  Callback() = default;
  ~Callback() { assert(!is_registered_); }

  /// Constructor from any std::function-compatible object, including lambdas.
  template <typename U>
  Callback(U func) : func_(std::forward<std::function<function_type>>(func)) {}

  Callback& operator=(std::function<function_type> func) {
    func_ = std::move(func);
    return (*this);
  }

  /// Adapter for generic std::function<> to interop callback.
  static void MRS_CALL StaticExec(void* user_data, Args... args) {
    auto self = (Callback*)user_data;
    self->func_(std::forward<Args>(args)...);
  }

  std::function<function_type> func_;
  bool is_registered_{false};
};

/// Convenience macro to fill the interop callback registration arguments.
#define CB(x) &x.StaticExec, &x

/// Helper to create and close a peer connection.
class PCRaii {
 public:
  PCRaii() {
    PeerConnectionConfiguration config{};
    config.encoded_ice_servers = "stun:stun.l.google.com:19302";
    mrsPeerConnectionInteropHandle interop_handle = (void*)0x1;
    mrsPeerConnectionCreate(config, interop_handle, &handle_);
  }
  PCRaii(const PeerConnectionConfiguration& config,
         mrsPeerConnectionInteropHandle interop_handle = (void*)0x1) {
    mrsPeerConnectionCreate(config, interop_handle, &handle_);
  }
  ~PCRaii() { mrsPeerConnectionClose(&handle_); }
  PeerConnectionHandle handle() const { return handle_; }

 protected:
  PeerConnectionHandle handle_{};
};
