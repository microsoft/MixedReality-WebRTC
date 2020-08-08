// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// Abstract base component for a custom video source delivering raw video frames
    /// directly to the WebRTC implementation.
    /// </summary>
    public abstract class CustomVideoSource<T> : VideoTrackSource where T : IVideoFrameStorage
    {
        protected virtual void OnEnable()
        {
            Debug.Assert(Source == null);

            // Create the external source
            //< TODO - Better abstraction
            if (typeof(T) == typeof(I420AVideoFrameStorage))
            {
                AttachSource(ExternalVideoTrackSource.CreateFromI420ACallback(OnFrameRequested));
            }
            else if (typeof(T) == typeof(Argb32VideoFrameStorage))
            {
                AttachSource(ExternalVideoTrackSource.CreateFromArgb32Callback(OnFrameRequested));
            }
            else
            {
                throw new NotSupportedException("This frame storage is not supported. Use I420AVideoFrameStorage or Argb32VideoFrameStorage.");
            }
        }

        protected virtual void OnDisable()
        {
            DisposeSource();
        }

        protected abstract void OnFrameRequested(in FrameRequest request);
    }
}
