// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.MixedReality.WebRTC.Interop;
using System;
using System.Diagnostics;

namespace Microsoft.MixedReality.WebRTC
{
    /// <summary>
    /// Transceiver for audio tracks.
    /// </summary>
    /// <remarks>
    /// Note that unlike most other objects in this library, transceivers are not disposable,
    /// and are always alive after being added to a peer connection until that peer connection
    /// is disposed of. See <see cref="Transceiver"/> for details.
    /// </remarks>
    public class AudioTransceiver : Transceiver
    {
        /// <summary>
        /// Local audio track associated with the transceiver and sending some audio to the remote peer.
        /// This may be <c>null</c> if the transceiver is currently only receiving, or is inactive.
        /// </summary>
        public LocalAudioTrack LocalTrack
        {
            get { return _localTrack; }
            set { SetLocalTrack(value); }
        }

        /// <summary>
        /// Remote audio track associated with the transceiver and receiving some audio from the remote peer.
        /// This may be <c>null</c> if the transceiver is currently only sending, or is inactive.
        /// </summary>
        public RemoteAudioTrack RemoteTrack { get; private set; } = null;

        private LocalAudioTrack _localTrack = null;

        // Constructor for interop-based creation; SetHandle() will be called later
        internal AudioTransceiver(PeerConnection peerConnection, int mlineIndex, string name, Direction initialDesiredDirection)
            : base(MediaKind.Audio, peerConnection, mlineIndex, name)
        {
            _desiredDirection = initialDesiredDirection;
        }

        /// <summary>
        /// Change the local audio track sending data to the remote peer.
        /// This detach the previous local audio track if any, and attach the new one instead.
        /// </summary>
        /// <param name="track">The new local audio track sending data to the remote peer.</param>
        public void SetLocalTrack(LocalAudioTrack track)
        {
            if (track == _localTrack)
            {
                return;
            }

            if (track != null)
            {
                if ((track.PeerConnection != null) && (track.PeerConnection != PeerConnection))
                {
                    throw new InvalidOperationException($"Cannot set track {track} of peer connection {track.PeerConnection} on audio transceiver {this} of different peer connection {PeerConnection}.");
                }
                var res = TransceiverInterop.Transceiver_SetLocalAudioTrack(_nativeHandle, track._nativeHandle);
                Utils.ThrowOnErrorCode(res);
            }
            else
            {
                // Note: Cannot pass null for SafeHandle parameter value (ArgumentNullException)
                var res = TransceiverInterop.Transceiver_SetLocalAudioTrack(_nativeHandle, new LocalAudioTrackHandle());
                Utils.ThrowOnErrorCode(res);
            }

            // Capture peer connection; it gets reset during track manipulation below
            var peerConnection = PeerConnection;

            // Remove old track
            if (_localTrack != null)
            {
                _localTrack.OnTrackRemoved(peerConnection);
            }
            Debug.Assert(_localTrack == null);

            // Add new track
            if (track != null)
            {
                track.OnTrackAdded(peerConnection, this);
                Debug.Assert(track == _localTrack);
                Debug.Assert(_localTrack.PeerConnection == PeerConnection);
                Debug.Assert(_localTrack.Transceiver == this);
                Debug.Assert(_localTrack.Transceiver.LocalTrack == _localTrack);
            }
        }

        /// <inheritdoc/>
        protected override void OnLocalTrackMuteChanged(bool muted)
        {
            LocalTrack?.OnMute(muted);
        }

        /// <inheritdoc/>
        protected override void OnRemoteTrackMuteChanged(bool muted)
        {
            RemoteTrack?.OnMute(muted);
        }

        internal void OnLocalTrackAdded(LocalAudioTrack track)
        {
            Debug.Assert(_localTrack == null);
            _localTrack = track;
            PeerConnection.OnLocalTrackAdded(track);
        }

        internal void OnLocalTrackRemoved(LocalAudioTrack track)
        {
            Debug.Assert(_localTrack == track);
            _localTrack = null;
            PeerConnection.OnLocalTrackRemoved(track);
        }

        internal void OnRemoteTrackAdded(RemoteAudioTrack track)
        {
            Debug.Assert(RemoteTrack == null);
            RemoteTrack = track;
            PeerConnection.OnRemoteTrackAdded(track);
        }

        internal void OnRemoteTrackRemoved(RemoteAudioTrack track)
        {
            Debug.Assert(RemoteTrack == track);
            RemoteTrack = null;
            PeerConnection.OnRemoteTrackRemoved(track);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"(VideoTransceiver)\"{Name}\"";
        }
    }
}
