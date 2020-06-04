// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using System.Collections.Concurrent;
using System.Text;
using System.Data;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// Media line abstraction for a peer connection.
    ///
    /// This container binds together a sender component (<see cref="MediaSender"/>) and/or a receiver component
    /// (<see cref="MediaReceiver"/>) on one side, with a transceiver on the other side. The media line is a
    /// declarative representation of this association, which is then turned into a binding by the implementation
    /// during an SDP negotiation. This forms the core of the algorithm allowing automatic transceiver pairing
    /// between the two peers based on the declaration of intent of the user.
    ///
    /// Assigning Unity components to the <see cref="Source"/> and <see cref="Receiver"/> fields serves
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
        /// Backing field to serialize the <see cref="Kind"/> property.
        /// </summary>
        /// <seealso cref="Kind"/>
        [SerializeField]
        private MediaKind _kind;

        /// <summary>
        /// Kind of media of the media line and its attached transceiver.
        ///
        /// This is assiged when the media line is created with <see cref="PeerConnection.AddMediaLine(MediaKind)"/>
        /// and is immutable for the lifetime of the peer connection.
        /// </summary>
        public MediaKind Kind => _kind;

        /// <summary>
        /// Backing field to serialize the <see cref="Source"/> property.
        /// </summary>
        /// <seealso cref="Source"/>
        [SerializeField]
        private MediaTrackSource _source;

        /// <summary>
        /// Media source producing the media to send through the transceiver attached to this media line.
        /// This must be an instance of a class derived from <see cref="AudioTrackSource"/> or <see cref="VideoTrackSource"/>
        /// depending on whether <see cref="Kind"/> is <see xref="Microsoft.MixedReality.WebRTC.MediaKind.Audio"/>
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
        /// </summary>
        public MediaTrackSource Source
        {
            get { return _source; }
            set
            {
                if (_source != value)
                {
                    _source = value;
                    UpdateSenderOnSourceChanged();
                    UpdateTransceiverDesiredDirection();
                }
            }
        }

        /// <summary>
        /// Media sender (track) bridging the <see cref="Source"/> with the underlying <see cref="Transceiver"/>.
        /// 
        /// The media track is automatically managed when <see cref="Source"/> changes or the transceiver direction
        /// is renegotiated, as needed.
        /// </summary>
        public MediaSender Sender { get; private set; } = null;

        /// <summary>
        /// Backing field to serialize the <see cref="Receiver"/> property.
        /// </summary>
        /// <seealso cref="Receiver"/>
        [SerializeField]
        private MediaReceiver _receiver;

        /// <summary>
        /// Media receiver consuming the media received through the transceiver attached to this media line.
        /// This must be an instance of a class derived from <see cref="AudioReceiver"/> or <see cref="VideoReceiver"/>
        /// depending on whether <see cref="Kind"/> is <see xref="Microsoft.MixedReality.WebRTC.MediaKind.Audio"/>
        /// or <see xref="Microsoft.MixedReality.WebRTC.MediaKind.Video"/>, respectively.
        ///
        /// If this is non-<c>null</c> then the peer connection will negotiate receiving some media, otherwise
        /// it will signal the remote peer that it does not wish to receive (send-only or inactive).
        ///
        /// If <see cref="Transceiver"/> is valid, that is a first session negotiation has already been conducted,
        /// then changing this value raises a <see xref="WebRTC.PeerConnection.RenegotiationNeeded"/> event on the
        /// peer connection of <see cref="Transceiver"/>.
        /// </summary>
        public MediaReceiver Receiver
        {
            get { return _receiver; }
            set
            {
                if (_receiver != value)
                {
                    _receiver = value;
                    UpdateTransceiverDesiredDirection();
                }
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
        /// Sender actually attached during <see cref="PeerConnection.HandleConnectionMessageAsync(string, string)"/>.
        /// This is different from <see cref="Source"/> until a negotiation is achieved.
        /// </summary>
        [NonSerialized]
        private MediaSender _attachedSender;

        /// <summary>
        /// Receiver actually paired during <see cref="PeerConnection.HandleConnectionMessageAsync(string, string)"/>.
        /// This is different from <see cref="Receiver"/> until a negotiation is achieved.
        /// </summary>
        [NonSerialized]
        private MediaReceiver _pairedReceiver;

        /// <param name="kind">Immutable value assigned to the <see cref="Kind"/> property on construction.</param>
        public MediaLine(MediaKind kind)
        {
            _kind = kind;
        }

        protected void UpdateSenderOnSourceChanged()
        {
            if ((_source != null) && (Sender == null))
            {
                CreateSender();
            }
            else if ((_source == null) && (Sender != null))
            {
                DestroySender();
            }
        }

        protected void UpdateTransceiverDesiredDirection()
        {
            if (Transceiver != null)
            {
                bool wantsSend = (_source != null);
                bool wantsRecv = (_receiver != null);
                Transceiver.DesiredDirection = Transceiver.DirectionFromSendRecv(wantsSend, wantsRecv);
            }
        }

        internal void AttachTrack()
        {
            Debug.Assert(Transceiver != null);
            Debug.Assert(Source != null);
            Debug.Assert(_attachedSender == null);
            // Note: Sender is null if the source was inactive
            if (Sender != null)
            {
                Sender.AttachTrack();
                _attachedSender = Sender;
            }
        }

        internal void DetachTrack()
        {
            Debug.Assert(Transceiver != null);
            Debug.Assert(Source == null);
            Debug.Assert(_attachedSender != null);
            _attachedSender.DetachTrack();
            _attachedSender = null;
        }

        internal void UpdateReceiver()
        {
            var transceiver = Transceiver;
            Debug.Assert(transceiver != null);
            bool wantsRecv = (Receiver != null);
            bool wasReceiving = (_pairedReceiver != null);
            bool isReceiving = (transceiver.RemoteTrack != null);
            // Note the extra "isReceiving" check, which ensures that when the remote track was
            // just removed by OnUnpaired(RemoteTrack) from the TrackRemoved event then it is not
            // immediately re-added by mistake.
            if (wantsRecv && isReceiving && !wasReceiving)
            {
                // Transceiver started receiving, and user actually wants to receive
                Receiver.OnPaired(transceiver.RemoteTrack);
                _pairedReceiver = Receiver;
            }
            else if (!isReceiving && wasReceiving)
            {
                // Transceiver stopped receiving (user intent does not matter here)
                Receiver.OnUnpaired(transceiver.RemoteTrack);
                _pairedReceiver = null;
            }
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
            if (_pairedReceiver != null)
            {
                _pairedReceiver.OnUnpaired(track);
                _pairedReceiver = null;
            }
        }

        private void CreateSender()
        {
            Debug.Assert(Source != null);
            if (Source.isActiveAndEnabled)
            {
                if (Kind == MediaKind.Audio)
                {
                    Sender = AudioSender.CreateFromSource((AudioTrackSource)Source, trackName: /*TODO*/string.Empty);
                }
                else
                {
                    Debug.Assert(Kind == MediaKind.Video);
                    Sender = VideoSender.CreateFromSource((VideoTrackSource)Source, trackName: /*TODO*/string.Empty);
                }
            }
        }

        private void DestroySender()
        {
            Debug.Assert(Sender != null);
            if (Kind == MediaKind.Audio)
            {
                ((AudioSender)Sender).Dispose();
            }
            else
            {
                Debug.Assert(Kind == MediaKind.Video);
                ((VideoSender)Sender).Dispose();
            }
            Sender = null;
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
            if (tr.MediaKind != Kind)
            {
                throw new InvalidTransceiverMediaKindException();
            }
            Transceiver = tr;

            // Keep the transceiver direction in sync with Sender and Receiver.
            UpdateTransceiverDesiredDirection();

            // Always do this even after first offer, because the sender and receiver
            // components can be assigned to the media line later, and therefore will
            // need to be updated on the next offer even if that is not the first one.
            bool wantsSend = (Source != null);
            bool wantsRecv = (Receiver != null);
            if (wantsSend)
            {
                if (Sender == null)
                {
                    CreateSender();
                }
                // CreateSender() might do nothing if the source is inactive
                if (Sender != null)
                {
                    Sender.AttachToTransceiver(Transceiver);
                }
            }
            if (wantsRecv)
            {
                Receiver.AttachToTransceiver(Transceiver);
            }
        }

        internal void UnpairTransceiver()
        {
            // Note: using Sender here and not Source to test actual applied state
            // and not user intended state. So this deal naturally with inactive sources,
            // since a sender was not created in that case.
            bool wasSending = (Sender != null);
            bool wasReceiving = (Receiver != null);
            if (wasSending)
            {
                Sender.DetachFromTransceiver(Transceiver);
                DestroySender();
            }
            if (wasReceiving)
            {
                Receiver.DetachFromTransceiver(Transceiver);
            }
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
            bool wasSending = (_attachedSender != null);
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
            bool wasSending = (_attachedSender != null);
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

            UpdateReceiver();
        }

        internal void UpdateOnReceiveAnswer()
        {
            UpdateReceiver();
        }
    }
}
