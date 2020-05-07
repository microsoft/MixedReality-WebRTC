// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Microsoft.MixedReality.WebRTC.Unity.Tests.Runtime
{
    public class MockVideoSender : CustomVideoSender<Argb32VideoFrameStorage>
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

        [Test(Description = "Capture starts automatically if AutoStartOnEnabled=true.")]
        public void CaptureStartAuto()
        {
            // Create the peer connections
            var pc_go = new GameObject("pc1");
            pc_go.SetActive(false); // prevent auto-activation of components
            var pc = pc_go.AddComponent<PeerConnection>();
            pc.AutoInitializeOnStart = false;

            // Batch changes manually
            pc.AutoCreateOfferOnRenegotiationNeeded = false;

            // Create the video source
            VideoSender sender = pc_go.AddComponent<MockVideoSender>();
            sender.AutoStartOnEnabled = true;
            sender.TrackName = "track_name";
            MediaLine ml = pc.AddMediaLine(MediaKind.Video);
            ml.Sender = sender;

            // Confirm the sender has no track yet because it is inactive
            Assert.IsNull(sender.Track);
            Assert.IsFalse(sender.IsCapturing);

            // Activate the game object and the video sender component on it
            pc_go.SetActive(true);

            // Confirm the sender now has a track because it became active and AutoStartOnEnabled
            // is true to capture started automatically.
            Assert.IsNotNull(sender.Track);
            Assert.IsTrue(sender.IsCapturing);
        }

        [Test(Description = "Capture doesn't start automatically if AutoStartOnEnabled=false.")]
        public async void CaptureStartManualActive()
        {
            // Create the peer connections
            var pc_go = new GameObject("pc1");
            pc_go.SetActive(false); // prevent auto-activation of components
            var pc = pc_go.AddComponent<PeerConnection>();
            pc.AutoInitializeOnStart = false;

            // Batch changes manually
            pc.AutoCreateOfferOnRenegotiationNeeded = false;

            // Create the video source
            VideoSender sender = pc_go.AddComponent<MockVideoSender>();
            sender.AutoStartOnEnabled = false;
            sender.TrackName = "track_name";
            MediaLine ml = pc.AddMediaLine(MediaKind.Video);
            ml.Sender = sender;

            // Confirm the sender has no track yet because it is inactive
            Assert.IsNull(sender.Track);
            Assert.IsFalse(sender.IsCapturing);

            // Activate the game object and the video sender component on it
            pc_go.SetActive(true);

            // Confirm the sender still has no track because AutoStartOnEnabled = false
            Assert.IsNull(sender.Track);
            Assert.IsFalse(sender.IsCapturing);

            // Manually start capture
            await sender.StartCaptureAsync();
            Assert.IsNotNull(sender.Track);
            Assert.IsTrue(sender.IsCapturing);
        }

        [Test(Description = "Capture can be manually started even if not active.")]
        public async void CaptureStartManualInactive()
        {
            // Create the peer connections
            var pc_go = new GameObject("pc1");
            pc_go.SetActive(false); // prevent auto-activation of components
            var pc = pc_go.AddComponent<PeerConnection>();
            pc.AutoInitializeOnStart = false;

            // Batch changes manually
            pc.AutoCreateOfferOnRenegotiationNeeded = false;

            // Create the video source
            VideoSender sender = pc_go.AddComponent<MockVideoSender>();
            sender.enabled = false;
            sender.AutoStartOnEnabled = false;
            sender.TrackName = "track_name";
            MediaLine ml = pc.AddMediaLine(MediaKind.Video);
            ml.Sender = sender;

            // Confirm the sender has no track yet because it is inactive
            Assert.IsNull(sender.Track);
            Assert.IsFalse(sender.IsCapturing);

            // Manually start capture even if not enabled/active
            await sender.StartCaptureAsync();
            Assert.IsNotNull(sender.Track);
            Assert.IsTrue(sender.IsCapturing);

            // Because the component is inactive, don't forget to manually stop capture,
            // because the Unity lifecycle handlers like OnDestroy() are not called.
            sender.StopCapture();
            Assert.IsNull(sender.Track);
            Assert.IsFalse(sender.IsCapturing);
        }

        [Test(Description = "Capture can start and stop multiple times.")]
        public async void CaptureStartStop()
        {
            // Create the peer connections
            var pc_go = new GameObject("pc1");
            pc_go.SetActive(false); // prevent auto-activation of components
            var pc = pc_go.AddComponent<PeerConnection>();
            pc.AutoInitializeOnStart = false;

            // Batch changes manually
            pc.AutoCreateOfferOnRenegotiationNeeded = false;

            // Create the video source
            VideoSender sender = pc_go.AddComponent<MockVideoSender>();
            sender.enabled = false;
            sender.AutoStartOnEnabled = false;
            sender.TrackName = "track_name";
            MediaLine ml = pc.AddMediaLine(MediaKind.Video);
            ml.Sender = sender;

            // Confirm the sender has no track yet because it is inactive
            Assert.IsNull(sender.Track);
            Assert.IsFalse(sender.IsCapturing);

            // Manually start capture
            await sender.StartCaptureAsync();
            Assert.IsNotNull(sender.Track);
            Assert.IsTrue(sender.IsCapturing);

            // Start capture is no-op if called twice
            await sender.StartCaptureAsync();
            Assert.IsNotNull(sender.Track);
            Assert.IsTrue(sender.IsCapturing);

            // Stop capture
            sender.StopCapture();
            Assert.IsNull(sender.Track);
            Assert.IsFalse(sender.IsCapturing);

            // Stop capture is no-op if called twice
            sender.StopCapture();
            Assert.IsNull(sender.Track);
            Assert.IsFalse(sender.IsCapturing);

            // Capture can restart after being stopped
            await sender.StartCaptureAsync();
            Assert.IsNotNull(sender.Track);
            Assert.IsTrue(sender.IsCapturing);

            // Clean-up
            sender.StopCapture();
            Assert.IsNull(sender.Track);
            Assert.IsFalse(sender.IsCapturing);
        }

        // Capture starts automatically on attach if active.
        [UnityTest]
        public IEnumerator CaptureOnAttachStartIfActive()
        {
            // Create the peer connections
            var pc1_go = new GameObject("pc1");
            pc1_go.SetActive(false); // prevent auto-activation of components
            var pc1 = pc1_go.AddComponent<PeerConnection>();
            pc1.AutoInitializeOnStart = false;
            var pc2_go = new GameObject("pc2");
            pc2_go.SetActive(false); // prevent auto-activation of components
            var pc2 = pc2_go.AddComponent<PeerConnection>();
            pc2.AutoInitializeOnStart = false;

            // Batch changes manually
            pc1.AutoCreateOfferOnRenegotiationNeeded = false;
            pc2.AutoCreateOfferOnRenegotiationNeeded = false;

            // Create the signaler
            var sig_go = new GameObject("signaler");
            var sig = sig_go.AddComponent<LocalOnlySignaler>();
            sig.Peer1 = pc1;
            sig.Peer2 = pc2;

            // Create the video sources on peer #1
            VideoSender sender1 = pc1_go.AddComponent<MockVideoSender>();
            sender1.enabled = true;
            sender1.AutoStartOnEnabled = false;
            sender1.TrackName = "track_name";
            MediaLine tr1 = pc1.AddMediaLine(MediaKind.Video);
            tr1.Sender = sender1;

            // Create the video sources on peer #2
            VideoReceiver receiver2 = pc1_go.AddComponent<VideoReceiver>();
            MediaLine tr2 = pc2.AddMediaLine(MediaKind.Video);
            tr2.Receiver = receiver2;

            // Activate
            pc1_go.SetActive(true);
            pc2_go.SetActive(true);

            // Initialize
            var initializedEvent1 = new ManualResetEventSlim(initialState: false);
            pc1.OnInitialized.AddListener(() => initializedEvent1.Set());
            Assert.IsNull(pc1.Peer);
            pc1.InitializeAsync().Wait(millisecondsTimeout: 50000);
            var initializedEvent2 = new ManualResetEventSlim(initialState: false);
            pc2.OnInitialized.AddListener(() => initializedEvent2.Set());
            Assert.IsNull(pc2.Peer);
            pc2.InitializeAsync().Wait(millisecondsTimeout: 50000);

            // Wait a frame so that the Unity event OnInitialized can propagate
            yield return null;

            // Check the event was raised
            Assert.IsTrue(initializedEvent1.Wait(millisecondsTimeout: 50000));
            Assert.IsNotNull(pc1.Peer);
            Assert.IsTrue(initializedEvent2.Wait(millisecondsTimeout: 50000));
            Assert.IsNotNull(pc2.Peer);

            // AutoStartOnEnabled = false
            Assert.IsNull(sender1.Track);

            // Confirm the receiver track is not added yet, since remote tracks are only instantiated
            // as the result of a session negotiation.
            Assert.IsNull(receiver2.Track);

            // Connect
            Assert.IsTrue(sig.Connect(millisecondsTimeout: 60000));

            // Wait a frame so that the Unity events for streams started can propagate
            yield return null;

            // Check attached and that attaching forced the track creation on active sender
            Assert.IsNotNull(sender1.Track);
            Assert.IsTrue(sender1.IsCapturing);
            Assert.IsTrue(sender1.IsStreaming);
            Assert.IsNotNull(sender1.Transceiver);
            Assert.IsNotNull(receiver2.Track);
            Assert.IsTrue(receiver2.IsLive);
            Assert.IsTrue(receiver2.IsStreaming);
            Assert.IsNotNull(receiver2.Transceiver);
        }

        // Capture doesn't start automatically on attach if inactive.
        [UnityTest]
        public IEnumerator CaptureOnAttachDoNotStartIfInactive()
        {
            // Create the peer connections
            var pc1_go = new GameObject("pc1");
            pc1_go.SetActive(false); // prevent auto-activation of components
            var pc1 = pc1_go.AddComponent<PeerConnection>();
            pc1.AutoInitializeOnStart = false;
            var pc2_go = new GameObject("pc2");
            pc2_go.SetActive(false); // prevent auto-activation of components
            var pc2 = pc2_go.AddComponent<PeerConnection>();
            pc2.AutoInitializeOnStart = false;

            // Batch changes manually
            pc1.AutoCreateOfferOnRenegotiationNeeded = false;
            pc2.AutoCreateOfferOnRenegotiationNeeded = false;

            // Create the signaler
            var sig_go = new GameObject("signaler");
            var sig = sig_go.AddComponent<LocalOnlySignaler>();
            sig.Peer1 = pc1;
            sig.Peer2 = pc2;

            // Create the video sources on peer #1
            VideoSender sender1 = pc1_go.AddComponent<MockVideoSender>();
            sender1.enabled = false;
            sender1.AutoStartOnEnabled = false;
            sender1.TrackName = "track_name";
            MediaLine tr1 = pc1.AddMediaLine(MediaKind.Video);
            tr1.Sender = sender1;

            // Create the video sources on peer #2
            VideoReceiver receiver2 = pc1_go.AddComponent<VideoReceiver>();
            MediaLine tr2 = pc2.AddMediaLine(MediaKind.Video);
            tr2.Receiver = receiver2;

            // Activate
            pc1_go.SetActive(true);
            pc2_go.SetActive(true);

            // Initialize
            var initializedEvent1 = new ManualResetEventSlim(initialState: false);
            pc1.OnInitialized.AddListener(() => initializedEvent1.Set());
            Assert.IsNull(pc1.Peer);
            pc1.InitializeAsync().Wait(millisecondsTimeout: 50000);
            var initializedEvent2 = new ManualResetEventSlim(initialState: false);
            pc2.OnInitialized.AddListener(() => initializedEvent2.Set());
            Assert.IsNull(pc2.Peer);
            pc2.InitializeAsync().Wait(millisecondsTimeout: 50000);

            // Wait a frame so that the Unity event OnInitialized can propagate
            yield return null;

            // Check the event was raised
            Assert.IsTrue(initializedEvent1.Wait(millisecondsTimeout: 50000));
            Assert.IsNotNull(pc1.Peer);
            Assert.IsTrue(initializedEvent2.Wait(millisecondsTimeout: 50000));
            Assert.IsNotNull(pc2.Peer);

            // AutoStartOnEnabled = false
            Assert.IsNull(sender1.Track);

            // Confirm the receiver track is not added yet, since remote tracks are only instantiated
            // as the result of a session negotiation.
            Assert.IsNull(receiver2.Track);

            // Connect
            Assert.IsTrue(sig.Connect(millisecondsTimeout: 60000));

            // Wait a frame so that the Unity events for streams started can propagate
            yield return null;

            // Check attached but attaching did not start track because sender was inactive (disabled)
            Assert.IsNull(sender1.Track);
            Assert.IsFalse(sender1.IsCapturing);
            Assert.IsFalse(sender1.IsStreaming);
            Assert.IsNotNull(sender1.Transceiver);
            Assert.IsNotNull(receiver2.Track);
            Assert.IsTrue(receiver2.IsLive);
            Assert.IsTrue(receiver2.IsStreaming);
            Assert.IsNotNull(receiver2.Transceiver);
        }
    }
}
