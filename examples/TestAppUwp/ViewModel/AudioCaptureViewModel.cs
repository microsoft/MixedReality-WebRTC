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

        public async Task AddAudioTrackFromDeviceAsync(string trackName)
        {
            const string DefaultAudioDeviceName = "Default audio device";

            await RequestMediaAccessAsync(StreamingCaptureMode.Audio);

                    // FIXME - this leaks 'source', never disposed (and is the track itself disposed??)
            var initConfig = new LocalAudioDeviceInitConfig();
            var source = await AudioTrackSource.CreateFromDeviceAsync(initConfig);

            var settings = new LocalAudioTrackInitConfig
            {
                trackName = trackName
            };
            var track = LocalAudioTrack.CreateFromSource(source, settings);

            SessionModel.Current.AudioTracks.Add(new AudioTrackViewModel
            {
                Source = source,
                Track = track,
                TrackImpl = track,
                IsRemote = false,
                DeviceName = DefaultAudioDeviceName
            });
            SessionModel.Current.LocalTracks.Add(new TrackViewModel(Symbol.Volume) { DisplayName = DefaultAudioDeviceName });
        }

        private async Task RequestMediaAccessAsync(StreamingCaptureMode mode)
        {
            // Ensure that the UWP app was authorized to capture audio (cap:microphone)
            // or video (cap:webcam), otherwise the native plugin will fail.
            try
            {
                MediaCapture mediaAccessRequester = new MediaCapture();
                var mediaSettings = new MediaCaptureInitializationSettings
                {
                    AudioDeviceId = "",
                    VideoDeviceId = "",
                    StreamingCaptureMode = mode,
                    PhotoCaptureSource = PhotoCaptureSource.VideoPreview
                };
                await mediaAccessRequester.InitializeAsync(mediaSettings);
            }
            catch (UnauthorizedAccessException uae)
            {
                Logger.Log("Access to A/V denied, check app permissions: " + uae.Message);
                throw uae;
            }
            catch (Exception ex)
            {
                Logger.Log("Failed to initialize A/V with unknown exception: " + ex.Message);
                throw ex;
            }
        }
    }
}
