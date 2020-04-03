// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.MixedReality.WebRTC
{
    /// <summary>
    /// Interface for video tracks, whether local or remote.
    /// </summary>
    public interface IVideoTrack
    {
        /// <summary>
        /// Event that occurs when a new video frame is available from the track, either
        /// because the track produced it locally (<see cref="LocalVideoTrack"/>) or because
        /// it received it from the remote peer (<see cref="RemoteVideoTrack"/>).
        /// 
        /// The frame is delivered as an I420A-encoded video frame.
        /// </summary>
        /// <seealso cref="LocalVideoTrack"/>
        /// <seealso cref="RemoteVideoTrack"/>
        event I420AVideoFrameDelegate I420AVideoFrameReady;

        /// <summary>
        /// Event that occurs when a new video frame is available from the track, either
        /// because the track produced it locally (<see cref="LocalVideoTrack"/>) or because
        /// it received it from the remote peer (<see cref="RemoteVideoTrack"/>).
        /// 
        /// The frame is delivered as an ARGB32-encoded video frame.
        /// </summary>
        /// <seealso cref="LocalVideoTrack"/>
        /// <seealso cref="RemoteVideoTrack"/>
        event Argb32VideoFrameDelegate Argb32VideoFrameReady;

        /// <summary>
        /// Enabled status of the track. If enabled, produces video frames as expected. If
        /// disabled, produces black frames instead.
        /// </summary>
        bool Enabled { get; }
    }
}
