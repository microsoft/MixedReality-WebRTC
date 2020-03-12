// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// This component represents a media receiver added as a media track to an
    /// existing WebRTC peer connection by a remote peer and received locally.
    /// The media track can optionally be displayed locally with a <see cref="MediaPlayer"/>.
    /// </summary>
    public abstract class MediaReceiver : MediaSource
    {
        /// <summary>
        /// Automatically play the remote track when it is paired.
        /// This is equivalent to manually calling <see cref="Play"/> when the media receiver is paired with
        /// a remote track after <see cref="PeerConnection.SetRemoteDescriptionAsync(string, string)"/>.
        /// </summary>
        /// <seealso cref="Play"/>
        /// <seealso cref="Stop()"/>
        public bool AutoPlayOnPaired = true;
    }
}
