// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using UnityEngine;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    public class AudioReceiver : MediaReceiver, IAudioSource
    {
        /// <summary>
        /// Remote audio track receiving data from the remote peer.
        /// </summary>
        /// <remarks>
        /// This is <c>null</c> until <see cref="IMediaReceiver.Transceiver"/> is set to a non-null
        /// value and a remote track is added to that transceiver.
        /// </remarks>
        public RemoteAudioTrack AudioTrack { get; private set; }

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
        public bool IsStreaming { get; protected set; }

        /// <summary>
        /// Register a frame callback to listen to incoming audio data receiving through this
        /// audio receiver from the remote peer.
        ///
        /// The callback can only be registered once <see cref="Track"/> is valid, that is once
        /// the <see cref="AudioStreamStarted"/> event was raised.
        /// </summary>
        /// <param name="callback">The new frame callback to register.</param>
        /// <remarks>
        /// Note that audio is output automatically through the <see cref="UnityEngine.AudioSource"/>
        /// on the game object, so the data passed to the callback shouldn't be using for audio output.
        ///
        /// A typical application might use this callback to display some feedback of audio being
        /// received, like a spectrum analyzer, but more commonly will not need that callback because
        /// of the above restriction on automated audio output.
        ///
        /// Note that registering a callback does not influence the audio capture and sending
        /// to the remote peer, which occurs whether or not a callback is registered.
        /// </remarks>
        public void RegisterCallback(AudioFrameDelegate callback)
        {
            if (Track != null)
            {
                AudioTrack.AudioFrameReady += callback;
            }
        }

        /// <inheritdoc/>
        public void UnregisterCallback(AudioFrameDelegate callback)
        {
            if (Track != null)
            {
                AudioTrack.AudioFrameReady -= callback;
            }
        }

        /// <inheritdoc/>
        public AudioStreamStartedEvent GetAudioStreamStarted() { return AudioStreamStarted; }

        /// <inheritdoc/>
        public AudioStreamStoppedEvent GetAudioStreamStopped() { return AudioStreamStopped; }

        /// <inheritdoc/>
        public override MediaKind MediaKind => MediaKind.Audio;
        /// <inheritdoc/>
        public override MediaTrack Track => AudioTrack;

        protected internal override void OnPaired(MediaTrack track)
        {
            var remoteAudioTrack = (RemoteAudioTrack)track;

            Debug.Assert(Track == null);
            AudioTrack = remoteAudioTrack;
            IsStreaming = true;
            AudioStreamStarted.Invoke(this);
        }

        protected internal override void OnUnpaired(MediaTrack track)
        {
            Debug.Assert(track is RemoteAudioTrack);
            Debug.Assert(Track == track);
            AudioTrack = null;
            AudioStreamStopped.Invoke(this);
            IsStreaming = false;
        }
    }
}
