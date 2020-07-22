// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using UnityEngine;
using Microsoft.MixedReality.WebRTC.Unity;
using System.Threading;
using System.Collections;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// Simple signaler using two peer connections in the same process,
    /// and hard-coding their SDP message delivery to avoid the need for
    /// any kind of networking to deliver SDP messages.
    ///
    /// This component is designed to be used in demos where both peers
    /// are present in the same scene.
    /// </summary>
    public class LocalOnlySignaler : WorkQueue
    {
        /// <summary>
        /// First peer to connect, which will generate an offer.
        /// </summary>
        public PeerConnection Peer1;

        /// <summary>
        /// Second peer to connect, which will wait for an offer from the first peer.
        /// </summary>
        public PeerConnection Peer2;

        /// <summary>
        /// Check if the last connection attempt successfully completed. This is reset to <c>false</c> each
        /// time <see cref="StartConnection"/> is called, and is updated after <see cref="WaitForConnection"/>
        /// returned to indicate if the connection succeeded.
        /// </summary>
        public bool IsConnected { get; private set; } = false;

        private ManualResetEventSlim _remoteApplied1 = new ManualResetEventSlim();
        private ManualResetEventSlim _remoteApplied2 = new ManualResetEventSlim();

        /// <summary>
        /// Initiate a connection by having <see cref="Peer1"/> send an offer to <see cref="Peer2"/>,
        /// and wait until the SDP exchange completed. To wait for completion, use <see cref="WaitForConnection(int)"/>
        /// then check the value of <see cref="IsConnected"/> after that to determine if
        /// <see cref="WaitForConnection(int)"/> terminated due to the connection being established or
        /// if it timed out.
        /// </summary>
        /// <returns><c>true</c> if the exchange started successfully, or <c>false</c> otherwise.</returns>
        /// <seealso cref="Connect"/>
        public bool StartConnection()
        {
            EnsureIsMainAppThread();
            _remoteApplied1.Reset();
            _remoteApplied2.Reset();
            IsConnected = false;
            return Peer1.StartConnection();
        }

        /// <summary>
        /// Wait for the connection being established.
        /// </summary>
        /// <param name="millisecondsTimeout">Timeout in milliseconds to wait for the connection.</param>
        /// <returns>An enumerator used to <c>yield</c> while waiting.</returns>
        /// <example>
        /// Assert.IsTrue(signaler.StartConnection());
        /// yield return signaler.WaitForConnection(millisecondsTimeout: 10000);
        /// Assert.IsTrue(signaler.IsConnected);
        /// </example>
        public IEnumerator WaitForConnection(int millisecondsTimeout)
        {
            float timeoutTime = Time.time + (millisecondsTimeout / 1000f);
            while (true)
            {
                if (_remoteApplied1.IsSet && _remoteApplied2.IsSet)
                {
                    IsConnected = true;
                    break;
                }
                if (Time.time >= timeoutTime)
                {
                    break;
                }
                yield return null;
            }
        }

        private void Start()
        {
            Peer1.OnInitialized.AddListener(OnInitialized1);
            Peer2.OnInitialized.AddListener(OnInitialized2);
        }

        private void OnInitialized1()
        {
            Peer1.Peer.LocalSdpReadytoSend += Peer1_LocalSdpReadytoSend;
            Peer1.Peer.IceCandidateReadytoSend += Peer1_IceCandidateReadytoSend;
        }

        private void OnInitialized2()
        {
            Peer2.Peer.LocalSdpReadytoSend += Peer2_LocalSdpReadytoSend;
            Peer2.Peer.IceCandidateReadytoSend += Peer2_IceCandidateReadytoSend;
        }

        private void Peer1_LocalSdpReadytoSend(Microsoft.MixedReality.WebRTC.SdpMessage message)
        {
            InvokeOnAppThread(async () =>
            {
                if (Peer2.Peer == null)
                {
                    Debug.Log("Discarding SDP message for peer #2 (disabled)");
                    return;
                }
                await Peer2.HandleConnectionMessageAsync(message);
                _remoteApplied2.Set();
                if (message.Type == Microsoft.MixedReality.WebRTC.SdpMessageType.Offer)
                {
                    Peer2.Peer.CreateAnswer();
                }
            });
        }

        private void Peer2_LocalSdpReadytoSend(Microsoft.MixedReality.WebRTC.SdpMessage message)
        {
            InvokeOnAppThread(async () =>
            {
                if (Peer1.Peer == null)
                {
                    Debug.Log("Discarding SDP message for peer #1 (disabled)");
                    return;
                }
                await Peer1.HandleConnectionMessageAsync(message);
                _remoteApplied1.Set();
                if (message.Type == Microsoft.MixedReality.WebRTC.SdpMessageType.Offer)
                {
                    Peer1.Peer.CreateAnswer();
                }
            });
        }

        private void Peer1_IceCandidateReadytoSend(Microsoft.MixedReality.WebRTC.IceCandidate candidate)
        {
            if (Peer2.Peer == null)
            {
                Debug.Log("Discarding ICE message for peer #2 (disabled)");
                return;
            }
            Peer2.Peer.AddIceCandidate(candidate);
        }

        private void Peer2_IceCandidateReadytoSend(Microsoft.MixedReality.WebRTC.IceCandidate candidate)
        {
            if (Peer1.Peer == null)
            {
                Debug.Log("Discarding ICE message for peer #1 (disabled)");
                return;
            }
            Peer1.Peer.AddIceCandidate(candidate);
        }
    }
}
