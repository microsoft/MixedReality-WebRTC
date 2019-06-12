// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using UnityEngine;
using UnityEngine.Events;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    [Serializable]
    public class AudioStreamStartedEvent : UnityEvent
    { };

    [Serializable]
    public class AudioStreamStoppedEvent : UnityEvent
    { };

    public class AudioSource : MonoBehaviour
    {
        public AudioStreamStartedEvent AudioStreamStarted = new AudioStreamStartedEvent();
        public AudioStreamStoppedEvent AudioStreamStopped = new AudioStreamStoppedEvent();
    }
}
