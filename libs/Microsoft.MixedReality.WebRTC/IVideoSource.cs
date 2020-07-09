// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.MixedReality.WebRTC
{
    /// <summary>
    /// Enumeration of video encodings.
    /// </summary>
    public enum VideoEncoding
    {
        /// <summary>
        /// I420A video encoding with chroma (UV) halved in both directions (4:2:0),
        /// and optional Alpha plane.
        /// </summary>
        I420A,

        /// <summary>
        /// 32-bit ARGB32 video encoding with 8-bit per component, encoded as uint32 little-endian
        /// 0xAARRGGBB value, or equivalently (B,G,R,A) in byte order.
        /// </summary>
        Argb32
    }

    /// <summary>
    /// Interface for video sources, whether local or remote.
    /// </summary>
    /// <seealso cref="VideoTrackSource"/>
    /// <seealso cref="LocalVideoTrack"/>
    /// <seealso cref="RemoteVideoTrack"/>
    public interface IVideoSource
    {
        /// <summary>
        /// Video encoding indicating the kind of frames the source is producing.
        /// </summary>
        VideoEncoding FrameEncoding { get; }

        /// <summary>
        /// Event that occurs when a new video frame is available from the source, either
        /// because the source produced it locally (<see cref="VideoTrackSource"/>, <see cref="LocalVideoTrack"/>) or because
        /// it received it from the remote peer (<see cref="RemoteVideoTrack"/>).
        /// </summary>
        /// <remarks>
        /// Handlers must process the
        /// frame as fast as possible without blocking the caller thread, and cannot remove themselves
        /// from the event nor add other handlers to the event, otherwise the caller thread will deadlock.
        ///
        /// The event delivers to the handlers an I420-encoded video frame.
        /// </remarks>
        event I420AVideoFrameDelegate I420AVideoFrameReady;

        /// <summary>
        /// Event that occurs when a new video frame is available from the source, either
        /// because the source produced it locally (<see cref="VideoTrackSource"/>, <see cref="LocalVideoTrack"/>) or because
        /// it received it from the remote peer (<see cref="RemoteVideoTrack"/>).
        /// </summary>
        /// <remarks>
        /// Handlers must process the
        /// frame as fast as possible without blocking the caller thread, and cannot remove themselves
        /// from the event nor add other handlers to the event, otherwise the caller thread will deadlock.
        ///
        /// The event delivers to the handlers an ARGB32-encoded video frame.
        /// </remarks>
        event Argb32VideoFrameDelegate Argb32VideoFrameReady;

        /// <summary>
        /// Enabled status of the source. If enabled, produces video frames as expected. If
        /// disabled, produces black frames instead.
        /// </summary>
        bool Enabled { get; }
    }
}
