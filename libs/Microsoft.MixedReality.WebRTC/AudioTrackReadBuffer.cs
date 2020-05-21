// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.MixedReality.WebRTC.Interop;
using System;

namespace Microsoft.MixedReality.WebRTC
{
    /// <summary>
    /// High level interface for consuming WebRTC audio tracks.
    /// Enqueues audio frames for a <see cref="RemoteAudioTrack"/> in an internal buffer as they
    /// arrive. Users should call
    /// <see cref="Read(int, int, float[], out int, out bool, AudioTrackReadBuffer.PadBehavior)"/>
    /// to read samples from the buffer when needed.
    /// </summary>
    /// <seealso cref="RemoteAudioTrack.CreateReadBuffer()"/>
    public class AudioTrackReadBuffer : IDisposable
    {
        private readonly RemoteAudioTrackInterop.ReadBufferHandle _nativeHandle;

        /// <summary>
        /// Controls the padding behavior of
        /// <see cref="Read(int, int, float[], out int, out bool, PadBehavior)"/>
        /// on underrun.
        /// </summary>
        public enum PadBehavior
        {
            /// <summary>
            /// Do not pad the samples array.
            /// </summary>
            DoNotPad = 0,

            /// <summary>
            /// Pad with zeros (silence).
            /// </summary>
            PadWithZero = 1,

            /// <summary>
            /// Pad with a sine function.
            /// </summary>
            /// <remarks>
            /// Generates audible artifacts on underrun. Use for debugging.
            /// </remarks>
            PadWithSine = 2
        }

        internal AudioTrackReadBuffer(RemoteAudioTrackInterop.ReadBufferHandle handle)
        {
            _nativeHandle = handle;
        }

        /// <summary>
        /// Fill <paramref name="samplesOut"/> with samples from the internal buffer.
        /// </summary>
        /// <remarks>
        /// This method reads the internal buffer starting from the oldest data.
        /// If the internal buffer is exhausted (underrun), <paramref name="samplesOut"/>
        /// is padded according to the value of <paramref name="padBehavior"/>.
        ///
        /// This method should be called regularly to consume the audio data as it is
        /// received. Note that the internal buffer can overrun (and some frames can be
        /// dropped) if this is not called frequently enough.
        /// </remarks>
        /// <param name="sampleRate">
        /// Desired sample rate. Data in the buffer is resampled if this is different from
        /// the native track rate.
        /// </param>
        /// <param name="numChannels">
        /// Desired number of channels. Should be 1 or 2. Data in the buffer is split/averaged
        /// if this is different from the native track channels number.
        /// </param>
        /// <param name="samplesOut">
        /// Will be filled with the samples read from the internal buffer. The function will
        /// try to fill the entire length of the array.
        /// </param>
        /// <param name="numSamplesRead">
        /// Set to the effective number of samples read.
        /// This will be generally equal to the length of <paramref name="samplesOut"/>, but can be less in
        /// case of underrun.
        /// </param>
        /// <param name="hasOverrun">
        /// Set to <c>true</c> if frames have been dropped from the internal
        /// buffer between the previous call to <c>Read</c> and this.
        /// </param>
        /// <param name="padBehavior">Controls how <paramref name="samplesOut"/> is padded in case of underrun.</param>
        public void Read(int sampleRate, int numChannels,
            float[] samplesOut, out int numSamplesRead, out bool hasOverrun,
            PadBehavior padBehavior = PadBehavior.PadWithZero)
        {
            RemoteAudioTrackInterop.AudioTrackReadBuffer_Read(_nativeHandle,
                sampleRate, numChannels, padBehavior, samplesOut, samplesOut.Length, out numSamplesRead, out mrsBool has_overrun_res);
            hasOverrun = (bool)has_overrun_res;
        }

        /// <summary>
        /// Fill <paramref name="samplesOut"/> with samples from the internal buffer.
        /// See <see cref="Read(int, int, float[], out int, out bool, PadBehavior)"/>.
        /// </summary>
        public void Read(int sampleRate, int channels,
            float[] samplesOut, PadBehavior padBehavior = PadBehavior.PadWithZero)
        {
            Read(sampleRate, channels, samplesOut, out int _, out bool _, padBehavior);
        }

        /// <summary>
        /// Release the buffer.
        /// </summary>
        public void Dispose()
        {
            _nativeHandle.Dispose();
        }
    }
}
