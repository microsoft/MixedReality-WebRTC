// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.ObjectModel;
using Microsoft.MixedReality.WebRTC;

namespace TestAppUwp
{
    /// <summary>
    /// View model for a transceiver.
    /// </summary>
    public class TransceiverViewModel : NotifierBase
    {
        /// <summary>
        /// The peer connection transceiver this instance is a view model of.
        /// </summary>
        public Transceiver Transceiver { get; }

        /// <summary>
        /// Current sender track for the transceiver. This is the track used as the media
        /// source to send to the remote peer. This can be the null sender track
        /// (<see cref="TransceiverCollectionViewModel.NullSenderTrack"/>), but cannot be
        /// a <c>null</c> sender track view model (ignored).
        /// </summary>
        public SenderTrackViewModel Sender
        {
            get { return _sender; }
            set
            {
                // Ignore null values; this happens when the transceiver selection changes before
                // the list of senders is re-populated and the combo box selected item re-assigned.
                // A null track is actually abstracted inside a valid SenderTrack object, so this
                // doesn't prevent assigning a null track to the transceiver.
                if (value == null)
                {
                    return;
                }

                if (SetProperty(ref _sender, value))
                {
                    // Don't set tracks from main UI thread on UWP, that will most likely deadlock,
                    // as the current call with block on the signaling thread, which blocks on media
                    // being updated on the worker thread, which on UWP often uses some async API that
                    // need to pump the main UI thread, resulting in a 3-thread deadlock loop.
                    // See e.g. https://github.com/webrtc-uwp/webrtc-uwp-sdk/issues/143
                    MediaKind mediaKind = Transceiver.MediaKind;
                    MediaTrack newTrack = _sender.Track;
                    ThreadHelper.RunOnWorkerThread(() =>
                    {
                        if (mediaKind == MediaKind.Audio)
                        {
                            if (newTrack == null)
                            {
                                Transceiver.LocalAudioTrack = null;
                            }
                            else if (newTrack is LocalAudioTrack localAudioTrack)
                            {
                                Transceiver.LocalAudioTrack = localAudioTrack;
                            }
                            else
                            {
                                throw new ArgumentException();
                            }
                        }
                        else if (mediaKind == MediaKind.Video)
                        {
                            if (newTrack == null)
                            {
                                Transceiver.LocalVideoTrack = null;
                            }
                            else if (newTrack is LocalVideoTrack localVideoTrack)
                            {
                                Transceiver.LocalVideoTrack = localVideoTrack;
                            }
                            else
                            {
                                throw new ArgumentException();
                            }
                        }
                    });
                }
            }
        }

        /// <summary>
        /// Is the transceiver associated with a media line?
        /// </summary>
        public bool IsAssociated
        {
            get { return _isAssociated; }
            set { SetProperty(ref _isAssociated, value); }
        }

        /// <summary>
        /// Display name for the transceiver, shown above the sender/receiver tracks.
        /// </summary>
        public string DisplayName
        {
            get { return $"{Transceiver.MlineIndex}. {Transceiver.Name}"; }
        }

        /// <summary>
        /// Transceiver SDP name.
        /// </summary>
        public string Name => Transceiver.Name;

        /// <summary>
        /// Transceiver desired direction.
        /// </summary>
        public Transceiver.Direction DesiredDirection
        {
            get { return _desiredDirection; }
            set
            {
                if (SetProperty(ref _desiredDirection, value))
                {
                    Transceiver.DesiredDirection = _desiredDirection;
                }
            }
        }

        /// <summary>
        /// Transceiver negotiated direction.
        /// </summary>
        public Transceiver.Direction? NegotiatedDirection
        {
            get { return Transceiver.NegotiatedDirection; }
        }

        /// <summary>
        /// Receiver track SDP name.
        /// </summary>
        public string ReceiverDisplayName
        {
            get
            {
                return Transceiver.RemoteTrack?.Name;
            }
        }

        public bool IsAudioTransceiver => Transceiver.MediaKind == MediaKind.Audio;
        public bool IsVideoTransceiver => Transceiver.MediaKind == MediaKind.Video;

        public TransceiverViewModel(Transceiver transceiver)
        {
            Transceiver = transceiver;
            IsAssociated = (transceiver.MlineIndex >= 0);
            DesiredDirection = transceiver.DesiredDirection;
            transceiver.Associated += OnAssociated;
            transceiver.DirectionChanged += OnDirectionChanged;

            // Sender defaults to the NULL track
            _sender = TransceiverCollectionViewModel.NullSenderTrack;
        }

        /// <summary>
        /// Notify the current transceiver that its sender track may have changed, generally
        /// because the list of senders was refreshed after a new transceiver was selected.
        /// </summary>
        public void NotifySenderChanged()
        {
            RaisePropertyChanged("Sender");
        }

        /// <summary>
        /// Notify the current transceiver that its receiver track has changed, generally
        /// because a new remote track was created after an offer or answer was applied.
        /// </summary>
        public void NotifyReceiverChanged()
        {
            RaisePropertyChanged("ReceiverDisplayName");
        }

        /// <summary>
        /// Backing field for <see cref="IsAssociated"/>.
        /// </summary>
        private bool _isAssociated = false;

        /// <summary>
        /// Backing field for <see cref="Sender"/>.
        /// </summary>
        private SenderTrackViewModel _sender;

        private Transceiver.Direction _desiredDirection = Transceiver.Direction.Inactive;

        private void OnDirectionChanged(Transceiver transceiver)
        {
            // Transceiver.NegotiatedDirection changed, which is used
            // by the NegotiatedDirection bind property
            RaisePropertyChanged("NegotiatedDirection");
        }

        /// <summary>
        /// Callback when the transceiver gets associated as a result of an offer being applied.
        /// </summary>
        /// <param name="transceiver">The current transceiver being associated.</param>
        private void OnAssociated(Transceiver transceiver)
        {
            IsAssociated = (transceiver.MlineIndex >= 0);
            // Transceiver.MlineIndex changed, which is used by DisplayName
            RaisePropertyChanged("DisplayName");
        }
    }
}
