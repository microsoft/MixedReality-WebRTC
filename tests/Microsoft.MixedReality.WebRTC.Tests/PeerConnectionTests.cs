// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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

            pc1.LocalSdpReadytoSend += async (string type, string sdp) =>
            {
                await pc2.SetRemoteDescriptionAsync(type, sdp);
                if (type == "offer")
                {
                    pc2.CreateAnswer();
                }
            };
            pc2.LocalSdpReadytoSend += async (string type, string sdp) =>
            {
                await pc1.SetRemoteDescriptionAsync(type, sdp);
                if (type == "offer")
                {
                    pc1.CreateAnswer();
                }
            };

            var pcConfig = new PeerConnectionConfiguration();
            await pc1.InitializeAsync(pcConfig);
            await pc2.InitializeAsync(pcConfig);

            var ev1 = new ManualResetEventSlim(initialState: false);
            pc1.Connected += () => ev1.Set();
            pc1.CreateOffer();
            ev1.Wait(millisecondsTimeout: 5000);

            pc1.Close();
            pc2.Close();
        }

        protected async Task MakeICECall(PeerConnection pc1, PeerConnection pc2)
        {
            pc1.LocalSdpReadytoSend += async (string type, string sdp) =>
            {
                await pc2.SetRemoteDescriptionAsync(type, sdp);
                if (type == "offer")
                {
                    pc2.CreateAnswer();
                }
            };
            pc2.LocalSdpReadytoSend += async (string type, string sdp) =>
            {
                await pc1.SetRemoteDescriptionAsync(type, sdp);
                if (type == "offer")
                {
                    pc1.CreateAnswer();
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
            pc1.CreateOffer();
            ev1.Wait(millisecondsTimeout: 5000);
            ev2.Wait(millisecondsTimeout: 5000);
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
            pc[0].Close();
            pc[1].Close();
            for (int i = 3; i < 10; ++i)
            {
                pc[2 * i].Close();
                pc[2 * i + 1].Close();
            }
        }
    }
}
