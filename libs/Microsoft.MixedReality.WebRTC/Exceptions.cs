// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.MixedReality.WebRTC
{
    /// <summary>
    /// Exception thrown when trying to add a data channel to a peer connection after
    /// a connection to a remote peer was established without an SCTP handshake.
    /// When using data channels, at least one data channel must be added to the peer
    /// connection before calling <see cref="PeerConnection.CreateOffer()"/> to signal
    /// to the implementation the intent to use data channels and the need to perform a
    /// SCTP handshake during the connection.
    /// </summary>
    public class SctpNotNegotiatedException : Exception
    {
        /// <inheritdoc/>
        public SctpNotNegotiatedException()
            : base("Cannot add a first data channel after the connection handshake started. Call AddDataChannelAsync() at least once before calling CreateOffer().")
        {
        }

        /// <inheritdoc/>
        public SctpNotNegotiatedException(string message)
            : base(message)
        {
        }

        /// <inheritdoc/>
        public SctpNotNegotiatedException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }

    /// <summary>
    /// Exception thrown when an API function expects an interop handle to a valid native object,
    /// but receives an invalid handle instead.
    /// </summary>
    public class InvalidInteropNativeHandleException : Exception
    {
        /// <inheritdoc/>
        public InvalidInteropNativeHandleException()
            : base("Invalid interop handle to a native object.")
        {
        }

        /// <inheritdoc/>
        public InvalidInteropNativeHandleException(string message)
            : base(message)
        {
        }

        /// <inheritdoc/>
        public InvalidInteropNativeHandleException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
