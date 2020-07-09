// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using UnityEngine;
using UnityEngine.Events;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// Unity event corresponding to a new audio stream being started.
    /// </summary>
    [Serializable]
    public class AudioStreamStartedEvent : UnityEvent<IAudioSource>
    { };

    /// <summary>
    /// Unity event corresponding to an on-going audio stream being stopped.
    /// </summary>
    [Serializable]
    public class AudioStreamStoppedEvent : UnityEvent<IAudioSource>
    { };

    /// <summary>
    /// Endpoint for a WebRTC remote audio track.
    /// </summary>
    /// <remarks>
    /// Setting this on an audio <see cref="MediaLine"/> will enable the corresponding transceiver to receive.
    /// A remote track will be exposed through <see cref="AudioTrack"/> once a connection is established.
    /// The audio track can optionally be played locally with an <see cref="AudioRenderer"/>.
    /// </remarks>
    [AddComponentMenu("MixedReality-WebRTC/Audio Receiver")]
    public class AudioReceiver : MediaReceiver
    {
        /// <summary>
        /// Remote audio track receiving data from the remote peer.
        /// </summary>
        /// <remarks>
        /// This is <c>null</c> until:
        /// <list type="bullet">
        /// <description><see cref="MediaLine.Transceiver"/> is set to a non-null value, and</description>
        /// <description>the remote peer starts sending data to the paired transceiver after a session negotiation.</description>
        /// </list>
        /// </remarks>
        public RemoteAudioTrack AudioTrack { get; private set; }

        /// <summary>
        /// Event raised when the audio stream started.
        ///
        /// When this event is raised, the followings are true:
        /// - The <see cref="Track"/> property is a valid remote audio track.
        /// - The <see cref="MediaReceiver.IsLive"/> property is <c>true</c>.
        /// </summary>
        /// <remarks>
        /// This event is raised from the main Unity thread to allow Unity object access.
        /// </remarks>
        public AudioStreamStartedEvent AudioStreamStarted = new AudioStreamStartedEvent();

        /// <summary>
        /// Event raised when the audio stream stopped.
        ///
        /// When this event is raised, the followings are true:
        /// - The <see cref="MediaReceiver.IsLive"/> property is <c>false</c>.
        /// </summary>
        /// <remarks>
        /// This event is raised from the main Unity thread to allow Unity object access.
        /// </remarks>
        public AudioStreamStoppedEvent AudioStreamStopped = new AudioStreamStoppedEvent();

        /// <inheritdoc/>
        public override MediaKind MediaKind => MediaKind.Audio;
        /// <inheritdoc/>
        public override MediaTrack Track => AudioTrack;

        /// <inheritdoc/>
        protected internal override void OnPaired(MediaTrack track)
        {
            var remoteAudioTrack = (RemoteAudioTrack)track;

            Debug.Assert(Track == null);
            AudioTrack = remoteAudioTrack;
            AudioStreamStarted.Invoke(remoteAudioTrack);
        }

        /// <inheritdoc/>
        protected internal override void OnUnpaired(MediaTrack track)
        {
            Debug.Assert(Track == track);
            AudioTrack = null;
            AudioStreamStopped.Invoke((RemoteAudioTrack)track);
        }
    }
}
