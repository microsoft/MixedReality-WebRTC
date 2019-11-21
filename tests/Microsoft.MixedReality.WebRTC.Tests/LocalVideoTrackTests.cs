using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using NUnit.Framework.Internal;

namespace Microsoft.MixedReality.WebRTC.Tests
{
    [TestFixture]
    internal class LocalVideoTrackTests
    {
        PeerConnection pc1_ = null;
        PeerConnection pc2_ = null;
        ManualResetEventSlim connectedEvent1_ = null;
        ManualResetEventSlim connectedEvent2_ = null;
        ManualResetEventSlim renegotiationEvent1_ = null;
        ManualResetEventSlim renegotiationEvent2_ = null;
        ManualResetEventSlim trackAddedEvent1_ = null;
        ManualResetEventSlim trackAddedEvent2_ = null;
        ManualResetEventSlim trackRemovedEvent1_ = null;
        ManualResetEventSlim trackRemovedEvent2_ = null;
        ManualResetEventSlim iceConnectedEvent1_ = null;
        ManualResetEventSlim iceConnectedEvent2_ = null;

        [OneTimeSetUp]
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
            iceConnectedEvent1_ = new ManualResetEventSlim(false);
            iceConnectedEvent2_ = new ManualResetEventSlim(false);
            renegotiationEvent1_ = new ManualResetEventSlim(false);
            renegotiationEvent2_ = new ManualResetEventSlim(false);
            trackAddedEvent1_ = new ManualResetEventSlim(false);
            trackAddedEvent2_ = new ManualResetEventSlim(false);
            trackRemovedEvent1_ = new ManualResetEventSlim(false);
            trackRemovedEvent2_ = new ManualResetEventSlim(false);

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
            pc1_.TrackAdded += OnTrackAdded1;
            pc2_.TrackAdded += OnTrackAdded2;
            pc1_.TrackRemoved += OnTrackRemoved1;
            pc2_.TrackRemoved += OnTrackRemoved2;
        }

        [OneTimeTearDown]
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
            pc1_.TrackAdded -= OnTrackAdded1;
            pc2_.TrackAdded -= OnTrackAdded2;
            pc1_.TrackRemoved -= OnTrackRemoved1;
            pc2_.TrackRemoved -= OnTrackRemoved2;

            // Clean-up callback events
            trackAddedEvent1_.Dispose();
            trackAddedEvent1_ = null;
            trackRemovedEvent1_.Dispose();
            trackRemovedEvent1_ = null;
            trackAddedEvent2_.Dispose();
            trackAddedEvent2_ = null;
            trackRemovedEvent2_.Dispose();
            trackRemovedEvent2_ = null;
            renegotiationEvent1_.Dispose();
            renegotiationEvent1_ = null;
            renegotiationEvent2_.Dispose();
            renegotiationEvent2_ = null;
            iceConnectedEvent1_.Dispose();
            iceConnectedEvent1_ = null;
            iceConnectedEvent2_.Dispose();
            iceConnectedEvent2_ = null;
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

        private void OnLocalSdpReady1(string type, string sdp)
        {
            pc2_.SetRemoteDescription(type, sdp);
            if (type == "offer")
            {
                pc2_.CreateAnswer();
            }
        }

        private void OnLocalSdpReady2(string type, string sdp)
        {
            pc1_.SetRemoteDescription(type, sdp);
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
            if (pc1_.IsConnected)
            {
                pc1_.CreateOffer();
            }
        }

        private void OnRenegotiationNeeded2()
        {
            renegotiationEvent2_.Set();
            if (pc2_.IsConnected)
            {
                pc2_.CreateOffer();
            }
        }

        private void OnTrackAdded1(PeerConnection.TrackKind trackKind)
        {
            Assert.True(trackKind == PeerConnection.TrackKind.Video);
            trackAddedEvent1_.Set();
        }

        private void OnTrackAdded2(PeerConnection.TrackKind trackKind)
        {
            Assert.True(trackKind == PeerConnection.TrackKind.Video);
            trackAddedEvent2_.Set();
        }

        private void OnTrackRemoved1(PeerConnection.TrackKind trackKind)
        {
            Assert.True(trackKind == PeerConnection.TrackKind.Video);
            trackRemovedEvent1_.Set();
        }

        private void OnTrackRemoved2(PeerConnection.TrackKind trackKind)
        {
            Assert.True(trackKind == PeerConnection.TrackKind.Video);
            trackRemovedEvent2_.Set();
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

#if !MRSW_EXCLUDE_DEVICE_TESTS

        [Test]
        public async Task BeforeConnect()
        {
            // Add local video track channel to #1
            var settings = new PeerConnection.LocalVideoTrackSettings();
            LocalVideoTrack track1 = await pc1_.AddLocalVideoTrackAsync(settings);
            Assert.NotNull(track1);
            Assert.AreEqual(pc1_, track1.PeerConnection);

            // Wait for local SDP re-negotiation on #1.
            // This will not create an offer, since we're not connected yet.
            Assert.True(renegotiationEvent1_.Wait(TimeSpan.FromSeconds(60.0)));

            // Connect
            Assert.True(pc1_.CreateOffer());
            WaitForSdpExchangeCompleted();
            Assert.True(pc1_.IsConnected);
            Assert.True(pc2_.IsConnected);

            // Remove the track from #1
            renegotiationEvent1_.Reset();
            pc1_.RemoveLocalVideoTrack(track1);
            Assert.IsNull(track1.PeerConnection);

            // Wait for local SDP re-negotiation on #1
            Assert.True(renegotiationEvent1_.Wait(TimeSpan.FromSeconds(60.0)));

            // Confirm remote track was removed from #2
            Assert.True(trackRemovedEvent2_.Wait(TimeSpan.FromSeconds(60.0)));

            // Wait until SDP renegotiation finished
            WaitForSdpExchangeCompleted();
        }

        [Test]
        public async Task AfterConnect()
        {
            // Connect
            Assert.True(pc1_.CreateOffer());
            WaitForSdpExchangeCompleted();
            Assert.True(pc1_.IsConnected);
            Assert.True(pc2_.IsConnected);

            // Add local video track channel to #1
            var settings = new PeerConnection.LocalVideoTrackSettings();
            LocalVideoTrack track1 = await pc1_.AddLocalVideoTrackAsync(settings);
            Assert.NotNull(track1);
            Assert.AreEqual(pc1_, track1.PeerConnection);

            // Wait for local SDP re-negotiation on #1
            Assert.True(renegotiationEvent1_.Wait(TimeSpan.FromSeconds(60.0)));

            // Confirm remote track was added on #2
            Assert.True(trackAddedEvent2_.Wait(TimeSpan.FromSeconds(60.0)));

            // Wait until SDP renegotiation finished
            WaitForSdpExchangeCompleted();

            // Remove the track from #1
            renegotiationEvent1_.Reset();
            pc1_.RemoveLocalVideoTrack(track1);
            Assert.IsNull(track1.PeerConnection);

            // Wait for local SDP re-negotiation on #1
            Assert.True(renegotiationEvent1_.Wait(TimeSpan.FromSeconds(60.0)));

            // Confirm remote track was removed from #2
            Assert.True(trackRemovedEvent2_.Wait(TimeSpan.FromSeconds(60.0)));

            // Wait until SDP renegotiation finished
            WaitForSdpExchangeCompleted();
        }

#endif // !MRSW_EXCLUDE_DEVICE_TESTS
    }
}
