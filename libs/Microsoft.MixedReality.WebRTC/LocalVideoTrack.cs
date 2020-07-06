// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
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
    /// Settings for creating a new local video track.
    /// </summary>
    public class LocalVideoTrackInitConfig
    {
        /// <summary>
        /// Name of the track to create, as used for the SDP negotiation.
        /// This name needs to comply with the requirements of an SDP token, as described in the SDP RFC
        /// https://tools.ietf.org/html/rfc4566#page-43. In particular the name cannot contain spaces nor
        /// double quotes <code>"</code>.
        /// The track name can optionally be empty, in which case the implementation will create a valid
        /// random track name.
        /// </summary>
        public string trackName = string.Empty;
    }

    /// <summary>
    /// Video track sending to the remote peer video frames originating from
    /// a local track source.
    /// </summary>
    public class LocalVideoTrack : LocalMediaTrack, IVideoTrack
    {
        /// <summary>
        /// Video track source this track is pulling its video frames from.
        /// </summary>
        public VideoTrackSource Source { get; private set; } = null;

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

        /// <summary>
        /// Create a video track from an existing video track source.
        ///
        /// This does not add the track to any peer connection. Instead, the track must be added manually to
        /// a video transceiver to be attached to a peer connection and transmitted to a remote peer.
        /// </summary>
        /// <param name="source">The track source which provides the raw video frames to the newly created track.</param>
        /// <param name="initConfig">Configuration to initialize the track being created.</param>
        /// <returns>Asynchronous task completed once the track is created.</returns>
        public static LocalVideoTrack CreateFromSource(VideoTrackSource source, LocalVideoTrackInitConfig initConfig)
        {
            if (source == null)
            {
                throw new ArgumentNullException();
            }

            // Parse and marshal the settings
            string trackName = initConfig?.trackName;
            if (string.IsNullOrEmpty(trackName))
            {
                trackName = Guid.NewGuid().ToString();
            }
            var config = new LocalVideoTrackInterop.TrackInitConfig
            {
                TrackName = trackName
            };

            // Create interop wrappers
            var track = new LocalVideoTrack(trackName);

            // Create native implementation objects
            uint res = LocalVideoTrackInterop.LocalVideoTrack_CreateFromSource(in config,
                source._nativeHandle, out LocalVideoTrackHandle trackHandle);
            Utils.ThrowOnErrorCode(res);

            // Finish creating the track, and bind it to the source
            track.FinishCreate(trackHandle, source);

            return track;
        }

        // Constructor for interop-based creation; SetHandle() will be called later.
        internal LocalVideoTrack(string trackName) : base(null, trackName)
        {
            Transceiver = null;
        }

        internal void FinishCreate(LocalVideoTrackHandle handle, VideoTrackSource source)
        {
            Debug.Assert(!handle.IsClosed);
            // Either first-time assign or no-op (assign same value again)
            Debug.Assert(_nativeHandle.IsInvalid || (_nativeHandle == handle));
            if (_nativeHandle != handle)
            {
                _nativeHandle = handle;
                RegisterInteropCallbacks();
            }

            Debug.Assert(source != null);
            Source = source;
            source.OnTrackAddedToSource(this);
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
        public override void Dispose()
        {
            if (_nativeHandle.IsClosed)
            {
                return;
            }

            // Notify the source
            if (Source != null)
            {
                Source.OnTrackRemovedFromSource(this);
                Source = null;
            }

            // Remove the track from the peer connection, if any
            if (Transceiver != null)
            {
                Debug.Assert(PeerConnection != null);
                Debug.Assert(Transceiver.LocalTrack == this);
                Transceiver.LocalVideoTrack = null;
            }
            Debug.Assert(PeerConnection == null);
            Debug.Assert(Transceiver == null);

            // Unregister interop callbacks
            if (_selfHandle != IntPtr.Zero)
            {
                LocalVideoTrackInterop.LocalVideoTrack_RegisterI420AFrameCallback(_nativeHandle, null, IntPtr.Zero);
                LocalVideoTrackInterop.LocalVideoTrack_RegisterArgb32FrameCallback(_nativeHandle, null, IntPtr.Zero);
                Utils.ReleaseWrapperRef(_selfHandle);
                _selfHandle = IntPtr.Zero;
                _interopCallbackArgs = null;
            }

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

        internal override void OnMute(bool muted)
        {

        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"(LocalVideoTrack)\"{Name}\"";
        }
    }
}
