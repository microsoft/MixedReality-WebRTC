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
    [TestFixture(SdpSemantic.PlanB)]
    [TestFixture(SdpSemantic.UnifiedPlan)]
    internal class DataChannelTests : PeerConnectionTestBase
    {
        public DataChannelTests(SdpSemantic sdpSemantic) : base(sdpSemantic)
        {
        }

        [Test]
        public async Task InBand()
        {
            // Disable auto-renegotiation
            suspendOffer1_ = true;
            suspendOffer2_ = true;

            // Add dummy out-of-band data channel to force SCTP negotiating.
            // Otherwise after connecting AddDataChannelAsync() will fail.
            await pc1_.AddDataChannelAsync(42, "dummy", false, false);
            await pc2_.AddDataChannelAsync(42, "dummy", false, false);
            Assert.True(renegotiationEvent1_.Wait(TimeSpan.FromSeconds(60.0)));
            Assert.True(renegotiationEvent2_.Wait(TimeSpan.FromSeconds(60.0)));
            renegotiationEvent1_.Reset();
            renegotiationEvent2_.Reset();

            // Connect
            StartOfferWith(pc1_);
            WaitForSdpExchangeCompleted();

            // Ensure auto-renegotiation is active
            suspendOffer1_ = false;
            suspendOffer2_ = false;

            // Negotiate data channel in-band
            DataChannel data1 = null;
            DataChannel data2 = null;
            {
                var c2 = new ManualResetEventSlim(false);
                pc2_.DataChannelAdded += (DataChannel channel) =>
                {
                    data2 = channel;
                    c2.Set();
                };
                // Note that for SCTP data channels (always the case in MixedReality-WebRTC) a renegotiation
                // needed event is triggered only on the first data channel created. Since dummy channels were
                // added above to trigger SCTP handshake, this one below will not trigger a renegotiation event.
                data1 = await pc1_.AddDataChannelAsync("test_data_channel", ordered: true, reliable: true);
                Assert.IsNotNull(data1);
                Assert.True(c2.Wait(TimeSpan.FromSeconds(60.0)));
                Assert.IsNotNull(data2);
                Assert.AreEqual(data1.ID, data2.ID);
                Assert.AreEqual(data1.Label, data2.Label);
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
        }

        [Test]
        public async Task OutOfBand()
        {
            // Disable auto-renegotiation
            suspendOffer1_ = true;
            suspendOffer2_ = true;

            // Add out-of-band data channels
            const int DataChannelId = 42;
            DataChannel data1 = await pc1_.AddDataChannelAsync(DataChannelId, "my_channel", ordered: true, reliable: true);
            DataChannel data2 = await pc2_.AddDataChannelAsync(DataChannelId, "my_channel", ordered: true, reliable: true);

            // Check consistency
            Assert.AreEqual(data1.ID, data2.ID);
            Assert.AreEqual(data1.Label, data2.Label);

            // Prepare for state change
            var evOpen1 = new ManualResetEventSlim(initialState: false);
            data1.StateChanged += () =>
            {
                if (data1.State == DataChannel.ChannelState.Open)
                {
                    evOpen1.Set();
                }
            };
            var evOpen2 = new ManualResetEventSlim(initialState: false);
            data2.StateChanged += () =>
            {
                if (data2.State == DataChannel.ChannelState.Open)
                {
                    evOpen2.Set();
                }
            };

            // Connect
            StartOfferWith(pc1_);
            WaitForSdpExchangeCompleted();

            // Wait until the data channels are ready
            Assert.True(evOpen1.Wait(TimeSpan.FromSeconds(60.0)));
            Assert.True(evOpen2.Wait(TimeSpan.FromSeconds(60.0)));

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
        }

        [Test]
        public void SctpError()
        {
            // Connect
            StartOfferWith(pc1_);
            WaitForSdpExchangeCompleted();

            // Try to add a data channel. This should fail because SCTP was not negotiated.
            Assert.ThrowsAsync<SctpNotNegotiatedException>(async () => await pc1_.AddDataChannelAsync("dummy", false, false));
            Assert.ThrowsAsync<SctpNotNegotiatedException>(async () => await pc1_.AddDataChannelAsync(42, "dummy", false, false));
        }
    }
}
