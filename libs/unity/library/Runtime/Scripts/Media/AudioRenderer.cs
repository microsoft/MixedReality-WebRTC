// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using UnityEngine;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// Utility component used to play audio frames obtained from a WebRTC audio source.
    /// </summary>
    /// <remarks>
    /// Calling <see cref="StartRendering(IAudioSource)"/> and <see cref="StopRendering(IAudioSource)"/>
    /// will start/stop playing the passed <see cref="IAudioSource"/> through a <see cref="UnityEngine.AudioSource"/>
    /// component on the same object, if there is one.
    ///
    /// The component will play only while enabled.
    /// </remarks>
    /// <seealso cref="AudioReceiver"/>
    [AddComponentMenu("MixedReality-WebRTC/Audio Renderer")]
    [RequireComponent(typeof(UnityEngine.AudioSource))]
    public class AudioRenderer : MonoBehaviour
    {
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


        // Source that this renderer is currently subscribed to.
        private IAudioSource _source;

        protected void Awake()
        {
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
            OnAudioConfigurationChanged(deviceWasChanged: true);
        }

        protected void OnDestroy()
        {
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
        }

        protected void OnEnable()
        {
            if (_source != null)
            {
                StartReadBuffer();
            }
        }

        protected void OnDisable()
        {
            if (_source != null)
            {
                StopReadBuffer();
            }
        }

        /// <summary>
        /// Start rendering the passed source.
        /// </summary>
        /// <remarks>
        /// Can be used to handle <see cref="AudioReceiver.AudioStreamStarted"/>.
        /// </remarks>
        public void StartRendering(IAudioSource source)
        {
            Debug.Assert(_source == null);
            _source = source;

            if (isActiveAndEnabled)
            {
                StartReadBuffer();
            }
        }

        /// <summary>
        /// Stop rendering the passed source. Must be called with the same source passed to <see cref="StartRendering(IAudioSource)"/>
        /// </summary>
        /// <remarks>
        /// Can be used to handle <see cref="AudioReceiver.AudioStreamStopped"/>.
        /// </remarks>
        public void StopRendering(IAudioSource source)
        {
            Debug.Assert(_source == source);
            if (isActiveAndEnabled)
            {
                StopReadBuffer();
            }
            _source = null;
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

        private void StartReadBuffer()
        {
            Debug.Assert(_readBuffer == null);

            // OnAudioFilterRead reads the variable concurrently, but the update is atomic
            // so we don't need a lock.
            _readBuffer = _source.CreateReadBuffer();
        }

        private void StopReadBuffer()
        {
            lock (_readBufferLock)
            {
                // Under lock so OnAudioFilterRead won't use the buffer while/after it is disposed.
                _readBuffer.Dispose();
                _readBuffer = null;
            }
        }
}
}
