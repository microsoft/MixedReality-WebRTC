// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.MixedReality.WebRTC.Interop;

namespace Microsoft.MixedReality.WebRTC
{
    /// <summary>
    /// Audio source for WebRTC audio tracks.
    ///
    /// The audio source is not bound to any peer connection, and can therefore be shared by multiple audio
    /// tracks from different peer connections. This is especially useful to share local audio capture devices
    /// (microphones) amongst multiple peer connections when building a multi-peer experience with a mesh topology
    /// (one connection per pair of peers).
    ///
    /// The user owns the audio track source, and is in charge of keeping it alive until after all tracks using it
    /// are destroyed, and then dispose of it. The behavior of disposing of the track source while a track is still
    /// using it is undefined. The <see cref="Tracks"/> property contains the list of tracks currently using the
    /// source.
    /// </summary>
    /// <seealso cref="LocalAudioTrack"/>
    public abstract class AudioTrackSource : IDisposable
    {
        /// <summary>
        /// A name for the audio track source, used for logging and debugging.
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
        /// List of local audio tracks this source is providing raw audio frames to.
        /// </summary>
        public IReadOnlyList<LocalAudioTrack> Tracks => _tracks;

        /// <summary>
        /// Handle to the native AudioTrackSource object.
        /// </summary>
        /// <remarks>
        /// In native land this is a <code>Microsoft::MixedReality::WebRTC::AudioTrackSourceHandle</code>.
        /// </remarks>
        internal AudioTrackSourceHandle _nativeHandle { get; private set; } = new AudioTrackSourceHandle();

        /// <summary>
        /// Backing field for <see cref="Name"/>, and cache for the native name.
        /// Since the name can only be set by the user, this cached value is always up-to-date with the
        /// internal name of the native object, by design.
        /// </summary>
        private string _name = string.Empty;

        /// <summary>
        /// Backing field for <see cref="Tracks"/>.
        /// </summary>
        private List<LocalAudioTrack> _tracks = new List<LocalAudioTrack>();

        internal AudioTrackSource(AudioTrackSourceHandle nativeHandle)
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
                throw new InvalidOperationException($"Trying to dispose of AudioTrackSource '{Name}' while still in use by one or more audio tracks.");
            }

            // Unregister from tracks
            // TODO...
            //AudioTrackSourceInterop.AudioTrackSource_Shutdown(_nativeHandle);

            // Destroy the native object. This may be delayed if a P/Invoke callback is underway,
            // but will be handled at some point anyway, even if the managed instance is gone.
            _nativeHandle.Dispose();
        }

        /// <summary>
        /// Internal callback when a track starts using this source.
        /// </summary>
        /// <param name="track">The track using this source.</param>
        internal void OnTrackAddedToSource(LocalAudioTrack track)
        {
            Debug.Assert(!_nativeHandle.IsClosed);
            Debug.Assert(!_tracks.Contains(track));
            _tracks.Add(track);
        }

        /// <summary>
        /// Internal callback when a track stops using this source.
        /// </summary>
        /// <param name="track">The track not using this source anymore.</param>
        internal void OnTrackRemovedFromSource(LocalAudioTrack track)
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
        internal void OnTracksRemovedFromSource(IEnumerable<LocalAudioTrack> tracks)
        {
            Debug.Assert(!_nativeHandle.IsClosed);
            var remainingTracks = new List<LocalAudioTrack>();
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

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"(AudioTrackSource)\"{Name}\"";
        }
    }
}
