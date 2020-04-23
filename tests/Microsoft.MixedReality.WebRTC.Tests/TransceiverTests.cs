// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using NUnit.Framework.Internal;

namespace Microsoft.MixedReality.WebRTC.Tests
{
    [TestFixture(SdpSemantic.PlanB, MediaKind.Audio)]
    [TestFixture(SdpSemantic.UnifiedPlan, MediaKind.Audio)]
    [TestFixture(SdpSemantic.PlanB, MediaKind.Video)]
    [TestFixture(SdpSemantic.UnifiedPlan, MediaKind.Video)]
    internal class TransceiverTests : PeerConnectionTestBase
    {
        private readonly MediaKind MediaKind;

        public TransceiverTests(SdpSemantic sdpSemantic, MediaKind mediaKind) : base(sdpSemantic)
        {
            MediaKind = mediaKind;
        }

        [Test]
        public async Task SetDirection()
        {
            // This test use manual offers
            suspendOffer1_ = true;

            // Create video transceiver on #1. This triggers a renegotiation needed event.
            var transceiver_settings = new TransceiverInitSettings
            {
                Name = "transceiver1",
            };
            var transceiver1 = pc1_.AddTransceiver(MediaKind, transceiver_settings);
            Assert.NotNull(transceiver1);
            Assert.AreEqual(transceiver1.DesiredDirection, Transceiver.Direction.SendReceive); // from implementation
            Assert.AreEqual(transceiver1.NegotiatedDirection, null);
            Assert.AreEqual(pc1_, transceiver1.PeerConnection);
            Assert.IsTrue(pc1_.Transceivers.Contains(transceiver1));

            // Wait for local SDP re-negotiation event on #1.
            // This will not create an offer, since we're not connected yet.
            Assert.True(renegotiationEvent1_.Wait(TimeSpan.FromSeconds(60.0)));
            renegotiationEvent1_.Reset();

            // Connect
            await DoNegotiationStartFrom(pc1_);

            // Note: use manual list instead of Enum.GetValues() to control order, and not
            // get Inactive first (which is the current value, so wouldn't make any change).
            var desired = new List<Transceiver.Direction> {
                Transceiver.Direction.SendOnly, Transceiver.Direction.SendReceive,
                Transceiver.Direction.ReceiveOnly, Transceiver.Direction.Inactive };
            var negotiated = new List<Transceiver.Direction> {
                Transceiver.Direction.SendOnly, Transceiver.Direction.SendOnly,
                Transceiver.Direction.Inactive, Transceiver.Direction.Inactive };
            for (int i = 0; i < desired.Count; ++i)
            {
                var direction = desired[i];

                // Change flow direction
                renegotiationEvent1_.Reset();
                transceiver1.DesiredDirection = direction;
                Assert.AreEqual(transceiver1.DesiredDirection, direction);

                // Wait for local SDP re-negotiation event on #1.
                Assert.True(renegotiationEvent1_.Wait(TimeSpan.FromSeconds(10.0)));
                renegotiationEvent1_.Reset();

                // Renegotiate
                await DoNegotiationStartFrom(pc1_);

                // Observe the new negotiated direction
                Assert.AreEqual(transceiver1.DesiredDirection, direction);
                Assert.AreEqual(transceiver1.NegotiatedDirection, negotiated[i]);
            }
        }

        [Test(Description = "Check that the transceiver stream IDs are correctly broadcast to the remote peer.")]
        public async Task StreamIDs()
        {
            // This test use manual offers
            suspendOffer1_ = true;

            // Create video transceiver on #1. This triggers a renegotiation needed event.
            string name1 = "video_feed";
            var initSettings = new TransceiverInitSettings
            {
                Name = name1,
                InitialDesiredDirection = Transceiver.Direction.SendOnly,
                StreamIDs = new List<string> { "id1", "id2" }
            };
            var transceiver1 = pc1_.AddTransceiver(MediaKind, initSettings);
            Assert.NotNull(transceiver1);
            // Names are equal only because the transceiver was created by the local peer
            Assert.AreEqual(name1, transceiver1.Name);
            Assert.AreEqual(2, transceiver1.StreamIDs.Length);
            Assert.AreEqual("id1", transceiver1.StreamIDs[0]);
            Assert.AreEqual("id2", transceiver1.StreamIDs[1]);
            Assert.AreEqual(transceiver1.DesiredDirection, Transceiver.Direction.SendOnly);
            Assert.AreEqual(transceiver1.NegotiatedDirection, null);
            Assert.AreEqual(pc1_, transceiver1.PeerConnection);
            Assert.IsTrue(pc1_.Transceivers.Contains(transceiver1));

            // Connect
            await DoNegotiationStartFrom(pc1_);

            // Find the remote transceiver
            Assert.AreEqual(1, pc2_.Transceivers.Count);
            var transceiver2 = pc2_.Transceivers[0];
            Assert.NotNull(transceiver2);

            // Check stream IDs were associated
            Assert.AreEqual(2, transceiver2.StreamIDs.Length);
            Assert.AreEqual("id1", transceiver2.StreamIDs[0]);
            Assert.AreEqual("id2", transceiver2.StreamIDs[1]);
        }

        [Test(Description = "#179 - Ensure AddTransceiver(mediaKind, null) works.")]
        public void AddTransceiver_Null()
        {
            var tr = pc1_.AddTransceiver(MediaKind, null);
            Assert.IsNotNull(tr);
        }

        [Test(Description = "#179 - Ensure AddTransceiver(mediaKind, default) works.")]
        public void AddTransceiver_Default()
        {
            var settings = new TransceiverInitSettings();
            var tr = pc1_.AddTransceiver(MediaKind, settings);
            Assert.IsNotNull(tr);
        }

        [Test]
        public void AddTransceiver_InvalidName()
        {
            var settings = new TransceiverInitSettings();
            settings.Name = "invalid name";
            Transceiver tr = null;
            Assert.Throws<ArgumentException>(() => { tr = pc1_.AddTransceiver(MediaKind, settings); });
            Assert.IsNull(tr);
        }
    }
}
