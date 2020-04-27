// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// Abstract base component for a custom video source delivering raw video frames
    /// directly to the WebRTC implementation.
    /// </summary>
    public abstract class CustomVideoSender<T> : VideoSender where T : class, IVideoFrameStorage, new()
    {
        /// <summary>
        /// Video track source providing video frames to the local video track.
        /// </summary>
        public ExternalVideoTrackSource Source { get; private set; }

        public CustomVideoSender()
            : base(typeof(T) == typeof(I420AVideoFrameStorage) ? VideoEncoding.I420A : VideoEncoding.Argb32)
        {
        }

        protected override Task CreateLocalVideoTrackAsync()
        {
            // Ensure the track has a valid name
            string trackName = TrackName;
            if (string.IsNullOrEmpty(trackName))
            {
                // Generate a unique name (GUID)
                trackName = Guid.NewGuid().ToString();
                TrackName = trackName;
            }
            SdpTokenAttribute.Validate(trackName, allowEmpty: false);

            // Create the external source
            //< TODO - Better abstraction
            if (typeof(T) == typeof(I420AVideoFrameStorage))
            {
                Source = ExternalVideoTrackSource.CreateFromI420ACallback(OnFrameRequested);
            }
            else if (typeof(T) == typeof(Argb32VideoFrameStorage))
            {
                Source = ExternalVideoTrackSource.CreateFromArgb32Callback(OnFrameRequested);
            }
            else
            {
                throw new NotSupportedException("This frame storage is not supported. Use I420AVideoFrameStorage or Argb32VideoFrameStorage.");
            }
            if (Source == null)
            {
                throw new Exception("Failed to create external video track source.");
            }

            // Create the local video track
            Track = LocalVideoTrack.CreateFromExternalSource(trackName, Source);
            if (Track == null)
            {
                throw new Exception("Failed ot create webcam video track.");
            }

            // Synchronize the track status with the Unity component status
            Track.Enabled = enabled;

            // This implementation is fast, so executes synchronously.
            return Task.CompletedTask;
        }

        protected override void DestroyLocalVideoTrack()
        {
            base.DestroyLocalVideoTrack();
            if (Source != null)
            {
                Source.Dispose();
                Source = null;
            }
        }

        protected abstract void OnFrameRequested(in FrameRequest request);
    }
}
