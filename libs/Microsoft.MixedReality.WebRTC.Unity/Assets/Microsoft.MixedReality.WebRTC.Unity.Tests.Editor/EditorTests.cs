// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
using NUnit.Framework;

namespace Microsoft.MixedReality.WebRTC.Tests
{
    public class EditorTests
    {
        [Test]
        public async void PeerConnectionDefault()
        {
            using (var pc = new PeerConnection())
            {
                var config = new PeerConnectionConfiguration();
                await pc.InitializeAsync(config);
            }
        }

        //[Test]
        //public void PeerConnectionWrongIceUrl()
        //{
        //    using (var pc = new PeerConnection())
        //    {
        //        var config = new PeerConnectionConfiguration()
        //        {
        //            IceServers = { new IceServer { Urls = { "random url" } } }
        //        };
        //        try
        //        {
        //            pc.InitializeAsync(config).ContinueWith((task) => { });
        //        }
        //        catch (Exception _)
        //        {
        //        }
        //    }
        //}

        private void WaitForSdpExchangeCompleted(ManualResetEventSlim conn1, ManualResetEventSlim conn2)
        {
            Assert.True(conn1.Wait(TimeSpan.FromSeconds(60.0)));
            Assert.True(conn2.Wait(TimeSpan.FromSeconds(60.0)));
            conn1.Reset();
            conn2.Reset();
        }

        [Test]
        public async void PeerConnectionLocalConnect()
        {
            using (var pc1 = new PeerConnection())
            {
                await pc1.InitializeAsync();
                using (var pc2 = new PeerConnection())
                {
                    await pc2.InitializeAsync();

                    // Prepare SDP event handlers
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
                    pc1.IceCandidateReadytoSend += (string candidate, int sdpMlineindex, string sdpMid)
                        => pc2.AddIceCandidate(sdpMid, sdpMlineindex, candidate);
                    pc2.IceCandidateReadytoSend += (string candidate, int sdpMlineindex, string sdpMid)
                        => pc1.AddIceCandidate(sdpMid, sdpMlineindex, candidate);

                    // Connect
                    var conn1 = new ManualResetEventSlim(initialState: false);
                    var conn2 = new ManualResetEventSlim(initialState: false);
                    pc1.Connected += () => conn1.Set();
                    pc2.Connected += () => conn2.Set();
                    pc1.CreateOffer();
                    WaitForSdpExchangeCompleted(conn1, conn2);

                    pc1.Close();
                    pc2.Close();
                }
            }
        }
    }
}
