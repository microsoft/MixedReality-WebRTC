// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.MixedReality.WebRTC.Interop;
using Microsoft.MixedReality.WebRTC.Tracing;

namespace Microsoft.MixedReality.WebRTC
{
    /// <summary>
    /// Video track sending to the remote peer video frames originating from
    /// a local track source.
    /// </summary>
    public class LocalVideoTrack : WrapperBase
    {
        /// <summary>
        /// Peer connection this video track is added to, if any.
        /// This is <c>null</c> after the track has been removed from the peer connection.
        /// </summary>
        public PeerConnection PeerConnection { get; private set; }

        /// <summary>
        /// Track name as specified during creation. This property is immutable.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Enabled status of the track. If enabled, send local video frames to the remote peer as
        /// expected. If disabled, send only black frames instead.
        /// </summary>
        /// <remarks>
        /// Reading the value of this property after the track has been disposed is valid, and returns
        /// <c>false</c>. Writing to this property after the track has been disposed throws an exception.
        /// </remarks>
        public bool Enabled
        {
            get
            {
                return (LocalVideoTrackInterop.LocalVideoTrack_IsEnabled(_nativeHandle) != 0);
            }
            set
            {
                uint res = LocalVideoTrackInterop.LocalVideoTrack_SetEnabled(_nativeHandle, value ? -1 : 0);
                Utils.ThrowOnErrorCode(res);
            }
        }

        /// <summary>
        /// Event that occurs when a video frame has been produced by the underlying source and is available.
        /// </summary>
        public event I420VideoFrameDelegate I420VideoFrameReady;

        /// <summary>
        /// Event that occurs when a video frame has been produced by the underlying source and is available.
        /// </summary>
        public event ARGBVideoFrameDelegate ARGBVideoFrameReady;

        /// <summary>
        /// Handle to native peer connection C++ object the native track is added to, if any.
        /// </summary>
        protected IntPtr _nativePeerHandle = IntPtr.Zero;

        /// <summary>
        /// Handle to self for interop callbacks. This adds a reference to the current object, preventing
        /// it from being garbage-collected.
        /// </summary>
        private IntPtr _selfHandle = IntPtr.Zero;

        /// <summary>
        /// Callback arguments to ensure delegates registered with the native layer don't go out of scope.
        /// </summary>
        private LocalVideoTrackInterop.InteropCallbackArgs _interopCallbackArgs;

        internal LocalVideoTrack(PeerConnection peer, IntPtr nativePeerHandle, IntPtr nativeHandle, string trackName)
            : base(nativeHandle)
        {
            PeerConnection = peer;
            PeerConnectionInterop.PeerConnection_AddRef(nativePeerHandle);
            _nativePeerHandle = nativePeerHandle;
            Name = trackName;
            RegisterInteropCallbacks();
        }

        private void RegisterInteropCallbacks()
        {
            _interopCallbackArgs = new LocalVideoTrackInterop.InteropCallbackArgs()
            {
                Track = this,
                I420FrameCallback = LocalVideoTrackInterop.I420FrameCallback,
                ARGBFrameCallback = LocalVideoTrackInterop.ARGBFrameCallback,
            };
            _selfHandle = Utils.MakeWrapperRef(this);
            LocalVideoTrackInterop.LocalVideoTrack_RegisterI420FrameCallback(
                _nativeHandle, _interopCallbackArgs.I420FrameCallback, _selfHandle);
            LocalVideoTrackInterop.LocalVideoTrack_RegisterARGBFrameCallback(
                _nativeHandle, _interopCallbackArgs.ARGBFrameCallback, _selfHandle);
        }

        #region IDisposable support

        /// <inheritdoc/>
        protected override void AddRef()
        {
            LocalVideoTrackInterop.LocalVideoTrack_AddRef(_nativeHandle);
        }

        /// <inheritdoc/>
        protected override void RemoveRef()
        {
            LocalVideoTrackInterop.LocalVideoTrack_RemoveRef(_nativeHandle);
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (_nativeHandle != IntPtr.Zero)
            {
                // Unregister the callbacks
                if (_selfHandle != IntPtr.Zero)
                {
                    LocalVideoTrackInterop.LocalVideoTrack_RegisterI420FrameCallback(_nativeHandle, null, IntPtr.Zero);
                    LocalVideoTrackInterop.LocalVideoTrack_RegisterARGBFrameCallback(_nativeHandle, null, IntPtr.Zero);
                    GCHandle.FromIntPtr(_selfHandle).Free();
                    if (disposing)
                    {
                        _interopCallbackArgs = null;
                    }
                    _selfHandle = IntPtr.Zero;
                }

                // Remove the track from the peer connection, and release the reference
                // to the peer connection.
                if (_nativePeerHandle != IntPtr.Zero)
                {
                    PeerConnectionInterop.PeerConnection_RemoveLocalVideoTrack(_nativePeerHandle, _nativeHandle);
                    PeerConnectionInterop.PeerConnection_RemoveRef(_nativePeerHandle);
                    _nativePeerHandle = IntPtr.Zero;
                }
            }

            if (disposing)
            {
                PeerConnection = null;
            }

            base.Dispose(disposing);
        }

        #endregion

        internal void OnI420FrameReady(I420AVideoFrame frame)
        {
            MainEventSource.Log.I420LocalVideoFrameReady(frame.width, frame.height);
            I420VideoFrameReady?.Invoke(frame);
        }

        internal void OnARGBFrameReady(ARGBVideoFrame frame)
        {
            MainEventSource.Log.Argb32LocalVideoFrameReady(frame.width, frame.height);
            ARGBVideoFrameReady?.Invoke(frame);
        }

        internal void OnTrackRemoved(PeerConnection previousConnection)
        {
            Debug.Assert(PeerConnection == previousConnection);
            Debug.Assert(_nativeHandle != IntPtr.Zero);

            if (_nativePeerHandle != IntPtr.Zero)
            {
                PeerConnectionInterop.PeerConnection_RemoveLocalVideoTrack(_nativePeerHandle, _nativeHandle);
                PeerConnectionInterop.PeerConnection_RemoveRef(_nativePeerHandle);
                _nativePeerHandle = IntPtr.Zero;
            }

            PeerConnection = null;
        }
    }
}
