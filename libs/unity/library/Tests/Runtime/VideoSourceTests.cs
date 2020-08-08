// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Microsoft.MixedReality.WebRTC.Unity.Tests.Runtime
{
    public class VideoSourceTests
    {
        [SetUp]
        public void Setup()
        {
        }

        [TearDown]
        public void Shutdown()
        {
            // Force shutdown in case a test failure prevented cleaning-up some
            // native resources, thereby locking the native module and preventing
            // it from being unloaded/reloaded in the Unity editor.
            Library.ReportLiveObjects();
            //Library.ForceShutdown();
        }

        private IEnumerator SingleTwoWaysImpl(bool withSender1, bool withReceiver1, bool withSender2, bool withReceiver2)
        {
            // Create the peer connections
            var pc1_go = new GameObject("pc1");
            pc1_go.SetActive(false); // prevent auto-activation of components
            var pc1 = pc1_go.AddComponent<PeerConnection>();
            var pc2_go = new GameObject("pc2");
            pc2_go.SetActive(false); // prevent auto-activation of components
            var pc2 = pc2_go.AddComponent<PeerConnection>();

            // Batch changes manually
            pc1.AutoCreateOfferOnRenegotiationNeeded = false;
            pc2.AutoCreateOfferOnRenegotiationNeeded = false;

            // Create the signaler
            var sig_go = new GameObject("signaler");
            var sig = sig_go.AddComponent<LocalOnlySignaler>();
            sig.Peer1 = pc1;
            sig.Peer2 = pc2;

            // Create the video sources on peer #1
            VideoTrackSource source1 = null;
            VideoReceiver receiver1 = null;
            if (withSender1)
            {
                source1 = pc1_go.AddComponent<UniformColorVideoSource>();
            }
            if (withReceiver1)
            {
                receiver1 = pc1_go.AddComponent<VideoReceiver>();
            }
            MediaLine ml1 = pc1.AddMediaLine(MediaKind.Video);
            ml1.SenderTrackName = "video_track_1";
            ml1.Source = source1;
            ml1.Receiver = receiver1;

            // Create the video sources on peer #2
            VideoTrackSource source2 = null;
            VideoReceiver receiver2 = null;
            if (withSender2)
            {
                source2 = pc2_go.AddComponent<UniformColorVideoSource>();
            }
            if (withReceiver2)
            {
                receiver2 = pc1_go.AddComponent<VideoReceiver>();
            }
            MediaLine ml2 = pc2.AddMediaLine(MediaKind.Video);
            ml2.SenderTrackName = "video_track_2";
            ml2.Source = source2;
            ml2.Receiver = receiver2;

            // Initialize
            yield return PeerConnectionTests.InitializeAndWait(pc1);
            yield return PeerConnectionTests.InitializeAndWait(pc2);

            // Confirm the sources are ready
            if (withSender1)
            {
                Assert.IsTrue(source1.IsLive);

            }
            if (withSender2)
            {
                Assert.IsTrue(source2.IsLive);
            }

            // Confirm the sender track is not created yet; it will be when the connection starts
            Assert.IsNull(ml1.LocalTrack);
            Assert.IsNull(ml2.LocalTrack);

            // Confirm the receiver track is not added yet, since remote tracks are only instantiated
            // as the result of a session negotiation.
            if (withReceiver1)
            {
                Assert.IsNull(receiver1.Track);
            }
            if (withReceiver2)
            {
                Assert.IsNull(receiver2.Track);
            }

            // Connect
            Assert.IsTrue(sig.StartConnection());
            yield return sig.WaitForConnection(millisecondsTimeout: 10000);
            Assert.IsTrue(sig.IsConnected);

            // Wait a frame so that the Unity events for streams started can propagate
            yield return null;

            // Check pairing
            {
                bool hasSend1 = false;
                bool hasSend2 = false;
                bool hasRecv1 = false;
                bool hasRecv2 = false;

                // Local tracks exist if manually added (independently of negotiation)
                Assert.AreEqual(withSender1 ? 1 : 0, pc1.Peer.LocalVideoTracks.Count());
                Assert.AreEqual(withSender2 ? 1 : 0, pc2.Peer.LocalVideoTracks.Count());

                // Remote tracks exist if paired with a sender on the remote peer
                if (withReceiver1 && withSender2) // R <= S
                {
                    Assert.IsNotNull(receiver1.Track);
                    Assert.IsNotNull(ml2.LocalTrack);
                    hasRecv1 = true;
                    hasSend2 = true;
                }
                if (withSender1 && withReceiver2) // S => R
                {
                    Assert.IsNotNull(ml1.LocalTrack);
                    Assert.IsNotNull(receiver2.Track);
                    hasSend1 = true;
                    hasRecv2 = true;
                }
                Assert.AreEqual(hasRecv1 ? 1 : 0, pc1.Peer.RemoteVideoTracks.Count());
                Assert.AreEqual(hasRecv2 ? 1 : 0, pc2.Peer.RemoteVideoTracks.Count());

                // Transceivers are consistent with pairing
                Assert.IsTrue(ml1.Transceiver.NegotiatedDirection.HasValue);
                Assert.AreEqual(hasSend1, Transceiver.HasSend(ml1.Transceiver.NegotiatedDirection.Value));
                Assert.AreEqual(hasRecv1, Transceiver.HasRecv(ml1.Transceiver.NegotiatedDirection.Value));
                Assert.IsTrue(ml2.Transceiver.NegotiatedDirection.HasValue);
                Assert.AreEqual(hasSend2, Transceiver.HasSend(ml2.Transceiver.NegotiatedDirection.Value));
                Assert.AreEqual(hasRecv2, Transceiver.HasRecv(ml2.Transceiver.NegotiatedDirection.Value));
            }

            Object.Destroy(pc1_go);
            Object.Destroy(pc2_go);
            Object.Destroy(sig_go);
        }

        [UnityTest]
        public IEnumerator SingleMissingAll() // _ = _
        {
            yield return SingleTwoWaysImpl(withSender1: false, withReceiver1: false, withSender2: false, withReceiver2: false);
        }

        [UnityTest]
        public IEnumerator SingleOneWay() // S => R
        {
            yield return SingleTwoWaysImpl(withSender1: true, withReceiver1: false, withSender2: false, withReceiver2: true);
        }

        [UnityTest]
        public IEnumerator SingleOneWayMissingRecvOffer() // S = _
        {
            yield return SingleTwoWaysImpl(withSender1: true, withReceiver1: false, withSender2: false, withReceiver2: false);
        }

        [UnityTest]
        public IEnumerator SingleOneWayMissingSenderOffer() // _ = R
        {
            yield return SingleTwoWaysImpl(withSender1: false, withReceiver1: false, withSender2: false, withReceiver2: true);
        }

        [UnityTest]
        public IEnumerator SingleTwoWaysMissingSenderOffer() // _R <= SR
        {
            yield return SingleTwoWaysImpl(withSender1: false, withReceiver1: true, withSender2: true, withReceiver2: true);
        }

        [UnityTest]
        public IEnumerator SingleTwoWaysMissingReceiverOffer() // SR <= S_
        {
            yield return SingleTwoWaysImpl(withSender1: true, withReceiver1: true, withSender2: true, withReceiver2: false);
        }

        [UnityTest]
        public IEnumerator SingleTwoWaysMissingReceiverAnswer() // S_ => SR
        {
            yield return SingleTwoWaysImpl(withSender1: true, withReceiver1: false, withSender2: true, withReceiver2: true);
        }

        [UnityTest]
        public IEnumerator SingleTwoWaysMissingSenderAnswer() // SR => _R
        {
            yield return SingleTwoWaysImpl(withSender1: true, withReceiver1: true, withSender2: false, withReceiver2: true);
        }

        [UnityTest]
        public IEnumerator SingleTwoWays() // SR <=> SR
        {
            yield return SingleTwoWaysImpl(withSender1: true, withReceiver1: true, withSender2: true, withReceiver2: true);
        }

        class PeerConfig
        {
            // Input
            public Transceiver.Direction desiredDirection;
            public MediaLine mediaLine;
            public UniformColorVideoSource source;
            public VideoReceiver receiver;

            // Output
            public bool expectSender;
            public bool expectReceiver;
        }

        class MultiConfig
        {
            public PeerConfig peer1;
            public PeerConfig peer2;
        };

        [UnityTest]
        public IEnumerator Multi()
        {
            // Create the peer connections
            var pc1_go = new GameObject("pc1");
            pc1_go.SetActive(false); // prevent auto-activation of components
            var pc1 = pc1_go.AddComponent<PeerConnection>();
            var pc2_go = new GameObject("pc2");
            pc2_go.SetActive(false); // prevent auto-activation of components
            var pc2 = pc2_go.AddComponent<PeerConnection>();

            // Batch changes manually
            pc1.AutoCreateOfferOnRenegotiationNeeded = false;
            pc2.AutoCreateOfferOnRenegotiationNeeded = false;

            // Create the signaler
            var sig_go = new GameObject("signaler");
            var sig = sig_go.AddComponent<LocalOnlySignaler>();
            sig.Peer1 = pc1;
            sig.Peer2 = pc2;

            // Create the senders and receivers
            //     P1     P2
            // 0 : S   =>  R
            // 1 : SR <=> SR
            // 2 : S   => SR
            // 3 :  R <=  SR
            // 4 : S   =>  R

            const int NumTransceivers = 5;

            // P1 has 4 senders added to it
            int numLocal1 = 4;

            // P1 receives 2 tracks from the 3 P2 senders (one is refused)
            int numRemote1 = 2;

            // P2 has 3 senders added to it
            int numLocal2 = 3;

            // P2 receives 4 tracks from the 4 P1 senders
            int numRemote2 = 4;

            var cfgs = new MultiConfig[NumTransceivers]
            {
                new MultiConfig {
                    peer1 = new PeerConfig {
                        desiredDirection = Transceiver.Direction.SendOnly,
                        expectSender = true,
                        expectReceiver = false,
                    },
                    peer2 = new PeerConfig {
                        desiredDirection = Transceiver.Direction.ReceiveOnly,
                        expectSender = false,
                        expectReceiver = true,
                    }
                },
                new MultiConfig {
                    peer1 = new PeerConfig {
                        desiredDirection = Transceiver.Direction.SendReceive,
                        expectSender = true,
                        expectReceiver = true,
                    },
                    peer2 = new PeerConfig {
                        desiredDirection = Transceiver.Direction.SendReceive,
                        expectSender = true,
                        expectReceiver = true,
                    },
                },
                new MultiConfig {
                    peer1 = new PeerConfig {
                        desiredDirection = Transceiver.Direction.SendOnly,
                        expectSender = true,
                        expectReceiver = false,
                    },
                    peer2 = new PeerConfig {
                        desiredDirection = Transceiver.Direction.SendReceive,
                        expectSender = true,
                        expectReceiver = true,
                    },
                },
                new MultiConfig {
                    peer1 = new PeerConfig {
                        desiredDirection = Transceiver.Direction.ReceiveOnly,
                        expectSender = false,
                        expectReceiver = true,
                    },
                    peer2 = new PeerConfig {
                        desiredDirection = Transceiver.Direction.SendReceive,
                        expectSender = true,
                        expectReceiver = false,
                    },
                },
                new MultiConfig {
                    peer1 = new PeerConfig {
                        desiredDirection = Transceiver.Direction.SendOnly,
                        expectSender = true,
                        expectReceiver = false,
                    },
                    peer2 = new PeerConfig {
                        desiredDirection = Transceiver.Direction.ReceiveOnly,
                        expectSender = false,
                        expectReceiver = true,
                    },
                },
            };
            for (int i = 0; i < NumTransceivers; ++i)
            {
                var cfg = cfgs[i];

                {
                    MediaLine ml1 = pc1.AddMediaLine(MediaKind.Video);
                    var peer = cfg.peer1;
                    peer.mediaLine = ml1;
                    if (Transceiver.HasSend(peer.desiredDirection))
                    {
                        var source1 = pc1_go.AddComponent<UniformColorVideoSource>();
                        peer.source = source1;
                        ml1.Source = source1;
                        ml1.SenderTrackName = $"track{i}";
                    }
                    if (Transceiver.HasRecv(peer.desiredDirection))
                    {
                        var receiver1 = pc1_go.AddComponent<VideoReceiver>();
                        peer.receiver = receiver1;
                        ml1.Receiver = receiver1;
                    }
                }

                {
                    MediaLine ml2 = pc2.AddMediaLine(MediaKind.Video);
                    var peer = cfg.peer2;
                    peer.mediaLine = ml2;
                    if (Transceiver.HasSend(peer.desiredDirection))
                    {
                        var source2 = pc2_go.AddComponent<UniformColorVideoSource>();
                        peer.source = source2;
                        ml2.Source = source2;
                        ml2.SenderTrackName = $"track{i}";
                    }
                    if (Transceiver.HasRecv(peer.desiredDirection))
                    {
                        var receiver2 = pc2_go.AddComponent<VideoReceiver>();
                        peer.receiver = receiver2;
                        ml2.Receiver = receiver2;
                    }
                }
            }

            // Initialize
            yield return PeerConnectionTests.InitializeAndWait(pc1);
            yield return PeerConnectionTests.InitializeAndWait(pc2);

            // Confirm the sources are ready
            for (int i = 0; i < NumTransceivers; ++i)
            {
                var cfg = cfgs[i];
                if (cfg.peer1.expectSender)
                {
                    Assert.IsNotNull(cfg.peer1.source, $"Missing source #{i} on Peer #1");
                    Assert.IsTrue(cfg.peer1.source.IsLive, $"Source #{i} is not ready on Peer #1");
                    Assert.IsNull(cfg.peer1.mediaLine.LocalTrack); // created during connection
                }
                if (cfg.peer2.expectSender)
                {
                    Assert.IsNotNull(cfg.peer2.source, $"Missing source #{i} on Peer #2");
                    Assert.IsTrue(cfg.peer2.source.IsLive, $"Source #{i} is not ready on Peer #2");
                    Assert.IsNull(cfg.peer2.mediaLine.LocalTrack); // created during connection
                }
            }

            // Connect
            Assert.IsTrue(sig.StartConnection());
            yield return sig.WaitForConnection(millisecondsTimeout: 60000);
            Assert.IsTrue(sig.IsConnected);

            // Wait a frame so that the Unity events for streams started can propagate
            yield return null;

            // Check pairing
            Assert.AreEqual(numLocal1, pc1.Peer.LocalVideoTracks.Count());
            Assert.AreEqual(numRemote1, pc1.Peer.RemoteVideoTracks.Count());
            Assert.AreEqual(numLocal2, pc2.Peer.LocalVideoTracks.Count());
            Assert.AreEqual(numRemote2, pc2.Peer.RemoteVideoTracks.Count());
            for (int i = 0; i < NumTransceivers; ++i)
            {
                var cfg = cfgs[i];
                if (cfg.peer1.expectSender)
                {
                    Assert.IsNotNull(cfg.peer1.mediaLine.LocalTrack, $"Transceiver #{i} missing local sender track on Peer #1");
                }
                if (cfg.peer1.expectReceiver)
                {
                    Assert.IsNotNull(cfg.peer1.receiver.Track, $"Transceiver #{i} missing remote track on Peer #1");
                }
                if (cfg.peer2.expectSender)
                {
                    Assert.IsNotNull(cfg.peer2.mediaLine.LocalTrack, $"Transceiver #{i} missing local sender track on Peer #2");
                }
                if (cfg.peer2.expectReceiver)
                {
                    Assert.IsNotNull(cfg.peer2.receiver.Track, $"Transceiver #{i} Missing remote track on Peer #2");
                }
            }

            // Change the senders and receivers and transceivers direction
            //        old            new
            //     P1     P2      P1     P2
            // 0 : S   =>  R          =   R     P1 stops sending
            // 1 : SR <=> SR      SR  =>  R     P2 stops sending
            // 2 : S   => SR      SR <=> SR     P1 starts receiving
            // 3 :  R <=  SR      SR <=> SR     P1 starts sending
            // 4 : S   =>  R      S   =         P2 stops receiving

            numLocal1 = 4;
            numRemote1 = 2;
            numLocal2 = 2;
            numRemote2 = 3;

            // #0 - P1 stops sending
            {
                var cfg = cfgs[0];
                cfg.peer1.mediaLine.Source = null;
                cfg.peer1.expectSender = false;
                cfg.peer1.expectReceiver = false;
                cfg.peer2.expectSender = false;
                cfg.peer2.expectReceiver = false;
            }

            // #1 - P2 stops sending
            {
                var cfg = cfgs[1];
                cfg.peer2.mediaLine.Source = null;
                cfg.peer1.expectSender = true;
                cfg.peer1.expectReceiver = false;
                cfg.peer2.expectSender = false;
                cfg.peer2.expectReceiver = true;
            }

            // #2 - P1 starts receiving
            {
                var cfg = cfgs[2];
                var receiver2 = pc2_go.AddComponent<VideoReceiver>();
                cfg.peer1.receiver = receiver2;
                cfg.peer1.mediaLine.Receiver = receiver2;
                cfg.peer1.expectSender = true;
                cfg.peer1.expectReceiver = true;
                cfg.peer2.expectSender = true;
                cfg.peer2.expectReceiver = true;
            }

            // #3 - P1 starts sending
            {
                var cfg = cfgs[3];
                var source1 = pc1_go.AddComponent<UniformColorVideoSource>();
                cfg.peer1.source = source1;
                cfg.peer1.mediaLine.Source = source1;
                cfg.peer1.expectSender = true;
                cfg.peer1.expectReceiver = true;
                cfg.peer2.expectSender = true;
                cfg.peer2.expectReceiver = true;
            }

            // #4 - P2 stops receiving
            {
                var cfg = cfgs[4];
                cfg.peer2.mediaLine.Receiver = null;
                cfg.peer1.expectSender = false;
                cfg.peer1.expectReceiver = false;
                cfg.peer2.expectSender = false;
                cfg.peer2.expectReceiver = false;
            }

            // Renegotiate
            Assert.IsTrue(sig.StartConnection());
            yield return sig.WaitForConnection(millisecondsTimeout: 60000);
            Assert.IsTrue(sig.IsConnected);

            // Wait a frame so that the Unity events for streams started can propagate
            yield return null;

            // Check pairing
            Assert.AreEqual(numLocal1, pc1.Peer.LocalVideoTracks.Count());
            Assert.AreEqual(numRemote1, pc1.Peer.RemoteVideoTracks.Count());
            Assert.AreEqual(numLocal2, pc2.Peer.LocalVideoTracks.Count());
            Assert.AreEqual(numRemote2, pc2.Peer.RemoteVideoTracks.Count());
            for (int i = 0; i < NumTransceivers; ++i)
            {
                var cfg = cfgs[i];
                if (cfg.peer1.expectReceiver)
                {
                    Assert.IsNotNull(cfg.peer1.receiver.Track, $"Transceiver #{i} missing remote track on Peer #1");
                }
                if (cfg.peer2.expectReceiver)
                {
                    Assert.IsNotNull(cfg.peer2.receiver.Track, $"Transceiver #{i} Missing remote track on Peer #2");
                }
            }

            Object.Destroy(pc1_go);
            Object.Destroy(pc2_go);
            Object.Destroy(sig_go);
        }

        /// Negotiate 3 sessions in a row:
        ///     P1    P2
        /// a.  S  =>  R   One way P1 => P2
        /// b.     =   R   Stop sending; transceiver goes to inactive
        /// c.  S  =>  R   Resume sending; transceiver re-activate
        [UnityTest]
        public IEnumerator Negotiate_SxS_to_R()
        {
            // Create the peer connections
            var pc1_go = new GameObject("pc1");
            pc1_go.SetActive(false); // prevent auto-activation of components
            var pc1 = pc1_go.AddComponent<PeerConnection>();
            var pc2_go = new GameObject("pc2");
            pc2_go.SetActive(false); // prevent auto-activation of components
            var pc2 = pc2_go.AddComponent<PeerConnection>();

            // Batch changes manually
            pc1.AutoCreateOfferOnRenegotiationNeeded = false;
            pc2.AutoCreateOfferOnRenegotiationNeeded = false;

            // Create the signaler
            var sig_go = new GameObject("signaler");
            var sig = sig_go.AddComponent<LocalOnlySignaler>();
            sig.Peer1 = pc1;
            sig.Peer2 = pc2;

            // Create the sender video source
            var source1 = pc1_go.AddComponent<UniformColorVideoSource>();
            //source1.SenderTrackName = "track_name";
            MediaLine ml1 = pc1.AddMediaLine(MediaKind.Video);
            Assert.IsNotNull(ml1);
            Assert.AreEqual(MediaKind.Video, ml1.MediaKind);
            ml1.Source = source1;

            // Create the receiver video source
            var receiver2 = pc2_go.AddComponent<VideoReceiver>();
            MediaLine ml2 = pc2.AddMediaLine(MediaKind.Video);
            Assert.IsNotNull(ml2);
            Assert.AreEqual(MediaKind.Video, ml2.MediaKind);
            ml2.Receiver = receiver2;

            // Initialize
            yield return PeerConnectionTests.InitializeAndWait(pc1);
            yield return PeerConnectionTests.InitializeAndWait(pc2);

            // Confirm the source is ready, but the sender track is not created yet

            Assert.IsTrue(source1.IsLive);
            Assert.IsNull(ml1.LocalTrack);

            // Connect
            Assert.IsTrue(sig.StartConnection());
            yield return sig.WaitForConnection(millisecondsTimeout: 60000);
            Assert.IsTrue(sig.IsConnected);

            // Wait a frame so that the Unity events for streams started can propagate
            yield return null;

            // Check transceiver update
            Assert.IsNotNull(ml1.Transceiver); // first negotiation creates this
            Assert.IsNotNull(ml2.Transceiver); // first negotiation creates this
            Assert.AreEqual(Transceiver.Direction.SendOnly, ml1.Transceiver.DesiredDirection);
            Assert.AreEqual(Transceiver.Direction.ReceiveOnly, ml2.Transceiver.DesiredDirection);
            Assert.IsTrue(ml1.Transceiver.NegotiatedDirection.HasValue);
            Assert.IsTrue(ml2.Transceiver.NegotiatedDirection.HasValue);
            Assert.AreEqual(Transceiver.Direction.SendOnly, ml1.Transceiver.NegotiatedDirection.Value);
            Assert.AreEqual(Transceiver.Direction.ReceiveOnly, ml2.Transceiver.NegotiatedDirection.Value);
            var video_tr1 = ml1.Transceiver;
            Assert.IsNotNull(video_tr1);
            Assert.AreEqual(MediaKind.Video, video_tr1.MediaKind);
            var video_tr2 = ml2.Transceiver;
            Assert.IsNotNull(video_tr2);
            Assert.AreEqual(MediaKind.Video, video_tr2.MediaKind);

            // Check track pairing
            Assert.IsNotNull(ml1.LocalTrack); // created during connection
            Assert.IsNotNull(receiver2.Track); // paired
            Assert.AreEqual(video_tr1.LocalTrack, ml1.LocalTrack); // sender attached
            Assert.AreEqual(video_tr2.RemoteTrack, receiver2.Track); // receiver paired

            // ====== Remove sender ==============================

            // Remove the sender from #1
            ml1.Source = null;

            // Renegotiate
            Assert.IsTrue(sig.StartConnection());
            yield return sig.WaitForConnection(millisecondsTimeout: 60000);
            Assert.IsTrue(sig.IsConnected);

            // Wait a frame so that the Unity events for streams started can propagate
            yield return null;

            // Check transceiver update
            Assert.IsNotNull(ml1.Transceiver); // immutable
            Assert.IsNotNull(ml2.Transceiver); // immutable
            Assert.AreEqual(Transceiver.Direction.Inactive, ml1.Transceiver.DesiredDirection); // set ml1 sender to null above
            Assert.AreEqual(Transceiver.Direction.ReceiveOnly, ml2.Transceiver.DesiredDirection); // no change
            Assert.IsTrue(ml1.Transceiver.NegotiatedDirection.HasValue);
            Assert.IsTrue(ml2.Transceiver.NegotiatedDirection.HasValue);
            Assert.AreEqual(Transceiver.Direction.Inactive, ml1.Transceiver.NegotiatedDirection.Value); // desired
            Assert.AreEqual(Transceiver.Direction.Inactive, ml2.Transceiver.NegotiatedDirection.Value); // inactive * recvonly = inactive
            Assert.AreEqual(video_tr1, ml1.Transceiver); // immutable
            Assert.AreEqual(video_tr2, ml2.Transceiver); // immutable

            // Check track pairing
            Assert.IsNull(ml1.LocalTrack); // no source, so media line destroyed its sender track...
            Assert.IsNull(video_tr1.LocalTrack); // ...after detaching it from the transceiver
            Assert.IsNull(receiver2.Track); // transceiver is inactive and remote tracks are not owned
            Assert.IsNull(video_tr2.RemoteTrack); // unpaired

            // ====== Re-add sender ==============================

            // Re-add the sender on #1
            ml1.Source = source1;

            // Renegotiate
            Assert.IsTrue(sig.StartConnection());
            yield return sig.WaitForConnection(millisecondsTimeout: 60000);
            Assert.IsTrue(sig.IsConnected);

            // Wait a frame so that the Unity events for streams started can propagate
            yield return null;

            // Check transceiver update
            Assert.IsNotNull(ml1.Transceiver); // immutable
            Assert.IsNotNull(ml2.Transceiver); // immutable
            Assert.AreEqual(Transceiver.Direction.SendOnly, ml1.Transceiver.DesiredDirection); // set ml1 sender above
            Assert.AreEqual(Transceiver.Direction.ReceiveOnly, ml2.Transceiver.DesiredDirection); // no change
            Assert.IsTrue(ml1.Transceiver.NegotiatedDirection.HasValue);
            Assert.IsTrue(ml2.Transceiver.NegotiatedDirection.HasValue);
            Assert.AreEqual(Transceiver.Direction.SendOnly, ml1.Transceiver.NegotiatedDirection.Value); // desired
            Assert.AreEqual(Transceiver.Direction.ReceiveOnly, ml2.Transceiver.NegotiatedDirection.Value); // accepted
            Assert.AreEqual(video_tr1, ml1.Transceiver); // immutable
            Assert.AreEqual(video_tr2, ml2.Transceiver); // immutable

            // Check track pairing
            Assert.IsNotNull(ml1.LocalTrack); // new source again, media line re-created a sender track
            Assert.IsNotNull(ml1.LocalTrack.Transceiver); // ...and attached it to the transceiver...
            Assert.AreEqual(video_tr1, ml1.LocalTrack.Transceiver);
            Assert.IsNotNull(video_tr1.LocalTrack); // ...and conversely
            Assert.AreEqual(ml1.LocalTrack, video_tr1.LocalTrack);
            Assert.IsNotNull(receiver2.Track); // transceiver is active again and remote track was re-created
            Assert.IsNotNull(receiver2.Track.Transceiver);
            Assert.AreEqual(video_tr2, receiver2.Track.Transceiver);
            Assert.IsNotNull(video_tr2.RemoteTrack); // re-paired
            Assert.AreEqual(receiver2.Track, video_tr2.RemoteTrack);

            Object.Destroy(pc1_go);
            Object.Destroy(pc2_go);
            Object.Destroy(sig_go);
        }

        /// <summary>
        /// Test interleaving of media transceivers and data channels, which produce a discontinuity in
        /// the media line indices of the media transceivers since data channels also consume some media
        /// line. This test ensures the transceiver indexing and pairing is robust to those discontinuities.
        /// </summary>
        [UnityTest]
        public IEnumerator InterleavedMediaAndData()
        {
            // Create the peer connections
            var pc1_go = new GameObject("pc1");
            pc1_go.SetActive(false); // prevent auto-activation of components
            var pc1 = pc1_go.AddComponent<PeerConnection>();
            var pc2_go = new GameObject("pc2");
            pc2_go.SetActive(false); // prevent auto-activation of components
            var pc2 = pc2_go.AddComponent<PeerConnection>();

            // Batch changes manually
            pc1.AutoCreateOfferOnRenegotiationNeeded = false;
            pc2.AutoCreateOfferOnRenegotiationNeeded = false;

            // Create the signaler
            var sig_go = new GameObject("signaler");
            var sig = sig_go.AddComponent<LocalOnlySignaler>();
            sig.Peer1 = pc1;
            sig.Peer2 = pc2;

            // Create the sender video source
            var source1 = pc1_go.AddComponent<UniformColorVideoSource>();
            //sender1.SenderTrackName = "track_name";
            MediaLine ml1 = pc1.AddMediaLine(MediaKind.Video);
            Assert.IsNotNull(ml1);
            ml1.Source = source1;

            // Create the receiver video source
            var receiver2 = pc2_go.AddComponent<VideoReceiver>();
            MediaLine ml2 = pc2.AddMediaLine(MediaKind.Video);
            Assert.IsNotNull(ml2);
            ml2.Receiver = receiver2;

            // Initialize
            yield return PeerConnectionTests.InitializeAndWait(pc1);
            yield return PeerConnectionTests.InitializeAndWait(pc2);

            // Confirm the source is ready, but the sender track is not created yet
            Assert.IsTrue(source1.IsLive);
            Assert.IsNull(ml1.LocalTrack);

            // Create some dummy out-of-band data channel to force SCTP negotiation
            // during the first offer, and be able to add some in-band data channels
            // later via subsequent SDP session negotiations.
            {
                Task<DataChannel> t1 = pc1.Peer.AddDataChannelAsync(42, "dummy", ordered: true, reliable: true);
                Task<DataChannel> t2 = pc2.Peer.AddDataChannelAsync(42, "dummy", ordered: true, reliable: true);
                Assert.IsTrue(t1.Wait(millisecondsTimeout: 10000));
                Assert.IsTrue(t2.Wait(millisecondsTimeout: 10000));
            }

            // Connect
            Assert.IsTrue(sig.StartConnection());
            yield return sig.WaitForConnection(millisecondsTimeout: 60000);
            Assert.IsTrue(sig.IsConnected);

            // Wait a frame so that the Unity events for streams started can propagate
            yield return null;

            // Check transceiver update
            var video_tr1 = ml1.Transceiver;
            Assert.IsNotNull(video_tr1);
            var video_tr2 = ml2.Transceiver;
            Assert.IsNotNull(video_tr2);
            Assert.AreEqual(0, video_tr1.MlineIndex);
            Assert.AreEqual(0, video_tr2.MlineIndex);

            // ====== Add in-band data channel ====================================

            // Add an in-band data channel on peer #1
            DataChannel dc1;
            {
                Task<DataChannel> t1 = pc1.Peer.AddDataChannelAsync("test_data_channel", ordered: true, reliable: true);
                Assert.IsTrue(t1.Wait(millisecondsTimeout: 10000));
                dc1 = t1.Result;
            }

            // Prepare to receive a new data channel on peer #2
            DataChannel dc2 = null;
            var dc2_added_ev = new ManualResetEventSlim(initialState: false);
            pc2.Peer.DataChannelAdded += (DataChannel channel) => { dc2 = channel; dc2_added_ev.Set(); };

            // Renegotiate; data channel will consume media line #1
            Assert.IsTrue(sig.StartConnection());
            yield return sig.WaitForConnection(millisecondsTimeout: 60000);
            Assert.IsTrue(sig.IsConnected);

            // Do not assume that connecting is enough to get the data channel, as callbacks are
            // asynchronously invoked. Instead explicitly wait for the created event to be raised.
            Assert.IsTrue(dc2_added_ev.Wait(millisecondsTimeout: 10000));

            // Check the data channel is ready
            Assert.IsNotNull(dc2);
            Assert.AreEqual(dc1.ID, dc2.ID);
            Assert.AreEqual(DataChannel.ChannelState.Open, dc1.State);
            Assert.AreEqual(DataChannel.ChannelState.Open, dc2.State);

            // ====== Add an extra media transceiver ==============================

            // Create the receiver video source
            var receiver1b = pc1_go.AddComponent<VideoReceiver>();
            MediaLine ml1b = pc1.AddMediaLine(MediaKind.Video);
            Assert.IsNotNull(ml1b);
            ml1b.Receiver = receiver1b;

            // Create the sender video source
            var source2b = pc2_go.AddComponent<UniformColorVideoSource>();
            //sender2b.SenderTrackName = "track_name_2";
            MediaLine ml2b = pc2.AddMediaLine(MediaKind.Video);
            Assert.IsNotNull(ml2b);
            ml2b.Source = source2b;

            // Renegotiate
            Assert.IsTrue(sig.StartConnection());
            yield return sig.WaitForConnection(millisecondsTimeout: 60000);
            Assert.IsTrue(sig.IsConnected);

            // Wait a frame so that the Unity events for streams started can propagate
            yield return null;

            // Check transceiver update
            var video_tr1b = ml1b.Transceiver;
            Assert.IsNotNull(video_tr1b);
            var video_tr2b = ml2b.Transceiver;
            Assert.IsNotNull(video_tr2b);
            Assert.AreEqual(2, video_tr1b.MlineIndex);
            Assert.AreEqual(2, video_tr2b.MlineIndex);

            Object.Destroy(pc1_go);
            Object.Destroy(pc2_go);
            Object.Destroy(sig_go);
        }

        [UnityTest]
        public IEnumerator SwapSource()
        {
            // Create the peer connections
            var pc1_go = new GameObject("pc1");
            pc1_go.SetActive(false); // prevent auto-activation of components
            var pc1 = pc1_go.AddComponent<PeerConnection>();
            var pc2_go = new GameObject("pc2");
            pc2_go.SetActive(false); // prevent auto-activation of components
            var pc2 = pc2_go.AddComponent<PeerConnection>();

            // Batch changes manually
            pc1.AutoCreateOfferOnRenegotiationNeeded = false;
            pc2.AutoCreateOfferOnRenegotiationNeeded = false;

            // Create the signaler
            var sig_go = new GameObject("signaler");
            var sig = sig_go.AddComponent<LocalOnlySignaler>();
            sig.Peer1 = pc1;
            sig.Peer2 = pc2;

            // Create the video sources on peer #1
            VideoTrackSource source1 = pc1_go.AddComponent<UniformColorVideoSource>();
            VideoTrackSource source2 = pc1_go.AddComponent<UniformColorVideoSource>();

            MediaLine ml = pc1.AddMediaLine(MediaKind.Video);
            ml.SenderTrackName = "video_track_1";
            ml.Source = source1;

            // Create the receiver on peer #2
            {
                VideoReceiver receiver = pc2_go.AddComponent<VideoReceiver>();
                MediaLine receiverMl = pc2.AddMediaLine(MediaKind.Video);
                receiverMl.Receiver = receiver;
            }

            // Initialize
            yield return PeerConnectionTests.InitializeAndWait(pc1);
            yield return PeerConnectionTests.InitializeAndWait(pc2);

            // Connect
            Assert.IsTrue(sig.StartConnection());
            yield return sig.WaitForConnection(millisecondsTimeout: 10000);

            // Wait a frame so that the Unity events for streams started can propagate
            yield return null;

            // source1 is correctly wired.
            Assert.AreEqual(ml.Transceiver.DesiredDirection, Transceiver.Direction.SendOnly);
            Assert.AreEqual(pc1.Peer.LocalVideoTracks.Count(), 1);
            Assert.IsNotNull(ml.LocalTrack);
            Assert.AreEqual(((LocalVideoTrack)ml.LocalTrack).Source, source1.Source);
            Assert.AreEqual(source1.MediaLines.Single(), ml);

            // Reset source
            ml.Source = null;

            // source1 has been detached.
            Assert.IsNull(ml.LocalTrack);
            Assert.AreEqual(ml.Transceiver.DesiredDirection, Transceiver.Direction.Inactive);
            Assert.AreEqual(pc1.Peer.LocalVideoTracks.Count(), 0);
            Assert.IsEmpty(source1.MediaLines);

            // Set source2.
            ml.Source = source2;

            // source2 is correctly wired.
            Assert.IsNotNull(ml.LocalTrack);
            Assert.AreEqual(ml.Transceiver.DesiredDirection, Transceiver.Direction.SendOnly);
            Assert.AreEqual(pc1.Peer.LocalVideoTracks.Count(), 1);
            Assert.AreEqual(source2.MediaLines.Single(), ml);
            Assert.AreEqual(((LocalVideoTrack)ml.LocalTrack).Source, source2.Source);

            // Swap source2 with source1.
            ml.Source = source1;

            // source1 is correctly wired.
            Assert.IsNotNull(ml.LocalTrack);
            Assert.AreEqual(ml.Transceiver.DesiredDirection, Transceiver.Direction.SendOnly);
            Assert.AreEqual(pc1.Peer.LocalVideoTracks.Count(), 1);
            Assert.AreEqual(source1.MediaLines.Single(), ml);
            Assert.AreEqual(((LocalVideoTrack)ml.LocalTrack).Source, source1.Source);

            // source2 has been detached.
            Assert.IsEmpty(source2.MediaLines);

            Object.Destroy(pc1_go);
            Object.Destroy(pc2_go);
        }

        [UnityTest]
        public IEnumerator SwapReceiver()
        {
            // Create the peer connections
            var pc1_go = new GameObject("pc1");
            pc1_go.SetActive(false); // prevent auto-activation of components
            var pc1 = pc1_go.AddComponent<PeerConnection>();
            var pc2_go = new GameObject("pc2");
            pc2_go.SetActive(false); // prevent auto-activation of components
            var pc2 = pc2_go.AddComponent<PeerConnection>();

            // Batch changes manually
            pc1.AutoCreateOfferOnRenegotiationNeeded = false;
            pc2.AutoCreateOfferOnRenegotiationNeeded = false;

            // Create the signaler
            var sig_go = new GameObject("signaler");
            var sig = sig_go.AddComponent<LocalOnlySignaler>();
            sig.Peer1 = pc1;
            sig.Peer2 = pc2;

            // Create the video source on peer #1
            {
                VideoTrackSource source = pc1_go.AddComponent<UniformColorVideoSource>();
                MediaLine senderMl = pc1.AddMediaLine(MediaKind.Video);
                senderMl.SenderTrackName = "video_track_1";
                senderMl.Source = source;
            }

            // Create the receivers on peer #2
            VideoReceiver receiver1 = pc2_go.AddComponent<VideoReceiver>();
            VideoReceiver receiver2 = pc2_go.AddComponent<VideoReceiver>();
            MediaLine ml = pc2.AddMediaLine(MediaKind.Video);
            ml.Receiver = receiver1;

            // Initialize
            yield return PeerConnectionTests.InitializeAndWait(pc1);
            yield return PeerConnectionTests.InitializeAndWait(pc2);

            // Connect
            Assert.IsTrue(sig.StartConnection());
            yield return sig.WaitForConnection(millisecondsTimeout: 10000);

            // Wait a frame so that the Unity events for streams started can propagate
            yield return null;

            // receiver1 is correctly wired.
            Assert.AreEqual(ml.Transceiver.DesiredDirection, Transceiver.Direction.ReceiveOnly);
            Assert.AreEqual(pc2.Peer.RemoteVideoTracks.Count(), 1);
            Assert.IsTrue(receiver1.IsLive);
            Assert.AreEqual(receiver1.Track, ml.Transceiver.RemoteTrack);
            Assert.AreEqual(receiver1.MediaLine, ml);

            // Reset receiver
            ml.Receiver = null;

            // receiver1 has been detached.
            Assert.AreEqual(ml.Transceiver.DesiredDirection, Transceiver.Direction.Inactive);
            Assert.IsFalse(receiver1.IsLive);
            Assert.IsNull(receiver1.MediaLine);

            // Set receiver2.
            ml.Receiver = receiver2;

            // receiver2 is correctly wired.
            Assert.AreEqual(ml.Transceiver.DesiredDirection, Transceiver.Direction.ReceiveOnly);
            Assert.AreEqual(pc2.Peer.RemoteVideoTracks.Count(), 1);
            Assert.IsTrue(receiver2.IsLive);
            Assert.AreEqual(receiver2.Track, ml.Transceiver.RemoteTrack);
            Assert.AreEqual(receiver2.MediaLine, ml);

            // Swap receiver2 with receiver1.
            ml.Receiver = receiver1;

            // receiver1 is correctly wired.
            Assert.AreEqual(ml.Transceiver.DesiredDirection, Transceiver.Direction.ReceiveOnly);
            Assert.AreEqual(pc2.Peer.RemoteVideoTracks.Count(), 1);
            Assert.IsTrue(receiver1.IsLive);
            Assert.AreEqual(receiver1.Track, ml.Transceiver.RemoteTrack);
            Assert.AreEqual(receiver1.MediaLine, ml);

            // receiver2 has been detached.
            Assert.IsFalse(receiver2.IsLive);
            Assert.IsNull(receiver2.MediaLine);

            Object.Destroy(pc1_go);
            Object.Destroy(pc2_go);
        }
    }
}
