// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using UnityEngine;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// Base class for WebRTC signaling implementations in Unity.
    /// </summary>
    public abstract class Signaler : MonoBehaviour, ISignaler
    {
        /// <summary>
        /// The <see cref="PeerConnection"/> this signaler is attached to, or <c>null</c>
        /// if not attached yet to any connection. This is updated automatically by the peer
        /// connection once it finished initializing.
        /// </summary>
        public PeerConnection PeerConnection { get; private set; }


        #region ISignaler interface

        public event Action OnConnect;
        public event Action OnDisconnect;
        public event Action<SignalerMessage> OnMessage;
        public event Action<Exception> OnFailure;

        /// <summary>
        /// Asynchronously send a signaling message to the remote peer.
        /// </summary>
        /// <param name="message">The signaling message to send to the remote peer.</param>
        /// <returns>
        /// A <see cref="Task"/> object completed once the message has been sent,
        /// but not necessarily delivered.
        /// </returns>
        public abstract Task SendMessageAsync(SignalerMessage message);

        #endregion


        /// <summary>
        /// Native <xref href="Microsoft.MixedReality.WebRTC.PeerConnection"/> object from the underlying WebRTC C# library, available once
        /// the peer has been initialized and the signaler is attached to it.
        /// </summary>
        protected WebRTC.PeerConnection _nativePeer;


        private ConcurrentQueue<Action> _mainThreadQueue = new ConcurrentQueue<Action>();

        /// <summary>
        /// Callback fired from the <see cref="PeerConnection"/> when it finished
        /// initializing, to subscribe to signaling-related events.
        /// </summary>
        /// <param name="peer">The peer connection to attach to</param>
        public void OnPeerInitialized(PeerConnection peer)
        {
            PeerConnection = peer;
            _nativePeer = peer.Peer;

            // Register handlers for the SDP events
            //_nativePeer.IceCandidateReadytoSend += OnIceCandiateReadyToSend_Listener;
            //_nativePeer.LocalSdpReadytoSend += OnLocalSdpReadyToSend_Listener;
        }

        /// <summary>
        /// Callback fired from the <see cref="PeerConnection"/> before it starts
        /// uninitializing itself and disposing of the underlying implementation object.
        /// </summary>
        /// <param name="peer">The peer connection about to be deinitialized</param>
        public void OnPeerUninitializing(PeerConnection peer)
        {
            // Unregister handlers for the SDP events
            //_nativePeer.IceCandidateReadytoSend -= OnIceCandiateReadyToSend_Listener;
            //_nativePeer.LocalSdpReadytoSend -= OnLocalSdpReadyToSend_Listener;
        }

        //private void OnIceCandiateReadyToSend_Listener(string candidate, int sdpMlineIndex, string sdpMid)
        //{
        //    _mainThreadQueue.Enqueue(() => OnIceCandiateReadyToSend(candidate, sdpMlineIndex, sdpMid));
        //}

        ///// <summary>
        ///// Helper to split SDP offer and answer messages and dispatch to the appropriate handler.
        ///// </summary>
        ///// <param name="type"></param>
        ///// <param name="sdp"></param>
        //private void OnLocalSdpReadyToSend_Listener(string type, string sdp)
        //{
        //    if (string.Equals(type, "offer", StringComparison.OrdinalIgnoreCase))
        //    {
        //        _mainThreadQueue.Enqueue(() => OnSdpOfferReadyToSend(sdp));
        //    }
        //    else if (string.Equals(type, "answer", StringComparison.OrdinalIgnoreCase))
        //    {
        //        _mainThreadQueue.Enqueue(() => OnSdpAnswerReadyToSend(sdp));
        //    }
        //}

        protected virtual void Update()
        {
            // Process workloads queued from background threads
            while (_mainThreadQueue.TryDequeue(out Action action))
            {
                action();
            }
        }

        /// <summary>
        /// Callback fired when an ICE candidate message has been generated and is ready to
        /// be sent to the remote peer by the signaling object.
        /// </summary>
        /// <param name="candidate"></param>
        /// <param name="sdpMlineIndex"></param>
        /// <param name="sdpMid"></param>
        protected abstract void OnIceCandiateReadyToSend(string candidate, int sdpMlineIndex, string sdpMid);

        /// <summary>
        /// Callback fired when a local SDP offer has been generated and is ready to
        /// be sent to the remote peer by the signaling object.
        /// </summary>
        /// <param name="offer">The SDP offer message to send.</param>
        protected abstract void OnSdpOfferReadyToSend(string offer);

        /// <summary>
        /// Callback fired when a local SDP answer has been generated and is ready to
        /// be sent to the remote peer by the signaling object.
        /// </summary>
        /// <param name="answer">The SDP answer message to send.</param>
        protected abstract void OnSdpAnswerReadyToSend(string answer);
    }
}
