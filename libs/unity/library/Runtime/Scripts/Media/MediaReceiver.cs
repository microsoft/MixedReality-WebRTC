// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using UnityEngine;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// Base class for media producers generating frames by receiving them from a remote peer.
    /// </summary>
    public abstract class MediaReceiver : MonoBehaviour
    {
        /// <summary>
        /// Media kind of the receiver.
        /// </summary>
        public abstract MediaKind MediaKind { get; }

        /// <summary>
        /// Remote track associated with this receiver.
        /// <c>null</c> if this object is not receiving at this time.
        /// </summary>
        /// <remarks>
        /// This is always a <see cref="RemoteAudioTrack"/> or a <see cref="RemoteVideoTrack"/>
        /// </remarks>
        public abstract MediaTrack Track { get; }

        /// <summary>
        /// Is the media source currently producing frames received from the remote peer?
        /// This is <c>true</c> while the remote media track exists, which is notified by
        /// events on the <see cref="AudioReceiver"/> or <see cref="VideoReceiver"/>.
        /// </summary>
        public bool IsLive => Track != null;

        /// <summary>
        /// Transceiver this receiver is paired with, if any.
        ///
        /// This is <c>null</c> until a remote description is applied which pairs the media line
        /// this receiver is associated with to a transceiver, or until the peer connection of this
        /// receiver's media line creates the receiver right before creating an SDP offer.
        /// </summary>
        public Transceiver Transceiver => MediaLine?.Transceiver;

        /// <summary>
        /// Media line this receiver is paired with, if any.
        /// </summary>
        /// <remarks>
        /// Note that this is set to the connected <see cref="Unity.MediaLine"/> only if the owning
        /// <see cref="PeerConnection"/> is awake. This will be automatically reset if the
        /// <see cref="PeerConnection"/> owning the <see cref="Unity.MediaLine"/>is destroyed.
        /// </remarks>
        public MediaLine MediaLine { get; private set; }

        /// <summary>
        /// Internal callback invoked when the media receiver is assigned to a media line.
        /// </summary>
        /// <param name="mediaLine">The new media line this receiver is assigned to.</param>
        protected internal virtual void OnAddedToMediaLine(MediaLine mediaLine)
        {
            Debug.Assert(MediaLine == null);
            MediaLine = mediaLine;
        }

        /// <summary>
        /// Internal callback invoked when the media receiver is de-assigned from a media line.
        /// </summary>
        /// <param name="mediaLine">The old media line this receiver was assigned to.</param>
        protected internal virtual void OnRemovedFromMediaLine(MediaLine mediaLine)
        {
            Debug.Assert(MediaLine == mediaLine);
            MediaLine = null;
        }

        /// <summary>
        /// Internal callback invoked when the receiver is paired with a media track.
        /// </summary>
        /// <remarks>
        /// This will be called on the Unity update thread.
        /// </remarks>
        /// <param name="track">The media track this receiver is paired with.</param>
        protected internal virtual void OnPaired(MediaTrack track) { }

        /// <summary>
        /// Internal callback invoked when the receiver is unpaired from a media track.
        /// </summary>
        /// <remarks>
        /// This will be called on the Unity update thread.
        /// </remarks>
        /// <param name="track">The media track this receiver was paired with.</param>
        protected internal virtual void OnUnpaired(MediaTrack track) { }

        protected void OnDestroy()
        {
            MediaLine?.OnReceiverDestroyed();
        }
    }
}
