// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Internal;

namespace Microsoft.MixedReality.WebRTC.Tests
{
    [TestFixture(SdpSemantic.PlanB)]
    [TestFixture(SdpSemantic.UnifiedPlan)]
    internal class AudioTransceiverTests : PeerConnectionTestBase
    {
        public AudioTransceiverTests(SdpSemantic sdpSemantic) : base(sdpSemantic)
        {
        }

        [Test]
        public void SetDirection()
        {
            // This test use manual offers
            suspendOffer1_ = true;

            // Create video transceiver on #1. This triggers a renegotiation needed event.
            var transceiver_settings = new TransceiverInitSettings
            {
                Name = "transceiver1",
            };
            var transceiver1 = pc1_.AddAudioTransceiver(transceiver_settings);
            Assert.NotNull(transceiver1);
            Assert.AreEqual(transceiver1.DesiredDirection, Transceiver.Direction.SendReceive); // from implementation
            Assert.AreEqual(transceiver1.NegotiatedDirection, null);
            Assert.AreEqual(pc1_, transceiver1.PeerConnection);
            Assert.IsTrue(pc1_.Transceivers.Contains(transceiver1));
            Assert.IsNull(transceiver1.LocalTrack);
            Assert.IsNull(transceiver1.RemoteTrack);

            // Wait for local SDP re-negotiation event on #1.
            // This will not create an offer, since we're not connected yet.
            Assert.True(renegotiationEvent1_.Wait(TimeSpan.FromSeconds(60.0)));
            renegotiationEvent1_.Reset();

            // Connect
            Assert.True(pc1_.CreateOffer());
            WaitForSdpExchangeCompleted();
            Assert.True(pc1_.IsConnected);
            Assert.True(pc2_.IsConnected);

            // Wait for transceiver to finish updating before changing its direction
            Assert.True(remoteDescAppliedEvent1_.Wait(TimeSpan.FromSeconds(10.0)));
            remoteDescAppliedEvent1_.Reset();

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
                transceiver1.SetDirection(direction);
                Assert.AreEqual(transceiver1.DesiredDirection, direction);

                // Wait for local SDP re-negotiation event on #1.
                Assert.True(renegotiationEvent1_.Wait(TimeSpan.FromSeconds(10.0)));
                renegotiationEvent1_.Reset();

                // Renegotiate
                remoteDescAppliedEvent1_.Reset();
                Assert.True(pc1_.CreateOffer());
                WaitForSdpExchangeCompleted();
                Assert.True(remoteDescAppliedEvent1_.Wait(TimeSpan.FromSeconds(10.0)));
                remoteDescAppliedEvent1_.Reset();

                // Observe the new negotiated direction
                Assert.AreEqual(transceiver1.DesiredDirection, direction);
                Assert.AreEqual(transceiver1.NegotiatedDirection, negotiated[i]);
            }
        }

        [Test(Description = "Check that the transceiver name is correctly broadcast to the remote peer when the remote transceiver is automatically created.")]
        public void PairingNameAuto()
        {
            // This test use manual offers
            suspendOffer1_ = true;

            // Create video transceiver on #1. This triggers a renegotiation needed event.
            string pairingName = "audio_feed";
            var initSettings = new TransceiverInitSettings
            {
                Name = pairingName,
                InitialDesiredDirection = Transceiver.Direction.SendOnly,
                StreamIDs = new List<string> { "id1", "id2" } // dummy
            };
            var transceiver1 = pc1_.AddAudioTransceiver(initSettings);
            Assert.NotNull(transceiver1);
            Assert.AreEqual(pairingName, transceiver1.Name);
            Assert.AreEqual(transceiver1.DesiredDirection, Transceiver.Direction.SendOnly);
            Assert.AreEqual(transceiver1.NegotiatedDirection, null);
            Assert.AreEqual(pc1_, transceiver1.PeerConnection);
            Assert.IsTrue(pc1_.Transceivers.Contains(transceiver1));
            Assert.IsNull(transceiver1.LocalTrack);
            Assert.IsNull(transceiver1.RemoteTrack);

            // Wait for local SDP re-negotiation event on #1.
            // This will not create an offer, since we're not connected yet.
            Assert.True(renegotiationEvent1_.Wait(TimeSpan.FromSeconds(60.0)));
            renegotiationEvent1_.Reset();

            // Connect
            Assert.True(pc1_.CreateOffer());
            WaitForSdpExchangeCompleted();
            Assert.True(pc1_.IsConnected);
            Assert.True(pc2_.IsConnected);

            // Wait for transceiver to finish updating before changing its direction
            Assert.True(remoteDescAppliedEvent1_.Wait(TimeSpan.FromSeconds(10.0)));
            remoteDescAppliedEvent1_.Reset();

            // Find the remote transceiver
            Assert.AreEqual(1, pc2_.Transceivers.Count);
            var transceiver2 = pc2_.Transceivers[0];
            Assert.NotNull(transceiver2);

            // Check name was associated
            Assert.AreEqual(pairingName, transceiver2.Name);
        }

        [Test(Description = "#179 - Ensure AddAudioTransceiver(null) works.")]
        public void AddAudioTransceiver_Null()
        {
            var tr = pc1_.AddAudioTransceiver();
            Assert.IsNotNull(tr);
        }

        [Test(Description = "#179 - Ensure AddAudioTransceiver(default) works.")]
        public void AddAudioTransceiver_Default()
        {
            var settings = new TransceiverInitSettings();
            var tr = pc1_.AddAudioTransceiver(settings);
            Assert.IsNotNull(tr);
        }

        [Test]
        public void AddAudioTransceiver_InvalidName()
        {
            var settings = new TransceiverInitSettings();
            settings.Name = "invalid name";
            AudioTransceiver tr = null;
            Assert.Throws<ArgumentException>(() => { tr = pc1_.AddAudioTransceiver(settings); });
            Assert.IsNull(tr);
        }
    }
}
