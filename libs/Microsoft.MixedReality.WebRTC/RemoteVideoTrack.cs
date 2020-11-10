// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using Microsoft.MixedReality.WebRTC.Interop;
using Microsoft.MixedReality.WebRTC.Tracing;

namespace Microsoft.MixedReality.WebRTC
{
    /// <summary>
    /// Video track receiving video frames from the remote peer.
    /// </summary>
    public class RemoteVideoTrack : MediaTrack, IVideoSource, VideoTrackSourceInterop.IVideoSource
    {
        /// <summary>
        /// Enabled status of the track. If enabled, receives video frames from the remote peer as
        /// expected. If disabled, receives only black frames instead.
        /// </summary>
        /// <remarks>
        /// Reading the value of this property after the track has been disposed is valid, and returns
        /// <c>false</c>.
        /// The remote video track enabled status is controlled by the remote peer only.
        /// </remarks>
        public bool Enabled
        {
            get
            {
                return (RemoteVideoTrackInterop.RemoteVideoTrack_IsEnabled(_nativeHandle) != 0);
            }
        }

        /// <inheritdoc/>
        public VideoEncoding FrameEncoding => VideoEncoding.I420A;

        /// <inheritdoc/>
        public event I420AVideoFrameDelegate I420AVideoFrameReady
        {
            add
            {
                bool isFirstHandler;
                lock (_videoFrameReadyLock)
                {
                    isFirstHandler = (_videoFrameReady == null);
                    _videoFrameReady += value;
                }
                // Do out of lock since this dispatches to the worker thread.
                if (isFirstHandler)
                {
                    RemoteVideoTrackInterop.RemoteVideoTrack_RegisterI420AFrameCallback(
                        _nativeHandle, VideoTrackSourceInterop.I420AFrameCallback, _selfHandle);
                }
            }
            remove
            {
                bool isLastHandler;
                lock (_videoFrameReadyLock)
                {
                    _videoFrameReady -= value;
                    isLastHandler = (_videoFrameReady == null);
                }
                // Do out of lock since this dispatches to the worker thread.
                if (isLastHandler)
                {
                    RemoteVideoTrackInterop.RemoteVideoTrack_RegisterI420AFrameCallback(
                        _nativeHandle, null, IntPtr.Zero);
                }
            }
        }

        /// <inheritdoc/>
        public event Argb32VideoFrameDelegate Argb32VideoFrameReady
        {
            // TODO - Remove ARGB callbacks, use I420 callbacks only and expose some conversion
            // utility to convert from ARGB to I420 when needed (to be called by the user).
            add
            {
                bool isFirstHandler;
                lock (_videoFrameReadyLock)
                {
                    isFirstHandler = (_argb32VideoFrameReady == null);
                    _argb32VideoFrameReady += value;
                }
                // Do out of lock since this dispatches to the worker thread.
                if (isFirstHandler)
                {
                    RemoteVideoTrackInterop.RemoteVideoTrack_RegisterArgb32FrameCallback(
                        _nativeHandle, VideoTrackSourceInterop.Argb32FrameCallback, _selfHandle);
                }
            }
            remove
            {
                bool isLastHandler;
                lock (_videoFrameReadyLock)
                {
                    _argb32VideoFrameReady -= value;
                    isLastHandler = (_argb32VideoFrameReady == null);
                }
                // Do out of lock since this dispatches to the worker thread.
                if (isLastHandler)
                {
                    RemoteVideoTrackInterop.RemoteVideoTrack_RegisterArgb32FrameCallback(
                        _nativeHandle, null, IntPtr.Zero);
                }
            }
        }

        /// <summary>
        /// Handle to the native RemoteVideoTrack object.
        /// </summary>
        /// <remarks>
        /// In native land this is a <code>Microsoft::MixedReality::WebRTC::RemoteVideoTrackHandle</code>.
        /// </remarks>
        internal RemoteVideoTrackInterop.RemoteVideoTrackHandle _nativeHandle = null;

        /// <summary>
        /// Handle to self for interop callbacks. This adds a reference to the current object, preventing
        /// it from being garbage-collected.
        /// </summary>
        private IntPtr _selfHandle = IntPtr.Zero;

        // Frame internal event handlers.
        private readonly object _videoFrameReadyLock = new object();
        private event I420AVideoFrameDelegate _videoFrameReady;
        private event Argb32VideoFrameDelegate _argb32VideoFrameReady;

        /// <summary>
        /// Retrieves an unsafe handle to video track. Currently for internal use by the Unity NativeVideoRenderer component.
        /// </summary>
        public IntPtr NativeHandle => _nativeHandle.DangerousGetHandle();

        // Constructor for interop-based creation; SetHandle() will be called later
        internal RemoteVideoTrack(RemoteVideoTrackInterop.RemoteVideoTrackHandle handle, PeerConnection peer, string trackName)
            : base(peer, trackName)
        {
            Debug.Assert(!handle.IsClosed);
            _nativeHandle = handle;
            // Note that this prevents the object from being garbage-collected until it is disposed.
            _selfHandle = Utils.MakeWrapperRef(this);
        }

        /// <summary>
        /// Dispose of the native track. Invoked by its owner (<see cref="PeerConnection"/>).
        /// </summary>
        internal void DestroyNative()
        {
            if (_nativeHandle.IsClosed)
            {
                return;
            }

            Debug.Assert(PeerConnection == null); // see OnTrackRemoved

            // Unregister interop callbacks
            _videoFrameReady = null;
            _argb32VideoFrameReady = null;
            Utils.ReleaseWrapperRef(_selfHandle);
            _selfHandle = IntPtr.Zero;

            _nativeHandle.Dispose();
        }

        void VideoTrackSourceInterop.IVideoSource.OnI420AFrameReady(I420AVideoFrame frame)
        {
            MainEventSource.Log.I420ARemoteVideoFrameReady(frame.width, frame.height);
            _videoFrameReady?.Invoke(frame);
        }

        void VideoTrackSourceInterop.IVideoSource.OnArgb32FrameReady(Argb32VideoFrame frame)
        {
            MainEventSource.Log.Argb32RemoteVideoFrameReady(frame.width, frame.height);
            _argb32VideoFrameReady?.Invoke(frame);
        }

        internal override void OnMute(bool muted)
        {

        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"(RemoteVideoTrack)\"{Name}\"";
        }
    }
}
