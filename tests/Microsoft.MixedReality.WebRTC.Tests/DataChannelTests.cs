// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using NUnit.Framework.Internal;

namespace Microsoft.MixedReality.WebRTC.Tests
{
    [TestFixture]
    internal class DataChannelTests
    {
        [Test]
        public async Task InBand()
        {
            // Setup
            var config = new PeerConnectionConfiguration();
            var pc1 = new PeerConnection();
            var pc2 = new PeerConnection();
            await pc1.InitializeAsync(config);
            await pc2.InitializeAsync(config);
            pc1.LocalSdpReadytoSend += async (string type, string sdp) =>
            {
                await pc2.SetRemoteDescriptionAsync(type, sdp);
                if (type == "offer")
                    pc2.CreateAnswer();
            };
            pc2.LocalSdpReadytoSend += async (string type, string sdp) =>
            {
                await pc1.SetRemoteDescriptionAsync(type, sdp);
                if (type == "offer")
                    pc1.CreateAnswer();
            };
            pc1.IceCandidateReadytoSend += (string candidate, int sdpMlineindex, string sdpMid)
                => pc2.AddIceCandidate(sdpMid, sdpMlineindex, candidate);
            pc2.IceCandidateReadytoSend += (string candidate, int sdpMlineindex, string sdpMid)
                => pc1.AddIceCandidate(sdpMid, sdpMlineindex, candidate);

            // Add dummy out-of-band data channel to force SCTP negotiating.
            // Otherwise after connecting AddDataChannelAsync() will fail.
            await pc1.AddDataChannelAsync(42, "dummy", false, false);
            await pc2.AddDataChannelAsync(42, "dummy", false, false);

            // Connect
            {
                var c1 = new ManualResetEventSlim(false);
                var c2 = new ManualResetEventSlim(false);
                pc1.Connected += () => c1.Set();
                pc2.Connected += () => c2.Set();
                Assert.True(pc1.CreateOffer());
                Assert.True(c1.Wait(TimeSpan.FromSeconds(60.0)));
                Assert.True(c2.Wait(TimeSpan.FromSeconds(60.0)));
                Assert.True(pc1.IsConnected);
                Assert.True(pc1.IsConnected);
            }

            // Negotiate data channel in-band
            DataChannel data1 = null;
            DataChannel data2 = null;
            {
                var c2 = new ManualResetEventSlim(false);
                pc2.DataChannelAdded += (DataChannel channel) =>
                {
                    data2 = channel;
                    c2.Set();
                };
                data1 = await pc1.AddDataChannelAsync("test_data_channel", true, true);
                Assert.IsNotNull(data1);
                Assert.True(c2.Wait(TimeSpan.FromSeconds(60.0)));
                Assert.IsNotNull(data2);
                Assert.AreEqual(data1.Label, data2.Label);
                // Do not test DataChannel.ID; at this point for in-band channels the ID has not
                // been agreed upon with the remote peer yet.
            }

            // Send data
            {
                var c2 = new ManualResetEventSlim(false);
                string sentText = "Some sample text";
                byte[] msg = Encoding.UTF8.GetBytes(sentText);
                data2.MessageReceived += (byte[] _msg) =>
                {
                    var receivedText = Encoding.UTF8.GetString(_msg);
                    Assert.AreEqual(sentText, receivedText);
                    c2.Set();
                };
                data1.SendMessage(msg);
                Assert.True(c2.Wait(TimeSpan.FromSeconds(60.0)));
            }

            // Clean-up
            pc1.Close();
            pc1.Dispose();
            pc2.Close();
            pc2.Dispose();
        }

        [Test]
        public async Task SctpError()
        {
            // Setup
            var config = new PeerConnectionConfiguration();
            var pc1 = new PeerConnection();
            var pc2 = new PeerConnection();
            await pc1.InitializeAsync(config);
            await pc2.InitializeAsync(config);
            pc1.LocalSdpReadytoSend += async (string type, string sdp) =>
            {
                await pc2.SetRemoteDescriptionAsync(type, sdp);
                if (type == "offer")
                    pc2.CreateAnswer();
            };
            pc2.LocalSdpReadytoSend += async (string type, string sdp) =>
            {
                await pc1.SetRemoteDescriptionAsync(type, sdp);
                if (type == "offer")
                    pc1.CreateAnswer();
            };
            pc1.IceCandidateReadytoSend += (string candidate, int sdpMlineindex, string sdpMid)
                => pc2.AddIceCandidate(sdpMid, sdpMlineindex, candidate);
            pc2.IceCandidateReadytoSend += (string candidate, int sdpMlineindex, string sdpMid)
                => pc1.AddIceCandidate(sdpMid, sdpMlineindex, candidate);

            // Connect
            {
                var c1 = new ManualResetEventSlim(false);
                var c2 = new ManualResetEventSlim(false);
                pc1.Connected += () => c1.Set();
                pc2.Connected += () => c2.Set();
                Assert.True(pc1.CreateOffer());
                Assert.True(c1.Wait(TimeSpan.FromSeconds(60.0)));
                Assert.True(c2.Wait(TimeSpan.FromSeconds(60.0)));
                Assert.True(pc1.IsConnected);
                Assert.True(pc1.IsConnected);
            }

            // Try to add a data channel. This should fail because SCTP was not negotiated.
            Assert.ThrowsAsync<SctpNotNegotiatedException>(async () => await pc1.AddDataChannelAsync("dummy", false, false));
            Assert.ThrowsAsync<SctpNotNegotiatedException>(async () => await pc1.AddDataChannelAsync(42, "dummy", false, false));

            // Clean-up
            pc1.Close();
            pc1.Dispose();
            pc2.Close();
            pc2.Dispose();
        }
    }
}
