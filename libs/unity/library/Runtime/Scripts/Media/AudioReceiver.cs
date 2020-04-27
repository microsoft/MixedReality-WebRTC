// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using UnityEngine;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// This component represents a remote audio source added as an audio track to an
    /// existing WebRTC peer connection by a remote peer and received locally.
    /// The audio track can optionally be displayed locally with a <see cref="MediaPlayer"/>.
    /// </summary>
    /// <seealso cref="MediaPlayer"/>
    [AddComponentMenu("MixedReality-WebRTC/Audio Receiver")]
    [RequireComponent(typeof(UnityEngine.AudioSource))]
    public class AudioReceiver : MediaReceiver, IAudioSource
    {
#if false // WIP
        /// <summary>
        /// Local storage of audio data to be fed to the output
        /// </summary>
        private AudioTrackReadBuffer _audioTrackReadBuffer = null;

        /// <summary>
        /// Cached sample rate since we can't access this in OnAudioFilterRead.
        /// </summary>
        private int _audioSampleRate = 0;
#endif
        /// <inheritdoc/>
        public bool IsStreaming { get; protected set; }

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
        public AudioStreamStartedEvent AudioStreamStarted = new AudioStreamStartedEvent();

        /// <summary>
        /// Event raised when the audio stream stopped.
        ///
        /// When this event is raised, the followings are true:
        /// - The <see cref="Track"/> property is <c>null</c>.
        /// - The <see cref="MediaReceiver.IsLive"/> property is <c>false</c>.
        /// - The <see cref="IsStreaming"/> has just become <c>false</c> right before the event
        ///   was raised, by design.
        /// </summary>
        /// <remarks>
        /// This event is raised from the main Unity thread to allow Unity object access.
        /// </remarks>
        public AudioStreamStoppedEvent AudioStreamStopped = new AudioStreamStoppedEvent();

        /// <inheritdoc/>
        public AudioStreamStartedEvent GetAudioStreamStarted() { return AudioStreamStarted; }

        /// <inheritdoc/>
        public AudioStreamStoppedEvent GetAudioStreamStopped() { return AudioStreamStopped; }

        /// <summary>
        /// Audio transceiver this receiver is paired with, if any.
        ///
        /// This is <c>null</c> until a remote description is applied which pairs the media line
        /// this receiver is associated with to a transceiver, or until the peer connection of this
        /// receiver's media line creates the audio receiver right before creating an SDP offer.
        /// </summary>
        public Transceiver Transceiver { get; private set; }

        /// <summary>
        /// Remote audio track receiving data from the remote peer.
        ///
        /// This is <c>null</c> until <see cref="Transceiver"/> is set to a non-null value and a
        /// remote track is added to that transceiver.
        /// </summary>
        public RemoteAudioTrack Track { get; private set; }

        /// <inheritdoc/>
        public AudioReceiver() : base(MediaKind.Audio)
        {
        }

#if false // WIP
        protected void Awake()
        {
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
        }

        protected void OnDestroy()
        {
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
        }

        private void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            _audioSampleRate = AudioSettings.outputSampleRate;
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            if (_audioTrackReadBuffer != null)
            {
                _audioTrackReadBuffer.ReadAudio(_audioSampleRate, data, channels);
            }
            else
            {
                for (int i = 0; i < data.Length; ++i)
                {
                    data[i] = 0.0f;
                }
            }
        }
#endif
        /// <summary>
        /// Register a frame callback to listen to incoming audio data receiving through this
        /// audio receiver from the remote peer.
        ///
        /// The callback can only be registered once <see cref="Track"/> is valid, that is once
        /// the <see cref="AudioStreamStarted"/> event was raised.
        /// </summary>
        /// <param name="callback">The new frame callback to register.</param>
        /// <remarks>
        /// <div class="WARNING alert alert-warning">
        /// <h5>WARNING</h5>
        /// <p>
        /// Currently audio output is done automatically, so this callback is not needed to output
        /// the remote audio, and using it to inject the audio in a custom audio pipeline will
        /// produce duplicated audio output.
        /// </p>
        /// </div>
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
                Track.AudioFrameReady += callback;
            }
        }

        /// <summary>
        /// Unregister a frame callback previously registered with <see cref="RegisterCallback(AudioFrameDelegate)"/>.
        /// </summary>
        /// <param name="callback">The frame callback to unregister.</param>
        public void UnregisterCallback(AudioFrameDelegate callback)
        {
            if (Track != null)
            {
                Track.AudioFrameReady -= callback;
            }
        }

        /// <summary>
        /// Internal callback invoked when the audio receiver is attached to a transceiver created
        /// just before the peer connection creates an SDP offer.
        /// </summary>
        /// <param name="audioTransceiver">The audio transceiver this receiver is attached with.</param>
        /// <remarks>
        /// At this time the transceiver does not yet contain a remote track. The remote track will be
        /// created when receiving an answer from the remote peer, if it agreed to send media data through
        /// that transceiver, and <see cref="OnPaired"/> will be invoked at that time.
        /// </remarks>
        internal void AttachToTransceiver(Transceiver audioTransceiver)
        {
            Debug.Assert((Transceiver == null) || (Transceiver == audioTransceiver));
            Transceiver = audioTransceiver;
        }

        /// <summary>
        /// Internal callback invoked when the audio receiver is detached from a transceiver about to be
        /// destroyed by the native implementation.
        /// </summary>
        /// <param name="audioTransceiver">The audio transceiver this receiver is attached with.</param>
        internal void DetachFromTransceiver(Transceiver audioTransceiver)
        {
            Debug.Assert((Transceiver == null) || (Transceiver == audioTransceiver));
            Transceiver = null;
        }

        /// <summary>
        /// Free-threaded callback invoked by the owning peer connection when a track is paired
        /// with this receiver, which enqueues the <see cref="AudioSource.AudioStreamStarted"/>
        /// event to be fired from the main Unity app thread.
        /// </summary>
        internal override void OnPaired(MediaTrack track)
        {
            var remoteAudioTrack = (RemoteAudioTrack)track;

            // Enqueue invoking from the main Unity app thread, both to avoid locks on public
            // properties and so that listeners of the event can directly access Unity objects
            // from their handler function.
            _mainThreadWorkQueue.Enqueue(() =>
            {
                Debug.Assert(Track == null);
                Track = remoteAudioTrack;
                IsLive = true;
                AudioStreamStarted.Invoke(this);
                IsStreaming = true;
            });
        }

        /// <summary>
        /// Free-threaded callback invoked by the owning peer connection when a track is unpaired
        /// from this receiver, which enqueues the <see cref="AudioSource.AudioStreamStopped"/>
        /// event to be fired from the main Unity app thread.
        /// </summary>
        internal override void OnUnpaired(MediaTrack track)
        {
            Debug.Assert(track is RemoteAudioTrack);

            // Enqueue invoking from the main Unity app thread, both to avoid locks on public
            // properties and so that listeners of the event can directly access Unity objects
            // from their handler function.
            _mainThreadWorkQueue.Enqueue(() =>
            {
                Debug.Assert(Track == track);
                Track = null;
                IsStreaming = false;
                IsLive = false;
                AudioStreamStopped.Invoke(this);
            });
        }
    }
}
