// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using UnityEngine;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// Media line abstraction for a peer connection.
    ///
    /// This container binds together a source component (<see cref="IMediaTrackSource"/>) and/or a receiver
    /// component (<see cref="IMediaReceiver"/>) on one side, with a transceiver on the other side. The media line
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
    public class MediaLine : ISerializationCallbackReceiver
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
        /// Must be changed on the main thread.
        /// </remarks>
        public MediaTrackSource Source
        {
            get { return (_source as MediaTrackSource); }
            set
            {
                // If the connection is active, ensure that this is run on the main thread
                // and doesn't race with the signaling handlers.
                // If the connection is not awake we cannot use EnsureIsMainAppThread, but
                // there should be no race on shared state, so skip the check.
                if (_peer.isActiveAndEnabled)
                {
                    _peer.EnsureIsMainAppThread();
                }

                if (value == null)
                {
                    if (_source != null)
                    {
                        _source.OnRemovedFromMediaLine(this);
                        _source = null;
                        DestroySenderIfNeeded();
                    }
                }
                else
                {
                    if (value.MediaKind != MediaKind)
                    {
                        throw new ArgumentException("Wrong media kind", nameof(Source));
                    }
                    if (_source != value)
                    {
                        if (_source != null)
                        {
                            _source.OnRemovedFromMediaLine(this);
                        }
                        _source = value;
                        _source.OnAddedToMediaLine(this);
                        CreateSenderIfNeeded();
                    }
                }

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
        /// Sender track created from a local source when starting a negotiation.
        /// </summary>
        public MediaTrack SenderTrack
        {
            get { return _senderTrack; }
        }

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
        /// Must be changed on the main thread.
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
                if (value!= null && value.MediaKind != MediaKind)
                {
                    throw new ArgumentException("Wrong media kind", nameof(Receiver));
                }

                // If the connection is active, ensure that this is run on the main thread
                // and doesn't race with the signaling handlers.
                // If the connection is not awake we cannot use EnsureIsMainAppThread, but
                // there should be no race on shared state, so skip the check.
                if (_peer.isActiveAndEnabled)
                {
                    _peer.EnsureIsMainAppThread();
                }

                if (_receiver != null)
                {
                    if (_remoteTrack != null)
                    {
                        _receiver.OnUnpaired(_remoteTrack);
                    }
                    _receiver.OnRemovedFromMediaLine(this);
                }
                _receiver = value;
                if (_receiver != null)
                {
                    if (_remoteTrack != null)
                    {
                        _receiver.OnPaired(_remoteTrack);
                    }
                    _receiver.OnAddedToMediaLine(this);
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


        #region Private fields

        [SerializeField]
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

        /// <summary>
        /// Media track bridging the <see cref="Source"/> with the underlying <see cref="Transceiver"/>.
        ///
        /// The media track is automatically managed when <see cref="Source"/> changes or the transceiver direction
        /// is renegotiated, as needed.
        /// </summary>
        private LocalMediaTrack _senderTrack = null;

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
            _peer = peer;
            _mediaKind = kind;
        }

        private void UpdateTransceiverDesiredDirection()
        {
            if (Transceiver != null)
            {
                // Avoid races on the desired direction by limiting changes to the main thread.
                _peer.EnsureIsMainAppThread();

                bool wantsSend = (_source != null);
                bool wantsRecv = (_receiver != null);
                Transceiver.DesiredDirection = Transceiver.DirectionFromSendRecv(wantsSend, wantsRecv);
            }
        }

        internal void AttachTrack()
        {
            Debug.Assert(Transceiver != null);
            Debug.Assert(Source != null);
            Debug.Assert(Transceiver.LocalTrack != _senderTrack);
            // Note: _senderTrack is null if the source was inactive
            if (_senderTrack != null)
            {
                if (_senderTrack is LocalAudioTrack audioTrack)
                {
                    Transceiver.LocalAudioTrack = audioTrack;
                }
                else if (_senderTrack is LocalVideoTrack videoTrack)
                {
                    Transceiver.LocalVideoTrack = videoTrack;
                }
            }
        }

        internal void DetachTrack()
        {
            Debug.Assert(Transceiver != null);
            Debug.Assert(Source == null);
            Debug.Assert(Transceiver.LocalTrack == _senderTrack);
            if (_senderTrack is LocalAudioTrack audioTrack)
            {
                Debug.Assert(Transceiver.LocalAudioTrack == audioTrack);
                Transceiver.LocalAudioTrack = null;
            }
            else if (_senderTrack is LocalVideoTrack videoTrack)
            {
                Debug.Assert(Transceiver.LocalVideoTrack == videoTrack);
                Transceiver.LocalVideoTrack = null;
            }
        }

        internal void UpdateReceiverPairingIfNeeded()
        {
            Debug.Assert(Transceiver != null);

            // Callbacks must be called on the main thread.
            _peer.EnsureIsMainAppThread();

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

        internal void OnUnpaired(MediaTrack track)
        {
            Debug.Assert(track != null);
            Debug.Assert(Transceiver != null);
            Debug.Assert(Transceiver.RemoteTrack == null); // already removed
            // This is called by the TrackRemoved event, which can be fired sometimes even
            // though we did not have any opportunity yet to pair. So only unpair if we did.
            // In details, the case is the answering peer being in sendonly mode, yet created
            // automatically by the implementation during SetRemoteDescription() in recvonly
            // mode (per the WebRTC spec). So the SetDirection(sendonly) triggers the TrackRemoved
            // event, but the pairing was never done because SetDirection() is called before
            // the received is updated.

            // Callbacks must be called on the main thread.
            _peer.InvokeOnAppThread(() =>
            {
                if (_receiver != null)
                {
                    bool wasReceiving = _remoteTrack != null;
                    if (wasReceiving)
                    {
                        _receiver.OnUnpaired(_remoteTrack);
                    }
                }
                _remoteTrack = null;
            });
        }

        /// <summary>
        /// Create the local sender track from the current media track source if that source
        /// is active and enabled. Otherwise do nothing.
        /// </summary>
        private void CreateSenderIfNeeded()
        {
            // Only create a sender track if the source is active, i.e. has an underlying frame source.
            if (_senderTrack == null && _source != null && _source.isActiveAndEnabled)
            {
                if (MediaKind == MediaKind.Audio)
                {
                    var audioSource = (_source as AudioTrackSource);

                    var initConfig = new LocalAudioTrackInitConfig
                    {
                        trackName = _senderTrackName
                    };
                    _senderTrack = LocalAudioTrack.CreateFromSource(audioSource.Source, initConfig);
                }
                else
                {
                    Debug.Assert(MediaKind == MediaKind.Video);
                    var videoSource = (_source as VideoTrackSource);

                    var initConfig = new LocalVideoTrackInitConfig
                    {
                        trackName = _senderTrackName
                    };
                    _senderTrack = LocalVideoTrack.CreateFromSource(videoSource.Source, initConfig);
                }
            }
        }

        private void DestroySenderIfNeeded()
        {
            if (_senderTrack != null)
            {
                _senderTrack.Dispose();
                _senderTrack = null;
            }
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
        internal void PairTransceiverOnFirstOffer(Transceiver tr)
        {
            Debug.Assert(tr != null);
            Debug.Assert((Transceiver == null) || (Transceiver == tr));

            // Check consistency before assigning
            if (tr.MediaKind != MediaKind)
            {
                throw new InvalidTransceiverMediaKindException();
            }
            Transceiver = tr;

            // Keep the transceiver direction in sync with Sender and Receiver.
            UpdateTransceiverDesiredDirection();

            // Always do this even after first offer, because the sender and receiver
            // components can be assigned to the media line later, and therefore will
            // need to be updated on the next offer even if that is not the first one.
            CreateSenderIfNeeded();
        }

        internal void UnpairTransceiver()
        {
            DestroySenderIfNeeded();
            Transceiver = null;
        }

        /// <summary>
        /// Update the media line when about to create an SDP offer message.
        /// </summary>
        /// <param name="tr">
        /// The transceiver associated in the offer with the same media line index as the current media line.
        /// </param>
        /// <returns>A task which completes when the update is done.</returns>
        /// <exception cref="InvalidTransceiverMediaKindException">
        /// The transceiver associated in the offer with the same media line index as the current media line
        /// has a different media kind than the media line. This is generally a result of the two peers having
        /// mismatching media line configurations.
        /// </exception>
        internal void UpdateForCreateOffer(Transceiver tr)
        {
            Debug.Assert(tr != null);

            PairTransceiverOnFirstOffer(tr);

            // Create the local sender track and attach it to the transceiver
            bool wantsSend = (Source != null);
            bool wasSending = (Transceiver.LocalTrack == _senderTrack);
            if (wantsSend && !wasSending)
            {
                AttachTrack();
            }
            else if (!wantsSend && wasSending)
            {
                DetachTrack();
            }

            // The remote track is only created when applying a description, so only attach
            // the transceiver for now (above) but do not try to pair remote tracks.
        }

        /// <summary>
        /// Update the media line when receiving an SDP offer message from the remote peer.
        /// </summary>
        /// <param name="tr">
        /// The transceiver associated in the offer with the same media line index as the current media line.
        /// </param>
        /// <returns>A task which completes when the update is done.</returns>
        /// <exception cref="InvalidTransceiverMediaKindException">
        /// The transceiver associated in the offer with the same media line index as the current media line
        /// has a different media kind than the media line. This is generally a result of the two peers having
        /// mismatching media line configurations.
        /// </exception>
        internal void UpdateOnReceiveOffer(Transceiver tr)
        {
            Debug.Assert(tr != null);

            PairTransceiverOnFirstOffer(tr);

            bool wantsSend = (Source != null);
            bool wasSending = (Transceiver.LocalTrack == _senderTrack);
            if (wantsSend && !wasSending)
            {
                // If the offer doesn't allow to send then this will generate a renegotiation needed event,
                // which will be temporarily delayed since we are in the middle of a negotiation already.
                AttachTrack();
            }
            else if (!wantsSend && wasSending)
            {
                DetachTrack();
            }

            UpdateReceiverPairingIfNeeded();
        }

        internal void UpdateOnReceiveAnswer()
        {
            UpdateReceiverPairingIfNeeded();
        }

        /// <summary>
        /// Internal callback when the underlying source providing media frames to the sender track
        /// is destroyed, and therefore the local media track needs to be destroyed too.
        /// </summary>
        /// <seealso cref="AudioTrackSource.OnDisable"/>
        /// <seealso cref="VideoTrackSource.OnDisable"/>
        internal void OnSourceDestroyed()
        {
            // Different from `Source = null`. Don't need to call Source.OnRemovedFromMediaLine
            // since the Source itself has called this.
            DestroySenderIfNeeded();
            _source = null;
            UpdateTransceiverDesiredDirection();
        }

        internal void OnReceiverDestroyed()
        {
            // Different from `Receiver = null`. Don't need to call Receiver.OnRemovedFromMediaLine
            // or Receiver.OnUnpaired since the Receiver itself has called this.
            _receiver = null;
            UpdateTransceiverDesiredDirection();
        }

        public void OnBeforeSerialize() {}

        public void OnAfterDeserialize()
        {
            if (_source)
            {
                // Fill the list of media lines for the source.
                _source.OnAddedToMediaLine(this);
            }
        }
    }
}
