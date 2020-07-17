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

        private void WaitForSdpExchangeCompleted(ManualResetEventSlim completed)
        {
            Assert.True(completed.Wait(TimeSpan.FromSeconds(60.0)));
            completed.Reset();
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
                    var completed = new ManualResetEventSlim(initialState: false);
                    pc1.LocalSdpReadytoSend += async (SdpMessage message) =>
                    {
                        // Send caller offer to callee
                        await pc2.SetRemoteDescriptionAsync(message);
                        Assert.AreEqual(SdpMessageType.Offer, message.Type);
                        pc2.CreateAnswer();
                    };
                    pc2.LocalSdpReadytoSend += async (SdpMessage message) =>
                    {
                        // Send callee answer back to caller
                        await pc1.SetRemoteDescriptionAsync(message);
                        Assert.AreEqual(SdpMessageType.Answer, message.Type);
                        completed.Set();
                    };
                    pc1.IceCandidateReadytoSend += (IceCandidate candidate) => pc2.AddIceCandidate(candidate);
                    pc2.IceCandidateReadytoSend += (IceCandidate candidate) => pc1.AddIceCandidate(candidate);

                    // Connect
                    pc1.CreateOffer();
                    WaitForSdpExchangeCompleted(completed);

                    pc1.Close();
                    pc2.Close();
                }
            }
        }
    }
}
