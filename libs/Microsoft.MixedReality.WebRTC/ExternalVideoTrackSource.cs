// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.MixedReality.WebRTC.Interop;

namespace Microsoft.MixedReality.WebRTC
{
    /// <summary>
    /// Request sent to an external video source via its registered callback to generate
    /// a new video frame for the track(s) connected to it.
    /// </summary>
    public ref struct FrameRequest
    {
        /// <summary>
        /// Video track source this request is associated with.
        /// </summary>
        public ExternalVideoTrackSource Source;

        /// <summary>
        /// Unique request identifier, for error checking.
        /// </summary>
        public uint RequestId;

        /// <summary>
        /// Frame timestamp, in milliseconds. This corresponds to the time when the request
        /// was made to the native video track source.
        /// </summary>
        public long TimestampMs;

        /// <summary>
        /// Complete the current request by providing a video frame for it.
        /// This must be used if the video track source was created with
        /// <see cref="ExternalVideoTrackSource.CreateFromI420ACallback(I420AVideoFrameRequestDelegate)"/>.
        /// </summary>
        /// <param name="frame">The video frame used to complete the request.</param>
        public void CompleteRequest(in I420AVideoFrame frame)
        {
            Source.CompleteFrameRequest(RequestId, TimestampMs, frame);
        }

        /// <summary>
        /// Complete the current request by providing a video frame for it.
        /// This must be used if the video track source was created with
        /// <see cref="ExternalVideoTrackSource.CreateFromArgb32Callback(Argb32VideoFrameRequestDelegate)"/>.
        /// </summary>
        /// <param name="frame">The video frame used to complete the request.</param>
        public void CompleteRequest(in Argb32VideoFrame frame)
        {
            Source.CompleteFrameRequest(RequestId, TimestampMs, frame);
        }
    }

    /// <summary>
    /// Callback invoked when the WebRTC pipeline needs an external video source to generate
    /// a new video frame for the track(s) it is connected to.
    /// </summary>
    /// <param name="request">The request to fulfill with a new I420A video frame.</param>
    public delegate void I420AVideoFrameRequestDelegate(in FrameRequest request);

    /// <summary>
    /// Callback invoked when the WebRTC pipeline needs an external video source to generate
    /// a new video frame for the track(s) it is connected to.
    /// </summary>
    /// <param name="request">The request to fulfill with a new ARGB32 video frame.</param>
    public delegate void Argb32VideoFrameRequestDelegate(in FrameRequest request);

    /// <summary>
    /// Video source for WebRTC video tracks based on a custom source
    /// of video frames managed by the user and external to the WebRTC
    /// implementation.
    ///
    /// This class is used to inject into the WebRTC engine a video track
    /// whose frames are produced by a user-managed source the WebRTC engine
    /// knows nothing about, like programmatically generated frames, including
    /// frames not strictly of video origin like a 3D rendered scene, or frames
    /// coming from a specific capture device not supported natively by WebRTC.
    /// This class serves as an adapter for such video frame sources.
    /// </summary>
    public class ExternalVideoTrackSource : VideoTrackSource
    {
        /// <inheritdoc/>
        public override VideoEncoding FrameEncoding { get; }

        /// <summary>
        /// GC handle to frame request callback args keeping the delegate alive
        /// while the callback is registered with the native implementation.
        /// </summary>
        protected IntPtr _frameRequestCallbackArgsHandle;

        /// <summary>
        /// Create a new external video track source from a given user callback providing I420A-encoded frames.
        /// </summary>
        /// <param name="frameCallback">The callback that will be used to request frames for tracks.</param>
        /// <returns>The newly created track source.</returns>
        public static ExternalVideoTrackSource CreateFromI420ACallback(I420AVideoFrameRequestDelegate frameCallback)
        {
            return ExternalVideoTrackSourceInterop.CreateExternalVideoTrackSourceFromI420ACallback(frameCallback);
        }

        /// <summary>
        /// Create a new external video track source from a given user callback providing ARGB32-encoded frames.
        /// </summary>
        /// <param name="frameCallback">The callback that will be used to request frames for tracks.</param>
        /// <returns>The newly created track source.</returns>
        public static ExternalVideoTrackSource CreateFromArgb32Callback(Argb32VideoFrameRequestDelegate frameCallback)
        {
            return ExternalVideoTrackSourceInterop.CreateExternalVideoTrackSourceFromArgb32Callback(frameCallback);
        }

        internal ExternalVideoTrackSource(IntPtr frameRequestCallbackArgsHandle, VideoEncoding videoEncoding)
        {
            _frameRequestCallbackArgsHandle = frameRequestCallbackArgsHandle;
            FrameEncoding = videoEncoding;
        }

        /// <summary>
        /// Complete the current request by providing a video frame for it.
        /// This must be used if the video track source was created with
        /// <see cref="CreateFromI420ACallback(I420AVideoFrameRequestDelegate)"/>.
        /// </summary>
        /// <param name="requestId">The original request ID.</param>
        /// <param name="timestampMs">The video frame timestamp.</param>
        /// <param name="frame">The video frame used to complete the request.</param>
        public void CompleteFrameRequest(uint requestId, long timestampMs, in I420AVideoFrame frame)
        {
            ExternalVideoTrackSourceInterop.CompleteFrameRequest((ExternalVideoTrackSourceHandle)_nativeHandle, requestId, timestampMs, frame);
        }

        /// <summary>
        /// Complete the current request by providing a video frame for it.
        /// This must be used if the video track source was created with
        /// <see cref="CreateFromArgb32Callback(Argb32VideoFrameRequestDelegate)"/>.
        /// </summary>
        /// <param name="requestId">The original request ID.</param>
        /// <param name="timestampMs">The video frame timestamp.</param>
        /// <param name="frame">The video frame used to complete the request.</param>
        public void CompleteFrameRequest(uint requestId, long timestampMs, in Argb32VideoFrame frame)
        {
            ExternalVideoTrackSourceInterop.CompleteFrameRequest((ExternalVideoTrackSourceHandle)_nativeHandle, requestId, timestampMs, frame);
        }

        /// <inheritdoc/>
        public override void Dispose()
        {
            if (_nativeHandle.IsClosed)
            {
                return;
            }

            // Unregister and release the track callbacks
            ExternalVideoTrackSourceInterop.ExternalVideoTrackSource_Shutdown((ExternalVideoTrackSourceHandle)_nativeHandle);
            Utils.ReleaseWrapperRef(_frameRequestCallbackArgsHandle);

            base.Dispose();
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"(ExternalVideoTrackSource)\"{Name}\"";
        }
    }
}
