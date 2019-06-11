// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using UnityEngine;
using UnityEngine.Events;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    [Serializable]
    public class VideoStreamStartedEvent : UnityEvent
    { };

    [Serializable]
    public class VideoStreamStoppedEvent : UnityEvent
    { };

    public class VideoSource : MonoBehaviour
    {
        public VideoFrameQueue<I420VideoFrameStorage> FrameQueue;
        public VideoStreamStartedEvent VideoStreamStarted = new VideoStreamStartedEvent();
        public VideoStreamStoppedEvent VideoStreamStopped = new VideoStreamStoppedEvent();
    }
}
