// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using UnityEngine;
using UnityEngine.Events;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// Unity event corresponding to a new video stream being started.
    /// </summary>
    [Serializable]
    public class VideoStreamStartedEvent : UnityEvent<IVideoSource>
    { };

    /// <summary>
    /// Unity event corresponding to an on-going video stream being stopped.
    /// </summary>
    [Serializable]
    public class VideoStreamStoppedEvent : UnityEvent<IVideoSource>
    { };

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
    /// Interface for video sources plugging into the internal peer connection API to
    /// expose a single video stream to a renderer (<see cref="MediaPlayer"/> or custom).
    /// </summary>
    public interface IVideoSource
    {
        bool IsPlaying { get; }

        VideoStreamStartedEvent GetVideoStreamStarted();
        VideoStreamStoppedEvent GetVideoStreamStopped();
        
        /// <summary>
        /// Video encoding indicating the kind of frames the source is producing.
        /// This is used for example by the <see cref="MediaPlayer"/> to determine how to
        /// render the frame.
        /// </summary>
        VideoEncoding FrameEncoding { get; }

        void RegisterCallback(I420AVideoFrameDelegate callback);
        void UnregisterCallback(I420AVideoFrameDelegate callback);
        void RegisterCallback(Argb32VideoFrameDelegate callback);
        void UnregisterCallback(Argb32VideoFrameDelegate callback);
    }
}
