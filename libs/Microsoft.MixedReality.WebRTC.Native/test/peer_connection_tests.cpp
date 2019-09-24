// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#include "pch.h"

#include "../include/api.h"

TEST(PeerConnection, LocalNoIce) {
  // Create PC
  PeerConnectionConfiguration config{};  // local connection only
  PCRaii pc1(config);
  ASSERT_NE(nullptr, pc1.handle());
  PCRaii pc2(config);
  ASSERT_NE(nullptr, pc2.handle());

  // Setup signaling
  SdpCallback sdp1_cb(
      pc1.handle(), [&pc2](const char* type, const char* sdp_data) {
        ASSERT_EQ(MRS_SUCCESS, mrsPeerConnectionSetRemoteDescription(
                                   pc2.handle(), type, sdp_data));
        if (kOfferString == type) {
          ASSERT_EQ(MRS_SUCCESS, mrsPeerConnectionCreateAnswer(pc2.handle()));
        }
      });
  SdpCallback sdp2_cb(
      pc2.handle(), [&pc1](const char* type, const char* sdp_data) {
        ASSERT_EQ(MRS_SUCCESS, mrsPeerConnectionSetRemoteDescription(
                                   pc1.handle(), type, sdp_data));
        if (kOfferString == type) {
          ASSERT_EQ(MRS_SUCCESS, mrsPeerConnectionCreateAnswer(pc1.handle()));
        }
      });

  // Connect
  Event ev;
  Callback<> on_connected([&ev]() { ev.Set(); });
  mrsPeerConnectionRegisterConnectedCallback(pc1.handle(), CB(on_connected));
  ASSERT_EQ(MRS_SUCCESS, mrsPeerConnectionCreateOffer(pc1.handle()));
  ASSERT_EQ(true, ev.WaitFor(5s));  // should complete within 5s (usually ~1s)
}

TEST(PeerConnection, LocalIce) {
  // Create PC
  PeerConnectionConfiguration config{};  // local connection only
  PCRaii pc1(config);
  ASSERT_NE(nullptr, pc1.handle());
  PCRaii pc2(config);
  ASSERT_NE(nullptr, pc2.handle());

  // Setup signaling
  SdpCallback sdp1_cb(
      pc1.handle(), [&pc2](const char* type, const char* sdp_data) {
        ASSERT_EQ(MRS_SUCCESS, mrsPeerConnectionSetRemoteDescription(
                                   pc2.handle(), type, sdp_data));
        if (kOfferString == type) {
          ASSERT_EQ(MRS_SUCCESS, mrsPeerConnectionCreateAnswer(pc2.handle()));
        }
      });
  SdpCallback sdp2_cb(
      pc2.handle(), [&pc1](const char* type, const char* sdp_data) {
        ASSERT_EQ(MRS_SUCCESS, mrsPeerConnectionSetRemoteDescription(
                                   pc1.handle(), type, sdp_data));
        if (kOfferString == type) {
          ASSERT_EQ(MRS_SUCCESS, mrsPeerConnectionCreateAnswer(pc1.handle()));
        }
      });
  IceCallback ice1_cb(pc1.handle(), [&pc2](const char* candidate,
                                           int sdpMlineindex,
                                           const char* sdpMid) {
    ASSERT_EQ(MRS_SUCCESS, mrsPeerConnectionAddIceCandidate(
                               pc2.handle(), sdpMid, sdpMlineindex, candidate));
  });
  IceCallback ice2_cb(pc2.handle(), [&pc1](const char* candidate,
                                           int sdpMlineindex,
                                           const char* sdpMid) {
    ASSERT_EQ(MRS_SUCCESS, mrsPeerConnectionAddIceCandidate(
                               pc1.handle(), sdpMid, sdpMlineindex, candidate));
  });

  // Connect
  Event ev;
  Callback<> on_connected([&ev]() { ev.Set(); });
  mrsPeerConnectionRegisterConnectedCallback(pc1.handle(), CB(on_connected));
  ASSERT_EQ(MRS_SUCCESS, mrsPeerConnectionCreateOffer(pc1.handle()));
  ASSERT_EQ(true, ev.WaitFor(5s));  // should complete within 5s (usually ~1s)

  // Clean-up, because ICE candidates continue to arrive
  mrsPeerConnectionRegisterIceCandidateReadytoSendCallback(pc1.handle(),
                                                           nullptr, nullptr);
  mrsPeerConnectionRegisterIceCandidateReadytoSendCallback(pc2.handle(),
                                                           nullptr, nullptr);
}
