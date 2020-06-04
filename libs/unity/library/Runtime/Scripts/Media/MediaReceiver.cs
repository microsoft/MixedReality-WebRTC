// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// Base class for media producers generating frames by receiving them from a remote peer.
    /// </summary>
    public abstract class MediaReceiver : MediaProducer
    {
        /// <summary>
        /// Is the media source currently producing frames received from the remote peer?
        /// This is <c>true</c> while the remote media track exists, which is notified by
        /// events on the <see cref="AudioReceiver"/> or <see cref="VideoReceiver"/>.
        /// </summary>
        public bool IsLive { get; protected set; }

        /// <summary>
        /// Transceiver this receiver is paired with, if any.
        ///
        /// This is <c>null</c> until a remote description is applied which pairs the media line
        /// this receiver is associated with to a transceiver, or until the peer connection of this
        /// receiver's media line creates the receiver right before creating an SDP offer.
        /// </summary>
        public Transceiver Transceiver { get; private set; }

        /// <inheritdoc/>
        public MediaReceiver(MediaKind mediaKind) : base(mediaKind)
        {
        }

        /// <summary>
        /// Internal callback invoked when the receiver is paired with a media track.
        /// </summary>
        /// <param name="track">The media track this receiver is paired with.</param>
        internal abstract void OnPaired(MediaTrack track);

        /// <summary>
        /// Internal callback invoked when the receiver is unpaired from a media track.
        /// </summary>
        /// <param name="track">The media track this receiver was paired with.</param>
        internal abstract void OnUnpaired(MediaTrack track);

        /// <summary>
        /// Internal callback invoked when the receiver is attached to a transceiver created
        /// just before the peer connection creates an SDP offer.
        /// </summary>
        /// <param name="transceiver">The transceiver this receiver is attached with.</param>
        /// <remarks>
        /// At this time the transceiver does not yet contain a remote track. The remote track will be
        /// created when receiving an answer from the remote peer, if it agreed to send media data through
        /// that transceiver, and <see cref="OnPaired"/> will be invoked at that time.
        /// </remarks>
        internal void AttachToTransceiver(Transceiver transceiver)
        {
            Debug.Assert((Transceiver == null) || (Transceiver == transceiver));
            Transceiver = transceiver;
        }

        /// <summary>
        /// Internal callback invoked when the receiver is detached from a transceiver about to be
        /// destroyed by the native implementation.
        /// </summary>
        /// <param name="transceiver">The transceiver this receiver is attached with.</param>
        internal void DetachFromTransceiver(Transceiver transceiver)
        {
            Debug.Assert((Transceiver == null) || (Transceiver == transceiver));
            Transceiver = null;
        }
    }
}
