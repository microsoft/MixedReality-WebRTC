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
        public RemoteAudioTrack RemoteTrack
        {
            get { return _remoteTrack; }
            set { SetRemoteTrack(value); }
        }

        /// <summary>
        /// Handle to the native AudioTransceiver object.
        /// </summary>
        /// <remarks>
        /// In native land this is a <code>Microsoft::MixedReality::WebRTC::AudioTransceiverHandle</code>.
        /// </remarks>
        internal AudioTransceiverHandle _nativeHandle = new AudioTransceiverHandle();

        private LocalAudioTrack _localTrack = null;
        private RemoteAudioTrack _remoteTrack = null;
        private IntPtr _argsRef = IntPtr.Zero;

        // Constructor for interop-based creation; SetHandle() will be called later
        internal AudioTransceiver(PeerConnection peerConnection, int mlineIndex, string name, Direction initialDesiredDirection)
            : base(MediaKind.Audio, peerConnection, mlineIndex, name)
        {
            _desiredDirection = initialDesiredDirection;
        }

        internal void SetHandle(AudioTransceiverHandle handle)
        {
            Debug.Assert(!handle.IsClosed);
            // Either first-time assign or no-op (assign same value again)
            Debug.Assert(_nativeHandle.IsInvalid || (_nativeHandle == handle));
            if (_nativeHandle != handle)
            {
                _nativeHandle = handle;
                AudioTransceiverInterop.RegisterCallbacks(this, out _argsRef);
            }
        }

        /// <summary>
        /// Change the media flowing direction of the transceiver.
        /// This triggers a renegotiation needed event to synchronize with the remote peer.
        /// </summary>
        /// <param name="newDirection">The new flowing direction.</param>
        public override void SetDirection(Direction newDirection)
        {
            if (newDirection == _desiredDirection)
            {
                return;
            }
            var res = AudioTransceiverInterop.AudioTransceiver_SetDirection(_nativeHandle, newDirection);
            Utils.ThrowOnErrorCode(res);
            _desiredDirection = newDirection;
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
                var res = AudioTransceiverInterop.AudioTransceiver_SetLocalTrack(_nativeHandle, track._nativeHandle);
                Utils.ThrowOnErrorCode(res);
            }
            else
            {
                // Note: Cannot pass null for SafeHandle parameter value (ArgumentNullException)
                var res = AudioTransceiverInterop.AudioTransceiver_SetLocalTrack(_nativeHandle, new LocalAudioTrackHandle());
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

            //// Update direction
            //switch (_desiredDirection)
            //{
            //case Direction.Inactive:
            //case Direction.ReceiveOnly:
            //    if (_localTrack != null)
            //    {
            //            // Add send bit
            //            _desiredDirection |= Direction.SendOnly;
            //    }
            //    break;
            //case Direction.SendOnly:
            //case Direction.SendReceive:
            //    if (_localTrack == null)
            //    {
            //            // Remove send bit
            //            _desiredDirection &= Direction.ReceiveOnly;
            //    }
            //    break;
            //}
        }

        /// <summary>
        /// Change the remote audio track receiving data from the remote peer.
        /// This detach the previous remote audio track if any, and attach the new one instead.
        /// </summary>
        /// <param name="track">The new remote audio track receiving data from the remote peer.</param>
        public void SetRemoteTrack(RemoteAudioTrack track)
        {
            throw new NotImplementedException(); //< TODO
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
            Debug.Assert(_remoteTrack == null);
            _remoteTrack = track;
            PeerConnection.OnRemoteTrackAdded(track);
        }

        internal void OnRemoteTrackRemoved(RemoteAudioTrack track)
        {
            Debug.Assert(_remoteTrack == track);
            _remoteTrack = null;
            PeerConnection.OnRemoteTrackRemoved(track);
        }

        internal void OnStateUpdated(Direction? negotiatedDirection, Direction desiredDirection)
        {
            // Desync generally happens only on first update
            _desiredDirection = desiredDirection;

            if (negotiatedDirection != NegotiatedDirection)
            {
                bool hadSendBefore = HasSend(NegotiatedDirection);
                bool hasSendNow = HasSend(negotiatedDirection);
                bool hadRecvBefore = HasRecv(NegotiatedDirection);
                bool hasRecvNow = HasRecv(negotiatedDirection);

                NegotiatedDirection = negotiatedDirection;

                if (hadSendBefore != hasSendNow)
                {
                    LocalTrack?.OnMute(!hasSendNow);
                }
                if (hadRecvBefore != hasRecvNow)
                {
                    RemoteTrack?.OnMute(!hasRecvNow);
                }
            }
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"(VideoTransceiver)\"{Name}\"";
        }
    }
}
