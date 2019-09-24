// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#include "pch.h"

#include "api.h"
#include "data_channel.h"

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
    Callback<mrsDataChannelInteropHandle, DataChannelHandle>;

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
  ASSERT_EQ(MRS_SUCCESS,
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
  ASSERT_EQ(MRS_SUCCESS,
            mrsPeerConnectionRegisterInteropCallbacks(pc2.handle(), &interop));

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

  // Add dummt out-of-band data channel to force SCTP negotiating, otherwise
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
    ASSERT_EQ(MRS_SUCCESS,
              mrsPeerConnectionAddDataChannel(pc1.handle(), interopHandle,
                                              data_config, callbacks, &handle));
    ASSERT_EQ(MRS_SUCCESS,
              mrsPeerConnectionAddDataChannel(pc2.handle(), interopHandle,
                                              data_config, callbacks, &handle));
  }

  // Connect
  Event ev1, ev2;
  Callback<> connectec1_cb([&ev1]() { ev1.Set(); });
  Callback<> connectec2_cb([&ev2]() { ev2.Set(); });
  mrsPeerConnectionRegisterConnectedCallback(pc1.handle(), CB(connectec1_cb));
  connectec1_cb.is_registered_ = true;
  mrsPeerConnectionRegisterConnectedCallback(pc2.handle(), CB(connectec2_cb));
  connectec2_cb.is_registered_ = true;
  ASSERT_EQ(MRS_SUCCESS, mrsPeerConnectionCreateOffer(pc1.handle()));
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
    ASSERT_EQ(MRS_SUCCESS, mrsPeerConnectionAddDataChannel(
                               pc1.handle(), interopHandle, data_config,
                               callbacks, &data1_handle));
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
