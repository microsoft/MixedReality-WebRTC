// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using UnityEngine;
using UnityEngine.Events;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// Unity event corresponding to a new video stream being started.
    /// </summary>
    [Serializable]
    public class VideoStreamStartedEvent : UnityEvent
    { };

    /// <summary>
    /// Unity event corresponding to an on-going video stream being stopped.
    /// </summary>
    [Serializable]
    public class VideoStreamStoppedEvent : UnityEvent
    { };

    /// <summary>
    /// Base class for video sources plugging into the internal peer connection API to
    /// expose a single video stream to a renderer (<see cref="MediaPlayer"/> or custom).
    /// </summary>
    public abstract class VideoSource : MonoBehaviour
    {
        /// <summary>
        /// Frame queue holding the pending frames enqueued by the video source itself,
        /// which a video renderer needs to read and display.
        /// </summary>
        public VideoFrameQueue<I420VideoFrameStorage> FrameQueue;

        /// <summary>
        /// Event invoked from the main Unity thread when the video stream starts.
        /// This means that video frames are available and the renderer should start polling.
        /// </summary>
        public VideoStreamStartedEvent VideoStreamStarted = new VideoStreamStartedEvent();

        /// <summary>
        /// Event invoked from the main Unity thread when the video stream stops.
        /// This means that the video frame queue is not populated anymore, though some frames
        /// may still be present in it that may be rendered.
        /// </summary>
        public VideoStreamStoppedEvent VideoStreamStopped = new VideoStreamStoppedEvent();
    }
}
