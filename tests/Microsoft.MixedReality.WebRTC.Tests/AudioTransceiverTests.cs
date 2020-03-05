// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using NUnit.Framework.Internal;

namespace Microsoft.MixedReality.WebRTC.Tests
{
    [TestFixture]
    internal class AudioTransceiverTests
    {
        PeerConnection pc1_ = null;
        PeerConnection pc2_ = null;
        ManualResetEventSlim connectedEvent1_ = null;
        ManualResetEventSlim connectedEvent2_ = null;
        ManualResetEventSlim remoteDescAppliedEvent1_ = null;
        ManualResetEventSlim remoteDescAppliedEvent2_ = null;
        ManualResetEventSlim renegotiationEvent1_ = null;
        ManualResetEventSlim renegotiationEvent2_ = null;
        ManualResetEventSlim audioTrackAddedEvent1_ = null;
        ManualResetEventSlim audioTrackAddedEvent2_ = null;
        ManualResetEventSlim audioTrackRemovedEvent1_ = null;
        ManualResetEventSlim audioTrackRemovedEvent2_ = null;
        ManualResetEventSlim videoTrackAddedEvent1_ = null;
        ManualResetEventSlim videoTrackAddedEvent2_ = null;
        ManualResetEventSlim videoTrackRemovedEvent1_ = null;
        ManualResetEventSlim videoTrackRemovedEvent2_ = null;
        ManualResetEventSlim iceConnectedEvent1_ = null;
        ManualResetEventSlim iceConnectedEvent2_ = null;
        bool suspendOffer1_ = false;
        bool suspendOffer2_ = false;

        [SetUp]
        public void SetupConnection()
        {
            // Create the 2 peers
            var config = new PeerConnectionConfiguration();
            pc1_ = new PeerConnection();
            pc2_ = new PeerConnection();
            pc1_.InitializeAsync(config).Wait(); // cannot use async/await in OneTimeSetUp
            pc2_.InitializeAsync(config).Wait();

            // Allocate callback events
            connectedEvent1_ = new ManualResetEventSlim(false);
            connectedEvent2_ = new ManualResetEventSlim(false);
            remoteDescAppliedEvent1_ = new ManualResetEventSlim(false);
            remoteDescAppliedEvent2_ = new ManualResetEventSlim(false);
            iceConnectedEvent1_ = new ManualResetEventSlim(false);
            iceConnectedEvent2_ = new ManualResetEventSlim(false);
            renegotiationEvent1_ = new ManualResetEventSlim(false);
            renegotiationEvent2_ = new ManualResetEventSlim(false);
            audioTrackAddedEvent1_ = new ManualResetEventSlim(false);
            audioTrackAddedEvent2_ = new ManualResetEventSlim(false);
            audioTrackRemovedEvent1_ = new ManualResetEventSlim(false);
            audioTrackRemovedEvent2_ = new ManualResetEventSlim(false);
            videoTrackAddedEvent1_ = new ManualResetEventSlim(false);
            videoTrackAddedEvent2_ = new ManualResetEventSlim(false);
            videoTrackRemovedEvent1_ = new ManualResetEventSlim(false);
            videoTrackRemovedEvent2_ = new ManualResetEventSlim(false);

            // Connect all signals
            pc1_.Connected += OnConnected1;
            pc2_.Connected += OnConnected2;
            pc1_.LocalSdpReadytoSend += OnLocalSdpReady1;
            pc2_.LocalSdpReadytoSend += OnLocalSdpReady2;
            pc1_.IceCandidateReadytoSend += OnIceCandidateReadytoSend1;
            pc2_.IceCandidateReadytoSend += OnIceCandidateReadytoSend2;
            pc1_.IceStateChanged += OnIceStateChanged1;
            pc2_.IceStateChanged += OnIceStateChanged2;
            pc1_.RenegotiationNeeded += OnRenegotiationNeeded1;
            pc2_.RenegotiationNeeded += OnRenegotiationNeeded2;
            pc1_.AudioTrackAdded += OnAudioTrackAdded1;
            pc2_.AudioTrackAdded += OnAudioTrackAdded2;
            pc1_.AudioTrackRemoved += OnAudioTrackRemoved1;
            pc2_.AudioTrackRemoved += OnAudioTrackRemoved2;
            pc1_.VideoTrackAdded += OnVideoTrackAdded1;
            pc2_.VideoTrackAdded += OnVideoTrackAdded2;
            pc1_.VideoTrackRemoved += OnVideoTrackRemoved1;
            pc2_.VideoTrackRemoved += OnVideoTrackRemoved2;
        }

        [TearDown]
        public void TearDownConnectin()
        {
            // Unregister all callbacks
            pc1_.LocalSdpReadytoSend -= OnLocalSdpReady1;
            pc2_.LocalSdpReadytoSend -= OnLocalSdpReady2;
            pc1_.IceCandidateReadytoSend -= OnIceCandidateReadytoSend1;
            pc2_.IceCandidateReadytoSend -= OnIceCandidateReadytoSend2;
            pc1_.IceStateChanged -= OnIceStateChanged1;
            pc2_.IceStateChanged -= OnIceStateChanged2;
            pc1_.RenegotiationNeeded -= OnRenegotiationNeeded1;
            pc2_.RenegotiationNeeded -= OnRenegotiationNeeded2;
            pc1_.AudioTrackAdded -= OnAudioTrackAdded1;
            pc2_.AudioTrackAdded -= OnAudioTrackAdded2;
            pc1_.AudioTrackRemoved -= OnAudioTrackRemoved1;
            pc2_.AudioTrackRemoved -= OnAudioTrackRemoved2;
            pc1_.VideoTrackAdded -= OnVideoTrackAdded1;
            pc2_.VideoTrackAdded -= OnVideoTrackAdded2;
            pc1_.VideoTrackRemoved -= OnVideoTrackRemoved1;
            pc2_.VideoTrackRemoved -= OnVideoTrackRemoved2;

            // Clean-up callback events
            audioTrackAddedEvent1_.Dispose();
            audioTrackAddedEvent1_ = null;
            audioTrackRemovedEvent1_.Dispose();
            audioTrackRemovedEvent1_ = null;
            audioTrackAddedEvent2_.Dispose();
            audioTrackAddedEvent2_ = null;
            audioTrackRemovedEvent2_.Dispose();
            audioTrackRemovedEvent2_ = null;
            videoTrackAddedEvent1_.Dispose();
            videoTrackAddedEvent1_ = null;
            videoTrackRemovedEvent1_.Dispose();
            videoTrackRemovedEvent1_ = null;
            videoTrackAddedEvent2_.Dispose();
            videoTrackAddedEvent2_ = null;
            videoTrackRemovedEvent2_.Dispose();
            videoTrackRemovedEvent2_ = null;
            renegotiationEvent1_.Dispose();
            renegotiationEvent1_ = null;
            renegotiationEvent2_.Dispose();
            renegotiationEvent2_ = null;
            iceConnectedEvent1_.Dispose();
            iceConnectedEvent1_ = null;
            iceConnectedEvent2_.Dispose();
            iceConnectedEvent2_ = null;
            remoteDescAppliedEvent1_.Dispose();
            remoteDescAppliedEvent1_ = null;
            remoteDescAppliedEvent2_.Dispose();
            remoteDescAppliedEvent2_ = null;
            connectedEvent1_.Dispose();
            connectedEvent1_ = null;
            connectedEvent2_.Dispose();
            connectedEvent2_ = null;

            // Destroy peers
            pc1_.Close();
            pc1_.Dispose();
            pc1_ = null;
            pc2_.Close();
            pc2_.Dispose();
            pc2_ = null;
        }

        private void OnConnected1()
        {
            connectedEvent1_.Set();
        }

        private void OnConnected2()
        {
            connectedEvent2_.Set();
        }

        private async void OnLocalSdpReady1(string type, string sdp)
        {
            await pc2_.SetRemoteDescriptionAsync(type, sdp);
            remoteDescAppliedEvent2_.Set();
            if (type == "offer")
            {
                pc2_.CreateAnswer();
            }
        }

        private async void OnLocalSdpReady2(string type, string sdp)
        {
            await pc1_.SetRemoteDescriptionAsync(type, sdp);
            remoteDescAppliedEvent1_.Set();
            if (type == "offer")
            {
                pc1_.CreateAnswer();
            }
        }

        private void OnIceCandidateReadytoSend1(string candidate, int sdpMlineindex, string sdpMid)
        {
            pc2_.AddIceCandidate(sdpMid, sdpMlineindex, candidate);
        }

        private void OnIceCandidateReadytoSend2(string candidate, int sdpMlineindex, string sdpMid)
        {
            pc1_.AddIceCandidate(sdpMid, sdpMlineindex, candidate);
        }

        private void OnRenegotiationNeeded1()
        {
            renegotiationEvent1_.Set();
            if (pc1_.IsConnected && !suspendOffer1_)
            {
                pc1_.CreateOffer();
            }
        }

        private void OnRenegotiationNeeded2()
        {
            renegotiationEvent2_.Set();
            if (pc2_.IsConnected && !suspendOffer2_)
            {
                pc2_.CreateOffer();
            }
        }

        private void OnAudioTrackAdded1(RemoteAudioTrack track)
        {
            audioTrackAddedEvent1_.Set();
        }

        private void OnAudioTrackAdded2(RemoteAudioTrack track)
        {
            audioTrackAddedEvent2_.Set();
        }

        private void OnAudioTrackRemoved1(AudioTransceiver transceiver, RemoteAudioTrack track)
        {
            audioTrackRemovedEvent1_.Set();
        }

        private void OnAudioTrackRemoved2(AudioTransceiver transceiver, RemoteAudioTrack track)
        {
            audioTrackRemovedEvent2_.Set();
        }

        private void OnVideoTrackAdded1(RemoteVideoTrack track)
        {
            videoTrackAddedEvent1_.Set();
        }

        private void OnVideoTrackAdded2(RemoteVideoTrack track)
        {
            videoTrackAddedEvent2_.Set();
        }

        private void OnVideoTrackRemoved1(VideoTransceiver transceiver, RemoteVideoTrack track)
        {
            videoTrackRemovedEvent1_.Set();
        }

        private void OnVideoTrackRemoved2(VideoTransceiver transceiver, RemoteVideoTrack track)
        {
            videoTrackRemovedEvent2_.Set();
        }

        private void OnIceStateChanged1(IceConnectionState newState)
        {
            if ((newState == IceConnectionState.Connected) || (newState == IceConnectionState.Completed))
            {
                iceConnectedEvent1_.Set();
            }
        }

        private void OnIceStateChanged2(IceConnectionState newState)
        {
            if ((newState == IceConnectionState.Connected) || (newState == IceConnectionState.Completed))
            {
                iceConnectedEvent2_.Set();
            }
        }

        private void WaitForSdpExchangeCompleted()
        {
            Assert.True(connectedEvent1_.Wait(TimeSpan.FromSeconds(60.0)));
            Assert.True(connectedEvent2_.Wait(TimeSpan.FromSeconds(60.0)));
            connectedEvent1_.Reset();
            connectedEvent2_.Reset();
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
