// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// This component represents an audio track source generating audio frames for one or more
    /// audio tracks.
    /// </summary>
    /// <seealso cref="MicrophoneSource"/>
    public abstract class AudioTrackSource : MonoBehaviour, IAudioSource, IMediaTrackSource, IMediaTrackSourceInternal
    {
        /// <summary>
        /// Audio track source object from the underlying C# library that this component encapsulates.
        ///
        /// The object is owned by this component, which will create it and dispose of it automatically.
        /// </summary>
        public WebRTC.AudioTrackSource Source { get; protected set; } = null;

        /// <summary>
        /// List of audio media lines using this source.
        /// </summary>
        public IReadOnlyList<MediaLine> MediaLines => _mediaLines;

        /// <summary>
        /// Event raised when the audio stream started.
        ///
        /// When this event is raised, the followings are true:
        /// - The <see cref="Source"/> property is a valid audio track source.
        /// - The the <see cref="IsStreaming"/> property will become <c>true</c> just after
        ///   the event is raised, by design.
        /// </summary>
        /// <remarks>
        /// This event is raised from the main Unity thread to allow Unity object access.
        /// </remarks>
        public AudioStreamStartedEvent AudioSourceStarted = new AudioStreamStartedEvent();

        /// <summary>
        /// Event raised when the audio stream stopped.
        ///
        /// When this event is raised, the followings are true:
        /// - The <see cref="Source"/> property is <c>null</c>.
        /// - The <see cref="IsStreaming"/> property has just become <c>false</c> right
        ///   before the event was raised, by design.
        /// </summary>
        /// <remarks>
        /// This event is raised from the main Unity thread to allow Unity object access.
        /// </remarks>
        public AudioStreamStoppedEvent AudioSourceStopped = new AudioStreamStoppedEvent();


        #region IAudioSource interface

        /// <inheritdoc/>
        public bool IsStreaming { get; protected set; }

        /// <inheritdoc/>
        public AudioStreamStartedEvent GetAudioStreamStarted() { return AudioSourceStarted; }

        /// <inheritdoc/>
        public AudioStreamStoppedEvent GetAudioStreamStopped() { return AudioSourceStopped; }

        /// <summary>
        /// Register a frame callback to listen to outgoing audio data produced by this audio sender
        /// and sent to the remote peer.
        ///
        /// <div class="WARNING alert alert-warning">
        /// <h5>WARNING</h5>
        /// <p>
        /// Currently the low-level WebRTC implementation does not support registering local audio callbacks,
        /// therefore this is not implemented and will throw a <see cref="System.NotImplementedException"/>.
        /// </p>
        /// </div>
        /// </summary>
        /// <param name="callback">The new frame callback to register.</param>
        /// <remarks>
        /// Unlike for video, where a typical application might display some local feedback of a local
        /// webcam recording, local audio feedback is rare, so this callback is not typically used.
        /// One possible use case would be to display some visual feedback, like an audio spectrum analyzer.
        ///
        /// Note that registering a callback does not influence the audio capture and sending to the
        /// remote peer, which occur whether or not a callback is registered.
        /// </remarks>
        public void RegisterCallback(AudioFrameDelegate callback)
        {
            throw new NotImplementedException("Local audio callbacks are not currently implemented.");
        }

        /// <inheritdoc/>
        public void UnregisterCallback(AudioFrameDelegate callback)
        {
            throw new NotImplementedException("Local audio callbacks are not currently implemented.");
        }

        #endregion


        #region IMediaTrackSource

        /// <inheritdoc/>
        MediaKind IMediaTrackSource.MediaKind => MediaKind.Audio;

        #endregion


        private readonly List<MediaLine> _mediaLines = new List<MediaLine>();

        protected virtual void OnDisable()
        {
            if (Source != null)
            {
                // Notify media lines using this source.
                foreach (var ml in _mediaLines)
                {
                    ml.OnSourceDestroyed();
                }
                _mediaLines.Clear();

                // Audio track sources are disposable objects owned by the user (this component)
                Source.Dispose();
                Source = null;

                AudioSourceStopped.Invoke(this);
                IsStreaming = false;
            }
        }

        void IMediaTrackSourceInternal.OnAddedToMediaLine(MediaLine mediaLine)
        {
            Debug.Assert(!_mediaLines.Contains(mediaLine));
            _mediaLines.Add(mediaLine);
        }

        void IMediaTrackSourceInternal.OnRemoveFromMediaLine(MediaLine mediaLine)
        {
            bool removed = _mediaLines.Remove(mediaLine);
            Debug.Assert(removed);
        }
    }
}
