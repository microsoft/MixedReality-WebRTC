// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#include "../include/api.h"

/// Simple wait event, similar to rtc::Event.
struct Event {
  void Set() { cv_.notify_one(); }
  void Wait() {
    std::unique_lock<std::mutex> lk(m_);
    cv_.wait(lk);
  }
  bool WaitFor(std::chrono::seconds seconds) {
    std::unique_lock<std::mutex> lk(m_);
    return (cv_.wait_for(lk, seconds) == std::cv_status::no_timeout);
  }
  std::mutex m_;
  std::condition_variable cv_;
};

/// Wrapper around an interop callback taking an extra raw pointer argument, to
/// trampoline its call to a generic std::function for convenience.
/// Use the CB() macro to register the callback with the interop layer.
/// Usage:
///   Callback<int> cb([](int i) { ... });
///   mrsRegisterXxxCallback(h, CB(cb));
template <typename... Args>
struct Callback {
  using function_type = void(Args...);
  using static_type = void(MRS_CALL*)(void*, Args...);
  using method_type = void (Callback::*)(Args...);
  Callback() { callback_ = &StaticExec; }
  Callback(std::function<function_type> func) : func_(std::move(func)) {
    callback_ = &StaticExec;
  }
  template <typename U>
  Callback(U func) : func_(std::forward<std::function<function_type>>(func)) {
    callback_ = &StaticExec;
  }
  Callback& operator=(std::function<function_type> func) {
    func_ = std::move(func);
    return (*this);
  }
  static void StaticExec(void* user_data, Args... args) {
    auto self = (Callback*)user_data;
    self->func_(std::forward<Args>(args)...);
  }
  std::function<function_type> func_;
  static_type callback_;
};

/// Convenience macro to fill the interop callback registration arguments.
#define CB(x) x.callback_, &x

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
