// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.MixedReality.WebRTC
{
    /// <summary>
    /// Interface for audio sources, whether local sources/tracks or remote tracks.
    /// </summary>
    /// <seealso cref="AudioTrackSource"/>
    /// <seealso cref="LocalAudioTrack"/>
    /// <seealso cref="RemoteAudioTrack"/>
    public interface IAudioSource
    {
        /// <summary>
        /// Event that occurs when a new audio frame is available from the source, either
        /// because the source produced it locally (<see cref="AudioTrackSource"/>, <see cref="LocalAudioTrack"/>) or because
        /// it received it from the remote peer (<see cref="RemoteAudioTrack"/>).
        /// </summary>
        /// <remarks>
        /// WebRTC audio tracks produce an audio frame every 10 ms.
        /// If you want to process the audio frames as soon as they are received, without conversions,
        /// subscribe to <see cref="AudioFrameReady"/>.
        /// If you want the audio frames to be buffered (and optionally resampled) automatically,
        /// and you want the application to control when new audio data is read, create an
        /// <see cref="AudioTrackReadBuffer"/> using <see cref="CreateReadBuffer"/>.
        /// </remarks>
        event AudioFrameDelegate AudioFrameReady;

        /// <summary>
        /// Enabled status of the source. If enabled, produces audio frames as expected. If
        /// disabled, produces silence instead.
        /// </summary>
        bool Enabled { get; }

        /// <summary>
        /// Starts buffering the audio frames from in an <see cref="AudioTrackReadBuffer"/>.
        /// </summary>
        /// <remarks>
        /// WebRTC audio tracks produce an audio frame every 10 ms.
        /// If you want the audio frames to be buffered (and optionally resampled) automatically,
        /// and you want the application to control when new audio data is read, create an
        /// <see cref="AudioTrackReadBuffer"/> using <see cref="CreateReadBuffer"/>.
        /// If you want to process the audio frames as soon as they are received, without conversions,
        /// subscribe to <see cref="AudioFrameReady"/> instead.
        /// </remarks>
        AudioTrackReadBuffer CreateReadBuffer();
    }
}
