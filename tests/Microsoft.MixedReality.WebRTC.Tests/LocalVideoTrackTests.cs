// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
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
        bool exchangePending_ = false;
        ManualResetEventSlim exchangeCompleted_ = null;
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
            Assert.AreEqual(0, Library.ReportLiveObjects());

            // Create the 2 peers
            var config = new PeerConnectionConfiguration();
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

            // Enable automatic renegotiation
            suspendOffer1_ = false;
            suspendOffer2_ = false;
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

            Assert.IsFalse(exchangePending_);
            exchangeCompleted_.Dispose();
            exchangeCompleted_ = null;

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

            Assert.AreEqual(0, Library.ReportLiveObjects());
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
            Assert.IsTrue(exchangePending_);
            await pc2_.SetRemoteDescriptionAsync(type, sdp);
            remoteDescAppliedEvent2_.Set();
            if (type == "offer")
            {
                pc2_.CreateAnswer();
            }
            else
            {
                exchangePending_ = false;
                exchangeCompleted_.Set();
            }
        }

        private async void OnLocalSdpReady2(string type, string sdp)
        {
            Assert.IsTrue(exchangePending_);
            await pc1_.SetRemoteDescriptionAsync(type, sdp);
            remoteDescAppliedEvent1_.Set();
            if (type == "offer")
            {
                pc1_.CreateAnswer();
            }
            else
            {
                exchangePending_ = false;
                exchangeCompleted_.Set();
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

        /// <summary>
        /// Start an SDP exchange by sending an offer from the given peer.
        /// </summary>
        /// <param name="offeringPeer">The peer to call <see cref="PeerConnection.CreateOffer"/> on.</param>
        private void StartOfferWith(PeerConnection offeringPeer)
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
        private void WaitForTransportsWritable()
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
        private void WaitForSdpExchangeCompleted()
        {
            Assert.IsTrue(exchangeCompleted_.Wait(TimeSpan.FromSeconds(60.0)));
            Assert.IsFalse(exchangePending_);
            exchangeCompleted_.Reset();
        }

        private unsafe void CustomI420AFrameCallback(in FrameRequest request)
        {
            var data = stackalloc byte[32 * 16 + 16 * 8 * 2];
            int k = 0;
            // Y plane (full resolution)
            for (int j = 0; j < 16; ++j)
            {
                for (int i = 0; i < 32; ++i)
                {
                    data[k++] = 0x7F;
                }
            }
            // U plane (halved chroma in both directions)
            for (int j = 0; j < 8; ++j)
            {
                for (int i = 0; i < 16; ++i)
                {
                    data[k++] = 0x30;
                }
            }
            // V plane (halved chroma in both directions)
            for (int j = 0; j < 8; ++j)
            {
                for (int i = 0; i < 16; ++i)
                {
                    data[k++] = 0xB2;
                }
            }
            var dataY = new IntPtr(data);
            var frame = new I420AVideoFrame
            {
                dataY = dataY,
                dataU = dataY + (32 * 16),
                dataV = dataY + (32 * 16) + (16 * 8),
                dataA = IntPtr.Zero,
                strideY = 32,
                strideU = 16,
                strideV = 16,
                strideA = 0,
                width = 32,
                height = 16
            };
            request.CompleteRequest(frame);
        }

        private unsafe void CustomArgb32FrameCallback(in FrameRequest request)
        {
            var data = stackalloc uint[32 * 16];
            int k = 0;
            // Create 2x2 checker pattern with 4 different colors
            for (int j = 0; j < 8; ++j)
            {
                for (int i = 0; i < 16; ++i)
                {
                    data[k++] = 0xFF0000FF;
                }
                for (int i = 16; i < 32; ++i)
                {
                    data[k++] = 0xFF00FF00;
                }
            }
            for (int j = 8; j < 16; ++j)
            {
                for (int i = 0; i < 16; ++i)
                {
                    data[k++] = 0xFFFF0000;
                }
                for (int i = 16; i < 32; ++i)
                {
                    data[k++] = 0xFF00FFFF;
                }
            }
            var frame = new Argb32VideoFrame
            {
                data = new IntPtr(data),
                stride = 128,
                width = 32,
                height = 16
            };
            request.CompleteRequest(frame);
        }

#if !MRSW_EXCLUDE_DEVICE_TESTS

        [Test]
        public async Task BeforeConnect()
        {
            // Create video transceiver on #1
            var transceiver_settings = new TransceiverInitSettings
            {
                Name = "transceiver1",
            };
            var transceiver1 = pc1_.AddVideoTransceiver(transceiver_settings);
            Assert.NotNull(transceiver1);

            // Wait for local SDP re-negotiation event on #1.
            // This will not create an offer, since we're not connected yet.
            Assert.True(renegotiationEvent1_.Wait(TimeSpan.FromSeconds(60.0)));

            // Create local video track
            var settings = new LocalVideoTrackSettings();
            LocalVideoTrack track1 = await LocalVideoTrack.CreateFromDeviceAsync(settings);
            Assert.NotNull(track1);

            // Add local video track channel to #1
            renegotiationEvent1_.Reset();
            transceiver1.SetLocalTrack(track1);
            Assert.IsFalse(renegotiationEvent1_.IsSet); // renegotiation not needed
            Assert.AreEqual(pc1_, track1.PeerConnection);
            Assert.AreEqual(track1, transceiver1.LocalTrack);
            Assert.IsNull(transceiver1.RemoteTrack);
            Assert.IsTrue(pc1_.Transceivers.Contains(transceiver1));
            Assert.IsTrue(pc1_.LocalVideoTracks.Contains(track1));
            Assert.AreEqual(0, pc1_.RemoteVideoTracks.Count);

            // Connect
            StartOfferWith(pc1_);
            WaitForTransportsWritable();
            Assert.True(pc1_.IsConnected);
            Assert.True(pc2_.IsConnected);

            // Now remote peer #2 has a 1 remote track
            Assert.AreEqual(0, pc2_.LocalVideoTracks.Count);
            Assert.AreEqual(1, pc2_.RemoteVideoTracks.Count);

            // Wait until the SDP exchange is completed
            WaitForSdpExchangeCompleted();

            // Remove the track from #1
            renegotiationEvent1_.Reset();
            transceiver1.LocalTrack = null;
            Assert.IsFalse(renegotiationEvent1_.IsSet); // renegotiation not needed
            Assert.IsNull(track1.PeerConnection);
            Assert.IsNull(track1.Transceiver);
            Assert.AreEqual(0, pc1_.LocalVideoTracks.Count);
            Assert.IsTrue(pc1_.Transceivers.Contains(transceiver1)); // never removed
            Assert.IsNull(transceiver1.LocalTrack);
            Assert.IsNull(transceiver1.RemoteTrack);
            track1.Dispose();

            // Remote peer #2 still has a track, because the transceiver is still receiving,
            // even if there is no track on the sending side (so effectively it receives only
            // black frames).
            Assert.AreEqual(0, pc2_.LocalVideoTracks.Count);
            Assert.AreEqual(1, pc2_.RemoteVideoTracks.Count);

            // Change the transceiver direction to stop receiving. This requires a renegotiation
            // to take effect, so nothing changes for now.
            remoteDescAppliedEvent1_.Reset();
            remoteDescAppliedEvent2_.Reset();
            transceiver1.DesiredDirection = Transceiver.Direction.Inactive;

            // Wait for renegotiate to complete
            Assert.True(renegotiationEvent1_.Wait(TimeSpan.FromSeconds(60.0)));
            WaitForSdpExchangeCompleted();
            Assert.True(remoteDescAppliedEvent1_.Wait(TimeSpan.FromSeconds(60.0)));
            Assert.True(remoteDescAppliedEvent2_.Wait(TimeSpan.FromSeconds(60.0)));

            // Now the remote track got removed from #2
            Assert.AreEqual(0, pc2_.LocalVideoTracks.Count);
            Assert.AreEqual(0, pc2_.RemoteVideoTracks.Count);
        }

        [Test]
        public async Task AfterConnect()
        {
            // Connect
            StartOfferWith(pc1_);
            WaitForTransportsWritable();
            Assert.True(pc1_.IsConnected);
            Assert.True(pc2_.IsConnected);

            // Wait for all transceivers to be updated on both peers
            WaitForSdpExchangeCompleted();
            Assert.True(remoteDescAppliedEvent1_.Wait(TimeSpan.FromSeconds(20.0)));
            Assert.True(remoteDescAppliedEvent2_.Wait(TimeSpan.FromSeconds(20.0)));
            remoteDescAppliedEvent1_.Reset();
            remoteDescAppliedEvent2_.Reset();

            // No track yet
            Assert.AreEqual(0, pc1_.LocalVideoTracks.Count);
            Assert.AreEqual(0, pc1_.RemoteVideoTracks.Count);
            Assert.AreEqual(0, pc2_.LocalVideoTracks.Count);
            Assert.AreEqual(0, pc2_.RemoteVideoTracks.Count);

            // Create video transceiver on #1 -- this generates a renegotiation
            renegotiationEvent1_.Reset();
            var transceiver_settings = new TransceiverInitSettings
            {
                Name = "transceiver1",
            };
            var transceiver1 = pc1_.AddVideoTransceiver(transceiver_settings);
            Assert.NotNull(transceiver1);
            Assert.IsTrue(pc1_.Transceivers.Contains(transceiver1));

            // Confirm (inactive) remote track was added on #2 due to transceiver being added
            Assert.True(videoTrackAddedEvent2_.Wait(TimeSpan.FromSeconds(60.0)));

            // Wait until SDP renegotiation finished
            Assert.True(renegotiationEvent1_.Wait(TimeSpan.FromSeconds(60.0)));
            WaitForSdpExchangeCompleted();

            // Now remote peer #2 has a 1 remote track (which is inactive).
            // Note that tracks are updated before transceivers here. This might be unintuitive, so we might
            // want to revisit this later.
            Assert.AreEqual(0, pc2_.LocalVideoTracks.Count);
            Assert.AreEqual(1, pc2_.RemoteVideoTracks.Count);

            // Transceiver has been updated to Send+Receive (default desired direction when added), but
            // since peer #2 doesn't intend to send, the actually negotiated direction on #1 is Send only.
            Assert.AreEqual(Transceiver.Direction.SendReceive, transceiver1.DesiredDirection);
            Assert.AreEqual(Transceiver.Direction.SendOnly, transceiver1.NegotiatedDirection);

            // Create local track
            renegotiationEvent1_.Reset();
            var settings = new LocalVideoTrackSettings();
            LocalVideoTrack track1 = await LocalVideoTrack.CreateFromDeviceAsync(settings);
            Assert.NotNull(track1);
            Assert.IsNull(track1.PeerConnection);
            Assert.IsNull(track1.Transceiver);
            Assert.IsFalse(renegotiationEvent1_.IsSet); // renegotiation not needed

            // Add local video track to #1
            renegotiationEvent1_.Reset();
            transceiver1.SetLocalTrack(track1);
            Assert.IsFalse(renegotiationEvent1_.IsSet); // renegotiation not needed
            Assert.AreEqual(pc1_, track1.PeerConnection);
            Assert.AreEqual(track1, transceiver1.LocalTrack);
            Assert.IsNull(transceiver1.RemoteTrack);
            Assert.IsTrue(pc1_.Transceivers.Contains(transceiver1));
            Assert.IsTrue(pc1_.LocalVideoTracks.Contains(track1));
            Assert.AreEqual(0, pc1_.RemoteVideoTracks.Count);

            // SetLocalTrack() does not change the transceiver directions
            Assert.AreEqual(Transceiver.Direction.SendReceive, transceiver1.DesiredDirection);
            Assert.AreEqual(Transceiver.Direction.SendOnly, transceiver1.NegotiatedDirection);

            // Remove the track from #1
            renegotiationEvent1_.Reset();
            transceiver1.SetLocalTrack(null);
            Assert.IsFalse(renegotiationEvent1_.IsSet); // renegotiation not needed
            Assert.IsNull(track1.PeerConnection);
            Assert.IsNull(track1.Transceiver);
            Assert.AreEqual(0, pc1_.LocalVideoTracks.Count);
            Assert.AreEqual(0, pc1_.RemoteVideoTracks.Count);
            Assert.IsTrue(pc1_.Transceivers.Contains(transceiver1)); // never removed
            Assert.IsNull(transceiver1.LocalTrack);
            Assert.IsNull(transceiver1.RemoteTrack);
            track1.Dispose();

            // SetLocalTrack() does not change the transceiver directions, even when the local
            // sending track is disposed of.
            Assert.AreEqual(Transceiver.Direction.SendReceive, transceiver1.DesiredDirection);
            Assert.AreEqual(Transceiver.Direction.SendOnly, transceiver1.NegotiatedDirection);

            // Remote peer #2 still has a track, because the transceiver is still receiving,
            // even if there is no track on the sending side (so effectively it receives only
            // black frames).
            Assert.AreEqual(0, pc2_.LocalVideoTracks.Count);
            Assert.AreEqual(1, pc2_.RemoteVideoTracks.Count);

            // Change the transceiver direction to stop receiving. This requires a renegotiation
            // to take effect, so nothing changes for now.
            remoteDescAppliedEvent1_.Reset();
            remoteDescAppliedEvent2_.Reset();
            transceiver1.DesiredDirection = Transceiver.Direction.Inactive;

            // Wait for renegotiate to complete
            Assert.True(renegotiationEvent1_.Wait(TimeSpan.FromSeconds(60.0)));
            WaitForSdpExchangeCompleted();

            // Now the remote track got removed from #2
            Assert.AreEqual(0, pc2_.LocalVideoTracks.Count);
            Assert.AreEqual(0, pc2_.RemoteVideoTracks.Count);
        }

#endif // !MRSW_EXCLUDE_DEVICE_TESTS


        [Test]
        public void SimpleExternalI420A()
        {
            // Connect
            StartOfferWith(pc1_);
            WaitForTransportsWritable();
            Assert.True(pc1_.IsConnected);
            Assert.True(pc2_.IsConnected);
            WaitForSdpExchangeCompleted();

            // No track yet
            Assert.AreEqual(0, pc1_.LocalVideoTracks.Count);
            Assert.AreEqual(0, pc1_.RemoteVideoTracks.Count);
            Assert.AreEqual(0, pc2_.LocalVideoTracks.Count);
            Assert.AreEqual(0, pc2_.RemoteVideoTracks.Count);

            // Create external I420A source
            var source1 = ExternalVideoTrackSource.CreateFromI420ACallback(CustomI420AFrameCallback);
            Assert.NotNull(source1);
            Assert.AreEqual(0, source1.Tracks.Count);

            // Add video transceiver #1
            renegotiationEvent1_.Reset();
            remoteDescAppliedEvent1_.Reset();
            remoteDescAppliedEvent2_.Reset();
            Assert.IsFalse(videoTrackAddedEvent2_.IsSet);
            var transceiver_settings = new TransceiverInitSettings
            {
                Name = "transceiver1",
            };
            var transceiver1 = pc1_.AddVideoTransceiver(transceiver_settings);
            Assert.NotNull(transceiver1);
            Assert.IsNull(transceiver1.LocalTrack);
            Assert.IsNull(transceiver1.RemoteTrack);
            Assert.AreEqual(pc1_, transceiver1.PeerConnection);

            // Wait for renegotiation
            Assert.True(renegotiationEvent1_.Wait(TimeSpan.FromSeconds(60.0)));
            Assert.True(videoTrackAddedEvent2_.Wait(TimeSpan.FromSeconds(60.0)));
            Assert.True(remoteDescAppliedEvent1_.Wait(TimeSpan.FromSeconds(60.0)));
            Assert.True(remoteDescAppliedEvent2_.Wait(TimeSpan.FromSeconds(60.0)));
            WaitForSdpExchangeCompleted();

            // Create external I420A track
            var track1 = LocalVideoTrack.CreateFromExternalSource("custom_i420a", source1);
            Assert.NotNull(track1);
            Assert.AreEqual(source1, track1.Source);
            Assert.IsNull(track1.PeerConnection);
            Assert.IsNull(track1.Transceiver);
            Assert.IsFalse(pc1_.LocalVideoTracks.Contains(track1));

            // Set track on transceiver
            renegotiationEvent1_.Reset();
            transceiver1.SetLocalTrack(track1);
            Assert.AreEqual(pc1_, track1.PeerConnection);
            Assert.IsTrue(pc1_.LocalVideoTracks.Contains(track1));
            Assert.IsFalse(renegotiationEvent1_.IsSet); // renegotiation not needed

            // Remove the track from #1
            renegotiationEvent1_.Reset();
            transceiver1.LocalTrack = null;
            Assert.IsNull(track1.PeerConnection);
            Assert.IsNull(track1.Transceiver);
            Assert.IsFalse(pc1_.LocalVideoTracks.Contains(track1));

            // Dispose of the track and its source
            track1.Dispose();
            track1 = null;
            source1.Dispose();
            source1 = null;

            // On peer #1 the track was replaced on the transceiver, but the transceiver stays
            // on the peer connection, so no renegotiation is needed.
            Assert.IsFalse(renegotiationEvent1_.IsSet);
        }

        [Test]
        public void SimpleExternalArgb32()
        {
            // Connect
            StartOfferWith(pc1_);
            WaitForTransportsWritable();
            Assert.True(pc1_.IsConnected);
            Assert.True(pc2_.IsConnected);
            WaitForSdpExchangeCompleted();

            // No track yet
            Assert.AreEqual(0, pc1_.LocalVideoTracks.Count);
            Assert.AreEqual(0, pc1_.RemoteVideoTracks.Count);
            Assert.AreEqual(0, pc2_.LocalVideoTracks.Count);
            Assert.AreEqual(0, pc2_.RemoteVideoTracks.Count);

            // Create external ARGB32 source
            var source1 = ExternalVideoTrackSource.CreateFromArgb32Callback(CustomArgb32FrameCallback);
            Assert.NotNull(source1);
            Assert.AreEqual(0, source1.Tracks.Count);

            // Add video transceiver #1
            renegotiationEvent1_.Reset();
            remoteDescAppliedEvent1_.Reset();
            remoteDescAppliedEvent2_.Reset();
            Assert.IsFalse(videoTrackAddedEvent2_.IsSet);
            var transceiver_settings = new TransceiverInitSettings
            {
                Name = "transceiver1",
            };
            var transceiver1 = pc1_.AddVideoTransceiver(transceiver_settings);
            Assert.NotNull(transceiver1);
            Assert.IsNull(transceiver1.LocalTrack);
            Assert.IsNull(transceiver1.RemoteTrack);
            Assert.AreEqual(pc1_, transceiver1.PeerConnection);

            // Wait for renegotiation
            Assert.True(renegotiationEvent1_.Wait(TimeSpan.FromSeconds(60.0)));
            Assert.True(videoTrackAddedEvent2_.Wait(TimeSpan.FromSeconds(60.0)));
            Assert.True(remoteDescAppliedEvent1_.Wait(TimeSpan.FromSeconds(60.0)));
            Assert.True(remoteDescAppliedEvent2_.Wait(TimeSpan.FromSeconds(60.0)));
            WaitForSdpExchangeCompleted();

            // Create external ARGB32 track
            var track1 = LocalVideoTrack.CreateFromExternalSource("custom_argb32", source1);
            Assert.NotNull(track1);
            Assert.AreEqual(source1, track1.Source);
            Assert.IsNull(track1.PeerConnection);
            Assert.IsNull(track1.Transceiver);
            Assert.IsFalse(pc1_.LocalVideoTracks.Contains(track1));

            // Set track on transceiver
            renegotiationEvent1_.Reset();
            transceiver1.SetLocalTrack(track1);
            Assert.AreEqual(pc1_, track1.PeerConnection);
            Assert.IsTrue(pc1_.LocalVideoTracks.Contains(track1));
            Assert.IsFalse(renegotiationEvent1_.IsSet); // renegotiation not needed

            // Remove the track from #1
            renegotiationEvent1_.Reset();
            transceiver1.LocalTrack = null;
            Assert.IsNull(track1.PeerConnection);
            Assert.IsNull(track1.Transceiver);
            Assert.IsFalse(pc1_.LocalVideoTracks.Contains(track1));

            // Dispose of the track and its source
            track1.Dispose();
            track1 = null;
            source1.Dispose();
            source1 = null;

            // On peer #1 the track was replaced on the transceiver, but the transceiver stays
            // on the peer connection, so no renegotiation is needed.
            Assert.IsFalse(renegotiationEvent1_.IsSet);
        }

        [Test]
        public void MultiExternalI420A()
        {
            // Batch changes in this test, and manually (re)negotiate
            suspendOffer1_ = true;

            // Connect
            StartOfferWith(pc1_);
            WaitForTransportsWritable();
            Assert.True(pc1_.IsConnected);
            Assert.True(pc2_.IsConnected);
            WaitForSdpExchangeCompleted();

            // No track yet
            Assert.AreEqual(0, pc1_.LocalVideoTracks.Count);
            Assert.AreEqual(0, pc1_.RemoteVideoTracks.Count);
            Assert.AreEqual(0, pc2_.LocalVideoTracks.Count);
            Assert.AreEqual(0, pc2_.RemoteVideoTracks.Count);

            // Create external I420A source
            var source1 = ExternalVideoTrackSource.CreateFromI420ACallback(CustomI420AFrameCallback);
            Assert.NotNull(source1);
            Assert.AreEqual(0, source1.Tracks.Count);

            // Add external I420A tracks
            const int kNumTracks = 5;
            var transceivers = new VideoTransceiver[kNumTracks];
            var tracks = new LocalVideoTrack[kNumTracks];
            for (int i = 0; i < kNumTracks; ++i)
            {
                var transceiver_settings = new TransceiverInitSettings
                {
                    Name = $"transceiver1_{i}",
                };
                transceivers[i] = pc1_.AddVideoTransceiver(transceiver_settings);
                Assert.NotNull(transceivers[i]);

                tracks[i] = LocalVideoTrack.CreateFromExternalSource($"track_i420a_{i}", source1);
                Assert.NotNull(tracks[i]);
                Assert.IsTrue(source1.Tracks.Contains(tracks[i]));

                transceivers[i].LocalTrack = tracks[i];
                Assert.AreEqual(pc1_, tracks[i].PeerConnection);
                Assert.IsTrue(pc1_.LocalVideoTracks.Contains(tracks[i]));
            }
            Assert.AreEqual(kNumTracks, source1.Tracks.Count);
            Assert.AreEqual(kNumTracks, pc1_.LocalVideoTracks.Count);
            Assert.AreEqual(0, pc1_.RemoteVideoTracks.Count);

            // Wait for local SDP re-negotiation on #1
            Assert.True(renegotiationEvent1_.Wait(TimeSpan.FromSeconds(60.0)));

            // Renegotiate
            StartOfferWith(pc1_);

            // Confirm remote track was added on #2
            Assert.True(videoTrackAddedEvent2_.Wait(TimeSpan.FromSeconds(60.0)));

            // Wait until SDP renegotiation finished
            WaitForSdpExchangeCompleted();
            Assert.AreEqual(0, pc2_.LocalVideoTracks.Count);
            Assert.AreEqual(kNumTracks, pc2_.RemoteVideoTracks.Count);

            // Remove the track from #1
            renegotiationEvent1_.Reset();
            for (int i = 0; i < kNumTracks; ++i)
            {
                transceivers[i].LocalTrack = null;
                Assert.IsNull(tracks[i].PeerConnection);
                Assert.IsFalse(pc1_.LocalVideoTracks.Contains(tracks[i]));
                Assert.IsFalse(source1.Tracks.Contains(tracks[i]));
                tracks[i].Dispose();
                tracks[i] = null;
            }
            Assert.AreEqual(0, pc1_.LocalVideoTracks.Count);
            Assert.AreEqual(0, pc1_.RemoteVideoTracks.Count);
            Assert.AreEqual(0, source1.Tracks.Count);

            // Dispose of source
            source1.Dispose();
            source1 = null;

            // Changes on transceiver's local track do not require renegotiation
            Assert.False(renegotiationEvent1_.IsSet);

            // Change the transceivers direction to stop sending
            renegotiationEvent1_.Reset();
            videoTrackRemovedEvent2_.Reset();
            remoteDescAppliedEvent1_.Reset();
            remoteDescAppliedEvent2_.Reset();
            for (int i = 0; i < kNumTracks; ++i)
            {
                transceivers[i].DesiredDirection = Transceiver.Direction.Inactive;
            }

            // Renegotiate manually the batch of changes
            Assert.True(renegotiationEvent1_.Wait(TimeSpan.FromSeconds(60.0)));
            StartOfferWith(pc1_);

            // Wait everything to be ready
            WaitForSdpExchangeCompleted();

            // Confirm remote tracks were removed from #2 as part of removing all transceivers
            Assert.True(videoTrackRemovedEvent2_.IsSet);

            // Remote peer #2 doesn't have any track anymore
            Assert.AreEqual(0, pc2_.LocalVideoTracks.Count);
            Assert.AreEqual(0, pc2_.RemoteVideoTracks.Count);
        }
    }
}
