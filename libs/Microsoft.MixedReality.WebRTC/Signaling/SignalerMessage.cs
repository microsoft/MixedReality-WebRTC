// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.MixedReality.WebRTC
{
    /// <summary>
    /// Data that makes up a signaler message
    /// </summary>
    /// <remarks>
    /// Note: the same data is used for transmitting and receiving
    /// </remarks>
    [Serializable]
    public class SignalerMessage
    {
        /// <summary>
        /// Possible message types as-serialized on the wire
        /// </summary>
        public enum WireMessageType
        {
            /// <summary>
            /// An unrecognized message
            /// </summary>
            Unknown = 0,
            /// <summary>
            /// A SDP offer message
            /// </summary>
            Offer,
            /// <summary>
            /// A SDP answer message
            /// </summary>
            Answer,
            /// <summary>
            /// A trickle-ice or ice message
            /// </summary>
            Ice
        }

        /// <summary>
        /// The message type
        /// </summary>
        public WireMessageType MessageType;

        /// <summary>
        /// The primary message contents
        /// </summary>
        public string Data;

        /// <summary>
        /// The data separator needed for proper ICE serialization
        /// </summary>
        public string IceDataSeparator;

        /// <summary>
        /// The target id to which we send messages
        /// </summary>
        /// <remarks>
        /// This is expected to be set when <see cref="ISignaler.SendMessageAsync(SignalerMessage)"/> is called
        /// </remarks>
        [NonSerialized]
        public string TargetId;
    }
}
