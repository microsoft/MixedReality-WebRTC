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
  virtual ~Callback() { assert(!is_registered_); }

  /// Constructor from any std::function-compatible object, including lambdas.
  template <typename U>
  Callback(U func)
      : func_(std::function<function_type>(std::forward<U>(func))) {}

  template <typename U>
  Callback& operator=(U func) {
    func_ = std::function<function_type>(std::forward<U>(func));
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

// OnLocalSdpReadyToSend
class SdpCallback : public Callback<const char*, const char*> {
 public:
  using Base = Callback<const char*, const char*>;
  using callback_type = void(const char*, const char*);
  SdpCallback(PeerConnectionHandle pc) : pc_(pc) {}
  SdpCallback(PeerConnectionHandle pc, std::function<callback_type> func)
      : Base(std::move(func)), pc_(pc) {
    mrsPeerConnectionRegisterLocalSdpReadytoSendCallback(pc_, &StaticExec,
                                                         this);
    is_registered_ = true;
  }
  ~SdpCallback() override {
    mrsPeerConnectionRegisterLocalSdpReadytoSendCallback(pc_, nullptr, nullptr);
    is_registered_ = false;
  }
  SdpCallback& operator=(std::function<callback_type> func) {
    Base::operator=(std::move(func));
    mrsPeerConnectionRegisterLocalSdpReadytoSendCallback(pc_, &StaticExec,
                                                         this);
    is_registered_ = true;
    return (*this);
  }

 protected:
  PeerConnectionHandle pc_{};
};

// OnIceCandidateReadyToSend
class IceCallback : public Callback<const char*, int, const char*> {
 public:
  using Base = Callback<const char*, int, const char*>;
  using callback_type = void(const char*, int, const char*);
  IceCallback(PeerConnectionHandle pc) : pc_(pc) {}
  IceCallback(PeerConnectionHandle pc, std::function<callback_type> func)
      : Base(std::move(func)), pc_(pc) {
    mrsPeerConnectionRegisterIceCandidateReadytoSendCallback(pc_, &StaticExec,
                                                             this);
    is_registered_ = true;
  }
  ~IceCallback() override {
    mrsPeerConnectionRegisterIceCandidateReadytoSendCallback(pc_, nullptr,
                                                             nullptr);
    is_registered_ = false;
  }
  IceCallback& operator=(std::function<callback_type> func) {
    Base::operator=(std::move(func));
    mrsPeerConnectionRegisterIceCandidateReadytoSendCallback(pc_, &StaticExec,
                                                             this);
    is_registered_ = true;
    return (*this);
  }

 protected:
  PeerConnectionHandle pc_{};
};

constexpr const std::string_view kOfferString{"offer"};

/// Helper to create a pair of peer connections and locally connect them to each
/// other via simple hard-coded signaling.
class LocalPeerPairRaii {
 public:
  LocalPeerPairRaii()
      : sdp1_cb_(pc1()), sdp2_cb_(pc2()), ice1_cb_(pc1()), ice2_cb_(pc2()) {
    setup();
  }
  LocalPeerPairRaii(const PeerConnectionConfiguration& config)
      : pc1_(config),
        pc2_(config),
        sdp1_cb_(pc1()),
        sdp2_cb_(pc2()),
        ice1_cb_(pc1()),
        ice2_cb_(pc2()) {
    setup();
  }
  ~LocalPeerPairRaii() { shutdown(); }

  PeerConnectionHandle pc1() const { return pc1_.handle(); }
  PeerConnectionHandle pc2() const { return pc2_.handle(); }

  void ConnectAndWait() {
    Event ev1, ev2;
    connected1_cb_ = [&ev1]() { ev1.Set(); };
    connected2_cb_ = [&ev2]() { ev2.Set(); };
    mrsPeerConnectionRegisterConnectedCallback(pc1(), CB(connected1_cb_));
    connected1_cb_.is_registered_ = true;
    mrsPeerConnectionRegisterConnectedCallback(pc2(), CB(connected2_cb_));
    connected2_cb_.is_registered_ = true;
    ASSERT_EQ(MRS_SUCCESS, mrsPeerConnectionCreateOffer(pc1()));
    ASSERT_EQ(true, ev1.WaitFor(60s));
    ASSERT_EQ(true, ev2.WaitFor(60s));
  }

 protected:
  PCRaii pc1_;
  PCRaii pc2_;
  SdpCallback sdp1_cb_;
  SdpCallback sdp2_cb_;
  IceCallback ice1_cb_;
  IceCallback ice2_cb_;
  Callback<> connected1_cb_;
  Callback<> connected2_cb_;
  void setup() {
    sdp1_cb_ = [this](const char* type, const char* sdp_data) {
      ASSERT_EQ(MRS_SUCCESS, mrsPeerConnectionSetRemoteDescription(
                                 pc2_.handle(), type, sdp_data));
      if (kOfferString == type) {
        ASSERT_EQ(MRS_SUCCESS, mrsPeerConnectionCreateAnswer(pc2_.handle()));
      }
    };
    sdp2_cb_ = [this](const char* type, const char* sdp_data) {
      ASSERT_EQ(MRS_SUCCESS, mrsPeerConnectionSetRemoteDescription(
                                 pc1_.handle(), type, sdp_data));
      if (kOfferString == type) {
        ASSERT_EQ(MRS_SUCCESS, mrsPeerConnectionCreateAnswer(pc1_.handle()));
      }
    };
    ice1_cb_ = [this](const char* candidate, int sdpMlineindex,
                      const char* sdpMid) {
      ASSERT_EQ(MRS_SUCCESS,
                mrsPeerConnectionAddIceCandidate(pc2_.handle(), sdpMid,
                                                 sdpMlineindex, candidate));
    };
    ice2_cb_ = [this](const char* candidate, int sdpMlineindex,
                      const char* sdpMid) {
      ASSERT_EQ(MRS_SUCCESS,
                mrsPeerConnectionAddIceCandidate(pc1_.handle(), sdpMid,
                                                 sdpMlineindex, candidate));
    };
  }

  void shutdown() {
    if (connected1_cb_.is_registered_) {
      mrsPeerConnectionRegisterConnectedCallback(pc1(), nullptr, nullptr);
      connected1_cb_.is_registered_ = false;
    }
    if (connected2_cb_.is_registered_) {
      mrsPeerConnectionRegisterConnectedCallback(pc2(), nullptr, nullptr);
      connected2_cb_.is_registered_ = false;
    }
  }
};
