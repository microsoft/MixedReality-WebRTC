// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using UnityEngine.Events;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// Unity event corresponding to a new audio stream being started.
    /// </summary>
    [Serializable]
    public class AudioStreamStartedEvent : UnityEvent<IAudioSource>
    { };

    /// <summary>
    /// Unity event corresponding to an on-going audio stream being stopped.
    /// </summary>
    [Serializable]
    public class AudioStreamStoppedEvent : UnityEvent<IAudioSource>
    { };

    /// <summary>
    /// Interface for audio sources plugging into the internal peer connection API to
    /// expose a single audio stream to a renderer (<see cref="MediaPlayer"/> or custom).
    /// </summary>
    public interface IAudioSource
    {
        /// <summary>
        /// Is the audio source currently streaming some audio frames?
        /// This must be <c>true</c> between the invoking of the audio stream started
        /// and stopped events. That is, this becomes <c>true</c> after the stream started
        /// event and becomes <c>false</c> before the stream stopped event.
        /// </summary>
        bool IsStreaming { get; }

        /// <summary>
        /// Get the event notifying the user that the audio stream started.
        /// </summary>
        /// <returns>The event associated with the audio source.</returns>
        AudioStreamStartedEvent GetAudioStreamStarted();

        /// <summary>
        /// Get the event notifying the user that the audio stream stopped.
        /// </summary>
        /// <returns>The event associated with the audio source.</returns>
        AudioStreamStoppedEvent GetAudioStreamStopped();

        void RegisterCallback(AudioFrameDelegate callback);
        void UnregisterCallback(AudioFrameDelegate callback);
    }
}
