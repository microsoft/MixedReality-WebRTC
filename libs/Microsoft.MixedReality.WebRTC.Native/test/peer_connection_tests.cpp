// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include "interop_api.h"

namespace {

void MRS_CALL SetEventOnCompleted(void* user_data) {
  Event* ev = (Event*)user_data;
  ev->Set();
}

}  // namespace

TEST(PeerConnection, LocalNoIce) {
  for (int i = 0; i < 3; ++i) {
    // Create PC -- do not use PCRaii, which registers ICE callbacks
    PeerConnectionConfiguration config{};  // local connection only
    PCRaii pc1(config);
    ASSERT_NE(nullptr, pc1.handle());
    PCRaii pc2(config);
    ASSERT_NE(nullptr, pc2.handle());

    // Setup signaling
    SdpCallback sdp1_cb(pc1.handle(), [&pc2](const char* type,
                                             const char* sdp_data) {
      Event ev;
      ASSERT_EQ(Result::kSuccess,
                mrsPeerConnectionSetRemoteDescriptionAsync(
                    pc2.handle(), type, sdp_data, &SetEventOnCompleted, &ev));
      ev.Wait();
      if (kOfferString == type) {
        ASSERT_EQ(Result::kSuccess,
                  mrsPeerConnectionCreateAnswer(pc2.handle()));
      }
    });
    SdpCallback sdp2_cb(pc2.handle(), [&pc1](const char* type,
                                             const char* sdp_data) {
      Event ev;
      ASSERT_EQ(Result::kSuccess,
                mrsPeerConnectionSetRemoteDescriptionAsync(
                    pc1.handle(), type, sdp_data, &SetEventOnCompleted, &ev));
      ev.Wait();
      if (kOfferString == type) {
        ASSERT_EQ(Result::kSuccess,
                  mrsPeerConnectionCreateAnswer(pc1.handle()));
      }
    });

    // Connect
    Event ev;
    InteropCallback<> on_connected([&ev]() { ev.Set(); });
    mrsPeerConnectionRegisterConnectedCallback(pc1.handle(), CB(on_connected));
    ASSERT_EQ(Result::kSuccess, mrsPeerConnectionCreateOffer(pc1.handle()));
    ASSERT_EQ(true, ev.WaitFor(5s));  // should complete within 5s (usually ~1s)
  }
}

TEST(PeerConnection, LocalIce) {
  for (int i = 0; i < 3; ++i) {
    // Create PC
    PeerConnectionConfiguration config{};  // local connection only
    LocalPeerPairRaii pair(config);
    ASSERT_NE(nullptr, pair.pc1());
    ASSERT_NE(nullptr, pair.pc2());

    // Connect
    Event ev;
    InteropCallback<> on_connected([&ev]() { ev.Set(); });
    mrsPeerConnectionRegisterConnectedCallback(pair.pc1(), CB(on_connected));
    ASSERT_EQ(Result::kSuccess, mrsPeerConnectionCreateOffer(pair.pc1()));
    ASSERT_EQ(true, ev.WaitFor(5s));  // should complete within 5s (usually ~1s)

    // Clean-up, because ICE candidates continue to arrive
    mrsPeerConnectionRegisterIceCandidateReadytoSendCallback(pair.pc1(),
                                                             nullptr, nullptr);
    mrsPeerConnectionRegisterIceCandidateReadytoSendCallback(pair.pc2(),
                                                             nullptr, nullptr);
  }
}
