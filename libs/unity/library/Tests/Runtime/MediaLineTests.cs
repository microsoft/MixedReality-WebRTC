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

        class DummyAudioSource : IMediaTrackSource { public MediaKind MediaKind => MediaKind.Audio; }

        class DummyNonMonoBehaviourVideoSource : IMediaTrackSource, IMediaTrackSourceInternal
        {
            public MediaKind MediaKind => MediaKind.Video;
            public void OnAddedToMediaLine(MediaLine mediaLine) => throw new NotImplementedException();
            public void OnRemoveFromMediaLine(MediaLine mediaLine) => throw new NotImplementedException();
        }

        class DummyMissingInternalInterfaceVideoSource : MonoBehaviour, IMediaTrackSource
        {
            public MediaKind MediaKind => MediaKind.Video;
        }

        class DummyNonMonoBehaviourVideoReceiver : IMediaReceiver, IMediaReceiverInternal
        {
            public MediaKind MediaKind => MediaKind.Video;
            public bool IsLive => throw new NotImplementedException();
            public Transceiver Transceiver => throw new NotImplementedException();
            public void AttachToTransceiver(Transceiver transceiver) => throw new NotImplementedException();
            public void DetachFromTransceiver(Transceiver transceiver) => throw new NotImplementedException();
            public void OnAddedToMediaLine(MediaLine mediaLine) => throw new NotImplementedException();
            public void OnPaired(MediaTrack track) => throw new NotImplementedException();
            public void OnUnpaired(MediaTrack track) => throw new NotImplementedException();
            public void OnRemoveFromMediaLine(MediaLine mediaLine) => throw new NotImplementedException();

        }

        class DummyMissingInternalInterfaceVideoReceiver : MonoBehaviour, IMediaReceiver
        {
            public MediaKind MediaKind => MediaKind.Video;
            public bool IsLive => throw new NotImplementedException();
            public Transceiver Transceiver => throw new NotImplementedException();
        }

        private MediaLine CreateMediaLine(PeerConnection pc)
        {
            MediaLine mediaLine = pc.AddMediaLine(MediaKind.Video);
            Assert.IsNotNull(mediaLine);
            Assert.AreEqual(MediaKind.Video, mediaLine.MediaKind);
            Assert.IsNull(mediaLine.Transceiver); // no connection
            Assert.IsNull(mediaLine.Source);
            Assert.IsNull(mediaLine.Receiver);
            Assert.IsNull(mediaLine.SenderTrack);
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

        [Test(Description = "MediaLine.Source")]
        public void SetSource()
        {
            // Create the peer connections
            var pc_go = new GameObject("pc1");
            pc_go.SetActive(false); // prevent auto-activation of components
            var pc = pc_go.AddComponent<PeerConnection>();
            pc.AutoInitializeOnStart = false;

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
            Assert.Throws<ArgumentException>(() => mediaLine.Source = new DummyAudioSource());

            // Set an invalid source (not a MonoBehaviour)
            Assert.Throws<ArgumentException>(() => mediaLine.Source = new DummyNonMonoBehaviourVideoSource());

            // Set an invalid source (not implementing IMediaTrackSourceInternal)
            Assert.Throws<ArgumentException>(() => mediaLine.Source = pc_go.AddComponent<DummyMissingInternalInterfaceVideoSource>());
        }

        [Test(Description = "MediaLine.Receiver")]
        public void SetReceiver()
        {
            // Create the peer connections
            var pc_go = new GameObject("pc1");
            pc_go.SetActive(false); // prevent auto-activation of components
            var pc = pc_go.AddComponent<PeerConnection>();
            pc.AutoInitializeOnStart = false;

            // Create some video track sources
            VideoReceiver receiver1 = pc_go.AddComponent<VideoReceiver>();
            VideoReceiver receiver2 = pc_go.AddComponent<VideoReceiver>();
            Assert.AreEqual(0, receiver1.MediaLines.Count);
            Assert.AreEqual(0, receiver2.MediaLines.Count);

            // Create the media line
            MediaLine mediaLine = pc.AddMediaLine(MediaKind.Video);

            // Assign a video source to the media line
            mediaLine.Receiver = receiver1;
            Assert.AreEqual(mediaLine.Receiver, receiver1);
            Assert.AreEqual(1, receiver1.MediaLines.Count);
            Assert.IsTrue(receiver1.MediaLines.Contains(mediaLine));

            // No-op
            mediaLine.Receiver = receiver1;

            // Assign another video source to the media line
            mediaLine.Receiver = receiver2;
            Assert.AreEqual(mediaLine.Receiver, receiver2);
            Assert.AreEqual(0, receiver1.MediaLines.Count);
            Assert.IsFalse(receiver1.MediaLines.Contains(mediaLine));
            Assert.AreEqual(1, receiver2.MediaLines.Count);
            Assert.IsTrue(receiver2.MediaLines.Contains(mediaLine));

            // Remove it from the media line
            mediaLine.Receiver = null;
            Assert.IsNull(mediaLine.Receiver);
            Assert.AreEqual(0, receiver2.MediaLines.Count);
            Assert.IsFalse(receiver2.MediaLines.Contains(mediaLine));

            // No-op
            mediaLine.Receiver = null;

            // Set an invalid source (wrong media kind)
            Assert.Throws<ArgumentException>(() => mediaLine.Receiver = pc_go.AddComponent<AudioReceiver>());

            // Set an invalid source (not a MonoBehaviour)
            Assert.Throws<ArgumentException>(() => mediaLine.Receiver = new DummyNonMonoBehaviourVideoReceiver());

            // Set an invalid source (not implementing IMediaReceiverInternal)
            Assert.Throws<ArgumentException>(() => mediaLine.Receiver = pc_go.AddComponent<DummyMissingInternalInterfaceVideoReceiver>());
        }
    }
}
