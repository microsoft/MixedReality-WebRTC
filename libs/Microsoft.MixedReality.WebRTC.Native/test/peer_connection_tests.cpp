// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#include "pch.h"

#include "../include/api.h"

#if !defined(MRSW_EXCLUDE_DEVICE_TESTS)

struct SdpHelper {
  struct Args {
    SdpHelper* self;
    PeerConnectionHandle handle;
  };

  SdpHelper(PeerConnectionHandle handle1, PeerConnectionHandle handle2)
      : handle1_(handle1), handle2_(handle2) {
    args1_.self = this;
    args1_.handle = handle1;
    args2_.self = this;
    args2_.handle = handle2;
    mrsPeerConnectionRegisterLocalSdpReadytoSendCallback(handle1_, &OnLocalSdp,
                                                         &args1_);
    mrsPeerConnectionRegisterIceCandidateReadytoSendCallback(
        handle1_, &OnIceCandidate, &args1_);
    mrsPeerConnectionRegisterLocalSdpReadytoSendCallback(handle2_, &OnLocalSdp,
                                                         &args2_);
    mrsPeerConnectionRegisterIceCandidateReadytoSendCallback(
        handle2_, &OnIceCandidate, &args2_);
  }

  ~SdpHelper() {
    mrsPeerConnectionRegisterLocalSdpReadytoSendCallback(handle1_, nullptr,
                                                         nullptr);
    mrsPeerConnectionRegisterIceCandidateReadytoSendCallback(handle1_, nullptr,
                                                             nullptr);
    mrsPeerConnectionRegisterLocalSdpReadytoSendCallback(handle2_, nullptr,
                                                         nullptr);
    mrsPeerConnectionRegisterIceCandidateReadytoSendCallback(handle2_, nullptr,
                                                             nullptr);
  }

  static void MRS_CALL OnLocalSdp(void* user_data,
                                  const char* type,
                                  const char* sdp_data) {
    auto args = (Args*)user_data;
    if (args->handle == args->self->handle1_) {  // 1 -> 2
      args->self->SendSdpTo(args->self->handle2_, type, sdp_data);
    } else if (args->handle == args->self->handle2_) {  // 2 -> 1
      args->self->SendSdpTo(args->self->handle1_, type, sdp_data);
    } else {
      assert(false);
    }
  }

  static void MRS_CALL OnIceCandidate(void* user_data,
                                      const char* candidate,
                                      int sdpMlineindex,
                                      const char* sdpMid) {
    auto args = (Args*)user_data;
    if (args->handle == args->self->handle1_) {  // 1 -> 2
      args->self->SendIceTo(args->self->handle2_, candidate, sdpMlineindex,
                            sdpMid);
    } else if (args->handle == args->self->handle2_) {  // 2 -> 1
      args->self->SendIceTo(args->self->handle1_, candidate, sdpMlineindex,
                            sdpMid);
    } else {
      assert(false);
    }
  }

  void SendSdpTo(PeerConnectionHandle dest,
                 const char* type,
                 const char* sdp_data) {
    ASSERT_EQ(MRS_SUCCESS,
              mrsPeerConnectionSetRemoteDescription(dest, type, sdp_data));
    if (std::string("offer") == type) {
      ASSERT_EQ(MRS_SUCCESS, mrsPeerConnectionCreateAnswer(dest));
    }
  }

  void SendIceTo(PeerConnectionHandle dest,
                 const char* candidate,
                 int sdpMlineindex,
                 const char* sdpMid) {
    ASSERT_EQ(MRS_SUCCESS, mrsPeerConnectionAddIceCandidate(
                               dest, sdpMid, sdpMlineindex, candidate));
  }

  PeerConnectionHandle handle1_;
  PeerConnectionHandle handle2_;
  Args args1_;
  Args args2_;
};

TEST(PeerConnection, Local) {
  // Create PC
  PeerConnectionConfiguration config{};  // local connection only
  PCRaii pc1(config);
  ASSERT_NE(nullptr, pc1.handle());
  PCRaii pc2(config);
  ASSERT_NE(nullptr, pc2.handle());

  // Setup signaling
  SdpHelper helper1(pc1.handle(), pc2.handle());

  // Connect
  Event ev;
  Callback<> on_connected([&ev]() { ev.Set(); });
  mrsPeerConnectionRegisterConnectedCallback(pc1.handle(), CB(on_connected));
  ASSERT_EQ(MRS_SUCCESS, mrsPeerConnectionCreateOffer(pc1.handle()));
  ASSERT_EQ(true, ev.WaitFor(5s));  // should complete within 5s (usually ~1s)
}

#endif  // MRSW_EXCLUDE_DEVICE_TESTS
