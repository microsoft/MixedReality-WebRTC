// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using NUnit.Framework.Internal;

namespace Microsoft.MixedReality.WebRTC.Tests
{
    [TestFixture(SdpSemantic.PlanB)]
    [TestFixture(SdpSemantic.UnifiedPlan)]
    internal class LocalVideoTrackTests : PeerConnectionTestBase
    {
        public LocalVideoTrackTests(SdpSemantic sdpSemantic) : base(sdpSemantic)
        {
        }

#if !MRSW_EXCLUDE_DEVICE_TESTS

        [Test]
        public async Task BeforeConnect()
        {
            // Create video transceiver on #1
            var transceiver_settings = new TransceiverInitSettings
            {
                Name = "transceiver1",
                InitialDesiredDirection = Transceiver.Direction.SendReceive
            };
            var transceiver1 = pc1_.AddTransceiver(MediaKind.Video, transceiver_settings);
            Assert.NotNull(transceiver1);

            // Wait for local SDP re-negotiation event on #1.
            // This will not create an offer, since we're not connected yet.
            Assert.True(renegotiationEvent1_.Wait(TimeSpan.FromSeconds(60.0)));

            // Create video track source
            var source1 = await DeviceVideoTrackSource.CreateAsync();
            Assert.IsNotNull(source1);

            // Create local video track
            var settings = new LocalVideoTrackInitConfig();
            LocalVideoTrack track1 = LocalVideoTrack.CreateFromSource(source1, settings);
            Assert.IsNotNull(track1);

            // Add local video track to #1
            renegotiationEvent1_.Reset();
            transceiver1.LocalVideoTrack = track1;
            Assert.IsFalse(renegotiationEvent1_.IsSet); // renegotiation not needed
            Assert.AreEqual(pc1_, track1.PeerConnection);
            Assert.AreEqual(track1, transceiver1.LocalTrack);
            Assert.IsNull(transceiver1.RemoteTrack);
            Assert.IsTrue(pc1_.Transceivers.Contains(transceiver1));
            Assert.IsTrue(pc1_.LocalVideoTracks.Contains(track1));
            Assert.AreEqual(0, pc1_.RemoteVideoTracks.Count());

            // Connect
            StartOfferWith(pc1_);
            WaitForTransportsWritable();
            Assert.True(pc1_.IsConnected);
            Assert.True(pc2_.IsConnected);

            // Now remote peer #2 has a 1 remote track
            Assert.AreEqual(0, pc2_.LocalVideoTracks.Count());
            Assert.AreEqual(1, pc2_.RemoteVideoTracks.Count());

            // Wait until the SDP exchange is completed
            WaitForSdpExchangeCompleted();

            // Remove the track from #1
            renegotiationEvent1_.Reset();
            transceiver1.LocalVideoTrack = null;
            Assert.IsFalse(renegotiationEvent1_.IsSet); // renegotiation not needed
            Assert.IsNull(track1.PeerConnection);
            Assert.IsNull(track1.Transceiver);
            Assert.AreEqual(0, pc1_.LocalVideoTracks.Count());
            Assert.IsTrue(pc1_.Transceivers.Contains(transceiver1)); // never removed
            Assert.IsNull(transceiver1.LocalTrack);
            Assert.IsNull(transceiver1.RemoteTrack);
            track1.Dispose();

            // Destroy the video source
            source1.Dispose();

            // SetLocalTrack() does not change the transceiver directions, even when the local
            // sending track is disposed of.
            Assert.AreEqual(Transceiver.Direction.SendReceive, transceiver1.DesiredDirection);
            Assert.AreEqual(Transceiver.Direction.SendOnly, transceiver1.NegotiatedDirection);

            // Remote peer #2 still has a track, because the transceiver is still receiving,
            // even if there is no track on the sending side (so effectively it receives only
            // black frames).
            Assert.AreEqual(0, pc2_.LocalVideoTracks.Count());
            Assert.AreEqual(1, pc2_.RemoteVideoTracks.Count());

            // Change the transceiver direction to stop receiving. This requires a renegotiation
            // to take effect, so nothing changes for now.
            // Note: In Plan B, a renegotiation needed event is manually forced for parity with
            // Unified Plan. However setting the transceiver to inactive removes the remote peer's
            // remote track, which causes another renegotiation needed event. So we suspend the
            // automatic offer to trigger it manually.
            suspendOffer1_ = true;
            remoteDescAppliedEvent1_.Reset();
            remoteDescAppliedEvent2_.Reset();
            transceiver1.DesiredDirection = Transceiver.Direction.Inactive;
            Assert.True(renegotiationEvent1_.Wait(TimeSpan.FromSeconds(60.0)));

            // Renegotiate
            await DoNegotiationStartFrom(pc1_);
            Assert.True(remoteDescAppliedEvent1_.Wait(TimeSpan.FromSeconds(60.0)));
            Assert.True(remoteDescAppliedEvent2_.Wait(TimeSpan.FromSeconds(60.0)));

            // Now the remote track got removed from #2
            Assert.AreEqual(0, pc2_.LocalVideoTracks.Count());
            Assert.AreEqual(0, pc2_.RemoteVideoTracks.Count());
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
            Assert.AreEqual(0, pc1_.LocalVideoTracks.Count());
            Assert.AreEqual(0, pc1_.RemoteVideoTracks.Count());
            Assert.AreEqual(0, pc2_.LocalVideoTracks.Count());
            Assert.AreEqual(0, pc2_.RemoteVideoTracks.Count());

            // Create video transceiver on #1 -- this generates a renegotiation
            renegotiationEvent1_.Reset();
            var transceiver_settings = new TransceiverInitSettings
            {
                Name = "transceiver1",
                InitialDesiredDirection = Transceiver.Direction.SendReceive
            };
            var transceiver1 = pc1_.AddTransceiver(MediaKind.Video, transceiver_settings);
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
            Assert.AreEqual(0, pc2_.LocalVideoTracks.Count());
            Assert.AreEqual(1, pc2_.RemoteVideoTracks.Count());

            // Transceiver has been updated to Send+Receive (desired direction when added), but since peer #2
            // doesn't intend to send, the actually negotiated direction on #1 is Send only.
            Assert.AreEqual(Transceiver.Direction.SendReceive, transceiver1.DesiredDirection);
            Assert.AreEqual(Transceiver.Direction.SendOnly, transceiver1.NegotiatedDirection);

            // Create video track source
            var source1 = await DeviceVideoTrackSource.CreateAsync();
            Assert.IsNotNull(source1);

            // Create local video track
            renegotiationEvent1_.Reset();
            var settings = new LocalVideoTrackInitConfig();
            LocalVideoTrack track1 = LocalVideoTrack.CreateFromSource(source1, settings);
            Assert.IsNotNull(track1);
            Assert.IsNull(track1.PeerConnection);
            Assert.IsNull(track1.Transceiver);
            Assert.IsFalse(renegotiationEvent1_.IsSet); // renegotiation not needed

            // Add local video track to #1
            renegotiationEvent1_.Reset();
            transceiver1.LocalVideoTrack = track1;
            Assert.IsFalse(renegotiationEvent1_.IsSet); // renegotiation not needed
            Assert.AreEqual(pc1_, track1.PeerConnection);
            Assert.AreEqual(track1, transceiver1.LocalTrack);
            Assert.IsNull(transceiver1.RemoteTrack);
            Assert.IsTrue(pc1_.Transceivers.Contains(transceiver1));
            Assert.IsTrue(pc1_.LocalVideoTracks.Contains(track1));
            Assert.AreEqual(0, pc1_.RemoteVideoTracks.Count());

            // SetLocalTrack() does not change the transceiver directions
            Assert.AreEqual(Transceiver.Direction.SendReceive, transceiver1.DesiredDirection);
            Assert.AreEqual(Transceiver.Direction.SendOnly, transceiver1.NegotiatedDirection);

            // Remove the track from #1
            renegotiationEvent1_.Reset();
            transceiver1.LocalVideoTrack = null;
            Assert.IsFalse(renegotiationEvent1_.IsSet); // renegotiation not needed
            Assert.IsNull(track1.PeerConnection);
            Assert.IsNull(track1.Transceiver);
            Assert.AreEqual(0, pc1_.LocalVideoTracks.Count());
            Assert.AreEqual(0, pc1_.RemoteVideoTracks.Count());
            Assert.IsTrue(pc1_.Transceivers.Contains(transceiver1)); // never removed
            Assert.IsNull(transceiver1.LocalTrack);
            Assert.IsNull(transceiver1.RemoteTrack);
            track1.Dispose();

            // Destroy the video source
            source1.Dispose();

            // SetLocalTrack() does not change the transceiver directions, even when the local
            // sending track is disposed of.
            Assert.AreEqual(Transceiver.Direction.SendReceive, transceiver1.DesiredDirection);
            Assert.AreEqual(Transceiver.Direction.SendOnly, transceiver1.NegotiatedDirection);

            // Remote peer #2 still has a track, because the transceiver is still receiving,
            // even if there is no track on the sending side (so effectively it receives only
            // black frames).
            Assert.AreEqual(0, pc2_.LocalVideoTracks.Count());
            Assert.AreEqual(1, pc2_.RemoteVideoTracks.Count());

            // Change the transceiver direction to stop receiving. This requires a renegotiation
            // to take effect, so nothing changes for now.
            // Note: In Plan B, a renegotiation needed event is manually forced for parity with
            // Unified Plan. However setting the transceiver to inactive removes the remote peer's
            // remote track, which causes another renegotiation needed event. So we suspend the
            // automatic offer to trigger it manually.
            suspendOffer1_ = true;
            remoteDescAppliedEvent1_.Reset();
            remoteDescAppliedEvent2_.Reset();
            transceiver1.DesiredDirection = Transceiver.Direction.Inactive;
            Assert.True(renegotiationEvent1_.Wait(TimeSpan.FromSeconds(60.0)));

            // Renegotiate
            await DoNegotiationStartFrom(pc1_);
            Assert.True(remoteDescAppliedEvent1_.Wait(TimeSpan.FromSeconds(60.0)));
            Assert.True(remoteDescAppliedEvent2_.Wait(TimeSpan.FromSeconds(60.0)));

            // Now the remote track got removed from #2
            Assert.AreEqual(0, pc2_.LocalVideoTracks.Count());
            Assert.AreEqual(0, pc2_.RemoteVideoTracks.Count());
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
            Assert.AreEqual(0, pc1_.LocalVideoTracks.Count());
            Assert.AreEqual(0, pc1_.RemoteVideoTracks.Count());
            Assert.AreEqual(0, pc2_.LocalVideoTracks.Count());
            Assert.AreEqual(0, pc2_.RemoteVideoTracks.Count());

            // Create external I420A source
            var source1 = ExternalVideoTrackSource.CreateFromI420ACallback(
                VideoTrackSourceTests.CustomI420AFrameCallback);
            Assert.NotNull(source1);
            Assert.AreEqual(0, source1.Tracks.Count());

            // Add video transceiver #1
            renegotiationEvent1_.Reset();
            remoteDescAppliedEvent1_.Reset();
            remoteDescAppliedEvent2_.Reset();
            Assert.IsFalse(videoTrackAddedEvent2_.IsSet);
            var transceiver_settings = new TransceiverInitSettings
            {
                Name = "transceiver1",
            };
            var transceiver1 = pc1_.AddTransceiver(MediaKind.Video, transceiver_settings);
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
            var track_config1 = new LocalVideoTrackInitConfig
            {
                trackName = "custom_i420a"
            };
            var track1 = LocalVideoTrack.CreateFromSource(source1, track_config1);
            Assert.NotNull(track1);
            Assert.AreEqual(source1, track1.Source);
            Assert.IsNull(track1.PeerConnection);
            Assert.IsNull(track1.Transceiver);
            Assert.IsFalse(pc1_.LocalVideoTracks.Contains(track1));

            // Set track on transceiver
            renegotiationEvent1_.Reset();
            transceiver1.LocalVideoTrack = track1;
            Assert.AreEqual(pc1_, track1.PeerConnection);
            Assert.IsTrue(pc1_.LocalVideoTracks.Contains(track1));
            Assert.IsFalse(renegotiationEvent1_.IsSet); // renegotiation not needed

            // Remove the track from #1
            renegotiationEvent1_.Reset();
            transceiver1.LocalVideoTrack = null;
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
            Assert.AreEqual(0, pc1_.LocalVideoTracks.Count());
            Assert.AreEqual(0, pc1_.RemoteVideoTracks.Count());
            Assert.AreEqual(0, pc2_.LocalVideoTracks.Count());
            Assert.AreEqual(0, pc2_.RemoteVideoTracks.Count());

            // Create external ARGB32 source
            var source1 = ExternalVideoTrackSource.CreateFromArgb32Callback(
                VideoTrackSourceTests.CustomArgb32FrameCallback);
            Assert.NotNull(source1);
            Assert.AreEqual(0, source1.Tracks.Count());

            // Add video transceiver #1
            renegotiationEvent1_.Reset();
            remoteDescAppliedEvent1_.Reset();
            remoteDescAppliedEvent2_.Reset();
            Assert.IsFalse(videoTrackAddedEvent2_.IsSet);
            var transceiver_settings = new TransceiverInitSettings
            {
                Name = "transceiver1",
            };
            var transceiver1 = pc1_.AddTransceiver(MediaKind.Video, transceiver_settings);
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
            var track_config1 = new LocalVideoTrackInitConfig
            {
                trackName = "custom_argb32"
            };
            var track1 = LocalVideoTrack.CreateFromSource(source1, track_config1);
            Assert.NotNull(track1);
            Assert.AreEqual(source1, track1.Source);
            Assert.IsNull(track1.PeerConnection);
            Assert.IsNull(track1.Transceiver);
            Assert.IsFalse(pc1_.LocalVideoTracks.Contains(track1));

            // Set track on transceiver
            renegotiationEvent1_.Reset();
            transceiver1.LocalVideoTrack = track1;
            Assert.AreEqual(pc1_, track1.PeerConnection);
            Assert.IsTrue(pc1_.LocalVideoTracks.Contains(track1));
            Assert.IsFalse(renegotiationEvent1_.IsSet); // renegotiation not needed

            // Remove the track from #1
            renegotiationEvent1_.Reset();
            transceiver1.LocalVideoTrack = null;
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
        public void FrameReadyCallbacks()
        {
            var track_config = new LocalVideoTrackInitConfig
            {
                trackName = "custom_i420a"
            };
            using (var source = ExternalVideoTrackSource.CreateFromI420ACallback(
                VideoTrackSourceTests.CustomI420AFrameCallback))
            {
                using (var track = LocalVideoTrack.CreateFromSource(source, track_config))
                {
                    VideoTrackSourceTests.TestFrameReadyCallbacks(track);
                }
            }
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
            Assert.AreEqual(0, pc1_.LocalVideoTracks.Count());
            Assert.AreEqual(0, pc1_.RemoteVideoTracks.Count());
            Assert.AreEqual(0, pc2_.LocalVideoTracks.Count());
            Assert.AreEqual(0, pc2_.RemoteVideoTracks.Count());

            // Create external I420A source
            var source1 = ExternalVideoTrackSource.CreateFromI420ACallback(
                VideoTrackSourceTests.CustomI420AFrameCallback);
            Assert.NotNull(source1);
            Assert.AreEqual(0, source1.Tracks.Count());

            // Add external I420A tracks
            const int kNumTracks = 5;
            var transceivers = new Transceiver[kNumTracks];
            var tracks = new LocalVideoTrack[kNumTracks];
            for (int i = 0; i < kNumTracks; ++i)
            {
                var transceiver_settings = new TransceiverInitSettings
                {
                    Name = $"transceiver1_{i}",
                };
                transceivers[i] = pc1_.AddTransceiver(MediaKind.Video, transceiver_settings);
                Assert.NotNull(transceivers[i]);

                var track_config = new LocalVideoTrackInitConfig
                {
                    trackName = $"track_i420a_{i}"
                };
                tracks[i] = LocalVideoTrack.CreateFromSource(source1, track_config);
                Assert.NotNull(tracks[i]);
                Assert.IsTrue(source1.Tracks.Contains(tracks[i]));

                transceivers[i].LocalVideoTrack = tracks[i];
                Assert.AreEqual(pc1_, tracks[i].PeerConnection);
                Assert.IsTrue(pc1_.LocalVideoTracks.Contains(tracks[i]));
            }
            Assert.AreEqual(kNumTracks, source1.Tracks.Count());
            Assert.AreEqual(kNumTracks, pc1_.LocalVideoTracks.Count());
            Assert.AreEqual(0, pc1_.RemoteVideoTracks.Count());

            // Wait for local SDP re-negotiation on #1
            Assert.True(renegotiationEvent1_.Wait(TimeSpan.FromSeconds(60.0)));

            // Renegotiate
            StartOfferWith(pc1_);

            // Confirm remote track was added on #2
            Assert.True(videoTrackAddedEvent2_.Wait(TimeSpan.FromSeconds(60.0)));

            // Wait until SDP renegotiation finished
            WaitForSdpExchangeCompleted();
            Assert.AreEqual(0, pc2_.LocalVideoTracks.Count());
            Assert.AreEqual(kNumTracks, pc2_.RemoteVideoTracks.Count());

            // Remove the track from #1
            renegotiationEvent1_.Reset();
            for (int i = 0; i < kNumTracks; ++i)
            {
                transceivers[i].LocalVideoTrack = null;
                Assert.IsNull(tracks[i].PeerConnection);
                Assert.IsFalse(pc1_.LocalVideoTracks.Contains(tracks[i]));
                Assert.IsTrue(source1.Tracks.Contains(tracks[i])); // does not change yet
                tracks[i].Dispose();
                tracks[i] = null;
                Assert.IsFalse(source1.Tracks.Contains(tracks[i]));
            }
            Assert.AreEqual(0, pc1_.LocalVideoTracks.Count());
            Assert.AreEqual(0, pc1_.RemoteVideoTracks.Count());
            Assert.AreEqual(0, source1.Tracks.Count());

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
            Assert.AreEqual(0, pc2_.LocalVideoTracks.Count());
            Assert.AreEqual(0, pc2_.RemoteVideoTracks.Count());
        }
    }
}
