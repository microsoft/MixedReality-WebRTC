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
    /// Settings for adding a local audio track backed by a local audio capture device (e.g. microphone).
    /// </summary>
    public class LocalAudioTrackInitConfig
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
    /// Audio track sending to the remote peer audio frames originating from
    /// a local track source (local microphone or other audio recording device).
    /// </summary>
    public class LocalAudioTrack : LocalMediaTrack, IAudioSource
    {
        /// <summary>
        /// Enabled status of the track. If enabled, send local audio frames to the remote peer as
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
                return (LocalAudioTrackInterop.LocalAudioTrack_IsEnabled(_nativeHandle) != 0);
            }
            set
            {
                uint res = LocalAudioTrackInterop.LocalAudioTrack_SetEnabled(_nativeHandle, value ? -1 : 0);
                Utils.ThrowOnErrorCode(res);
            }
        }

        /// <summary>
        /// Audio track source this track is pulling its audio frames from.
        /// </summary>
        public AudioTrackSource Source { get; private set; } = null;

        /// <inheritdoc/>
        public event AudioFrameDelegate AudioFrameReady;

        /// <summary>
        /// Handle to the native LocalAudioTrack object.
        /// </summary>
        /// <remarks>
        /// In native land this is a <code>Microsoft::MixedReality::WebRTC::LocalAudioTrackHandle</code>.
        /// </remarks>
        internal LocalAudioTrackHandle _nativeHandle { get; private set; } = new LocalAudioTrackHandle();

        /// <summary>
        /// Handle to self for interop callbacks. This adds a reference to the current object, preventing
        /// it from being garbage-collected.
        /// </summary>
        private IntPtr _selfHandle = IntPtr.Zero;

        /// <summary>
        /// Callback arguments to ensure delegates registered with the native layer don't go out of scope.
        /// </summary>
        private LocalAudioTrackInterop.InteropCallbackArgs _interopCallbackArgs;

        /// <summary>
        /// Create an audio track from an existing audio track source.
        ///
        /// This does not add the track to any peer connection. Instead, the track must be added manually to
        /// an audio transceiver to be attached to a peer connection and transmitted to a remote peer.
        /// </summary>
        /// <param name="source">The track source which provides the raw audio frames to the newly created track.</param>
        /// <param name="initConfig">Configuration to initialize the track being created.</param>
        /// <returns>Asynchronous task completed once the track is created.</returns>
        public static LocalAudioTrack CreateFromSource(AudioTrackSource source, LocalAudioTrackInitConfig initConfig)
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
            var config = new LocalAudioTrackInterop.TrackInitConfig
            {
                TrackName = trackName
            };

            // Create interop wrappers
            var track = new LocalAudioTrack(trackName);

            // Create native implementation objects
            uint res = LocalAudioTrackInterop.LocalAudioTrack_CreateFromSource(in config,
                source._nativeHandle, out LocalAudioTrackHandle trackHandle);
            Utils.ThrowOnErrorCode(res);

            // Finish creating the track, and bind it to the source
            track.FinishCreate(trackHandle, source);

            return track;
        }

        // Constructor for interop-based creation; FinishCreate() will be called later.
        // Constructor for standalone track not associated to a peer connection.
        internal LocalAudioTrack(string trackName)
            : base(null, trackName)
        {
            Transceiver = null;
        }

        // Constructor for interop-based creation; FinishCreate() will be called later.
        // Constructor for a track associated with a peer connection.
        internal LocalAudioTrack(PeerConnection peer, Transceiver transceiver, string trackName)
            : base(peer, trackName)
        {
            Debug.Assert(transceiver.MediaKind == MediaKind.Audio);
            Debug.Assert(transceiver.LocalAudioTrack == null);
            Transceiver = transceiver;
            transceiver.LocalAudioTrack = this;
        }

        internal void FinishCreate(LocalAudioTrackHandle handle, AudioTrackSource source)
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
            _interopCallbackArgs = new LocalAudioTrackInterop.InteropCallbackArgs()
            {
                Track = this,
                FrameCallback = LocalAudioTrackInterop.FrameCallback,
            };
            _selfHandle = Utils.MakeWrapperRef(this);
            LocalAudioTrackInterop.LocalAudioTrack_RegisterFrameCallback(
                _nativeHandle, _interopCallbackArgs.FrameCallback, _selfHandle);
        }

        /// <inheritdoc/>
        public override void Dispose()
        {
            if (_nativeHandle.IsClosed)
            {
                return;
            }

            // Remove the track from the peer connection, if any
            if (Transceiver != null)
            {
                Debug.Assert(PeerConnection != null);
                Debug.Assert(Transceiver.LocalTrack == this);
                Transceiver.LocalAudioTrack = null;
            }
            Debug.Assert(PeerConnection == null);
            Debug.Assert(Transceiver == null);

            // Notify the source
            if (Source != null)
            {
                Source.OnTrackRemovedFromSource(this);
                Source = null;
            }

            // Unregister interop callbacks
            if (_selfHandle != IntPtr.Zero)
            {
                LocalAudioTrackInterop.LocalAudioTrack_RegisterFrameCallback(_nativeHandle, null, IntPtr.Zero);
                Utils.ReleaseWrapperRef(_selfHandle);
                _selfHandle = IntPtr.Zero;
                _interopCallbackArgs = null;
            }

            // Destroy the native object. This may be delayed if a P/Invoke callback is underway,
            // but will be handled at some point anyway, even if the managed instance is gone.
            _nativeHandle.Dispose();
        }

        internal void OnFrameReady(AudioFrame frame)
        {
            MainEventSource.Log.LocalAudioFrameReady(frame.bitsPerSample, frame.sampleRate, frame.channelCount, frame.sampleCount);
            AudioFrameReady?.Invoke(frame);
        }

        internal override void OnMute(bool muted)
        {

        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"(LocalAudioTrack)\"{Name}\"";
        }

        /// <inheritdoc/>
        public AudioTrackReadBuffer CreateReadBuffer()
        {
            // FIXME implement, or remove IAudioTrack from base
            throw new NotImplementedException();
        }
    }
}
