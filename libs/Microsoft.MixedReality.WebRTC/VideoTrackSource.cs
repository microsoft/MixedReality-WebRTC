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
    public abstract class VideoTrackSource : IDisposable
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

        /// <summary>
        /// Event raised when a video frame has been produced by the source. Handlers must process the
        /// frame as fast as possible without blocking the caller thread, and cannot remove themselves
        /// from the event nor add other handlers to the event, otherwise the caller thread will deadlock.
        /// The event delivers to the handlers an I420-encoded video frame.
        /// </summary>
        public event I420AVideoFrameDelegate VideoFrameReady
        {
            add
            {
                lock (_videoFrameReadyLock)
                {
                    bool isFirstHandler = (_videoFrameReady == null);
                    _videoFrameReady += value;
                    if (isFirstHandler)
                    {
                        RegisterVideoFrameCallback();
                    }
                }
            }
            remove
            {
                lock (_videoFrameReadyLock)
                {
                    _videoFrameReady -= value;
                    bool isLastHandler = (_videoFrameReady == null);
                    if (isLastHandler)
                    {
                        UnregisterVideoFrameCallback();
                    }
                }
            }
        }

        /// <summary>
        /// Event raised when a video frame has been produced by the source. Handlers must process the
        /// frame as fast as possible without blocking the caller thread, and cannot remove themselves
        /// from the event nor add other handlers to the event, otherwise the caller thread will deadlock.
        /// The event delivers to the handlers an ARGB32-encoded video frame.
        /// </summary>
        public event Argb32VideoFrameDelegate ARGB32VideoFrameReady
        {
            // TODO - Remove ARGB callbacks, use I420 callbacks only and expose some conversion
            // utility to convert from ARGB to I420 when needed (to be called by the user).
            add
            {
                lock (_videoFrameReadyLock)
                {
                    bool isFirstHandler = (_argb32videoFrameReady == null);
                    _argb32videoFrameReady += value;
                    if (isFirstHandler)
                    {
                        RegisterArgb32VideoFrameCallback();
                    }
                }
            }
            remove
            {
                lock (_videoFrameReadyLock)
                {
                    _argb32videoFrameReady -= value;
                    bool isLastHandler = (_argb32videoFrameReady == null);
                    if (isLastHandler)
                    {
                        UnregisterArgb32VideoFrameCallback();
                    }
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

        private readonly object _videoFrameReadyLock = new object();
        private event I420AVideoFrameDelegate _videoFrameReady;
        private event Argb32VideoFrameDelegate _argb32videoFrameReady;

        internal VideoTrackSource()
        {
        }

        internal VideoTrackSource(VideoTrackSourceHandle nativeHandle)
        {
            Debug.Assert(!nativeHandle.IsClosed);
            _nativeHandle = nativeHandle;
        }

        internal void SetHandle(VideoTrackSourceHandle nativeHandle)
        {
            Debug.Assert(_nativeHandle == null);
            _nativeHandle = nativeHandle;
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

        private void RegisterArgb32VideoFrameCallback()
        {
            _interopCallbackArgs = new VideoTrackSourceInterop.InteropCallbackArgs()
            {
                Source = this,
                Argb32FrameCallback = VideoTrackSourceInterop.Argb32FrameCallback,
            };
            _selfHandle = Utils.MakeWrapperRef(this);
            VideoTrackSourceInterop.VideoTrackSource_RegisterArgb32FrameCallback(
                _nativeHandle, _interopCallbackArgs.Argb32FrameCallback, _selfHandle);
        }

        private void UnregisterArgb32VideoFrameCallback()
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
            lock (_videoFrameReadyLock)
            {
                _videoFrameReady?.Invoke(frame);
            }
        }

        internal void OnArgb32FrameReady(Argb32VideoFrame frame)
        {
            MainEventSource.Log.Argb32LocalVideoFrameReady(frame.width, frame.height);
            lock (_videoFrameReadyLock)
            {
                _argb32videoFrameReady?.Invoke(frame);
            }
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"(VideoTrackSource)\"{Name}\"";
        }
    }
}
