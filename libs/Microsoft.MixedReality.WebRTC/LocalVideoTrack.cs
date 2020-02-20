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
    /// Kind of video profile. This corresponds to the <see xref="Windows.Media.Capture.KnownVideoProfile"/>
    /// enum of the <see xref="Windows.Media.Capture.MediaCapture"/> API.
    /// </summary>
    /// <seealso href="https://docs.microsoft.com/en-us/uwp/api/windows.media.capture.knownvideoprofile"/>
    public enum VideoProfileKind : int
    {
        /// <summary>
        /// Unspecified video profile kind. Used to remove any constraint on the video profile kind.
        /// </summary>
        Unspecified,

        /// <summary>
        /// Video profile for video recording, often of higher quality and framerate at the expense
        /// of power consumption and latency.
        /// </summary>
        VideoRecording,

        /// <summary>
        /// Video profile for high quality photo capture.
        /// </summary>
        HighQualityPhoto,

        /// <summary>
        /// Balanced video profile to capture both videos and photos.
        /// </summary>
        BalancedVideoAndPhoto,

        /// <summary>
        /// Video profile for video conferencing, often of lower power consumption
        /// and lower latency by deprioritizing higher resolutions.
        /// This is the recommended profile for most WebRTC applications, if supported.
        /// </summary>
        VideoConferencing,

        /// <summary>
        /// Video profile for capturing a sequence of photos.
        /// </summary>
        PhotoSequence,

        /// <summary>
        /// Video profile containing high framerate capture formats.
        /// </summary>
        HighFrameRate,

        /// <summary>
        /// Video profile for capturing a variable sequence of photos.
        /// </summary>
        VariablePhotoSequence,

        /// <summary>
        /// Video profile for capturing videos with High Dynamic Range (HDR) and Wide Color Gamut (WCG).
        /// </summary>
        HdrWithWcgVideo,

        /// <summary>
        /// Video profile for capturing photos with High Dynamic Range (HDR) and Wide Color Gamut (WCG).
        /// </summary>
        HdrWithWcgPhoto,

        /// <summary>
        /// Video profile for capturing videos with High Dynamic Range (HDR).
        /// </summary>
        VideoHdr8,
    };

    /// <summary>
    /// Video track sending to the remote peer video frames originating from
    /// a local track source.
    /// </summary>
    public class LocalVideoTrack : IDisposable
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
        /// External source for this video track, or <c>null</c> if the source is
        /// some internal video capture device.
        /// </summary>
        public ExternalVideoTrackSource Source { get; } = null;

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
        public event I420AVideoFrameDelegate I420AVideoFrameReady;

        /// <summary>
        /// Event that occurs when a video frame has been produced by the underlying source and is available.
        /// </summary>
        public event Argb32VideoFrameDelegate Argb32VideoFrameReady;

        /// <summary>
        /// Handle to the native LocalVideoTrack object.
        /// </summary>
        /// <remarks>
        /// In native land this is a <code>Microsoft::MixedReality::WebRTC::LocalVideoTrackHandle</code>.
        /// </remarks>
        internal LocalVideoTrackHandle _nativeHandle { get; private set; } = new LocalVideoTrackHandle();

        /// <summary>
        /// Handle to self for interop callbacks. This adds a reference to the current object, preventing
        /// it from being garbage-collected.
        /// </summary>
        private IntPtr _selfHandle = IntPtr.Zero;

        /// <summary>
        /// Callback arguments to ensure delegates registered with the native layer don't go out of scope.
        /// </summary>
        private LocalVideoTrackInterop.InteropCallbackArgs _interopCallbackArgs;

        internal LocalVideoTrack(LocalVideoTrackHandle nativeHandle, PeerConnection peer, string trackName, ExternalVideoTrackSource source = null)
        {
            _nativeHandle = nativeHandle;
            PeerConnection = peer;
            Name = trackName;
            Source = source;
            RegisterInteropCallbacks();
        }

        private void RegisterInteropCallbacks()
        {
            _interopCallbackArgs = new LocalVideoTrackInterop.InteropCallbackArgs()
            {
                Track = this,
                I420AFrameCallback = LocalVideoTrackInterop.I420AFrameCallback,
                Argb32FrameCallback = LocalVideoTrackInterop.Argb32FrameCallback,
            };
            _selfHandle = Utils.MakeWrapperRef(this);
            LocalVideoTrackInterop.LocalVideoTrack_RegisterI420AFrameCallback(
                _nativeHandle, _interopCallbackArgs.I420AFrameCallback, _selfHandle);
            LocalVideoTrackInterop.LocalVideoTrack_RegisterArgb32FrameCallback(
                _nativeHandle, _interopCallbackArgs.Argb32FrameCallback, _selfHandle);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_nativeHandle.IsClosed)
            {
                return;
            }

            // Remove the track from the peer connection, if any
            PeerConnection?.RemoveLocalVideoTrack(this);
            Debug.Assert(PeerConnection == null); // see OnTrackRemoved

            // Unregister interop callbacks
            if (_selfHandle != IntPtr.Zero)
            {
                LocalVideoTrackInterop.LocalVideoTrack_RegisterI420AFrameCallback(_nativeHandle, null, IntPtr.Zero);
                LocalVideoTrackInterop.LocalVideoTrack_RegisterArgb32FrameCallback(_nativeHandle, null, IntPtr.Zero);
                Utils.ReleaseWrapperRef(_selfHandle);
                _selfHandle = IntPtr.Zero;
                _interopCallbackArgs = null;
            }

            // Currently there is a 1:1 mapping between track and source, so the track owns its
            // source and must dipose of it.
            Source?.Dispose();

            // Destroy the native object. This may be delayed if a P/Invoke callback is underway,
            // but will be handled at some point anyway, even if the managed instance is gone.
            _nativeHandle.Dispose();
        }

        internal void OnI420AFrameReady(I420AVideoFrame frame)
        {
            MainEventSource.Log.I420ALocalVideoFrameReady(frame.width, frame.height);
            I420AVideoFrameReady?.Invoke(frame);
        }

        internal void OnArgb32FrameReady(Argb32VideoFrame frame)
        {
            MainEventSource.Log.Argb32LocalVideoFrameReady(frame.width, frame.height);
            Argb32VideoFrameReady?.Invoke(frame);
        }

        internal void OnTrackRemoved(PeerConnection previousConnection)
        {
            Debug.Assert(PeerConnection == previousConnection);
            Debug.Assert(!_nativeHandle.IsClosed);
            PeerConnection = null;
        }
    }
}
