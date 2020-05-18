// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.MixedReality.WebRTC.Interop;

namespace Microsoft.MixedReality.WebRTC
{
    /// <summary>
    /// Configuration to initialize capture on a local audio device (microphone).
    /// </summary>
    public class LocalAudioDeviceInitConfig
    {
        /// <summary>
        /// Enable automated gain control (AGC) on the audio device capture pipeline.
        /// </summary>
        public bool? AutoGainControl = null;
    }

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
    public class AudioTrackSource : IDisposable
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
                AudioTrackSourceInterop.AudioTrackSource_SetName(_nativeHandle, value);
                _name = value;
            }
        }

        /// <summary>
        /// List of local audio tracks this source is providing raw audio frames to.
        /// </summary>
        public List<LocalAudioTrack> Tracks { get; private set; } = new List<LocalAudioTrack>();

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
        /// Create an audio track source using a local audio capture device (microphone).
        /// </summary>
        /// <param name="initConfig">Optional configuration to initialize the audio capture on the device.</param>
        /// <returns>The newly create audio track source.</returns>
        public static Task<AudioTrackSource> CreateFromDeviceAsync(LocalAudioDeviceInitConfig initConfig = null)
        {
            return Task.Run(() =>
            {
                // On UWP this cannot be called from the main UI thread, so always call it from
                // a background worker thread.

                var config = new AudioTrackSourceInterop.LocalAudioDeviceMarshalInitConfig(initConfig);
                uint ret = AudioTrackSourceInterop.AudioTrackSource_CreateFromDevice(in config, out AudioTrackSourceHandle handle);
                Utils.ThrowOnErrorCode(ret);
                return new AudioTrackSource(handle);
            });
        }

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
            if (Tracks.Count > 0)
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
            Debug.Assert(!Tracks.Contains(track));
            Tracks.Add(track);
        }

        /// <summary>
        /// Internal callback when a track stops using this source.
        /// </summary>
        /// <param name="track">The track not using this source anymore.</param>
        internal void OnTrackRemovedFromSource(LocalAudioTrack track)
        {
            Debug.Assert(!_nativeHandle.IsClosed);
            bool removed = Tracks.Remove(track);
            Debug.Assert(removed);
        }

        /// <summary>
        /// Internal callback when a list of tracks stop using this source, generally
        /// as a result of a peer connection owning said tracks being closed.
        /// </summary>
        /// <param name="tracks">The list of tracks not using this source anymore.</param>
        internal void OnTracksRemovedFromSource(List<LocalAudioTrack> tracks)
        {
            Debug.Assert(!_nativeHandle.IsClosed);
            var remainingTracks = new List<LocalAudioTrack>();
            foreach (var track in tracks)
            {
                if (track.Source == this)
                {
                    bool removed = Tracks.Remove(track);
                    Debug.Assert(removed);
                }
                else
                {
                    remainingTracks.Add(track);
                }
            }
            Tracks = remainingTracks;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"(AudioTrackSource)\"{Name}\"";
        }
    }
}
