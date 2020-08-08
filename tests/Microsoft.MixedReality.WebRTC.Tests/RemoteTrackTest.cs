// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using NUnit.Framework.Internal;

namespace Microsoft.MixedReality.WebRTC.Tests
{
    [TestFixture(SdpSemantic.PlanB)]
    [TestFixture(SdpSemantic.UnifiedPlan)]
    internal class RemoteTrackTests : PeerConnectionTestBase
    {
        public RemoteTrackTests(SdpSemantic sdpSemantic) : base(sdpSemantic)
        {
        }

        [Test]
        public async Task VideoCall()
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

            var transceiver1 = pc1_.AddTransceiver(MediaKind.Video, initSettings);

            var track_config = new LocalVideoTrackInitConfig
            {
                trackName = "custom_i420a"
            };

            // Add a local video track.
            using (var source = ExternalVideoTrackSource.CreateFromI420ACallback(
                VideoTrackSourceTests.CustomI420AFrameCallback))
            {
                using (var localTrack = LocalVideoTrack.CreateFromSource(source, track_config))
                {
                    transceiver1.LocalVideoTrack = localTrack;

                    // Connect
                    await DoNegotiationStartFrom(pc1_);

                    // Find the remote track
                    Assert.AreEqual(1, pc2_.Transceivers.Count);
                    var transceiver2 = pc2_.Transceivers[0];
                    var remoteTrack = transceiver2.RemoteVideoTrack;
                    Assert.IsNotNull(remoteTrack);
                    Assert.AreEqual(transceiver2, remoteTrack.Transceiver);
                    Assert.AreEqual(pc2_, remoteTrack.PeerConnection);

                    // Remote track receives frames.
                    VideoTrackSourceTests.TestFrameReadyCallbacks(remoteTrack);

                    // Cleanup.
                    transceiver1.LocalVideoTrack = null;
                }
            }
        }
    }
}
