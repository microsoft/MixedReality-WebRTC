// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using UnityEngine;
using Microsoft.MixedReality.WebRTC.Unity;
using System.Threading;

/// <summary>
/// Simple signaler using two peer connections in the same process,
/// and hard-coding their SDP message delivery to avoid the need for
/// any kind of networking to deliver SDP messages.
/// 
/// This component is designed to be used in demos where both peers
/// are present in the same scene.
/// </summary>
public class LocalOnlySignaler : MonoBehaviour
{
    /// <summary>
    /// First peer to connect, which will generate an offer.
    /// </summary>
    public PeerConnection Peer1;

    /// <summary>
    /// Second peer to connect, which will wait for an offer from the first peer.
    /// </summary>
    public PeerConnection Peer2;

    private ManualResetEventSlim _remoteApplied1 = new ManualResetEventSlim();
    private ManualResetEventSlim _remoteApplied2 = new ManualResetEventSlim();

    /// <summary>
    /// Initiate a connection by having <see cref="Peer1"/> send an offer to <see cref="Peer2"/>,
    /// and wait indefinitely until the SDP exchange completed.
    /// </summary>
    /// <seealso cref="Connect(int)"/>
    public void Connect()
    {
        _remoteApplied1.Reset();
        _remoteApplied2.Reset();
        Peer1.StartConnection();
        _remoteApplied1.Wait();
        _remoteApplied2.Wait();
    }

    /// <summary>
    /// Initiate a connection by having <see cref="Peer1"/> send an offer to <see cref="Peer2"/>,
    /// and wait until the SDP exchange completed.
    /// 
    /// If the exchange does not completes within the given timeout, return <c>false</c>.
    /// </summary>
    /// <param name="millisecondsTimeout">Timeout in milliseconds for the SDP exchange to complete.</param>
    /// <returns>This variant returns <c>true</c> if the exchange completed within the given timeout,
    /// or <c>false</c> otherwise.</returns>
    /// <seealso cref="Connect"/>
    public bool Connect(int millisecondsTimeout)
    {
        _remoteApplied1.Reset();
        _remoteApplied2.Reset();
        Peer1.StartConnection();
        if (!_remoteApplied1.Wait(millisecondsTimeout))
        {
            return false;
        }
        if (!_remoteApplied2.Wait(millisecondsTimeout))
        {
            return false;
        }
        return true;
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

    private async void Peer1_LocalSdpReadytoSend(Microsoft.MixedReality.WebRTC.SdpMessage message)
    {
        await Peer2.HandleConnectionMessageAsync(message);
        _remoteApplied2.Set();
        if (message.Type == Microsoft.MixedReality.WebRTC.SdpMessageType.Offer)
        {
            Peer2.Peer.CreateAnswer();
        }
    }

    private async void Peer2_LocalSdpReadytoSend(Microsoft.MixedReality.WebRTC.SdpMessage message)
    {
        await Peer1.HandleConnectionMessageAsync(message);
        _remoteApplied1.Set();
        if (message.Type == Microsoft.MixedReality.WebRTC.SdpMessageType.Offer)
        {
            Peer1.Peer.CreateAnswer();
        }
    }

    private void Peer1_IceCandidateReadytoSend(Microsoft.MixedReality.WebRTC.IceCandidate candidate)
    {
        Peer2.Peer.AddIceCandidate(candidate);
    }

    private void Peer2_IceCandidateReadytoSend(Microsoft.MixedReality.WebRTC.IceCandidate candidate)
    {
        Peer1.Peer.AddIceCandidate(candidate);
    }
}
