// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// Base class for media sources producing media frames from a remote media
    /// track receiving them from a remote peer.
    /// </summary>
    public abstract class MediaReceiver : MediaSource
    {
        /// <summary>
        /// Is the media source currently generating frames received from the remote peer?
        /// This is <c>true</c> while the remote media track exists, which is notified by
        /// events on the <see cref="AudioReceiver"/> or <see cref="VideoReceiver"/>.
        /// </summary>
        public bool IsLive { get; protected set; }

        /// <inheritdoc/>
        public MediaReceiver(MediaKind mediaKind) : base(mediaKind)
        {
        }

        internal abstract void OnPaired(MediaTrack track);
        internal abstract void OnUnpaired(MediaTrack track);
    }
}
