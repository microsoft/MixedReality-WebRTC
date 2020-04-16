// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace TestAppUwp
{
    public sealed partial class AddAudioTrackPage : Page
    {
        public AudioCaptureViewModel AudioCaptureViewModel { get; } = new AudioCaptureViewModel();

        public AddAudioTrackPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            // Reset status
            createTrackStatusText.Text = string.Empty;
            progressRing.IsActive = false;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // Pre-populate track name control with unique name
            // TODO - Ensure unique and valid SDP token
            int numAudioTracks = SessionModel.Current.AudioTracks.Count;
            trackName.Text = $"audio_track_{numAudioTracks}";
        }

        private void CloseClicked(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            Frame.GoBack();
        }

        private async void CreateTrackClicked(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var button = (Button)sender;
            button.IsEnabled = false;
            progressRing.IsActive = true;
            createTrackStatusText.Text = "Creating track...";
            bool wasCreated = true;
            try
            {
                await AudioCaptureViewModel.AddAudioTrackFromDeviceAsync(trackName.Text);
                createTrackStatusText.Text = "Track created.";
            }
            catch (Exception ex)
            {
                createTrackStatusText.Text = "Failed: " + ex.Message;
                wasCreated = false;
            }
            progressRing.IsActive = false;
            button.IsEnabled = true;
            if (wasCreated)
            {
                Frame.GoBack();
            }
        }
    }
}
