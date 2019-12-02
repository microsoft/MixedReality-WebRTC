// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.MixedReality.WebRTC
{
    /// <summary>
    /// Single raw uncompressed audio frame.
    /// </summary>
    /// <remarks>
    /// The use of <c>ref struct</c> is an optimization to avoid heap allocation on each frame while
    /// having a nicer-to-use container to pass a frame accross methods.
    /// </remarks>
    public ref struct AudioFrame
    {
        /// <summary>
        /// Buffer of audio samples for all channels.
        /// </summary>
        public IntPtr audioData;

        /// <summary>
        /// Number of bits per sample, generally 8 or 16.
        /// </summary>
        public uint bitsPerSample;

        /// <summary>
        /// Sample rate, in Hz. Generally in the range 8-48 kHz.
        /// </summary>
        public uint sampleRate;

        /// <summary>
        /// Number of audio channels.
        /// </summary>
        public uint channelCount;

        /// <summary>
        /// Number of consecutive samples in the audio data buffer.
        /// WebRTC generally delivers frames in 10ms chunks, so for e.g. a 16 kHz
        /// sample rate the sample count would be 1000.
        /// </summary>
        public uint sampleCount;
    }

    /// <summary>
    /// Delegate used for events when an audio frame has been produced
    /// and is ready for consumption.
    /// </summary>
    /// <param name="frame">The newly available audio frame.</param>
    public delegate void AudioFrameDelegate(AudioFrame frame);
}
