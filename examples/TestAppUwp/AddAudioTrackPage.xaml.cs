// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Windows.UI.Xaml.Controls;

namespace TestAppUwp
{
    public sealed partial class AddAudioTrackPage : Page
    {
        public AudioCaptureViewModel AudioCaptureViewModel { get; } = new AudioCaptureViewModel();

        public AddAudioTrackPage()
        {
            this.InitializeComponent();
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
