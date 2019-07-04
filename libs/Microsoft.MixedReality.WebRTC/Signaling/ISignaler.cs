// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.WebRTC
{
    /// <summary>
    /// Interface for a signaling implementation, which provides <see cref="PeerConnection"/> with the capability
    /// to send and receive messages in order to establish a connection with a remote peer.
    /// </summary>
    public interface ISignaler
    {
        /// <summary>
        /// Event that occurs when signaling is connected.
        /// This should fire even if the implementation is connection-less; in that case, the
        /// implementation should fire this event when it has confirmed that some message can
        /// be sent to the remote peer, even if none has been so far.
        /// </summary>
        event Action OnConnect;

        /// <summary>
        /// Event that occurs when signaling is disconnected.
        /// This may not fire if the implementation is connection-less.
        /// </summary>
        event Action OnDisconnect;

        /// <summary>
        /// Event that occurs when the signaler receives a new message for the local peer from a remote peer.
        /// </summary>
        event Action<SignalerMessage> OnMessage;

        /// <summary>
        /// Event that occurs when the signaler experiences some failure.
        /// </summary>
        event Action<Exception> OnFailure;

        /// <summary>
        /// Send a message to the remote peer.
        /// </summary>
        /// <param name="message">The message to send.</param>
        Task SendMessageAsync(SignalerMessage message);
    }
}
