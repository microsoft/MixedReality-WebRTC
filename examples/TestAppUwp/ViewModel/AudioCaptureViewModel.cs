// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Microsoft.MixedReality.WebRTC;
using Windows.Media.Capture;
using Windows.UI.Xaml.Controls;

namespace TestAppUwp
{
    public class AudioCaptureViewModel : NotifierBase
    {
        private bool _canCreateTrack = true;

        public bool CanCreateTrack
        {
            get { return _canCreateTrack; }
            set { SetProperty(ref _canCreateTrack, value); }
        }

        public string ErrorMessage
        {
            get { return _errorMessage; }
            set { SetProperty(ref _errorMessage, value); }
        }

        private string _errorMessage;

        public static async Task AddAudioTrackFromDeviceAsync(string trackName)
        {
            const string DefaultAudioDeviceName = "Default audio device";

            await Utils.RequestMediaAccessAsync(StreamingCaptureMode.Audio);

                    // FIXME - this leaks 'source', never disposed (and is the track itself disposed??)
            var initConfig = new LocalAudioDeviceInitConfig();
            var source = await DeviceAudioTrackSource.CreateAsync(initConfig);

            var settings = new LocalAudioTrackInitConfig
            {
                trackName = trackName
            };
            var track = LocalAudioTrack.CreateFromSource(source, settings);

            SessionModel.Current.AddAudioTrack(track, DefaultAudioDeviceName);
        }
    }
}
