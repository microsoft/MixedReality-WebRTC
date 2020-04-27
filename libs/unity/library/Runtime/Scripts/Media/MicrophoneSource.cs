// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// This component represents a local audio sender generating audio frames from a local
    /// audio capture device (microphone). The audio sender can be added to an audio transceiver
    /// in order for the audio data to be sent to the remote peer. The captured audio frames can
    /// also optionally be played locally with a <see cref="MediaPlayer"/>.
    /// </summary>
    [AddComponentMenu("MixedReality-WebRTC/Microphone Source")]
    public class MicrophoneSource : AudioSender
    {
        protected override async Task CreateLocalAudioTrackAsyncImpl()
        {
            if (Track == null)
            {
                // Ensure the track has a valid name
                string trackName = TrackName;
                if (trackName.Length == 0)
                {
                    trackName = Guid.NewGuid().ToString();
                    // Re-assign the generated track name for consistency
                    TrackName = trackName;
                }
                SdpTokenAttribute.Validate(trackName, allowEmpty: false);

                // Create the local track
                var trackSettings = new LocalAudioTrackSettings
                {
                    trackName = trackName
                };
                Track = await LocalAudioTrack.CreateFromDeviceAsync(trackSettings);

                // Synchronize the track status with the Unity component status
                Track.Enabled = enabled;
            }
        }
    }
}
