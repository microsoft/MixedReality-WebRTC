// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.MixedReality.WebRTC.Interop;
using System;
using System.Diagnostics;

namespace Microsoft.MixedReality.WebRTC
{
    /// <summary>
    /// Transceiver for video tracks.
    /// </summary>
    public class VideoTransceiver : Transceiver
    {
        /// <summary>
        /// Local video track associated with the transceiver and sending some video to the remote peer.
        /// This may be <c>null</c> if the transceiver is currently only receiving, or is inactive.
        /// </summary>
        public LocalVideoTrack LocalTrack
        {
            get { return _localTrack; }
            set { SetLocalTrack(value); }
        }

        /// <summary>
        /// Remote video track associated with the transceiver and receiving some video from the remote peer.
        /// This property is updated when the transceiver negotiates a new media direction. If the new direction
        /// allows receiving some video, a new <see cref="RemoteVideoTrack"/> is created and assigned to the
        /// property. Otherwise the property is cleared to <c>null</c>.
        /// </summary>
        public RemoteVideoTrack RemoteTrack { get; private set; } = null;

        /// <summary>
        /// Backing field for <see cref="LocalTrack"/>.
        /// </summary>
        private LocalVideoTrack _localTrack = null;

        internal VideoTransceiver(IntPtr handle, PeerConnection peerConnection, int mlineIndex, string name, Direction initialDesiredDirection)
            : base(handle, MediaKind.Video, peerConnection, mlineIndex, name, initialDesiredDirection)
        {
        }

        /// <summary>
        /// Change the local video track sending data to the remote peer.
        /// 
        /// This detaches the previous local video track if any, and attaches the new one instead.
        /// Note that the transceiver will only send some video data to the remote peer if its
        /// negotiated direction includes sending some data and it has an attached local track to
        /// produce this data.
        /// 
        /// This change is transparent to the session, and does not trigger any renegotiation.
        /// </summary>
        /// <param name="track">The new local video track attached to the transceiver, and used to
        /// produce video data to send to the remote peer if the transceiver is sending.
        /// Passing <c>null</c> is allowed, and will detach the current track if any.</param>
        public void SetLocalTrack(LocalVideoTrack track)
        {
            if (track == _localTrack)
            {
                return;
            }

            if (track != null)
            {
                if ((track.PeerConnection != null) && (track.PeerConnection != PeerConnection))
                {
                    throw new InvalidOperationException($"Cannot set track {track} of peer connection {track.PeerConnection} on video transceiver {this} of different peer connection {PeerConnection}.");
                }
                var res = TransceiverInterop.Transceiver_SetLocalVideoTrack(_nativeHandle, track._nativeHandle);
                Utils.ThrowOnErrorCode(res);
            }
            else
            {
                // Note: Cannot pass null for SafeHandle parameter value (ArgumentNullException)
                var res = TransceiverInterop.Transceiver_SetLocalVideoTrack(_nativeHandle, new LocalVideoTrackHandle());
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

        internal void OnLocalTrackAdded(LocalVideoTrack track)
        {
            Debug.Assert(_localTrack == null);
            _localTrack = track;
            PeerConnection.OnLocalTrackAdded(track);
        }

        internal void OnLocalTrackRemoved(LocalVideoTrack track)
        {
            Debug.Assert(_localTrack == track);
            _localTrack = null;
            PeerConnection.OnLocalTrackRemoved(track);
        }

        internal void OnRemoteTrackAdded(RemoteVideoTrack track)
        {
            Debug.Assert(RemoteTrack == null);
            RemoteTrack = track;
            PeerConnection.OnRemoteTrackAdded(track);
        }

        internal void OnRemoteTrackRemoved(RemoteVideoTrack track)
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
