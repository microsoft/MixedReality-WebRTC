// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include "interop_api.h"

#include "test_utils.h"

namespace {

class PeerConnectionTests : public TestUtils::TestBase,
                            public testing::WithParamInterface<mrsSdpSemantic> {
};

}  // namespace

INSTANTIATE_TEST_CASE_P(,
                        PeerConnectionTests,
                        testing::ValuesIn(TestUtils::TestSemantics),
                        TestUtils::SdpSemanticToString);

TEST_P(PeerConnectionTests, LocalNoIce) {
  for (int i = 0; i < 3; ++i) {
    // Create PC -- do not use PCRaii, which registers ICE callbacks
    mrsPeerConnectionConfiguration pc_config{};  // local connection only
    pc_config.sdp_semantic = GetParam();
    PCRaii pc1(pc_config);
    ASSERT_NE(nullptr, pc1.handle());
    PCRaii pc2(pc_config);
    ASSERT_NE(nullptr, pc2.handle());

    // Setup signaling
    Event ev_completed;
    SdpCallback sdp1_cb(pc1.handle(), [&pc2, &ev_completed](
                                          mrsSdpMessageType type,
                                          const char* sdp_data) {
      Event ev;
      ASSERT_EQ(Result::kSuccess, mrsPeerConnectionSetRemoteDescriptionAsync(
                                      pc2.handle(), type, sdp_data,
                                      &TestUtils::SetEventOnCompleted, &ev));
      ev.Wait();
      if (type == mrsSdpMessageType::kOffer) {
        ASSERT_EQ(Result::kSuccess,
                  mrsPeerConnectionCreateAnswer(pc2.handle()));
      } else {
        ev_completed.Set();
      }
    });
    SdpCallback sdp2_cb(pc2.handle(), [&pc1, &ev_completed](
                                          mrsSdpMessageType type,
                                          const char* sdp_data) {
      Event ev;
      ASSERT_EQ(Result::kSuccess, mrsPeerConnectionSetRemoteDescriptionAsync(
                                      pc1.handle(), type, sdp_data,
                                      &TestUtils::SetEventOnCompleted, &ev));
      ev.Wait();
      if (type == mrsSdpMessageType::kOffer) {
        ASSERT_EQ(Result::kSuccess,
                  mrsPeerConnectionCreateAnswer(pc1.handle()));
      } else {
        ev_completed.Set();
      }
    });

    // Connect
    Event ev_connected;
    InteropCallback<> on_connected([&ev_connected]() { ev_connected.Set(); });
    mrsPeerConnectionRegisterConnectedCallback(pc1.handle(), CB(on_connected));
    ev_completed.Reset();
    ASSERT_EQ(Result::kSuccess, mrsPeerConnectionCreateOffer(pc1.handle()));
    ASSERT_TRUE(ev_connected.WaitFor(5s));
    ASSERT_TRUE(ev_completed.WaitFor(5s));
  }
}

TEST_P(PeerConnectionTests, LocalIce) {
  for (int i = 0; i < 3; ++i) {
    mrsPeerConnectionConfiguration pc_config{};  // local connection only
    pc_config.sdp_semantic = GetParam();
    LocalPeerPairRaii pair(pc_config);
    ASSERT_NE(nullptr, pair.pc1());
    ASSERT_NE(nullptr, pair.pc2());
    pair.ConnectAndWait();
    ASSERT_TRUE(pair.WaitExchangeCompletedFor(5s));
    mrsPeerConnectionRegisterIceCandidateReadytoSendCallback(pair.pc1(),
                                                             nullptr, nullptr);
    mrsPeerConnectionRegisterIceCandidateReadytoSendCallback(pair.pc2(),
                                                             nullptr, nullptr);
  }
}
