// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using UnityEngine;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// Media line abstraction for a peer connection.
    ///
    /// This container binds together a source component (<see cref="MediaTrackSource"/>) and/or a receiver
    /// component (<see cref="MediaReceiver"/>) on one side, with a transceiver on the other side. The media line
    /// is a declarative representation of this association, which is then turned into a binding by the implementation
    /// during an SDP negotiation. This forms the core of the algorithm allowing automatic transceiver pairing
    /// between the two peers based on the declaration of intent of the user.
    ///
    /// Assigning Unity components to the <see cref="Source"/> and <see cref="Receiver"/> properties serves
    /// as an indication of the user intent to send and/or receive media through the transceiver, and is
    /// used during the SDP exchange to derive the <see xref="WebRTC.Transceiver.Direction"/> to negotiate.
    /// After the SDP negotiation is completed, the <see cref="Transceiver"/> property refers to the transceiver
    /// associated with this media line, and which the sender and receiver will use.
    ///
    /// Users typically interact with this class through the peer connection transceiver collection in the Unity
    /// inspector window, though direct manipulation via code is also possible.
    /// </summary>
    [Serializable]
    public class MediaLine
    {
        /// <summary>
        /// Kind of media of the media line and its attached transceiver.
        ///
        /// This is assiged when the media line is created with <see cref="PeerConnection.AddMediaLine(MediaKind)"/>
        /// and is immutable for the lifetime of the peer connection.
        /// </summary>
        public MediaKind MediaKind => _mediaKind;

        /// <summary>
        /// Media source producing the media to send through the transceiver attached to this media line.
        /// </summary>
        /// <remarks>
        /// This must be an instance of a class derived from <see cref="AudioTrackSource"/> or <see cref="VideoTrackSource"/>
        /// depending on whether <see cref="MediaKind"/> is <see xref="Microsoft.MixedReality.WebRTC.MediaKind.Audio"/>
        /// or <see xref="Microsoft.MixedReality.WebRTC.MediaKind.Video"/>, respectively.
        ///
        /// Internally the peer connection will automatically create and manage a media track to bridge the
        /// media source with the transceiver.
        ///
        /// If this is non-<c>null</c> then the peer connection will negotiate sending some media, otherwise
        /// it will signal the remote peer that it does not wish to send (receive-only or inactive).
        ///
        /// If <see cref="Transceiver"/> is valid, that is a first session negotiation has already been completed,
        /// then changing this value raises a <see xref="WebRTC.PeerConnection.RenegotiationNeeded"/> event on the
        /// peer connection of <see cref="Transceiver"/>.
        ///
        /// Must be changed on the main Unity app thread.
        /// </remarks>
        public MediaTrackSource Source
        {
            get { return _source; }
            set
            {
                if (_source == value)
                {
                    return;
                }
                if (value != null && value.MediaKind != MediaKind)
                {
                    throw new ArgumentException("Wrong media kind", nameof(Receiver));
                }

                var oldTrack = LocalTrack;
                if (_source != null && _peer.IsAwake)
                {
                    _source.OnRemovedFromMediaLine(this);
                }
                _source = value;
                if (_source != null && _peer.IsAwake)
                {
                    _source.OnAddedToMediaLine(this);
                    CreateLocalTrackIfNeeded();
                }
                // Dispose the old track *after* replacing it with the new one
                // so that there is no gap in sending.
                oldTrack?.Dispose();

                // Whatever the change, keep the direction consistent.
                UpdateTransceiverDesiredDirection();
            }
        }

        /// <summary>
        /// Name of the local media track this component will create when calling <see cref="StartCaptureAsync"/>.
        /// If left empty, the implementation will generate a unique name for the track (generally a GUID).
        /// </summary>
        /// <remarks>
        /// This value must comply with the 'msid' attribute rules as defined in
        /// https://tools.ietf.org/html/draft-ietf-mmusic-msid-05#section-2, which in
        /// particular constraints the set of allowed characters to those allowed for a
        /// 'token' element as specified in https://tools.ietf.org/html/rfc4566#page-43:
        /// - Symbols [!#$%'*+-.^_`{|}~] and ampersand &amp;
        /// - Alphanumerical characters [A-Za-z0-9]
        ///
        /// Users can manually test if a string is a valid SDP token with the utility method
        /// <see cref="SdpTokenAttribute.Validate(string, bool)"/>. The property setter will
        /// use this and throw an <see cref="ArgumentException"/> if the token is not a valid
        /// SDP token.
        ///
        /// The sender track name is taken into account each time the track is created. If this
        /// property is assigned after the track was created (already negotiated), the value will
        /// be used only for the next negotiation, and the current sender track will keep its
        /// current track name (either a previous value or a generated one).
        /// </remarks>
        /// <seealso cref="SdpTokenAttribute.Validate(string, bool)"/>
        public string SenderTrackName
        {
            get { return _senderTrackName; }
            set
            {
                SdpTokenAttribute.Validate(_senderTrackName);
                _senderTrackName = value;
            }
        }

        /// <summary>
        /// Local track created from a local source.
        /// </summary>
        /// <remarks>
        /// This is non-<c>null</c> when a live source is attached to the <see cref="MediaLine"/>, and the owning
        /// <see cref="PeerConnection"/> is connected.
        /// </remarks>
        public LocalMediaTrack LocalTrack => Transceiver?.LocalTrack;

        /// <summary>
        /// Media receiver consuming the media received through the transceiver attached to this media line.
        /// </summary>
        /// <remarks>
        /// This must be an instance of a class derived from <see cref="AudioReceiver"/> or <see cref="VideoReceiver"/>
        /// depending on whether <see cref="MediaKind"/> is <see xref="Microsoft.MixedReality.WebRTC.MediaKind.Audio"/>
        /// or <see xref="Microsoft.MixedReality.WebRTC.MediaKind.Video"/>, respectively.
        ///
        /// If this is non-<c>null</c> then the peer connection will negotiate receiving some media, otherwise
        /// it will signal the remote peer that it does not wish to receive (send-only or inactive).
        ///
        /// If <see cref="Transceiver"/> is valid, that is a first session negotiation has already been conducted,
        /// then changing this value raises a <see xref="WebRTC.PeerConnection.RenegotiationNeeded"/> event on the
        /// peer connection of <see cref="Transceiver"/>.
        ///
        /// Must be changed on the main Unity app thread.
        /// </remarks>
        public MediaReceiver Receiver
        {
            get { return _receiver; }
            set
            {
                if (_receiver == value)
                {
                    return;
                }
                if (value != null && value.MediaKind != MediaKind)
                {
                    throw new ArgumentException("Wrong media kind", nameof(Receiver));
                }

                if (_receiver != null && _peer.IsAwake)
                {
                    if (_remoteTrack != null)
                    {
                        _receiver.OnUnpaired(_remoteTrack);
                    }
                    _receiver.OnRemovedFromMediaLine(this);
                }
                _receiver = value;
                if (_receiver != null && _peer.IsAwake)
                {
                    _receiver.OnAddedToMediaLine(this);
                    if (_remoteTrack != null)
                    {
                        _receiver.OnPaired(_remoteTrack);
                    }
                }

                // Whatever the change, keep the direction consistent.
                UpdateTransceiverDesiredDirection();
            }
        }

        /// <summary>
        /// Transceiver attached with this media line.
        ///
        /// On the offering peer this changes during <see cref="PeerConnection.StartConnection"/>, while this is updated by
        /// <see cref="PeerConnection.HandleConnectionMessageAsync(string, string)"/> when receiving an offer on the answering peer.
        ///
        /// Because transceivers cannot be destroyed, once this property is assigned a non-<c>null</c> value it keeps that
        /// value until the peer connection owning the media line is closed.
        /// </summary>
        public Transceiver Transceiver { get; private set; }

        /// <summary>
        /// <see cref="PeerConnection"/> owning this <see cref="MediaLine"/>.
        /// </summary>
        public PeerConnection Peer
        {
            get => _peer;
            internal set
            {
                Debug.Assert(Peer == null || Peer == value);
                _peer = value;
            }
        }

        #region Private fields
        private PeerConnection _peer;

        /// <summary>
        /// Backing field to serialize the <see cref="MediaKind"/> property.
        /// </summary>
        /// <seealso cref="MediaKind"/>
        [SerializeField]
        private MediaKind _mediaKind;

        /// <summary>
        /// Backing field to serialize the <see cref="Source"/> property.
        /// </summary>
        /// <seealso cref="Source"/>
        [SerializeField]
        private MediaTrackSource _source;

        /// <summary>
        /// Backing field to serialize the <see cref="Receiver"/> property.
        /// </summary>
        /// <seealso cref="Receiver"/>
        [SerializeField]
        private MediaReceiver _receiver;

        /// <summary>
        /// Backing field to serialize the sender track's name.
        /// </summary>
        [SerializeField]
        [Tooltip("SDP track name")]
        [SdpToken(allowEmpty: true)]
        private string _senderTrackName;

        // Cache for the remote track opened by the latest negotiation.
        // Comparing it to Transceiver.RemoteTrack will tell if streaming has just started/stopped.
        private MediaTrack _remoteTrack;

        #endregion


        /// <summary>
        /// Constructor called internally by <see cref="PeerConnection.AddMediaLine(MediaKind)"/>.
        /// </summary>
        /// <param name="kind">Immutable value assigned to the <see cref="MediaKind"/> property on construction.</param>
        internal MediaLine(PeerConnection peer, MediaKind kind)
        {
            Peer = peer;
            _mediaKind = kind;
        }

        private void UpdateTransceiverDesiredDirection()
        {
            if (Transceiver != null)
            {
                // Avoid races on the desired direction by limiting changes to the main thread.
                // Note that EnsureIsMainAppThread cannot be used if _peer is not awake, so only
                // check when there is a transceiver (meaning _peer is enabled).
                Peer.EnsureIsMainAppThread();

                bool wantsSend = _source != null && _source.IsLive;
                bool wantsRecv = (_receiver != null);
                Transceiver.DesiredDirection = Transceiver.DirectionFromSendRecv(wantsSend, wantsRecv);
            }
        }

        // Initializes and attaches a local track if all the preconditions are satisfied.
        private void CreateLocalTrackIfNeeded()
        {
            if (_source != null && _source.IsLive && Transceiver != null)
            {
                if (MediaKind == MediaKind.Audio)
                {
                    var audioSource = (AudioTrackSource)_source;

                    var initConfig = new LocalAudioTrackInitConfig
                    {
                        trackName = _senderTrackName
                    };
                    var audioTrack = LocalAudioTrack.CreateFromSource(audioSource.Source, initConfig);
                    Transceiver.LocalAudioTrack = audioTrack;
                }
                else
                {
                    Debug.Assert(MediaKind == MediaKind.Video);
                    var videoSource = (VideoTrackSource)_source;

                    var initConfig = new LocalVideoTrackInitConfig
                    {
                        trackName = _senderTrackName
                    };
                    var videoTrack = LocalVideoTrack.CreateFromSource(videoSource.Source, initConfig);
                    Transceiver.LocalVideoTrack = videoTrack;
                }
            }
        }

        // Detaches and disposes the local track if there is one.
        private void DestroyLocalTrackIfAny()
        {
            var localTrack = Transceiver?.LocalTrack;
            if (localTrack != null)
            {
                if (MediaKind == MediaKind.Audio)
                {
                    Transceiver.LocalAudioTrack = null;
                }
                else
                {
                    Debug.Assert(MediaKind == MediaKind.Video);
                    Transceiver.LocalVideoTrack = null;
                }
                localTrack.Dispose();
            }
        }

        internal void UpdateAfterSdpReceived()
        {
            Debug.Assert(Transceiver != null);

            // Callbacks must be called on the main Unity app thread.
            Peer.EnsureIsMainAppThread();

            var newRemoteTrack = Transceiver.RemoteTrack;
            if (_receiver != null)
            {
                bool wasReceiving = _remoteTrack != null;
                bool isReceiving = newRemoteTrack != null;
                if (isReceiving && !wasReceiving)
                {
                    // Transceiver started receiving, and user actually wants to receive
                    _receiver.OnPaired(newRemoteTrack);
                }
                else if (!isReceiving && wasReceiving)
                {
                    // Transceiver stopped receiving (user intent does not matter here)
                    _receiver.OnUnpaired(_remoteTrack);
                }
            }
            _remoteTrack = newRemoteTrack;
        }

        /// <summary>
        /// Pair the given transceiver with the current media line.
        /// </summary>
        /// <param name="tr">The transceiver to pair with.</param>
        /// <exception cref="InvalidTransceiverMediaKindException">
        /// The transceiver associated in the offer with the same media line index as the current media line
        /// has a different media kind than the media line. This is generally a result of the two peers having
        /// mismatching media line configurations.
        /// </exception>
        internal void PairTransceiver(Transceiver tr)
        {
            Peer.EnsureIsMainAppThread();

            Debug.Assert(tr != null);
            Debug.Assert(Transceiver == null);

            // Check consistency before assigning
            if (tr.MediaKind != MediaKind)
            {
                throw new InvalidTransceiverMediaKindException();
            }
            Transceiver = tr;

            // Initialize the transceiver direction in sync with Sender and Receiver.
            UpdateTransceiverDesiredDirection();

            // Start the local track if there is a live source.
            CreateLocalTrackIfNeeded();
        }

        internal void UnpairTransceiver()
        {
            Peer.EnsureIsMainAppThread();

            // Notify the receiver.
            if (_remoteTrack != null && _receiver != null)
            {
                _receiver.OnUnpaired(_remoteTrack);
            }
            _remoteTrack = null;

            DestroyLocalTrackIfAny();

            Transceiver = null;
        }

        /// <summary>
        /// Internal callback when the underlying source providing media frames to the sender track
        /// is created, and therefore the local media track needs to be created too.
        /// </summary>
        /// <seealso cref="AudioTrackSource.AttachSource(WebRTC.AudioTrackSource)"/>
        /// <seealso cref="VideoTrackSource.AttachSource(WebRTC.VideoTrackSource)"/>
        internal void AttachSource()
        {
            Debug.Assert(Source.IsLive);
            CreateLocalTrackIfNeeded();
            UpdateTransceiverDesiredDirection();
        }

        /// <summary>
        /// Internal callback when the underlying source providing media frames to the sender track
        /// is destroyed, and therefore the local media track needs to be destroyed too.
        /// </summary>
        /// <seealso cref="AudioTrackSource.DisposeSource"/>
        /// <seealso cref="VideoTrackSource.DisposeSource"/>
        internal void DetachSource()
        {
            Debug.Assert(Source.IsLive);
            DestroyLocalTrackIfAny();
            UpdateTransceiverDesiredDirection();
        }

        internal void OnReceiverDestroyed()
        {
            // Different from `Receiver = null`. Don't need to call Receiver.OnRemovedFromMediaLine
            // or Receiver.OnUnpaired since the Receiver itself has called this.
            _receiver = null;
            UpdateTransceiverDesiredDirection();
        }

        // Called by PeerConnection.Awake.
        internal void Awake()
        {
            if (_source)
            {
                // Fill the list of media lines for the source.
                _source.OnAddedToMediaLine(this);
            }
            if (_receiver)
            {
                _receiver.OnAddedToMediaLine(this);
            }
        }

        // Called by PeerConnection.OnDestroy.
        internal void OnDestroy()
        {
            if (_source)
            {
                // Fill the list of media lines for the source.
                _source.OnRemovedFromMediaLine(this);
            }
            if (_receiver)
            {
                _receiver.OnRemovedFromMediaLine(this);
            }
        }
    }
}
