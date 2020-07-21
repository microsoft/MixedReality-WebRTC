// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Microsoft.MixedReality.WebRTC.Unity.Tests.Runtime
{
    public class VideoSenderTests
    {
        [SetUp]
        public void Setup()
        {
        }

        [TearDown]
        public void Shutdown()
        {
            // Note - this runs before OnDisabled/OnDestroy, so will always report false positives
            Library.ReportLiveObjects();
        }

        [UnityTest(/*Description = "Capture starts automatically when the component is activated"*/)]
        public IEnumerator CaptureStartsOnActivate()
        {
            // Create the peer connections
            var pc_go = new GameObject("pc1");
            pc_go.SetActive(false); // prevent auto-activation of components
            var pc = pc_go.AddComponent<PeerConnection>();

            // Batch changes manually
            pc.AutoCreateOfferOnRenegotiationNeeded = false;

            // Create the video track source
            VideoTrackSource source = pc_go.AddComponent<MockVideoSource>();

            // Create the media line
            MediaLine ml = pc.AddMediaLine(MediaKind.Video);
            ml.SenderTrackName = "track_name";

            // Assign the video source to the media line
            ml.Source = source;

            // MediaLine has not been connected yet.
            Assert.IsEmpty(source.MediaLines);

            // Add event handlers to check IsStreaming state
            source.VideoStreamStarted.AddListener((IVideoSource self) =>
            {
                // Becomes true *before* this handler by design
                Assert.IsTrue(source.IsLive);
            });
            source.VideoStreamStopped.AddListener((IVideoSource self) =>
            {
                // Still true until *after* this handler by design
                Assert.IsTrue(source.IsLive);
            });

            // Confirm the source is not capturing yet because the component is inactive
            Assert.IsFalse(source.IsLive);

            // Confirm the sender has no track because the component is inactive
            Assert.IsNull(ml.LocalTrack);

            // Activate the game object and the video track source component on it
            pc_go.SetActive(true);

            // MediaLine is connected.
            Assert.AreEqual(source.MediaLines.Single(), ml);

            // Confirm the sender is capturing because the component is now active
            Assert.IsTrue(source.IsLive);

            // Confirm the sender still has no track because there's no connection
            Assert.IsNull(ml.LocalTrack);

            // Deactivate the game object and the video track source component on it
            pc_go.SetActive(false);

            // Confirm the source stops streaming
            Assert.IsFalse(source.IsLive);

            Object.Destroy(pc_go);

            // Terminate the coroutine.
            yield return null;
        }
    }
}
