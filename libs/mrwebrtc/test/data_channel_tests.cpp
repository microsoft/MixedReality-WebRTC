// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include "data_channel_interop.h"
#include "interop_api.h"

#include "test_utils.h"

namespace {

class DataChannelTests : public TestUtils::TestBase,
                         public testing::WithParamInterface<mrsSdpSemantic> {};

// OnDataChannelAdded
using DataAddedCallback = InteropCallback<const mrsDataChannelAddedInfo*>;

void MRS_CALL StaticMessageCallback(void* user_data,
                                    const void* data,
                                    const uint64_t size) noexcept {
  auto func = *static_cast<std::function<void(const void*, const uint64_t)>*>(
      user_data);
  func(data, size);
}

void MRS_CALL StaticStateCallback(void* user_data,
                                  mrsDataChannelState state,
                                  int32_t id) noexcept {
  auto func = *static_cast<std::function<void(mrsDataChannelState, int32_t)>*>(
      user_data);
  func(state, id);
}

}  // namespace

INSTANTIATE_TEST_CASE_P(,
                        DataChannelTests,
                        testing::ValuesIn(TestUtils::TestSemantics),
                        TestUtils::SdpSemanticToString);

TEST_P(DataChannelTests, AddChannelBeforeInit) {
  mrsPeerConnectionConfiguration pc_config{};
  pc_config.sdp_semantic = GetParam();
  PCRaii pc(pc_config);
  ASSERT_NE(nullptr, pc.handle());
  mrsDataChannelConfig config{};
  config.label = "data";
  config.flags = mrsDataChannelConfigFlags::kOrdered |
                 mrsDataChannelConfigFlags::kReliable;
  mrsDataChannelHandle handle;
  ASSERT_EQ(Result::kSuccess,
            mrsPeerConnectionAddDataChannel(pc.handle(), &config, &handle));
}

TEST_P(DataChannelTests, InBand) {
  mrsPeerConnectionConfiguration pc_config{};
  pc_config.sdp_semantic = GetParam();
  LocalPeerPairRaii pair(pc_config);

  // Add dummy out-of-band data channel to force SCTP negotiating, otherwise
  // further data channel opening after connecting will fail.
  // ID must be >= 0 for negotiated (out-of-band) channel.
  constexpr int kDummyDataChannelId = 25;
  {
    mrsDataChannelConfig data_config{};
    data_config.id = kDummyDataChannelId;
    data_config.label = "dummy_out_of_band";
    data_config.flags = mrsDataChannelConfigFlags::kOrdered |
                        mrsDataChannelConfigFlags::kReliable;
    mrsDataChannelHandle handle;
    ASSERT_EQ(Result::kSuccess, mrsPeerConnectionAddDataChannel(
                                    pair.pc1(), &data_config, &handle));
    ASSERT_EQ(Result::kSuccess, mrsPeerConnectionAddDataChannel(
                                    pair.pc2(), &data_config, &handle));
  }

  // Connect
  pair.ConnectAndWait();
  ASSERT_TRUE(pair.WaitExchangeCompletedFor(5s));

  // Register a callback on PC #2
  const std::string channel_label = "test data channel";
  Event data2_ev;
  DataAddedCallback data_added_cb =
      [kDummyDataChannelId, &data2_ev,
       &channel_label](const mrsDataChannelAddedInfo* info) {
        // Ignore dummy channel; even if previous session is established,
        // callback might be delayed until after this handler is registered.
        if (info->id != kDummyDataChannelId) {
          ASSERT_NE(nullptr, info->handle);
          ASSERT_STREQ(channel_label.c_str(), info->label);
          data2_ev.Set();
        }
      };
  mrsPeerConnectionRegisterDataChannelAddedCallback(pair.pc2(),
                                                    CB(data_added_cb));
  data_added_cb.is_registered_ = true;

  // Add a data channel on PC #1
  mrsDataChannelConfig data_config{};
  data_config.label = channel_label.c_str();
  data_config.flags = mrsDataChannelConfigFlags::kOrdered |
                      mrsDataChannelConfigFlags::kReliable;
  mrsDataChannelHandle data1_handle;
  ASSERT_EQ(Result::kSuccess, mrsPeerConnectionAddDataChannel(
                                  pair.pc1(), &data_config, &data1_handle));
  ASSERT_NE(nullptr, data1_handle);

  // Renegotiate, including the new data channel
  pair.ConnectAndWait();
  ASSERT_TRUE(pair.WaitExchangeCompletedFor(5s));

  // TODO expose label
  // ASSERT_EQ(channel_label, data1->label());

  // Ensure the data channel was created on peer #2 as part of the new session
  // negotiation.
  ASSERT_EQ(true, data2_ev.WaitFor(30s));

  // Clean-up
  mrsPeerConnectionRegisterDataChannelAddedCallback(pair.pc2(), nullptr,
                                                    nullptr);
  data_added_cb.is_registered_ = false;
}

TEST_P(DataChannelTests, MultiThreadCreate) {
  mrsPeerConnectionConfiguration pc_config{};
  pc_config.sdp_semantic = GetParam();
  PCRaii pc(pc_config);
  constexpr int kNumThreads = 16;
  std::thread threads[kNumThreads];
  Event ev_start;
  for (std::thread& t : threads) {
    t = std::move(*new std::thread([&ev_start, &pc]() {
      ev_start.Wait();
      mrsDataChannelConfig config{};
      mrsDataChannelHandle handle;
      ASSERT_EQ(Result::kSuccess,
                mrsPeerConnectionAddDataChannel(pc.handle(), &config, &handle));
    }));
  }
  ev_start.SetBroadcast();
  for (std::thread& t : threads) {
    t.join();
  }
}

TEST_P(DataChannelTests, Send) {
  mrsPeerConnectionConfiguration pc_config{};
  pc_config.sdp_semantic = GetParam();
  LocalPeerPairRaii pair(pc_config);

  const char msg1_data[] = "test message";
  const uint64_t msg1_size = sizeof(msg1_data);
  const char msg2_data[] =
      "This is a reply from peer #2 to peer #1 which is a bit longer than the "
      "previous message, just to make sure longer messages are also supported.";
  const uint64_t msg2_size = sizeof(msg2_data);

  // Id expected by the state callbacks below for newly created channels.
  // -1 means "no specific id".
  int expected_id = -1;
  Event ev_msg1, ev_state1;
  std::function<void(const void*, const uint64_t)> message1_cb(
      [&](const void* data, const uint64_t size) {
        ASSERT_EQ(msg2_size, size);
        ASSERT_NE(nullptr, data);
        ASSERT_EQ(0, memcmp(data, msg2_data, msg2_size));
        ev_msg1.Set();
      });
  std::function<void(mrsDataChannelState, int32_t)> state1_cb(
      [&](mrsDataChannelState state, int32_t id) {
        if (expected_id >= 0) {
          ASSERT_EQ(expected_id, id);
        }
        if (state == mrsDataChannelState::kOpen) {
          ev_state1.Set();
        }
      });

  Event ev_msg2, ev_state2;
  std::function<void(const void*, const uint64_t)> message2_cb(
      [&](const void* data, const uint64_t size) {
        ASSERT_EQ(msg1_size, size);
        ASSERT_NE(nullptr, data);
        ASSERT_EQ(0, memcmp(data, msg1_data, msg1_size));
        ev_msg2.Set();
      });
  std::function<void(mrsDataChannelState, int32_t)> state2_cb(
      [&](mrsDataChannelState state, int32_t id) {
        if (expected_id >= 0) {
          ASSERT_EQ(expected_id, id);
        }
        if (state == mrsDataChannelState::kOpen) {
          ev_state2.Set();
        }
      });
  mrsDataChannelCallbacks callbacks1{};
  callbacks1.message_callback = &StaticMessageCallback;
  callbacks1.message_user_data = &message1_cb;
  callbacks1.state_callback = &StaticStateCallback;
  callbacks1.state_user_data = &state1_cb;

  mrsDataChannelCallbacks callbacks2{};
  callbacks2.message_callback = &StaticMessageCallback;
  callbacks2.message_user_data = &message2_cb;
  callbacks2.state_callback = &StaticStateCallback;
  callbacks2.state_user_data = &state2_cb;

  // Send messages through an out-of-band channel.
  {
    const int kId = 42;
    mrsDataChannelConfig config{};
    config.id = kId;
    config.label = "data";
    config.flags = mrsDataChannelConfigFlags::kOrdered |
                   mrsDataChannelConfigFlags::kReliable;

    // Out-of-band channel; expect same id as passed.
    expected_id = kId;

    // Create channels.
    mrsDataChannelHandle handle1;
    ASSERT_EQ(Result::kSuccess,
              mrsPeerConnectionAddDataChannel(pair.pc1(), &config, &handle1));
    mrsDataChannelRegisterCallbacks(handle1, &callbacks1);
    mrsDataChannelHandle handle2;
    ASSERT_EQ(Result::kSuccess,
              mrsPeerConnectionAddDataChannel(pair.pc2(), &config, &handle2));
    mrsDataChannelRegisterCallbacks(handle2, &callbacks2);

    // Connect and wait for channels to be ready
    pair.ConnectAndWait();
    ASSERT_TRUE(ev_state1.WaitFor(60s));
    ASSERT_TRUE(ev_state2.WaitFor(60s));

    // Send message 1 -> 2
    ASSERT_EQ(Result::kSuccess,
              mrsDataChannelSendMessage(handle1, msg1_data, msg1_size));
    ASSERT_TRUE(ev_msg2.WaitFor(60s));

    // Send message 2 -> 1
    ASSERT_EQ(Result::kSuccess,
              mrsDataChannelSendMessage(handle2, msg2_data, msg2_size));
    ASSERT_TRUE(ev_msg1.WaitFor(60s));

    ASSERT_EQ(Result::kSuccess,
              mrsPeerConnectionRemoveDataChannel(pair.pc1(), handle1));
    ASSERT_EQ(Result::kSuccess,
              mrsPeerConnectionRemoveDataChannel(pair.pc2(), handle2));
  }

  // Send messages through an in-band channel.
  {
    mrsDataChannelConfig inband_config{};
    inband_config.label = "in-band";
    inband_config.flags = mrsDataChannelConfigFlags::kOrdered |
                          mrsDataChannelConfigFlags::kReliable;

    // In-band channel; do not expect a specific id.
    expected_id = -1;

    mrsDataChannelHandle inband_handle1{};
    mrsDataChannelHandle inband_handle2{};

    Event ev_inband1, ev_inband2;
    ev_state1.Reset();
    ev_state2.Reset();
    ev_msg1.Reset();
    ev_msg2.Reset();

    // Create channel on pc1 and wait for callback on both ends.
    mrsDataChannelHandle inband1_handle_from_callback{};
    DataAddedCallback inband_added_cb1 =
        [&](const mrsDataChannelAddedInfo* info) {
          ASSERT_NE(nullptr, info->handle);
          inband1_handle_from_callback = info->handle;
          ASSERT_STREQ(inband_config.label, info->label);
          mrsDataChannelRegisterCallbacks(info->handle, &callbacks1);
          ev_inband1.Set();
        };
    DataAddedCallback inband_added_cb2 =
        [&](const mrsDataChannelAddedInfo* info) {
          ASSERT_NE(nullptr, info->handle);
          inband_handle2 = info->handle;
          ASSERT_STREQ(inband_config.label, info->label);
          mrsDataChannelRegisterCallbacks(inband_handle2, &callbacks2);
          ev_inband2.Set();
        };
    mrsPeerConnectionRegisterDataChannelAddedCallback(pair.pc1(),
                                                      CB(inband_added_cb1));
    mrsPeerConnectionRegisterDataChannelAddedCallback(pair.pc2(),
                                                      CB(inband_added_cb2));
    ASSERT_EQ(Result::kSuccess,
              mrsPeerConnectionAddDataChannel(pair.pc1(), &inband_config,
                                              &inband_handle1));
    ASSERT_TRUE(ev_inband1.WaitFor(60s));
    ASSERT_TRUE(ev_inband2.WaitFor(60s));
    ASSERT_EQ(inband_handle1, inband1_handle_from_callback);

    // Wait for the channel to be open on both ends.
    ASSERT_TRUE(ev_state1.WaitFor(60s));
    ASSERT_TRUE(ev_state2.WaitFor(60s));

    // Send message 1 -> 2
    ASSERT_EQ(Result::kSuccess,
              mrsDataChannelSendMessage(inband_handle1, msg1_data, msg1_size));
    ASSERT_TRUE(ev_msg2.WaitFor(60s));

    // Send message 2 -> 1
    ASSERT_EQ(Result::kSuccess,
              mrsDataChannelSendMessage(inband_handle2, msg2_data, msg2_size));
    ASSERT_TRUE(ev_msg1.WaitFor(60s));

    // Clean-up
    ASSERT_EQ(Result::kSuccess,
              mrsPeerConnectionRemoveDataChannel(pair.pc1(), inband_handle1));
    ASSERT_EQ(Result::kSuccess,
              mrsPeerConnectionRemoveDataChannel(pair.pc2(), inband_handle2));

    mrsPeerConnectionRegisterDataChannelAddedCallback(pair.pc1(), nullptr,
                                                      nullptr);
    mrsPeerConnectionRegisterDataChannelAddedCallback(pair.pc2(), nullptr,
                                                      nullptr);
  }
}

TEST_F(DataChannelTests, Send_InvalidHandle) {
  const char msg[] = "test";
  const uint64_t size = sizeof(msg);
  ASSERT_EQ(Result::kInvalidNativeHandle,
            mrsDataChannelSendMessage(nullptr, msg, size));
}

// NOTE - This test is flaky, relies on the send loop being faster than what the
// local
//        network can send, without setting any explicit congestion control etc.
//        so is prone to false errors. This is still useful for local testing.
//
// TEST_F(DataChannelTests, Buffering) {
//  // Create PC
//  LocalPeerPairRaii pair;
//  ASSERT_NE(nullptr, pair.pc1());
//  ASSERT_NE(nullptr, pair.pc2());
//
//  // In order to allow creating interop wrappers from native code, register
//  the
//  // necessary interop callbacks.
//  mrsPeerConnectionInteropCallbacks interop{};
//  interop.data_channel_create_object = &FakeIterop_DataChannelCreate;
//  ASSERT_EQ(Result::kSuccess,
//            mrsPeerConnectionRegisterInteropCallbacks(pair.pc1(), &interop));
//  ASSERT_EQ(Result::kSuccess,
//            mrsPeerConnectionRegisterInteropCallbacks(pair.pc2(), &interop));
//
//  // Add dummy out-of-band data channel
//  mrsDataChannelHandle handle1, handle2;
//  uint64_t peak = 0;
//  {
//    mrsDataChannelConfig data_config{};
//    data_config.id = 25;  // must be >= 0 for negotiated (out-of-band) channel
//    data_config.label = "out_of_band";
//    data_config.flags = mrsDataChannelConfigFlags::kOrdered |
//                        mrsDataChannelConfigFlags::kReliable;
//    mrsDataChannelCallbacks callbacks{};
//	callbacks.buffering_user_data = &peak;
//    callbacks.buffering_callback = [](void* user_data, uint64_t previous,
//                                      uint64_t current, uint64_t limit) {
//      ASSERT_LT(previous, limit);
//      ASSERT_LT(current, limit);
//      uint64_t* ppeak = (uint64_t*)user_data;
//      *ppeak = std::max(*ppeak, current);
//    };
//
//    mrsDataChannelInteropHandle interopHandle = kFakeInteropDataChannelHandle;
//    ASSERT_EQ(Result::kSuccess,
//              mrsPeerConnectionAddDataChannel(
//                  pair.pc1(), interopHandle, data_config, callbacks,
//                  &handle1));
//    ASSERT_EQ(Result::kSuccess,
//              mrsPeerConnectionAddDataChannel(
//                  pair.pc2(), interopHandle, data_config, callbacks,
//                  &handle2));
//  }
//  auto data1 = (Microsoft::MixedReality::WebRTC::DataChannel*)handle1;
//  auto data2 = (Microsoft::MixedReality::WebRTC::DataChannel*)handle2;
//
//  pair.ConnectAndWait();
//
//  // Send data too fast, to trigger some buffering
//  char buffer[4096];
//  for (int i = 0; i < 10000; ++i)  // current impl has 16 MB buffer
//  {
//    ASSERT_TRUE(data1->Send(buffer, 4096));
//  }
//
//  ASSERT_GT(peak, 0);
//}
