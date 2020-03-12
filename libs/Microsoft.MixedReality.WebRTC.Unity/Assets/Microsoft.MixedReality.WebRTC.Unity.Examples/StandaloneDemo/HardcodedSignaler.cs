// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using UnityEngine;
using UnityEngine.UI;
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
public class HardcodedSignaler : MonoBehaviour
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
    /// The button that generates an offer from peer #1 to peer #2.
    /// </summary>
    [Tooltip("The button that generates an offer to a given target")]
    public Button CreateOfferButton;

    private ManualResetEventSlim _remoteApplied1 = new ManualResetEventSlim();
    private ManualResetEventSlim _remoteApplied2 = new ManualResetEventSlim();

    public void Connect()
    {
        _remoteApplied1.Reset();
        _remoteApplied2.Reset();
        Peer1.CreateOffer();
        _remoteApplied1.Wait();
        _remoteApplied2.Wait();
    }

    public bool Connect(int millisecondsTimeout)
    {
        _remoteApplied1.Reset();
        _remoteApplied2.Reset();
        Peer1.CreateOffer();
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

        CreateOfferButton?.onClick.AddListener(() =>
        {
            Peer1.CreateOffer();
        });
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

    private async void Peer1_LocalSdpReadytoSend(string type, string sdp)
    {
        await Peer2.SetRemoteDescriptionAsync(type, sdp);
        _remoteApplied2.Set();
        if (type == "offer")
        {
            Peer2.Peer.CreateAnswer();
        }
    }

    private async void Peer2_LocalSdpReadytoSend(string type, string sdp)
    {
        await Peer1.SetRemoteDescriptionAsync(type, sdp);
        _remoteApplied1.Set();
        if (type == "offer")
        {
            Peer1.Peer.CreateAnswer();
        }
    }

    private void Peer1_IceCandidateReadytoSend(string candidate, int sdpMlineindex, string sdpMid)
    {
        Peer2.Peer.AddIceCandidate(sdpMid, sdpMlineindex, candidate);
    }

    private void Peer2_IceCandidateReadytoSend(string candidate, int sdpMlineindex, string sdpMid)
    {
        Peer1.Peer.AddIceCandidate(sdpMid, sdpMlineindex, candidate);
    }
}
