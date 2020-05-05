// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Microsoft.MixedReality.WebRTC.Tests
{
    /// <summary>
    /// Base class for most C# tests.
    /// 
    /// This helper class provides several features:
    /// - Allows testing the various SDP semantics.
    /// - Ensures there is no live object before AND after the test.
    /// - Sets up and tears down the various event handlers for the most
    ///   common events, to avoid duplication in all tests.
    /// </summary>
    internal class PeerConnectionTestBase
    {
        /// <summary>
        /// SDP semantic for the current test case.
        /// </summary>
        protected readonly SdpSemantic sdpSemantic_;

        protected PeerConnection pc1_ = null;
        protected PeerConnection pc2_ = null;
        protected bool exchangePending_ = false;
        protected ManualResetEventSlim exchangeCompleted_ = null;
        protected ManualResetEventSlim connectedEvent1_ = null;
        protected ManualResetEventSlim connectedEvent2_ = null;
        protected ManualResetEventSlim remoteDescAppliedEvent1_ = null;
        protected ManualResetEventSlim remoteDescAppliedEvent2_ = null;
        protected ManualResetEventSlim renegotiationEvent1_ = null;
        protected ManualResetEventSlim renegotiationEvent2_ = null;
        protected ManualResetEventSlim dataChannelAddedEvent1_ = null;
        protected ManualResetEventSlim dataChannelAddedEvent2_ = null;
        protected ManualResetEventSlim dataChannelRemovedEvent1_ = null;
        protected ManualResetEventSlim dataChannelRemovedEvent2_ = null;
        protected ManualResetEventSlim audioTrackAddedEvent1_ = null;
        protected ManualResetEventSlim audioTrackAddedEvent2_ = null;
        protected ManualResetEventSlim audioTrackRemovedEvent1_ = null;
        protected ManualResetEventSlim audioTrackRemovedEvent2_ = null;
        protected ManualResetEventSlim videoTrackAddedEvent1_ = null;
        protected ManualResetEventSlim videoTrackAddedEvent2_ = null;
        protected ManualResetEventSlim videoTrackRemovedEvent1_ = null;
        protected ManualResetEventSlim videoTrackRemovedEvent2_ = null;
        protected ManualResetEventSlim iceConnectedEvent1_ = null;
        protected ManualResetEventSlim iceConnectedEvent2_ = null;
        protected bool suspendOffer1_ = false;
        protected bool suspendOffer2_ = false;

        public PeerConnectionTestBase(SdpSemantic sdpSemantic)
        {
            sdpSemantic_ = sdpSemantic;
        }

        [SetUp]
        public void SetupConnection()
        {
            Assert.AreEqual(0, Library.ReportLiveObjects());

            // Create the 2 peers
            var config = new PeerConnectionConfiguration();
            config.SdpSemantic = sdpSemantic_;
            pc1_ = new PeerConnection();
            pc2_ = new PeerConnection();
            pc1_.InitializeAsync(config).Wait(); // cannot use async/await in OneTimeSetUp
            pc2_.InitializeAsync(config).Wait();

            exchangePending_ = false;
            exchangeCompleted_ = new ManualResetEventSlim(false);

            // Allocate callback events
            connectedEvent1_ = new ManualResetEventSlim(false);
            connectedEvent2_ = new ManualResetEventSlim(false);
            remoteDescAppliedEvent1_ = new ManualResetEventSlim(false);
            remoteDescAppliedEvent2_ = new ManualResetEventSlim(false);
            iceConnectedEvent1_ = new ManualResetEventSlim(false);
            iceConnectedEvent2_ = new ManualResetEventSlim(false);
            renegotiationEvent1_ = new ManualResetEventSlim(false);
            renegotiationEvent2_ = new ManualResetEventSlim(false);
            dataChannelAddedEvent1_ = new ManualResetEventSlim(false);
            dataChannelAddedEvent2_ = new ManualResetEventSlim(false);
            dataChannelRemovedEvent1_ = new ManualResetEventSlim(false);
            dataChannelRemovedEvent2_ = new ManualResetEventSlim(false);
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
            pc1_.DataChannelAdded += OnDataChannelAdded1;
            pc2_.DataChannelAdded += OnDataChannelAdded2;
            pc1_.DataChannelRemoved += OnDataChannelRemoved1;
            pc2_.DataChannelRemoved += OnDataChannelRemoved2;
            pc1_.AudioTrackAdded += OnAudioTrackAdded1;
            pc2_.AudioTrackAdded += OnAudioTrackAdded2;
            pc1_.AudioTrackRemoved += OnAudioTrackRemoved1;
            pc2_.AudioTrackRemoved += OnAudioTrackRemoved2;
            pc1_.VideoTrackAdded += OnVideoTrackAdded1;
            pc2_.VideoTrackAdded += OnVideoTrackAdded2;
            pc1_.VideoTrackRemoved += OnVideoTrackRemoved1;
            pc2_.VideoTrackRemoved += OnVideoTrackRemoved2;

            // Enable automatic renegotiation
            suspendOffer1_ = false;
            suspendOffer2_ = false;
        }

        [TearDown]
        public void TearDownConnection()
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
            pc1_.DataChannelAdded -= OnDataChannelAdded1;
            pc2_.DataChannelAdded -= OnDataChannelAdded2;
            pc1_.DataChannelRemoved -= OnDataChannelRemoved1;
            pc2_.DataChannelRemoved -= OnDataChannelRemoved2;
            pc1_.AudioTrackAdded -= OnAudioTrackAdded1;
            pc2_.AudioTrackAdded -= OnAudioTrackAdded2;
            pc1_.AudioTrackRemoved -= OnAudioTrackRemoved1;
            pc2_.AudioTrackRemoved -= OnAudioTrackRemoved2;
            pc1_.VideoTrackAdded -= OnVideoTrackAdded1;
            pc2_.VideoTrackAdded -= OnVideoTrackAdded2;
            pc1_.VideoTrackRemoved -= OnVideoTrackRemoved1;
            pc2_.VideoTrackRemoved -= OnVideoTrackRemoved2;

            Assert.IsFalse(exchangePending_);
            exchangeCompleted_.Dispose();
            exchangeCompleted_ = null;

            // Clean-up callback events
            dataChannelAddedEvent1_.Dispose();
            dataChannelAddedEvent1_ = null;
            dataChannelRemovedEvent1_.Dispose();
            dataChannelRemovedEvent1_ = null;
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

            Assert.AreEqual(0, Library.ReportLiveObjects());
        }

        /// <summary>
        /// Start an SDP exchange by sending an offer from the given peer.
        /// </summary>
        /// <param name="offeringPeer">The peer to call <see cref="PeerConnection.CreateOffer"/> on.</param>
        protected void StartOfferWith(PeerConnection offeringPeer)
        {
            Assert.IsFalse(exchangePending_);
            exchangePending_ = true;
            exchangeCompleted_.Reset();
            connectedEvent1_.Reset();
            connectedEvent2_.Reset();
            Assert.IsTrue(offeringPeer.CreateOffer());
        }

        /// <summary>
        /// Wait until transports are writable. This is not the end of the SDP
        /// exchange, but transceivers are starting to send/receive. The offer
        /// was accepted, but the offering peer has yet to receive and apply an
        /// SDP answer though.
        /// </summary>
        protected void WaitForTransportsWritable()
        {
            Assert.IsTrue(exchangePending_);
            Assert.IsTrue(connectedEvent1_.Wait(TimeSpan.FromSeconds(60.0)));
            Assert.IsTrue(connectedEvent2_.Wait(TimeSpan.FromSeconds(60.0)));
            connectedEvent1_.Reset();
            connectedEvent2_.Reset();
        }

        /// <summary>
        /// Wait until the SDP exchange finally completed and the answer has been
        /// applied back on the offering peer.
        /// </summary>
        protected void WaitForSdpExchangeCompleted()
        {
            Assert.IsTrue(exchangeCompleted_.Wait(TimeSpan.FromSeconds(60.0)));
            Assert.IsFalse(exchangePending_);
            exchangeCompleted_.Reset();
        }

        protected Task DoNegotiationStartFrom(PeerConnection offeringPeer)
        {
            StartOfferWith(offeringPeer);
            return Task.Run(() => { WaitForSdpExchangeCompleted(); });
        }

        private void OnConnected1()
        {
            connectedEvent1_.Set();
        }

        private void OnConnected2()
        {
            connectedEvent2_.Set();
        }

        private async void OnLocalSdpReady1(SdpMessage message)
        {
            Assert.IsTrue(exchangePending_);
            await pc2_.SetRemoteDescriptionAsync(message);
            remoteDescAppliedEvent2_.Set();
            if (message.Type == SdpMessageType.Offer)
            {
                pc2_.CreateAnswer();
            }
            else
            {
                exchangePending_ = false;
                exchangeCompleted_.Set();
            }
        }

        private async void OnLocalSdpReady2(SdpMessage message)
        {
            Assert.IsTrue(exchangePending_);
            await pc1_.SetRemoteDescriptionAsync(message);
            remoteDescAppliedEvent1_.Set();
            if (message.Type == SdpMessageType.Offer)
            {
                pc1_.CreateAnswer();
            }
            else
            {
                exchangePending_ = false;
                exchangeCompleted_.Set();
            }
        }

        private void OnIceCandidateReadytoSend1(IceCandidate candidate)
        {
            pc2_.AddIceCandidate(candidate);
        }

        private void OnIceCandidateReadytoSend2(IceCandidate candidate)
        {
            pc1_.AddIceCandidate(candidate);
        }

        private void OnRenegotiationNeeded1()
        {
            renegotiationEvent1_.Set();
            if (pc1_.IsConnected && !suspendOffer1_)
            {
                StartOfferWith(pc1_);
            }
        }

        private void OnRenegotiationNeeded2()
        {
            renegotiationEvent2_.Set();
            if (pc2_.IsConnected && !suspendOffer2_)
            {
                StartOfferWith(pc2_);
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

        private void OnAudioTrackRemoved1(Transceiver transceiver, RemoteAudioTrack track)
        {
            audioTrackRemovedEvent1_.Set();
        }

        private void OnAudioTrackRemoved2(Transceiver transceiver, RemoteAudioTrack track)
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

        private void OnVideoTrackRemoved1(Transceiver transceiver, RemoteVideoTrack track)
        {
            videoTrackRemovedEvent1_.Set();
        }

        private void OnVideoTrackRemoved2(Transceiver transceiver, RemoteVideoTrack track)
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

        private void OnDataChannelAdded1(DataChannel channel)
        {
            dataChannelAddedEvent1_.Set();
        }

        private void OnDataChannelAdded2(DataChannel channel)
        {
            dataChannelAddedEvent2_.Set();
        }

        private void OnDataChannelRemoved1(DataChannel channel)
        {
            dataChannelRemovedEvent1_.Set();
        }

        private void OnDataChannelRemoved2(DataChannel channel)
        {
            dataChannelRemovedEvent2_.Set();
        }
    }
}
