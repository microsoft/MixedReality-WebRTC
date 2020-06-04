// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using UnityEngine;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// Base class for media senders acting as automated bridges between a media track source
    /// (frame producer) and a transceiver of a peer connection. The media sender is managed
    /// by the peer connection automatically, so users typically do not use this class or one
    /// of its derived classes directly.
    /// </summary>
    /// <seealso cref="AudioSender"/>
    /// <seealso cref="VideoSender"/>
    public abstract class MediaSender
    {
        /// <summary>
        /// Name of the local media track this component will create when calling <see cref="StartCaptureAsync"/>.
        /// If left empty, the implementation will generate a unique name for the track (generally a GUID).
        /// </summary>
        /// <remarks>
        /// This value must comply with the 'msid' attribute rules as defined in
        /// https://tools.ietf.org/html/draft-ietf-mmusic-msid-05#section-2, which in
        /// particular constraints the set of allowed characters to those allowed for a
        /// 'token' element as specified in https://tools.ietf.org/html/rfc4566#page-43:
        /// - Symbols [!#$%'*+-.^_`{|}~] and ampersand &amp;
        /// - Alphanumerical characters [A-Za-z0-9]
        /// 
        /// Users can manually test if a string is a valid SDP token with the utility
        /// method <see cref="SdpTokenAttribute.Validate(string, bool)"/>.
        /// </remarks>
        /// <seealso cref="SdpTokenAttribute.Validate(string, bool)"/>
        [Tooltip("SDP track name")]
        [SdpToken(allowEmpty: true)]
        public string TrackName;

        /// <summary>
        /// Kind of media the sender is managing. This field is immutable, and determines whether the
        /// concrete class is <see cref="AudioSender"/> or <see cref="VideoSender"/>.
        /// </summary>
        public readonly MediaKind MediaKind;

        /// <summary>
        /// Transceiver this sender is paired with, if any.
        /// 
        /// This is <c>null</c> until a remote description is applied which pairs the media line
        /// the sender is associated with to a transceiver.
        /// </summary>
        public Transceiver Transceiver { get; private set; }

        public MediaSender(MediaKind mediaKind)
        {
            MediaKind = mediaKind;
        }

        /// <summary>
        /// Mute or unmute the media. For audio, muting turns the source to silence. For video, this
        /// produces black frames. This is transparent to the SDP session and does not requite any
        /// renegotiation.
        /// </summary>
        /// <param name="mute"><c>true</c> to mute the local media track, or <c>false</c> to unmute
        /// it and resume producing media frames.</param>
        public void Mute(bool mute = true)
        {
            MuteImpl(mute);
        }

        /// <summary>
        /// Unmute the media and resume normal source playback.
        /// This is equivalent to <c>Mute(false)</c>, and provided for code clarity.
        /// </summary>
        public void Unmute()
        {
            MuteImpl(false);
        }

        /// <summary>
        /// Internal callback invoked when the sender is attached to a transceiver created
        /// just before the peer connection creates an SDP offer.
        /// </summary>
        /// <param name="transceiver">The transceiver this sender is attached with.</param>
        internal void AttachToTransceiver(Transceiver transceiver)
        {
            Debug.Assert((Transceiver == null) || (Transceiver == transceiver));
            Transceiver = transceiver;
        }

        /// <summary>
        /// Internal callback invoked when the sender is detached from a transceiver about to be
        /// destroyed by the native implementation.
        /// </summary>
        /// <param name="transceiver">The transceiver this sender is attached with.</param>
        internal void DetachFromTransceiver(Transceiver transceiver)
        {
            Debug.Assert((Transceiver == null) || (Transceiver == transceiver));
            Transceiver = null;
        }

        /// <summary>
        /// Internal callback invoked when a peer connection is about to create an offer, and
        /// determines that the media track needs to be attache to a new transceiver. The media
        /// sender must attach the local media track to <see cref="Transceiver"/>.
        /// </summary>
        internal abstract void AttachTrack();

        /// <summary>
        /// Internal callback invoked when a peer connection is about to create an offer,
        /// and determines that the media track needs to be detached from its current transceiver.
        /// The media sender must detach the local media track from <see cref="Transceiver"/>.
        /// </summary>
        internal abstract void DetachTrack();

        /// <summary>
        /// Derived classes implement the mute/unmute action on the track.
        /// </summary>
        /// <param name="mute"><c>true</c> to mute the track, or <c>false</c> to unmute it.</param>
        protected abstract void MuteImpl(bool mute);
    }
}
