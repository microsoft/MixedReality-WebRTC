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
        /// This property is updated when the transceiver negotiates a new media direction. If the new direction
        /// allows receiving some audio, a new <see cref="RemoteVideoTrack"/> is created and assigned to the
        /// property. Otherwise the property is cleared to <c>null</c>.
        /// </summary>
        public RemoteAudioTrack RemoteTrack { get; private set; } = null;

        /// <summary>
        /// Backing field for <see cref="LocalTrack"/>.
        /// </summary>
        private LocalAudioTrack _localTrack = null;

        internal AudioTransceiver(IntPtr handle, PeerConnection peerConnection, int mlineIndex, string name, Direction initialDesiredDirection)
            : base(handle, MediaKind.Audio, peerConnection, mlineIndex, name, initialDesiredDirection)
        {
        }

        /// <summary>
        /// Change the local audio track sending data to the remote peer.
        /// 
        /// This detaches the previous local audio track if any, and attaches the new one instead.
        /// Note that the transceiver will only send some audio data to the remote peer if its
        /// negotiated direction includes sending some data and it has an attached local track to
        /// produce this data.
        /// 
        /// This change is transparent to the session, and does not trigger any renegotiation.
        /// </summary>
        /// <param name="track">The new local audio track attached to the transceiver, and used to
        /// produce audio data to send to the remote peer if the transceiver is sending.
        /// Passing <c>null</c> is allowed, and will detach the current track if any.</param>
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
            return $"(AudioTransceiver)\"{Name}\"";
        }
    }
}
