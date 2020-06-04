// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using UnityEngine;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// This component represents a remote audio source added as an audio track to an
    /// existing WebRTC peer connection by a remote peer and received locally.
    /// The audio track can optionally be displayed locally with a <see cref="VideoRenderer"/>.
    /// </summary>
    /// <remarks>
    /// This component will play audio only while it is active and a remote track is associated
    /// to the paired <see cref="WebRTC.Transceiver"/>.
    /// </remarks>
    /// <seealso cref="VideoRenderer"/>
    [AddComponentMenu("MixedReality-WebRTC/Audio Receiver")]
    [RequireComponent(typeof(UnityEngine.AudioSource))]
    public class AudioReceiver : MediaReceiver, IAudioSource
    {
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
        /// Remote audio track receiving data from the remote peer.
        /// </summary>
        /// <remarks>
        /// This is <c>null</c> until <see cref="Transceiver"/> is set to a non-null value and a
        /// remote track is added to that transceiver.
        /// </remarks>
        public RemoteAudioTrack Track => _track;

        // This is set outside the main thread, make volatile so changes to this and other
        // public properties are correctly ordered.
        private volatile RemoteAudioTrack _track = null;

        /// <summary>
        /// If true, pad buffer underruns with a sine wave. This will cause artifacts on underruns.
        /// Use for debugging.
        /// </summary>
        public bool PadWithSine = false;

        // Local storage of audio data to be fed to the output
        private AudioTrackReadBuffer _readBuffer = null;

        // _readBuffer can be accessed concurrently by audio thread (OnAudioFilterRead)
        // and main thread (StartStreaming, StopStreaming).
        private readonly object _readBufferLock = new object();

        // Cached sample rate since we can't access this in OnAudioFilterRead.
        private int _audioSampleRate = 0;

        /// <inheritdoc/>
        public AudioReceiver() : base(MediaKind.Audio)
        {
        }

        protected void Awake()
        {
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
            OnAudioConfigurationChanged(deviceWasChanged: true);
        }

        protected void OnDestroy()
        {
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
        }

        private void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            _audioSampleRate = AudioSettings.outputSampleRate;
        }

        // Must be called within Unity main thread.
        private void StartStreaming()
        {
            Debug.Assert(_readBuffer == null);

            // OnAudioFilterRead reads the variable concurrently, but the update is atomic
            // so we don't need a lock.
            _readBuffer = Track.CreateReadBuffer();

            IsLive = true;
            AudioStreamStarted.Invoke(this);
            IsStreaming = true;
        }

        // Must be called within Unity main thread.
        private void StopStreaming()
        {
            IsLive = false;
            IsStreaming = false;
            AudioStreamStopped.Invoke(this);

            lock (_readBufferLock)
            {
                // Under lock so OnAudioFilterRead won't use the buffer while/after it is disposed.
                _readBuffer.Dispose();
                _readBuffer = null;
            }
        }

        protected new void Update()
        {
            base.Update();

            // Check if _track has been changed by OnPaired/OnUnpaired and
            // we need to start/stop streaming.
            if (_track != null && !IsStreaming)
            {
                StartStreaming();
            }
            else if (_track == null && IsStreaming)
            {
                StopStreaming();
            }
        }

        protected void OnDisable()
        {
            if (IsStreaming)
            {
                StopStreaming();
            }
        }

        protected void OnAudioFilterRead(float[] data, int channels)
        {
            var behavior = PadWithSine ?
                AudioTrackReadBuffer.PadBehavior.PadWithSine :
                AudioTrackReadBuffer.PadBehavior.PadWithZero;
            bool hasRead = false;
            bool hasOverrun = false;
            bool hasUnderrun = false;

            lock (_readBufferLock)
            {
                // Read and use buffer under lock to prevent disposal while in use.
                if (_readBuffer != null)
                {
                    _readBuffer.Read(_audioSampleRate, channels, data,
                        out int numSamplesRead, out hasOverrun, behavior);
                    hasRead = true;
                    hasUnderrun = numSamplesRead < data.Length;
                }
            }

            if (hasRead)
            {
                // Uncomment for debugging.
                //if (hasOverrun)
                //{
                //    Debug.LogWarning($"Overrun in track {Track.Name}");
                //}
                //if (hasUnderrun)
                //{
                //    Debug.LogWarning($"Underrun in track {Track.Name}");
                //}

                return;
            }

            // If there is no track/buffer, fill array with 0s.
            for (int i = 0; i < data.Length; ++i)
            {
                data[i] = 0.0f;
            }
        }

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
        /// Free-threaded callback invoked by the owning peer connection when a track is paired
        /// with this receiver, which enqueues the <see cref="AudioTrackSource.AudioStreamStarted"/>
        /// event to be raised from the main Unity app thread.
        /// </summary>
        internal override void OnPaired(MediaTrack track)
        {
            var remoteAudioTrack = (RemoteAudioTrack)track;

            Debug.Assert(Track == null);
            _track = remoteAudioTrack;
            // Streaming will be started from the main Unity app thread, both to avoid locks on public
            // properties and so that listeners of the event can directly access Unity objects
            // from their handler function.
        }

        /// <summary>
        /// Free-threaded callback invoked by the owning peer connection when a track is unpaired
        /// from this receiver, which enqueues the <see cref="AudioTrackSource.AudioStreamStopped"/>
        /// event to be raised from the main Unity app thread.
        /// </summary>
        internal override void OnUnpaired(MediaTrack track)
        {
            Debug.Assert(track is RemoteAudioTrack);
            Debug.Assert(Track == track);
            _track = null;
            // Streaming will be stopped from the main Unity app thread, both to avoid locks on public
            // properties and so that listeners of the event can directly access Unity objects
            // from their handler function.
        }
    }
}
