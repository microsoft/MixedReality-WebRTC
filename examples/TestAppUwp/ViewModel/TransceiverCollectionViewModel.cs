// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using Microsoft.MixedReality.WebRTC;

namespace TestAppUwp
{
    public class SenderTrackViewModel
    {
        public SenderTrackViewModel(MediaTrack track, string displayName = null)
        {
            Track = track;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? Track?.Name : displayName;
        }

        public MediaTrack Track { get; }

        public string DisplayName { get; }

        /// <summary>
        /// Can the sender track be attached to a transceiver? This is <c>false</c> if already
        /// attached to a transceiver, and <c>true</c> if not already attached to any transceiver.
        /// </summary>
        public bool CanBeAttached = false;
    }

    /// <summary>
    /// View model for the collection of transceivers of the peer connection. the currently
    /// selected transceiver in the Tracks window side panel, and the list of available sender
    /// and receiver tracks for that selected transceiver.
    /// </summary>
    public class TransceiverCollectionViewModel : CollectionViewModel<TransceiverViewModel>
    {
        private MediaKind _availableMediaKind = MediaKind.Audio;
        private ObservableCollection<SenderTrackViewModel> _availableSenders;

        public static readonly SenderTrackViewModel NullSenderTrack = new SenderTrackViewModel(null, "<none>");

        /// <summary>
        /// Collection of available sender tracks that can be assigned
        /// to the local track of the currently selected transceiver.
        /// </summary>
        public ObservableCollection<SenderTrackViewModel> AvailableSenders
        {
            get { return _availableSenders; }
            set { SetProperty(ref _availableSenders, value); }
        }

        public void RefreshSenderList(MediaKind mediaKind, bool force = false)
        {
            if ((mediaKind != _availableMediaKind) || force)
            {
                _availableMediaKind = mediaKind;

                // Rebuild the list of sender tracks for the given media kind
                var senders = new ObservableCollection<SenderTrackViewModel>();
                senders.Add(NullSenderTrack);
                if (_availableMediaKind == MediaKind.Audio)
                {
                    foreach (var trackViewModel in SessionModel.Current.AudioTracks)
                    {
                        if (trackViewModel.IsRemote)
                        {
                            continue;
                        }
                        var track = trackViewModel?.TrackImpl;
                        if (track != null)
                        {
                            senders.Add(new SenderTrackViewModel(track));
                        }
                    }
                }
                else if (_availableMediaKind == MediaKind.Video)
                {
                    foreach (var trackViewModel in SessionModel.Current.VideoTracks)
                    {
                        if (trackViewModel.IsRemote)
                        {
                            continue;
                        }
                        var track = trackViewModel?.TrackImpl;
                        if (track != null)
                        {
                            senders.Add(new SenderTrackViewModel(track));
                        }
                    }
                }
                AvailableSenders = senders; // FIXME - this is not thread-aware, and RefreshSenderList() can be called from non-UI thread... 
                SelectedItem?.NotifySenderChanged();
            }
        }

        protected override void OnSelectedItemChanged()
        {
            var tr = SelectedItem?.Transceiver;
            if (tr == null)
            {
                return;
            }
            RefreshSenderList(tr.MediaKind);
        }
    }
}
