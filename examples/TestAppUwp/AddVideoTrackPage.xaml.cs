// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Windows.UI.Xaml.Controls;

namespace TestAppUwp
{
    /// <summary>
    /// Page to create a new video track and add it to the collection of existing tracks.
    /// </summary>
    public sealed partial class AddVideoTrackPage : Page
    {
        public VideoCaptureViewModel VideoCaptureViewModel { get; } = new VideoCaptureViewModel();

        public AddVideoTrackPage()
        {
            this.InitializeComponent();
            _ = VideoCaptureViewModel.RefreshVideoCaptureDevicesAsync();
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
                await VideoCaptureViewModel.AddVideoTrackFromDeviceAsync(trackName.Text);
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
