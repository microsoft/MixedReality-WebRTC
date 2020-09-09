// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.MixedReality.WebRTC.Interop;
using Microsoft.MixedReality.WebRTC.Tracing;

namespace Microsoft.MixedReality.WebRTC
{
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
    /// <seealso cref="DeviceVideoTrackSource"/>
    /// <seealso cref="ExternalVideoTrackSource"/>
    /// <seealso cref="LocalVideoTrack"/>
    public abstract class VideoTrackSource : IVideoSource, IDisposable, VideoTrackSourceInterop.IVideoSource
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
                ObjectInterop.Object_SetName(_nativeHandle, value);
                _name = value;
            }
        }

        /// <summary>
        /// List of local video tracks this source is providing raw video frames to.
        /// </summary>
        public IReadOnlyList<LocalVideoTrack> Tracks => _tracks;

        /// <inheritdoc/>
        public abstract VideoEncoding FrameEncoding { get; }

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
                    VideoTrackSourceInterop.VideoTrackSource_RegisterFrameCallback(
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
                    VideoTrackSourceInterop.VideoTrackSource_RegisterFrameCallback(
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
                    VideoTrackSourceInterop.VideoTrackSource_RegisterArgb32FrameCallback(
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
                    VideoTrackSourceInterop.VideoTrackSource_RegisterArgb32FrameCallback(
                        _nativeHandle, null, IntPtr.Zero);
                }
            }
        }

        /// <summary>
        /// Handle to the native VideoTrackSource object.
        /// </summary>
        /// <remarks>
        /// In native land this is a <code>mrsVideoTrackSourceHandle</code>.
        /// </remarks>
        internal VideoTrackSourceHandle _nativeHandle { get; private set; } = null;

        /// <summary>
        /// Enabled status of the source. True until the object is disposed.
        /// </summary>
        public bool Enabled => !_nativeHandle.IsClosed;

        /// <summary>
        /// Handle to self for interop callbacks. This adds a reference to the current object, preventing
        /// it from being garbage-collected.
        /// </summary>
        private IntPtr _selfHandle = IntPtr.Zero;

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

        private readonly object _videoFrameReadyLock = new object();
        private event I420AVideoFrameDelegate _videoFrameReady;
        private event Argb32VideoFrameDelegate _argb32VideoFrameReady;

        // In some cases (e.g. external source) we need to create an object before we have a native handle.
        // If this ctor is used, SetHandle must be called too before the source is used.
        internal VideoTrackSource()
        {
        }

        internal VideoTrackSource(VideoTrackSourceHandle nativeHandle)
        {
            SetHandle(nativeHandle);
        }

        internal void SetHandle(VideoTrackSourceHandle nativeHandle)
        {
            Debug.Assert(_nativeHandle == null);
            Debug.Assert(!nativeHandle.IsClosed);
            _nativeHandle = nativeHandle;
            // Note that this prevents the object from being garbage-collected until it is disposed.
            _selfHandle = Utils.MakeWrapperRef(this);
        }

        /// <inheritdoc/>
        public virtual void Dispose()
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

            // Unregister interop callbacks
            _videoFrameReady = null;
            _argb32VideoFrameReady = null;

            // Unregister from tracks
            // TODO...
            //VideoTrackSourceInterop.VideoTrackSource_Shutdown(_nativeHandle);

            // Destroy the native object. This may be delayed if a P/Invoke callback is underway,
            // but will be handled at some point anyway, even if the managed instance is gone.
            _nativeHandle.Dispose();

            // _selfHandle is used by the FrameReady callbacks. Only release it once the native
            // source is disposed and the callbacks are not called anymore.
            Utils.ReleaseWrapperRef(_selfHandle);
            _selfHandle = IntPtr.Zero;
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

        void VideoTrackSourceInterop.IVideoSource.OnI420AFrameReady(I420AVideoFrame frame)
        {
            MainEventSource.Log.I420ALocalVideoFrameReady(frame.width, frame.height);
            _videoFrameReady?.Invoke(frame);
        }

        void VideoTrackSourceInterop.IVideoSource.OnArgb32FrameReady(Argb32VideoFrame frame)
        {
            MainEventSource.Log.Argb32LocalVideoFrameReady(frame.width, frame.height);
            _argb32VideoFrameReady?.Invoke(frame);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"(VideoTrackSource)\"{Name}\"";
        }
    }
}
