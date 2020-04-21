// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.MixedReality.WebRTC
{
    /// <summary>
    /// Interface for audio tracks, whether local or remote.
    /// </summary>
    public interface IAudioTrack
    {
        /// <summary>
        /// Event that occurs when a new audio frame is available from the track, either
        /// because the track produced it locally (<see cref="LocalAudioTrack"/>) or because
        /// it received it from the remote peer (<see cref="RemoteAudioTrack"/>).
        /// </summary>
        /// <seealso cref="LocalAudioTrack"/>
        /// <seealso cref="RemoteAudioTrack"/>
        event AudioFrameDelegate AudioFrameReady;

        /// <summary>
        /// Enabled status of the track. If enabled, produces audio frames as expected. If
        /// disabled, produces silence instead.
        /// </summary>
        bool Enabled { get; }
    }
}
