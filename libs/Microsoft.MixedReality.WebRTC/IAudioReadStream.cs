using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.MixedReality.WebRTC
{
    /// <summary>
    /// High level interface for consuming WebRTC audio tracks.
    /// The implementation builds on top of the low-level AudioFrame callbacks
    /// and handles all buffering and resampling.
    /// </summary>
    public interface IAudioTrackReadBuffer : IDisposable
    {
        /// <summary>
        /// Consume some data from the stream.
        /// </summary>
        /// <param name="sampleRate">The desired sample rate.</param>
        /// <param name="data">The buffer for output data.</param>
        /// <param name="channels">The number of channels in data.</param>
        void ReadAudio(int sampleRate, float[] data, int channels);
    }
}
