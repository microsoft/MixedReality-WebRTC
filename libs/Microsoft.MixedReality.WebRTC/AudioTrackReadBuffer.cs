// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#if false // WIP
using Microsoft.MixedReality.WebRTC.Interop;
using System;

namespace Microsoft.MixedReality.WebRTC
{
    /// <summary>
    /// High level interface for consuming WebRTC audio tracks.
    /// The implementation builds on top of the low-level AudioFrame callbacks
    /// and handles all buffering and resampling.
    /// </summary>
    /// <seealso cref="PeerConnection.CreateAudioTrackReadBuffer(int)"/>
    public class AudioTrackReadBuffer : IDisposable
    {
        private AudioTrackReadBufferInterop.Handle _nativeHandle;

        internal AudioTrackReadBuffer(PeerConnectionHandle peerHandle, int bufferMs)
        {
            uint res = AudioTrackReadBufferInterop.Create(peerHandle, bufferMs, out _nativeHandle);
            Utils.ThrowOnErrorCode(res);
        }

        /// <summary>
        /// Fill data with samples at the given sampleRate and number of channels.
        /// </summary>
        /// <remarks>
        /// If the internal buffer overruns, the oldest data will be dropped.
        /// If the internal buffer is exhausted, the data is padded with white noise.
        /// In any case the entire data array is filled.
        /// </remarks>
        public void ReadAudio(int sampleRate, float[] data, int channels)
        {
            AudioTrackReadBufferInterop.Read(_nativeHandle, sampleRate, data, data.Length, channels);
        }

        /// <summary>
        /// Release the buffer and unregister the remote audio callback on the associated PeerConnection.
        /// </summary>
        public void Dispose()
        {
            _nativeHandle.Dispose();
        }
    }
}
#endif
