// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Microsoft.MixedReality.WebRTC.Unity.Tests.Runtime
{
    public class MockVideoSource : CustomVideoSource<Argb32VideoFrameStorage>
    {
        protected override void OnFrameRequested(in FrameRequest request)
        {
            var data = new uint[16 * 16];
            for (int k = 0; k < 256; ++k)
            {
                data[k] = 0xFF0000FFu;
            }
            unsafe
            {
                fixed (uint* ptr = data)
                {
                    var frame = new Argb32VideoFrame
                    {
                        data = new IntPtr(ptr),
                        width = 16,
                        height = 16,
                        stride = 64
                    };
                    request.CompleteRequest(frame);
                }
            }
        }
    }

    public class MediaLineTests
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

        class DummyAudioSource : MediaTrackSource
        {
            public override MediaKind MediaKind => MediaKind.Audio;
            public override bool IsLive => true;
        }

        private MediaLine CreateMediaLine(PeerConnection pc)
        {
            MediaLine mediaLine = pc.AddMediaLine(MediaKind.Video);
            Assert.IsNotNull(mediaLine);
            Assert.AreEqual(MediaKind.Video, mediaLine.MediaKind);
            Assert.IsNull(mediaLine.Transceiver); // no connection
            Assert.IsNull(mediaLine.Source);
            Assert.IsNull(mediaLine.Receiver);
            Assert.IsNull(mediaLine.LocalTrack);
            return mediaLine;
        }

        private IEnumerator InitializePeer(PeerConnection pc, GameObject pc_go)
        {
            var ev = new ManualResetEventSlim(initialState: false);
            pc.OnInitialized.AddListener(() => ev.Set());
            pc_go.SetActive(true);
            // Wait for the peer connection to be initalize; this generally takes
            // at least 2 frames, one for the SetActive() to execute and one for
            // the OnInitialized() event to propagate.
            while (!ev.Wait(millisecondsTimeout: 200))
            {
                yield return null;
            }
            Assert.IsNotNull(pc.Peer);
        }

        private IEnumerator CreateMediaLineTest(bool initFirst)
        {
            // Create the peer connections
            var pc_go = new GameObject("pc1");
            pc_go.SetActive(false); // prevent auto-activation of components
            var pc = pc_go.AddComponent<PeerConnection>();

            MediaLine mediaLine;
            if (initFirst)
            {
                // Initialize the peer connection
                yield return InitializePeer(pc, pc_go);

                // Create the media line
                mediaLine = CreateMediaLine(pc);
            }
            else
            {
                // Create the media line
                mediaLine = CreateMediaLine(pc);

                // Initialize the peer connection
                yield return InitializePeer(pc, pc_go);

                // No change
                Assert.IsNull(mediaLine.Transceiver); // no connection
            }

            // Create an offer (which won't succeed as there's no signaler, but that doesn't matter)
            Assert.IsTrue(pc.StartConnection());

            // The transceiver was created by the implementation and assigned to the media line
            Assert.IsNotNull(mediaLine.Transceiver);
            Assert.AreEqual(mediaLine.Transceiver.MediaKind, mediaLine.MediaKind);

            UnityEngine.Object.Destroy(pc_go);
        }

        [UnityTest(/*Description = "Add a media line to a peer connection before it is initialized"*/)]
        public IEnumerator CreateBeforePeerInit()
        {
            return CreateMediaLineTest(initFirst: false);
        }

        [UnityTest(/*Description = "Add a media line to a peer connection after it is initialized"*/)]
        public IEnumerator CreateAfterPeerInit()
        {
            return CreateMediaLineTest(initFirst: true);
        }

        [UnityTest(/*Description = "MediaLine.Source"*/)]
        public IEnumerator SetSource()
        {
            // Create the peer connections
            var pc_go = new GameObject("pc1");
            var pc = pc_go.AddComponent<PeerConnection>();

            // Create some video track sources
            VideoTrackSource source1 = pc_go.AddComponent<MockVideoSource>();
            VideoTrackSource source2 = pc_go.AddComponent<MockVideoSource>();
            Assert.AreEqual(0, source1.MediaLines.Count);
            Assert.AreEqual(0, source2.MediaLines.Count);

            // Create the media line
            MediaLine mediaLine = pc.AddMediaLine(MediaKind.Video);

            // Assign a video source to the media line
            mediaLine.Source = source1;
            Assert.AreEqual(mediaLine.Source, source1);
            Assert.AreEqual(1, source1.MediaLines.Count);
            Assert.IsTrue(source1.MediaLines.Contains(mediaLine));

            // No-op
            mediaLine.Source = source1;

            // Assign another video source to the media line
            mediaLine.Source = source2;
            Assert.AreEqual(mediaLine.Source, source2);
            Assert.AreEqual(0, source1.MediaLines.Count);
            Assert.IsFalse(source1.MediaLines.Contains(mediaLine));
            Assert.AreEqual(1, source2.MediaLines.Count);
            Assert.IsTrue(source2.MediaLines.Contains(mediaLine));

            // Remove it from the media line
            mediaLine.Source = null;
            Assert.IsNull(mediaLine.Source);
            Assert.AreEqual(0, source2.MediaLines.Count);
            Assert.IsFalse(source2.MediaLines.Contains(mediaLine));

            // No-op
            mediaLine.Source = null;

            // Set an invalid source (wrong media kind)
            Assert.Throws<ArgumentException>(() => mediaLine.Source = pc_go.AddComponent<DummyAudioSource>());

            UnityEngine.Object.Destroy(pc_go);

            // Terminate the coroutine.
            yield return null;
        }

        [UnityTest(/*Description = "MediaLine.Receiver"*/)]
        public IEnumerator SetReceiver()
        {
            // Create the peer connections
            var pc_go = new GameObject("pc1");
            var pc = pc_go.AddComponent<PeerConnection>();

            // Create some video track sources
            VideoReceiver receiver1 = pc_go.AddComponent<VideoReceiver>();
            VideoReceiver receiver2 = pc_go.AddComponent<VideoReceiver>();
            Assert.IsNull(receiver1.MediaLine);
            Assert.IsNull(receiver2.MediaLine);

            // Create the media line
            MediaLine mediaLine = pc.AddMediaLine(MediaKind.Video);

            // Assign a video source to the media line
            mediaLine.Receiver = receiver1;
            Assert.AreEqual(mediaLine.Receiver, receiver1);
            Assert.AreEqual(receiver1.MediaLine, mediaLine);

            // No-op
            mediaLine.Receiver = receiver1;

            // Assign another video source to the media line
            mediaLine.Receiver = receiver2;
            Assert.AreEqual(mediaLine.Receiver, receiver2);
            Assert.IsNull(receiver1.MediaLine);
            Assert.AreEqual(receiver2.MediaLine, mediaLine);

            // Remove it from the media line
            mediaLine.Receiver = null;
            Assert.IsNull(mediaLine.Receiver);
            Assert.IsNull(receiver2.MediaLine);

            // No-op
            mediaLine.Receiver = null;

            // Set an invalid source (wrong media kind)
            Assert.Throws<ArgumentException>(() => mediaLine.Receiver = pc_go.AddComponent<AudioReceiver>());

            UnityEngine.Object.Destroy(pc_go);

            // Terminate the coroutine.
            yield return null;
        }

        [UnityTest]
        public IEnumerator DestroyPeerConnection()
        {
            // Create the component
            var go = new GameObject("test_go");
            go.SetActive(false); // prevent auto-activation of components
            var pc = go.AddComponent<PeerConnection>();

            // Add a media line while inactive.
            VideoTrackSource source1 = go.AddComponent<UniformColorVideoSource>();
            VideoReceiver receiver1 = go.AddComponent<VideoReceiver>();
            MediaLine ml1 = pc.AddMediaLine(MediaKind.Video);
            ml1.Source = source1;
            ml1.Receiver = receiver1;

            // Media lines have not been set yet.
            Assert.IsEmpty(source1.MediaLines);
            Assert.IsNull(receiver1.MediaLine);

            yield return PeerConnectionTests.InitializeAndWait(pc);

            // Media lines have been set now.
            Assert.AreEqual(source1.MediaLines.Single(), ml1);
            Assert.AreEqual(receiver1.MediaLine, ml1);

            // Add a media line while active.
            VideoReceiver receiver2 = go.AddComponent<VideoReceiver>();
            MediaLine ml2 = pc.AddMediaLine(MediaKind.Video);
            ml2.Source = source1;
            ml2.Receiver = receiver2;

            // Media line #2 is connected.
            Assert.AreEqual(source1.MediaLines[1], ml2);
            Assert.AreEqual(receiver2.MediaLine, ml2);

            // Disable the peer.
            pc.enabled = false;

            // Add a media line while disabled.
            VideoReceiver receiver3 = go.AddComponent<VideoReceiver>();
            MediaLine ml3 = pc.AddMediaLine(MediaKind.Video);
            ml3.Source = source1;
            ml3.Receiver = receiver3;

            // Media line #3 is connected.
            Assert.AreEqual(source1.MediaLines[2], ml3);
            Assert.AreEqual(receiver3.MediaLine, ml3);

            // Destroy the peer (wait a frame for destruction).
            UnityEngine.Object.Destroy(pc);
            yield return null;

            // Source and receivers are not connected anymore.
            Assert.IsEmpty(source1.MediaLines);
            Assert.IsNull(receiver1.MediaLine);
            Assert.IsNull(receiver2.MediaLine);
            Assert.IsNull(receiver3.MediaLine);

            UnityEngine.Object.Destroy(go);
        }
    }
}
