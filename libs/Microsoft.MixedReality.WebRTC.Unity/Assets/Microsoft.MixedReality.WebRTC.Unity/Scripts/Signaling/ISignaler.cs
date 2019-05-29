// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.WebRTC
{
    public interface ISignaler
    {
        /// <summary>
        /// Event that occurs when signaling is connected
        /// </summary>
        event Action OnConnect;

        /// <summary>
        /// Event that occurs when signaling is disconnected
        /// </summary>
        event Action OnDisconnect;

        /// <summary>
        /// Event that occurs when the signaler receives a new message
        /// </summary>
        event Action<SignalerMessage> OnMessage;

        /// <summary>
        /// Event that occurs when the signaler experiences some failure
        /// </summary>
        event Action<Exception> OnFailure;

        /// <summary>
        /// Send a message
        /// </summary>
        /// <param name="message">message to send</param>
        Task SendMessageAsync(SignalerMessage message);
    }
}
