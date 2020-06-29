// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using UnityEngine;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    public abstract class AudioReceiver : MediaReceiver
    {
        /// <summary>
        /// Remote audio track receiving data from the remote peer.
        /// </summary>
        /// <remarks>
        /// This is <c>null</c> until <see cref="IMediaReceiver.Transceiver"/> is set to a non-null
        /// value and a remote track is added to that transceiver.
        /// </remarks>
        public abstract RemoteAudioTrack AudioTrack { get; }

        /// <summary>
        /// Event raised when the audio stream started.
        ///
        /// When this event is raised, the followings are true:
        /// - The <see cref="Track"/> property is a valid remote audio track.
        /// - The <see cref="MediaReceiver.IsLive"/> property is <c>true</c>.
        /// - The <see cref="IsStreaming"/> will become <c>true</c> just after the event
        ///   is raised, by design.
        /// </summary>
        /// <remarks>
        /// This event is raised from the main Unity thread to allow Unity object access.
        /// </remarks>
        public readonly AudioStreamStartedEvent AudioStreamStarted = new AudioStreamStartedEvent();

        /// <summary>
        /// Event raised when the audio stream stopped.
        ///
        /// When this event is raised, the followings are true:
        /// - The <see cref="MediaReceiver.IsLive"/> property is <c>false</c>.
        /// - The <see cref="IsStreaming"/> has just become <c>false</c> right before the event
        ///   was raised, by design.
        /// </summary>
        /// <remarks>
        /// This event is raised from the main Unity thread to allow Unity object access.
        /// </remarks>
        public readonly AudioStreamStoppedEvent AudioStreamStopped = new AudioStreamStoppedEvent();

        /// <inheritdoc/>
        public override MediaKind MediaKind => MediaKind.Audio;
        /// <inheritdoc/>
        public override MediaTrack Track => AudioTrack;
    }
}
