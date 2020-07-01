// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
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
    [AddComponentMenu("MixedReality-WebRTC/Remote Audio Renderer")]
    [RequireComponent(typeof(UnityEngine.AudioSource))]
    public class RemoteAudioRenderer : WorkQueue
    {
        public AudioReceiver Receiver;

        /// <inheritdoc />
        public RemoteAudioTrack AudioTrack => _track;

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

        // This is set outside the main thread, make volatile so changes to this and other
        // public properties are correctly ordered.
        private volatile RemoteAudioTrack _track = null;

        protected override void Awake()
        {
            base.Awake();
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
            OnAudioConfigurationChanged(deviceWasChanged: true);
        }

        protected void OnDestroy()
        {
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
        }

        protected override void Update()
        {
            base.Update();

            // Check if _track has been changed by OnPaired/OnUnpaired and
            // we need to start/stop streaming.
            if (_track != null && !Receiver.IsStreaming)
            {
                StartStreaming();
            }
            else if (_track == null && Receiver.IsStreaming)
            {
                StopStreaming();
            }
        }

        protected void OnDisable()
        {
            if (Receiver.IsStreaming)
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

        private void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            _audioSampleRate = AudioSettings.outputSampleRate;
        }

        private void StartStreaming()
        {
            Debug.Assert(_readBuffer == null);
            EnsureIsMainAppThread();

            // OnAudioFilterRead reads the variable concurrently, but the update is atomic
            // so we don't need a lock.
            _readBuffer = AudioTrack.CreateReadBuffer();
        }

        private void StopStreaming()
        {
            EnsureIsMainAppThread();

            lock (_readBufferLock)
            {
                // Under lock so OnAudioFilterRead won't use the buffer while/after it is disposed.
                _readBuffer.Dispose();
                _readBuffer = null;
            }
        }
}
}
