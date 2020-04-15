// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.MixedReality.WebRTC;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace TestAppUwp
{
    /// <summary>
    /// 
    /// </summary>
    public sealed partial class SessionPage : Page
    {
        public TransceiverCollectionViewModel Transceivers
        {
            get { return SessionModel.Current.Transceivers; }
        }

        public SessionModel SessionModel
        {
            get { return SessionModel.Current; }
        }

        private SessionViewModel _sessionViewModel;

        public SessionPage()
        {
            this.InitializeComponent();
            _sessionViewModel = new SessionViewModel();
        }

        private void AddPendingTransceiver(MediaKind mediaKind, string name)
        {
            var settings = new TransceiverInitSettings
            {
                Name = name,
                InitialDesiredDirection = Transceiver.Direction.SendReceive,
            };
            SessionModel.Current.AddTransceiver(mediaKind, settings);
        }

        private void AddAudioTransceiver_Click(object sender, RoutedEventArgs e)
        {
            var name = newTransceiverName.Text ?? "audio_transceiver"; // TODO: validate SDP token
            AddPendingTransceiver(MediaKind.Audio, name);
        }

        private void AddVideoTransceiver_Click(object sender, RoutedEventArgs e)
        {
            var name = newTransceiverName.Text ?? "video_transceiver"; // TODO: validate SDP token
            AddPendingTransceiver(MediaKind.Video, name);
        }

        private void StartNegotiationButton_Click(object sender, RoutedEventArgs e)
        {
            SessionModel.Current.StartNegotiation();
        }

        private void TransceiverSenderSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var senderTrack = transceiverSenderTrack.SelectedItem as SenderTrackViewModel;
            var transceiverViewModel = SessionModel.Current.Transceivers.SelectedItem;
            transceiverViewModel.Sender = senderTrack;
        }

        private async void AddExtraDataChannelButtonClicked(object sender, RoutedEventArgs e)
        {
            //if (!PluginInitialized)
            //{
            //    return;
            //}
            //await _peerConnection.AddDataChannelAsync("extra_channel", true, true);
        }

        private void TextBlock_SelectionChanged(object sender, RoutedEventArgs e)
        {

        }
    }
}
