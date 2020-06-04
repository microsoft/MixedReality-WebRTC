// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// This component represents an audio track source generating audio frames for one or more
    /// audio tracks.
    /// </summary>
    /// <seealso cref="MicrophoneSource"/>
    public abstract class AudioTrackSource : MediaTrackSource, IAudioSource
    {
        #region IAudioSource interface

        /// <inheritdoc/>
        public bool IsStreaming { get; protected set; }

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

        /// <summary>
        /// Unregister a frame callback previously registered with <see cref="RegisterCallback(AudioFrameDelegate)"/>.
        /// </summary>
        /// <param name="callback">The frame callback to unregister.</param>
        public void UnregisterCallback(AudioFrameDelegate callback)
        {
            throw new NotImplementedException("Local audio callbacks are not currently implemented.");
        }

        #endregion


        /// <summary>
        /// Audio track source object from the underlying C# library that this component encapsulates.
        /// 
        /// The object is owned by this component, which will create it and dispose of it automatically.
        /// </summary>
        public WebRTC.AudioTrackSource Source { get; protected set; } = null;

        /// <summary>
        /// List of audio senders (tracks) using this source.
        /// </summary>
        public List<AudioSender> Senders { get; } = new List<AudioSender>();

        public AudioTrackSource() : base(MediaKind.Audio)
        {
        }


        #region MediaTrackSource implementation

        protected override async Task CreateSourceAsync()
        {
            if (Source == null)
            {
                // Defer track creation to derived classes, which will invoke some methods like
                // AudioTrackSource.CreateFromDeviceAsync().
                await CreateAudioTrackSourceAsyncImpl();
                Debug.Assert(Source != null, "Implementation did not create a valid Source property yet did not throw any exception.", this);

                // Dispatch the event to the main Unity app thread to allow Unity object access
                _mainThreadWorkQueue.Enqueue(() =>
                {
                    AudioSourceStarted.Invoke(this);

                    // Only clear this after the event handlers ran
                    IsStreaming = true;
                });
            }
        }

        protected override void DestroySource()
        {
            if (Source != null)
            {
                // Defer track destruction to derived classes.
                DestroyAudioTrackSource();
                Debug.Assert(Source == null, "Implementation did not destroy the existing Source property yet did not throw any exception.", this);

                // Clear this already to make sure it is false when the event is raised.
                IsStreaming = false;

                // Dispatch the event to the main Unity app thread to allow Unity object access
                _mainThreadWorkQueue.Enqueue(() =>
                {
                    AudioSourceStopped.Invoke(this);
                });
            }
        }

        #endregion


        /// <summary>
        /// Implement this callback to create the <see cref="Source"/> instance.
        /// On failure, this method must throw an exception. Otherwise it must set the <see cref="Source"/>
        /// property to a non-<c>null</c> instance.
        /// </summary>
        protected abstract Task CreateAudioTrackSourceAsyncImpl();

        /// <summary>
        /// Re-implement this callback to destroy the <see cref="Source"/> instance
        /// and other associated resources.
        /// </summary>
        protected virtual void DestroyAudioTrackSource()
        {
            if (Source != null)
            {
                // Notify senders using that source
                while (Senders.Count > 0) // Dispose() calls OnSenderRemoved() which will modify the collection
                {
                    Senders[Senders.Count - 1].Dispose();
                }

                // Audio track sources are disposable objects owned by the user (this component)
                Source.Dispose();
                Source = null;
            }
        }

        internal void OnSenderAdded(AudioSender sender)
        {
            Debug.Assert(!Senders.Contains(sender));
            Senders.Add(sender);
        }

        internal void OnSenderRemoved(AudioSender sender)
        {
            bool removed = Senders.Remove(sender);
            Debug.Assert(removed);
        }
    }
}
