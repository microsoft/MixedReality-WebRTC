// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include "data_channel.h"
#include "interop/interop_api.h"

namespace {

const mrsPeerConnectionInteropHandle kFakeInteropPeerConnectionHandle =
    (void*)0x1;
const mrsDataChannelInteropHandle kFakeInteropDataChannelHandle = (void*)0x2;

mrsDataChannelInteropHandle MRS_CALL
FakeIterop_DataChannelCreate(mrsPeerConnectionInteropHandle /*parent*/,
                             mrsDataChannelConfig /*config*/,
                             mrsDataChannelCallbacks* /*callbacks*/) {
  return kFakeInteropDataChannelHandle;
}

// OnDataChannelAdded
using DataAddedCallback =
    InteropCallback<mrsDataChannelInteropHandle, DataChannelHandle>;

}  // namespace

TEST(DataChannel, AddChannelBeforeInit) {
  PCRaii pc;
  ASSERT_NE(nullptr, pc.handle());
  mrsDataChannelConfig config{};
  config.label = "data";
  config.flags = mrsDataChannelConfigFlags::kOrdered |
                 mrsDataChannelConfigFlags::kReliable;
  mrsDataChannelCallbacks callbacks{};
  DataChannelHandle handle;
  mrsDataChannelInteropHandle interopHandle = kFakeInteropDataChannelHandle;
  ASSERT_EQ(Result::kSuccess,
            mrsPeerConnectionAddDataChannel(pc.handle(), interopHandle, config,
                                            callbacks, &handle));
}

TEST(DataChannel, InBand) {
  // Create PC
  PeerConnectionConfiguration config{};  // local connection only
  PCRaii pc1(config);
  ASSERT_NE(nullptr, pc1.handle());
  PCRaii pc2(config);
  ASSERT_NE(nullptr, pc2.handle());

  // In order to allow creating interop wrappers from native code, register the
  // necessary interop callbacks.
  mrsPeerConnectionInteropCallbacks interop{};
  interop.data_channel_create_object = &FakeIterop_DataChannelCreate;
  ASSERT_EQ(Result::kSuccess,
            mrsPeerConnectionRegisterInteropCallbacks(pc2.handle(), &interop));

  // Setup signaling
  SdpCallback sdp1_cb(pc1.handle(), [&pc2](const char* type,
                                           const char* sdp_data) {
    ASSERT_EQ(Result::kSuccess, mrsPeerConnectionSetRemoteDescription(
                                           pc2.handle(), type, sdp_data));
    if (kOfferString == type) {
      ASSERT_EQ(Result::kSuccess,
                mrsPeerConnectionCreateAnswer(pc2.handle()));
    }
  });
  SdpCallback sdp2_cb(pc2.handle(), [&pc1](const char* type,
                                           const char* sdp_data) {
    ASSERT_EQ(Result::kSuccess, mrsPeerConnectionSetRemoteDescription(
                                           pc1.handle(), type, sdp_data));
    if (kOfferString == type) {
      ASSERT_EQ(Result::kSuccess,
                mrsPeerConnectionCreateAnswer(pc1.handle()));
    }
  });
  IceCallback ice1_cb(
      pc1.handle(),
      [&pc2](const char* candidate, int sdpMlineindex, const char* sdpMid) {
        ASSERT_EQ(Result::kSuccess,
                  mrsPeerConnectionAddIceCandidate(pc2.handle(), sdpMid,
                                                   sdpMlineindex, candidate));
      });
  IceCallback ice2_cb(
      pc2.handle(),
      [&pc1](const char* candidate, int sdpMlineindex, const char* sdpMid) {
        ASSERT_EQ(Result::kSuccess,
                  mrsPeerConnectionAddIceCandidate(pc1.handle(), sdpMid,
                                                   sdpMlineindex, candidate));
      });

  // Add dummy out-of-band data channel to force SCTP negotiating, otherwise
  // further data channel opening after connecting will fail.
  {
    mrsDataChannelConfig data_config{};
    data_config.id = 25;  // must be >= 0 for negotiated (out-of-band) channel
    data_config.label = "dummy_out_of_band";
    data_config.flags = mrsDataChannelConfigFlags::kOrdered |
                        mrsDataChannelConfigFlags::kReliable;
    mrsDataChannelCallbacks callbacks{};
    DataChannelHandle handle;
    mrsDataChannelInteropHandle interopHandle = kFakeInteropDataChannelHandle;
    ASSERT_EQ(Result::kSuccess,
              mrsPeerConnectionAddDataChannel(pc1.handle(), interopHandle,
                                              data_config, callbacks, &handle));
    ASSERT_EQ(Result::kSuccess,
              mrsPeerConnectionAddDataChannel(pc2.handle(), interopHandle,
                                              data_config, callbacks, &handle));
  }

  // Connect
  Event ev1, ev2;
  InteropCallback<> connectec1_cb([&ev1]() { ev1.Set(); });
  InteropCallback<> connectec2_cb([&ev2]() { ev2.Set(); });
  mrsPeerConnectionRegisterConnectedCallback(pc1.handle(), CB(connectec1_cb));
  connectec1_cb.is_registered_ = true;
  mrsPeerConnectionRegisterConnectedCallback(pc2.handle(), CB(connectec2_cb));
  connectec2_cb.is_registered_ = true;
  ASSERT_EQ(Result::kSuccess,
            mrsPeerConnectionCreateOffer(pc1.handle()));
  ASSERT_EQ(true, ev1.WaitFor(55s));  // should complete within 5s (usually ~1s)
  ASSERT_EQ(true, ev2.WaitFor(55s));

  // Register a callback on PC #2
  const std::string channel_label = "test data channel";
  Event data2_ev;
  DataAddedCallback data_added_cb =
      [&pc2, &data2_ev, &channel_label](
          mrsDataChannelInteropHandle data_channel_wrapper,
          DataChannelHandle data_channel) {
        ASSERT_EQ(kFakeInteropDataChannelHandle, data_channel_wrapper);
        auto data2 =
            (Microsoft::MixedReality::WebRTC::DataChannel*)data_channel;
        ASSERT_NE(nullptr, data2);
        ASSERT_EQ(channel_label, data2->label());
        data2_ev.Set();
      };
  mrsPeerConnectionRegisterDataChannelAddedCallback(pc2.handle(),
                                                    CB(data_added_cb));
  data_added_cb.is_registered_ = true;

  // Add a data channel on PC #1, which should get negotiated to PC #2
  {
    mrsDataChannelConfig data_config{};
    data_config.label = channel_label.c_str();
    data_config.flags = mrsDataChannelConfigFlags::kOrdered |
                        mrsDataChannelConfigFlags::kReliable;
    mrsDataChannelCallbacks callbacks{};
    DataChannelHandle data1_handle;
    mrsDataChannelInteropHandle interopHandle = kFakeInteropDataChannelHandle;
    ASSERT_EQ(
        Result::kSuccess,
        mrsPeerConnectionAddDataChannel(pc1.handle(), interopHandle,
                                        data_config, callbacks, &data1_handle));
    ASSERT_NE(nullptr, data1_handle);
    auto data1 = (Microsoft::MixedReality::WebRTC::DataChannel*)data1_handle;
    ASSERT_EQ(channel_label, data1->label());
    ASSERT_EQ(true, data2_ev.WaitFor(30s));

    // Clean-up
    mrsPeerConnectionRegisterConnectedCallback(pc1.handle(), nullptr, nullptr);
    connectec1_cb.is_registered_ = false;
    mrsPeerConnectionRegisterConnectedCallback(pc2.handle(), nullptr, nullptr);
    connectec2_cb.is_registered_ = false;
    mrsPeerConnectionRegisterIceCandidateReadytoSendCallback(pc1.handle(),
                                                             nullptr, nullptr);
    ice1_cb.is_registered_ = false;
    mrsPeerConnectionRegisterIceCandidateReadytoSendCallback(pc2.handle(),
                                                             nullptr, nullptr);
    ice2_cb.is_registered_ = false;
    mrsPeerConnectionRegisterDataChannelAddedCallback(pc2.handle(), nullptr,
                                                      nullptr);
    data_added_cb.is_registered_ = false;
    mrsPeerConnectionRegisterLocalSdpReadytoSendCallback(pc1.handle(), nullptr,
                                                         nullptr);
    sdp1_cb.is_registered_ = false;
    mrsPeerConnectionRegisterLocalSdpReadytoSendCallback(pc2.handle(), nullptr,
                                                         nullptr);
    sdp2_cb.is_registered_ = false;
  }
}

TEST(DataChannel, MultiThreadCreate) {
  PCRaii pc;
  constexpr int kNumThreads = 16;
  std::thread threads[kNumThreads];
  Event ev_start;
  for (std::thread& t : threads) {
    t = std::move(*new std::thread([&ev_start, &pc]() {
      ev_start.Wait();
      mrsDataChannelConfig config{};
      DataChannelHandle handle;
      ASSERT_EQ(Result::kSuccess,
                mrsPeerConnectionAddDataChannel(
                    pc.handle(), (mrsDataChannelInteropHandle)0x1, config, {},
                    &handle));
    }));
  }
  ev_start.SetBroadcast();
  for (std::thread& t : threads) {
    t.join();
  }
}

// NOTE - This test is flaky, relies on the send loop being faster than what the
// local
//        network can send, without setting any explicit congestion control etc.
//        so is prone to false errors. This is still useful for local testing.
//
// TEST(DataChannel, Buffering) {
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
//  DataChannelHandle handle1, handle2;
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
