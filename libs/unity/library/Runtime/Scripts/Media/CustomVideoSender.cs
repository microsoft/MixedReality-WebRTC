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
    public abstract class CustomVideoSender<T> : VideoTrackSource where T : class, IVideoFrameStorage, new()
    {
        public CustomVideoSender()
            : base(typeof(T) == typeof(I420AVideoFrameStorage) ? VideoEncoding.I420A : VideoEncoding.Argb32)
        {
        }

        protected override Task CreateVideoTrackSourceAsync()
        {
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

            // This implementation is fast, so executes synchronously.
            return Task.CompletedTask;
        }

        protected abstract void OnFrameRequested(in FrameRequest request);
    }
}
