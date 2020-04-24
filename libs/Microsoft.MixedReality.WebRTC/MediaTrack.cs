// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.MixedReality.WebRTC
{
    /// <summary>
    /// Base class for media tracks sending to or receiving from the remote peer.
    /// </summary>
    public abstract class MediaTrack
    {
        /// <summary>
        /// Transceiver this track is attached to, if any.
        /// </summary>
        public Transceiver Transceiver { get; protected internal set; }

        /// <summary>
        /// Peer connection this media track is added to, if any.
        /// This is <c>null</c> after the track has been removed from the peer connection.
        /// </summary>
        public PeerConnection PeerConnection { get; protected internal set; }

        /// <summary>
        /// Track name as specified during creation. This property is immutable.
        /// For remote tracks the property is specified by the remote peer.
        /// </summary>
        public string Name { get; }

        internal MediaTrack(PeerConnection peer, string trackName)
        {
            PeerConnection = peer;
            Name = trackName;
        }

        internal abstract void OnMute(bool muted);

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"(MediaTrack)\"{Name}\"";
        }
    }
}
