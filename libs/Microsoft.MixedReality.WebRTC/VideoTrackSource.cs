// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.MixedReality.WebRTC.Interop;
using Microsoft.MixedReality.WebRTC.Tracing;

namespace Microsoft.MixedReality.WebRTC
{
    /// <summary>
    /// Configuration to initialize capture on a local video device (webcam).
    /// </summary>
    public class LocalVideoDeviceInitConfig
    {
        /// <summary>
        /// Optional video capture device to use for capture.
        /// Use the default device if not specified.
        /// </summary>
        public VideoCaptureDevice videoDevice = default;

        /// <summary>
        /// Optional unique identifier of the video profile to use for capture,
        /// if the device supports video profiles, as retrieved by one of:
        /// - <see xref="MediaCapture.FindAllVideoProfiles"/>
        /// - <see xref="MediaCapture.FindKnownVideoProfiles"/>
        /// This requires <see cref="videoDevice"/> to be specified.
        /// </summary>
        public string videoProfileId = string.Empty;

        /// <summary>
        /// Optional video profile kind to restrict the list of video profiles to consider.
        /// Note that this is not exclusive with <see cref="videoProfileId"/>, although in
        /// practice it is recommended to specify only one or the other.
        /// This requires <see cref="videoDevice"/> to be specified.
        /// </summary>
        public VideoProfileKind videoProfileKind = VideoProfileKind.Unspecified;

        /// <summary>
        /// Enable Mixed Reality Capture (MRC) on devices supporting the feature.
        /// This setting is silently ignored on device not supporting MRC.
        /// </summary>
        /// <remarks>
        /// This is only supported on UWP.
        /// </remarks>
        public bool enableMrc = true;

        /// <summary>
        /// Display the on-screen recording indicator while MRC is enabled.
        /// This setting is silently ignored on device not supporting MRC, or if
        /// <see cref="enableMrc"/> is set to <c>false</c>.
        /// </summary>
        /// <remarks>
        /// This is only supported on UWP.
        /// </remarks>
        public bool enableMrcRecordingIndicator = true;

        /// <summary>
        /// Optional capture resolution width, in pixels.
        /// This must be a resolution width the device supports.
        /// </summary>
        public uint? width;

        /// <summary>
        /// Optional capture resolution height, in pixels.
        /// This must be a resolution width the device supports.
        /// </summary>
        public uint? height;

        /// <summary>
        /// Optional capture frame rate, in frames per second (FPS).
        /// This must be a capture framerate the device supports.
        /// </summary>
        /// <remarks>
        /// This is compared by strict equality, so is best left unspecified or to an exact value
        /// retrieved by <see cref="PeerConnection.GetVideoCaptureFormatsAsync"/>.
        /// </remarks>
        public double? framerate;
    }

    /// <summary>
    /// Video source for WebRTC video tracks.
    /// 
    /// The video source is not bound to any peer connection, and can therefore be shared by multiple video
    /// tracks from different peer connections. This is especially useful to share local video capture devices
    /// (microphones) amongst multiple peer connections when building a multi-peer experience with a mesh topology
    /// (one connection per pair of peers).
    /// 
    /// The user owns the video track source, and is in charge of keeping it alive until after all tracks using it
    /// are destroyed, and then dispose of it. The behavior of disposing of the track source while a track is still
    /// using it is undefined. The <see cref="Tracks"/> property contains the list of tracks currently using the
    /// source.
    /// </summary>
    /// <seealso cref="LocalVideoTrack"/>
    public class VideoTrackSource : IDisposable
    {
        /// <summary>
        /// A name for the video track source, used for logging and debugging.
        /// </summary>
        public string Name
        {
            get
            {
                // Note: the name cannot change internally, so no need to query the native layer.
                // This avoids a round-trip to native and some string encoding conversion.
                return _name;
            }
            set
            {
                VideoTrackSourceInterop.VideoTrackSource_SetName(_nativeHandle, value);
                _name = value;
            }
        }

        /// <summary>
        /// List of local video tracks this source is providing raw video frames to.
        /// </summary>
        public IReadOnlyList<LocalVideoTrack> Tracks => _tracks;

        /// <summary>
        /// Event that occurs when a video frame has been produced by the source.
        /// </summary>
        public event I420AVideoFrameDelegate VideoFrameReady
        {
            add
            {
                bool isFirstHandler = (_videoFrameReady == null);
                _videoFrameReady += value;
                if (isFirstHandler)
                {
                    RegisterVideoFrameCallback();
                }
            }
            remove
            {
                // FIXME - ideally unregister first so no need to check for NULL event in handler
                _videoFrameReady -= value;
                bool isLastHandler = (_videoFrameReady == null);
                if (isLastHandler)
                {
                    UnregisterVideoFrameCallback();
                }
            }
        }

        /// <summary>
        /// Handle to the native VideoTrackSource object.
        /// </summary>
        /// <remarks>
        /// In native land this is a <code>Microsoft::MixedReality::WebRTC::VideoTrackSourceHandle</code>.
        /// </remarks>
        internal VideoTrackSourceHandle _nativeHandle { get; private set; } = new VideoTrackSourceHandle();

        /// <summary>
        /// Handle to self for interop callbacks. This adds a reference to the current object, preventing
        /// it from being garbage-collected.
        /// </summary>
        private IntPtr _selfHandle = IntPtr.Zero;

        /// <summary>
        /// Callback arguments to ensure delegates registered with the native layer don't go out of scope.
        /// </summary>
        private VideoTrackSourceInterop.InteropCallbackArgs _interopCallbackArgs;

        /// <summary>
        /// Backing field for <see cref="Name"/>, and cache for the native name.
        /// Since the name can only be set by the user, this cached value is always up-to-date with the
        /// internal name of the native object, by design.
        /// </summary>
        private string _name = string.Empty;

        /// <summary>
        /// Backing field for <see cref="Tracks"/>.
        /// </summary>
        private List<LocalVideoTrack> _tracks = new List<LocalVideoTrack>();

        private event I420AVideoFrameDelegate _videoFrameReady;

        /// <summary>
        /// Create a video track source using a local video capture device (webcam).
        /// 
        /// The video track source produces raw video frames by capturing them from a capture device accessible
        /// from the local host machine, generally a USB webcam or built-in device camera. The video source
        /// initially starts in the capturing state, and will remain live for as long as the source is alive.
        /// Once the source is not live anymore (ended), it cannot be restarted. A new source must be created to
        /// use the same video capture device again.
        /// 
        /// The source can be used to create one or more local video tracks (<see cref="LocalVideoTrack"/>), which
        /// once added to a video transceiver allow the video frames to be sent to a remote peer. The source itself
        /// is not associated with any peer connection, and can be used to create local video tracks from multiple
        /// peer connections at once, thereby being shared amongst those peer connections.
        /// 
        /// The source is owned by the user, who must ensure it stays alive while being in use by at least one local
        /// video track. Once it is not used anymore, the user is in charge of disposing of the source. Disposing of
        /// a source still in use by a local video track is undefined behavior.
        /// </summary>
        /// <param name="initConfig">Optional configuration to initialize the video capture on the device.</param>
        /// <returns>The newly create video track source.</returns>
        /// <remarks>
        /// On UWP this requires the "webcam" capability.
        /// See <see href="https://docs.microsoft.com/en-us/windows/uwp/packaging/app-capability-declarations"/>
        /// for more details.
        /// 
        /// The video capture device may be accessed several times during the initializing process,
        /// generally once for listing and validating the capture format, and once for actually starting
        /// the video capture. This is a limitation of the OS and/or hardware.
        /// 
        /// Note that the capture device must support a capture format with the given constraints of profile
        /// ID or kind, capture resolution, and framerate, otherwise the call will fail. That is, there is no
        /// fallback mechanism selecting a closest match. Developers should use
        /// <see cref="PeerConnection.GetVideoCaptureFormatsAsync(string)"/> to list the supported formats ahead
        /// of calling <see cref="CreateFromDeviceAsync(LocalVideoDeviceInitConfig)"/>, and can build their own
        /// fallback mechanism on top of this call if needed.
        /// </remarks>
        /// <example>
        /// Create a video track source with Mixed Reality Capture (MRC) enabled.
        /// This assumes that the platform supports MRC. Note that if MRC is not available
        /// the call will still succeed, but will return a track without MRC enabled.
        /// <code>
        /// var initConfig = new LocalVideoDeviceInitConfig
        /// {
        ///     enableMrc = true
        /// };
        /// var videoSource = await VideoTrackSource.CreateFromDeviceAsync(initConfig);
        /// </code>
        /// Create a video track source from a local webcam, asking for a capture format suited for video conferencing,
        /// and a target framerate of 30 frames per second (FPS). The implementation will select an appropriate
        /// capture resolution. This assumes that the device supports video profiles, and has at least one capture
        /// format supporting exactly 30 FPS capture associated with the VideoConferencing profile. Otherwise the call
        /// will fail.
        /// <code>
        /// var initConfig = new LocalVideoDeviceInitConfig
        /// {
        ///     videoProfileKind = VideoProfileKind.VideoConferencing,
        ///     framerate = 30.0
        /// };
        /// var videoSource = await VideoTrackSource.CreateFromDeviceAsync(initConfig);
        /// </code>
        /// </example>
        /// <seealso cref="LocalVideoTrack.CreateFromSourceAsync(VideoTrackSource, LocalVideoTrackInitConfig)"/>
        public static Task<VideoTrackSource> CreateFromDeviceAsync(LocalVideoDeviceInitConfig initConfig = null)
        {
            return Task.Run(() =>
            {
                // On UWP this cannot be called from the main UI thread, so always call it from
                // a background worker thread.

                var config = new VideoTrackSourceInterop.LocalVideoDeviceMarshalInitConfig(initConfig);
                uint ret = VideoTrackSourceInterop.VideoTrackSource_CreateFromDevice(in config, out VideoTrackSourceHandle handle);
                Utils.ThrowOnErrorCode(ret);
                return new VideoTrackSource(handle);
            });
        }

        internal VideoTrackSource(VideoTrackSourceHandle nativeHandle)
        {
            _nativeHandle = nativeHandle;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_nativeHandle.IsClosed)
            {
                return;
            }

            // TODO - Can we support destroying the source and leaving tracks with silence instead?
            if (_tracks.Count > 0)
            {
                throw new InvalidOperationException($"Trying to dispose of VideoTrackSource '{Name}' while still in use by one or more video tracks.");
            }

            // Unregister from tracks
            // TODO...
            //VideoTrackSourceInterop.VideoTrackSource_Shutdown(_nativeHandle);

            // Destroy the native object. This may be delayed if a P/Invoke callback is underway,
            // but will be handled at some point anyway, even if the managed instance is gone.
            _nativeHandle.Dispose();
        }

        private void RegisterVideoFrameCallback()
        {
            _interopCallbackArgs = new VideoTrackSourceInterop.InteropCallbackArgs()
            {
                Source = this,
                I420AFrameCallback = VideoTrackSourceInterop.I420AFrameCallback,
            };
            _selfHandle = Utils.MakeWrapperRef(this);
            VideoTrackSourceInterop.VideoTrackSource_RegisterFrameCallback(
                _nativeHandle, _interopCallbackArgs.I420AFrameCallback, _selfHandle);
        }

        private void UnregisterVideoFrameCallback()
        {
            VideoTrackSourceInterop.VideoTrackSource_RegisterFrameCallback(_nativeHandle, null, IntPtr.Zero);
            Utils.ReleaseWrapperRef(_selfHandle);
            _interopCallbackArgs = null;
        }

        /// <summary>
        /// Internal callback when a track starts using this source.
        /// </summary>
        /// <param name="track">The track using this source.</param>
        internal void OnTrackAddedToSource(LocalVideoTrack track)
        {
            Debug.Assert(!_nativeHandle.IsClosed);
            Debug.Assert(!_tracks.Contains(track));
            _tracks.Add(track);
        }

        /// <summary>
        /// Internal callback when a track stops using this source.
        /// </summary>
        /// <param name="track">The track not using this source anymore.</param>
        internal void OnTrackRemovedFromSource(LocalVideoTrack track)
        {
            Debug.Assert(!_nativeHandle.IsClosed);
            bool removed = _tracks.Remove(track);
            Debug.Assert(removed);
        }

        /// <summary>
        /// Internal callback when a list of tracks stop using this source, generally
        /// as a result of a peer connection owning said tracks being closed.
        /// </summary>
        /// <param name="tracks">The list of tracks not using this source anymore.</param>
        internal void OnTracksRemovedFromSource(IEnumerable<LocalVideoTrack> tracks)
        {
            Debug.Assert(!_nativeHandle.IsClosed);
            var remainingTracks = new List<LocalVideoTrack>();
            foreach (var track in tracks)
            {
                if (track.Source == this)
                {
                    Debug.Assert(_tracks.Contains(track));
                }
                else
                {
                    remainingTracks.Add(track);
                }
            }
            _tracks = remainingTracks;
        }

        internal void OnI420AFrameReady(I420AVideoFrame frame)
        {
            MainEventSource.Log.I420ALocalVideoFrameReady(frame.width, frame.height);
            _videoFrameReady?.Invoke(frame);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"(VideoTrackSource)\"{Name}\"";
        }
    }
}
