// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// Abstract base component for a custom video source delivering raw video frames
    /// directly to the WebRTC implementation.
    /// </summary>
    public abstract class CustomVideoSource<T> : VideoTrackSource where T : class, IVideoFrameStorage, new()
    {
        public CustomVideoSource()
            : base(typeof(T) == typeof(I420AVideoFrameStorage) ? VideoEncoding.I420A : VideoEncoding.Argb32)
        {
        }

        protected virtual void OnEnable()
        {
            if (Source != null)
            {
                return;
            }

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

            IsStreaming = true;
            VideoStreamStarted.Invoke(this);
        }

        protected abstract void OnFrameRequested(in FrameRequest request);
    }
}
