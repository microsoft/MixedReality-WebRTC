// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;

namespace Microsoft.MixedReality.WebRTC.Unity.Tests.Runtime
{
    public class PeerConnectionTests
    {
        public static IEnumerator InitializeAndWait(PeerConnection pc)
        {
            Debug.Assert(!pc.isActiveAndEnabled);

            // Subscribe to the event.
            bool isInitialized = false;
            UnityAction listener = () => isInitialized = true;
            pc.OnInitialized.AddListener(listener);

            // Activate
            if (!pc.gameObject.activeSelf)
            {
                pc.gameObject.SetActive(true);
            }
            if (!pc.enabled)
            {
                pc.enabled = true;
            }

            // Check the event was raised
            var timeout = DateTime.Now + TimeSpan.FromSeconds(10);
            yield return new WaitUntil(() => isInitialized || DateTime.Now > timeout);
            pc.OnInitialized.RemoveListener(listener);
            Assert.IsTrue(isInitialized);
            Assert.IsNotNull(pc.Peer);
        }

        public static IEnumerator ShutdownAndCheckEvent(PeerConnection pc)
        {
            // Subscribe to the event.
            bool isShutdown = false;
            UnityAction listener = () => isShutdown = true;
            pc.OnShutdown.AddListener(listener);

            pc.enabled = false;
            Assert.IsNull(pc.Peer);

            // Check the event was raised
            var timeout = DateTime.Now + TimeSpan.FromSeconds(10);
            yield return new WaitUntil(() => isShutdown || DateTime.Now > timeout);
            pc.OnShutdown.RemoveListener(listener);
            Assert.IsTrue(isShutdown);
        }

        [SetUp]
        public void Setup()
        {
        }

        [TearDown]
        public void Shutdown()
        {
        }

        private void VerifyLocalShutdown(MediaLine ml)
        {
            // The source is not impacted, but tracks and transceiver are gone.
            Assert.IsTrue(ml.Source.IsLive);
            Assert.IsFalse(ml.Receiver.IsLive);
            Assert.IsNull(ml.LocalTrack);
            Assert.IsNull(ml.Transceiver);
        }

        [UnityTest]
        public IEnumerator SimplePeerConnection()
        {
            // Create the component
            var go = new GameObject("test_go");
            go.SetActive(false); // prevent auto-activation of components
            var pc = go.AddComponent<PeerConnection>();
            Assert.IsNull(pc.Peer);

            // Initialize
            yield return InitializeAndWait(pc);
            UnityEngine.Object.Destroy(go);
        }

        [UnityTest]
        public IEnumerator EnableAndDisable()
        {
            // Create the component
            var go = new GameObject("test_go");
            go.SetActive(false); // prevent auto-activation of components
            var pc = go.AddComponent<PeerConnection>();
            Assert.IsNull(pc.Peer);
            for (int i = 0; i < 2; ++i)
            {
                yield return InitializeAndWait(pc);
                yield return ShutdownAndCheckEvent(pc);
            }

            UnityEngine.Object.Destroy(go);
        }

        [UnityTest]
        public IEnumerator EnableAndDisableWithTracks()
        {
            var pc1_go = new GameObject("pc1");
            pc1_go.SetActive(false); // prevent auto-activation of components
            var pc1 = pc1_go.AddComponent<PeerConnection>();
            var pc2_go = new GameObject("pc2");
            pc2_go.SetActive(false); // prevent auto-activation of components
            var pc2 = pc2_go.AddComponent<PeerConnection>();

            // Create the signaler
            var sig_go = new GameObject("signaler");
            var sig = sig_go.AddComponent<LocalOnlySignaler>();
            sig.Peer1 = pc1;
            sig.Peer2 = pc2;

            // Create the video source on peer #1
            VideoTrackSource source1 = pc1_go.AddComponent<UniformColorVideoSource>();
            VideoReceiver receiver1 = pc1_go.AddComponent<VideoReceiver>();
            MediaLine ml1 = pc1.AddMediaLine(MediaKind.Video);
            ml1.SenderTrackName = "video_track_1";
            ml1.Source = source1;
            ml1.Receiver = receiver1;

            // Create the video source on peer #2
            VideoTrackSource source2 = pc2_go.AddComponent<UniformColorVideoSource>();
            VideoReceiver receiver2 = pc2_go.AddComponent<VideoReceiver>();
            MediaLine ml2 = pc2.AddMediaLine(MediaKind.Video);
            ml2.SenderTrackName = "video_track_2";
            ml2.Source = source2;
            ml2.Receiver = receiver2;

            // Init/quit twice.
            for (int i = 0; i < 2; ++i)
            {
                // Initialize
                yield return InitializeAndWait(pc1);
                yield return InitializeAndWait(pc2);

                // Confirm the sources are ready.
                Assert.IsTrue(source1.IsLive);
                Assert.IsTrue(source2.IsLive);

                // Sender tracks will be created on connection.
                Assert.IsNull(ml1.LocalTrack);
                Assert.IsNull(ml2.LocalTrack);

                // Connect
                Assert.IsTrue(sig.StartConnection());
                yield return sig.WaitForConnection(millisecondsTimeout: 10000);
                Assert.IsTrue(sig.IsConnected);

                // Wait a frame so that the Unity events for streams started can propagate
                yield return null;

                // Check pairing
                Assert.IsNotNull(receiver1.Transceiver);
                Assert.IsTrue(receiver1.IsLive);
                Assert.AreEqual(1, pc1.Peer.RemoteVideoTracks.Count());
                Assert.IsNotNull(receiver2.Transceiver);
                Assert.IsTrue(receiver2.IsLive);
                Assert.AreEqual(1, pc2.Peer.RemoteVideoTracks.Count());

                // Shutdown peer #1
                pc1.enabled = false;
                Assert.IsNull(pc1.Peer);

                // We cannot reliably detect remote shutdown, so only check local peer.
                VerifyLocalShutdown(ml1);

                // Shutdown peer #2
                pc2.enabled = false;
                Assert.IsNull(pc2.Peer);

                VerifyLocalShutdown(ml2);
            }
            UnityEngine.Object.Destroy(pc1_go);
            UnityEngine.Object.Destroy(pc2_go);
            UnityEngine.Object.Destroy(sig_go);
        }
    }
}
