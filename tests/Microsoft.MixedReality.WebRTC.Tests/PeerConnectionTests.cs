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
    internal class PeerConnectionTests
    {
        [Test]
        public async Task LocalNoICE()
        {
            var pc1 = new PeerConnection();
            var pc2 = new PeerConnection();

            var evExchangeCompleted = new ManualResetEventSlim(initialState: false);
            pc1.LocalSdpReadytoSend += async (string type, string sdp) =>
            {
                await pc2.SetRemoteDescriptionAsync(type, sdp);
                if (type == "offer")
                {
                    pc2.CreateAnswer();
                }
                else
                {
                    evExchangeCompleted.Set();
                }
            };
            pc2.LocalSdpReadytoSend += async (string type, string sdp) =>
            {
                await pc1.SetRemoteDescriptionAsync(type, sdp);
                if (type == "offer")
                {
                    pc1.CreateAnswer();
                }
                else
                {
                    evExchangeCompleted.Set();
                }
            };

            var pcConfig = new PeerConnectionConfiguration();
            await pc1.InitializeAsync(pcConfig);
            await pc2.InitializeAsync(pcConfig);

            var ev1 = new ManualResetEventSlim(initialState: false);
            pc1.Connected += () => ev1.Set();
            evExchangeCompleted.Reset();
            pc1.CreateOffer();
            ev1.Wait(millisecondsTimeout: 5000);
            evExchangeCompleted.Wait(millisecondsTimeout: 5000);

            pc1.Close();
            pc2.Close();
        }

        protected async Task MakeICECall(PeerConnection pc1, PeerConnection pc2)
        {
            var evExchangeCompleted = new ManualResetEventSlim(initialState: false);
            pc1.LocalSdpReadytoSend += async (string type, string sdp) =>
            {
                await pc2.SetRemoteDescriptionAsync(type, sdp);
                if (type == "offer")
                {
                    pc2.CreateAnswer();
                }
                else
                {
                    evExchangeCompleted.Set();
                }
            };
            pc2.LocalSdpReadytoSend += async (string type, string sdp) =>
            {
                await pc1.SetRemoteDescriptionAsync(type, sdp);
                if (type == "offer")
                {
                    pc1.CreateAnswer();
                }
                else
                {
                    evExchangeCompleted.Set();
                }
            };
            pc1.IceCandidateReadytoSend += (string candidate, int sdpMlineindex, string sdpMid) =>
            {
                pc2.AddIceCandidate(sdpMid, sdpMlineindex, candidate);
            };
            pc2.IceCandidateReadytoSend += (string candidate, int sdpMlineindex, string sdpMid) =>
            {
                pc1.AddIceCandidate(sdpMid, sdpMlineindex, candidate);
            };

            var pcConfig = new PeerConnectionConfiguration();
            await pc1.InitializeAsync(pcConfig);
            await pc2.InitializeAsync(pcConfig);

            var ev1 = new ManualResetEventSlim(initialState: false);
            var ev2 = new ManualResetEventSlim(initialState: false);
            pc1.Connected += () => ev1.Set();
            pc2.Connected += () => ev2.Set();
            evExchangeCompleted.Reset();
            pc1.CreateOffer();
            ev1.Wait(millisecondsTimeout: 5000);
            ev2.Wait(millisecondsTimeout: 5000);
            evExchangeCompleted.Wait(millisecondsTimeout: 5000);
        }

        [Test]
        public async Task LocalICE()
        {
            var pc1 = new PeerConnection();
            var pc2 = new PeerConnection();
            await MakeICECall(pc1, pc2);
            pc1.Close();
            pc2.Close();
        }

        [Test]
        public async Task MultiOpenClose()
        {
            for (int i = 0; i < 10; ++i)
            {
                var pc1 = new PeerConnection();
                var pc2 = new PeerConnection();
                await MakeICECall(pc1, pc2);
                pc1.Close();
                pc2.Close();
            }
        }

        [Test]
        public async Task MultiCall()
        {
            var pc = new PeerConnection[20];
            for (int i = 0; i < 20; ++i)
            {
                pc[i] = new PeerConnection();
            }

            // Make 5 calls
            for (int i = 0; i < 5; ++i)
            {
                await MakeICECall(pc[2 * i], pc[2 * i + 1]);
            }

            // Close 2 calls
            for (int i = 1; i < 3; ++i)
            {
                pc[2 * i].Close();
                pc[2 * i + 1].Close();
            }

            // Make 5 other calls
            for (int i = 5; i < 10; ++i)
            {
                await MakeICECall(pc[2 * i], pc[2 * i + 1]);
            }

            // Close all remaining calls
            pc[0].Dispose();
            pc[1].Dispose();
            for (int i = 3; i < 10; ++i)
            {
                pc[2 * i].Dispose();
                pc[2 * i + 1].Dispose();
            }
        }

        [Test(Description = "SetRemoteDescriptionAsync() with invalid arguments")]
        public async Task SetRemoteDescription_Null()
        {
            using (var pc = new PeerConnection())
            {
                await pc.InitializeAsync();
                // Invalid arguments; SRD not even enqueued, fails immediately while validating arguments
                Assert.ThrowsAsync<ArgumentException>(async () => await pc.SetRemoteDescriptionAsync(null, null));
                Assert.ThrowsAsync<ArgumentException>(async () => await pc.SetRemoteDescriptionAsync("offer", null));
                Assert.ThrowsAsync<ArgumentException>(async () => await pc.SetRemoteDescriptionAsync(null, "v=0"));
            }
        }

        [Test(Description = "SetRemoteDescriptionAsync() with valid arguments but invalid message content or peer state.")]
        public async Task SetRemoteDescription_Invalid()
        {
            const string kDummyMessage = "v=0\r\n"
                + "o=- 496134922022744986 2 IN IP4 127.0.0.1\r\n"
                + "s=-\r\n"
                + "t=0 0\r\n"
                + "a=group:BUNDLE 0\r\n"
                + "a=msid-semantic: WMS\r\n"
                + "m=application 9 DTLS/SCTP 5000\r\n"
                + "c=IN IP4 0.0.0.0\r\n"
                + "a=setup:actpass\r\n"
                + "a=mid:0\r\n"
                + "a=sctpmap:5000 webrtc-datachannel 1024\r\n";

            using (var pc = new PeerConnection())
            {
                await pc.InitializeAsync();
                // Set answer without offer; SRD task enqueued, but fails when executing
                Assert.CatchAsync<InvalidOperationException>(async () => await pc.SetRemoteDescriptionAsync("answer", kDummyMessage));
            }
        }
    }
}
