// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.MixedReality.WebRTC;
using Windows.Media.Capture;
using Windows.UI.Xaml.Controls;

namespace TestAppUwp
{
    public class VideoCaptureDeviceInfo
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly Symbol Symbol = Symbol.Video;

        public VideoCaptureDeviceInfo(string id, string displayName)
        {
            Id = id;
            DisplayName = displayName;
            if (!string.IsNullOrWhiteSpace(Id))
            {
                SupportsVideoProfiles = MediaCapture.IsVideoProfileSupported(Id);
            }
            else
            {
                SupportsVideoProfiles = false;
            }
        }

        public bool SupportsVideoProfiles { get; }
    }

    public class VideoCaptureViewModel : NotifierBase
    {
        private CollectionViewModel<VideoCaptureDeviceInfo> _videoCaptureDevices
            = new CollectionViewModel<VideoCaptureDeviceInfo>();
        private CollectionViewModel<VideoCaptureFormatViewModel> _videoCaptureFormats
            = new CollectionViewModel<VideoCaptureFormatViewModel>();
        private bool _canCreateTrack = false;

        public CollectionViewModel<VideoCaptureDeviceInfo> VideoCaptureDevices
        {
            get { return _videoCaptureDevices; }
            set
            {
                if (SetProperty(ref _videoCaptureDevices, value))
                {
                    _videoCaptureDevices.SelectionChanged += VideoCaptureDevices_SelectionChanged;
                }
            }
        }

        public CollectionViewModel<VideoCaptureFormatViewModel> VideoCaptureFormats
        {
            get { return _videoCaptureFormats; }
            set { SetProperty(ref _videoCaptureFormats, value); }
        }

        public VideoProfileKind SelectedVideoProfileKind
        {
            get
            {
                //var videoProfileKindIndex = KnownVideoProfileKindComboBox.SelectedIndex;
                //if (videoProfileKindIndex < 0)
                //{
                //    return VideoProfileKind.Unspecified;
                //}
                //return (VideoProfileKind)Enum.GetValues(typeof(VideoProfileKind)).GetValue(videoProfileKindIndex);
                return VideoProfileKind.Unspecified;
            }
        }

        public CollectionViewModel<MediaCaptureVideoProfile> VideoProfiles { get; private set; }
            = new CollectionViewModel<MediaCaptureVideoProfile>();

        public CollectionViewModel<MediaCaptureVideoProfileMediaDescription> RecordMediaDescs { get; private set; }
            = new CollectionViewModel<MediaCaptureVideoProfileMediaDescription>();

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

        public async Task RefreshVideoCaptureDevicesAsync()
        {
            Logger.Log($"Refreshing list of video capture devices");

            ErrorMessage = null;
            try
            {
                await RequestMediaAccessAsync(StreamingCaptureMode.Video);
            }
            catch (UnauthorizedAccessException uae)
            {
                ErrorMessage = "This application is not authorized to access the local camera device. Change permission settings and restart the application.";
                throw uae;
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                throw ex;
            }

            // Populate the list of video capture devices (webcams).
            // On UWP this uses internally the API:
            //   Devices.Enumeration.DeviceInformation.FindAllAsync(VideoCapture)
            // Note that there's no API to pass a given device to WebRTC,
            // so there's no way to monitor and update that list if a device
            // gets plugged or unplugged. Even using DeviceInformation.CreateWatcher()
            // would yield some devices that might become unavailable by the time
            // WebRTC internally opens the video capture device.
            // This is more for demo purpose here because using the UWP API is nicer.
            var devices = await PeerConnection.GetVideoCaptureDevicesAsync();
            var deviceList = new CollectionViewModel<VideoCaptureDeviceInfo>();
            List<VideoCaptureDeviceInfo> vcds = new List<VideoCaptureDeviceInfo>(devices.Count);
            foreach (var device in devices)
            {
                Logger.Log($"Found video capture device: id={device.id} name={device.name}");
                deviceList.Add(new VideoCaptureDeviceInfo(id: device.id, displayName: device.name));
            }
            VideoCaptureDevices = deviceList;
        }

        private void VideoCaptureDevices_SelectionChanged()
        {
            CanCreateTrack = (VideoCaptureDevices.SelectedItem != null);
            _ = RefreshVideoCaptureFormatsAsync(VideoCaptureDevices.SelectedItem);
        }

        public async Task RefreshVideoCaptureFormatsAsync(VideoCaptureDeviceInfo item)
        {
            // Device doesn't support video profiles; fall back on flat list of capture formats.
            List<VideoCaptureFormat> formatsList = await PeerConnection.GetVideoCaptureFormatsAsync(item.Id);
            var formats = new CollectionViewModel<VideoCaptureFormatViewModel>();
            foreach (var format in formatsList)
            {
                formats.Add(new VideoCaptureFormatViewModel { Format = format });
            }
            VideoCaptureFormats = formats;
        }

        public async Task AddVideoTrackFromDeviceAsync(string trackName)
        {
            await RequestMediaAccessAsync(StreamingCaptureMode.Video);

            VideoCaptureDeviceInfo deviceInfo = VideoCaptureDevices.SelectedItem;
            if (deviceInfo == null)
            {
                throw new InvalidOperationException("No video capture device selected");
            }
            var settings = new LocalVideoTrackSettings
            {
                trackName = trackName,
                videoDevice = new VideoCaptureDevice { id = deviceInfo.Id },
            };
            VideoCaptureFormatViewModel formatInfo = VideoCaptureFormats.SelectedItem;
            if (formatInfo != null)
            {
                settings.width = formatInfo.Format.width;
                settings.height = formatInfo.Format.height;
                settings.framerate = formatInfo.Format.framerate;
            }
            var track = await LocalVideoTrack.CreateFromDeviceAsync(settings);

            SessionModel.Current.VideoTracks.Add(new VideoTrackViewModel
            {
                Track = track,
                TrackImpl = track,
                IsRemote = false,
                DeviceName = deviceInfo.DisplayName
            });
            SessionModel.Current.LocalTracks.Add(new TrackViewModel(Symbol.Video) { DisplayName = deviceInfo.DisplayName });
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


        /// <summary>
        /// Update the list of video profiles stored in <cref>VideoProfiles</cref>
        /// when the selected video capture device or known video profile kind change.
        /// </summary>
        private async void UpdateVideoProfiles()
        {
            VideoProfiles.Clear();
            VideoCaptureFormats.Clear();

            //// Get the video capture device selected by the user
            //var deviceIndex = VideoCaptureDeviceList.SelectedIndex;
            //if (deviceIndex < 0)
            //{
            //    return;
            //}
            //var device = VideoCaptureDevices[deviceIndex];

            //// Ensure that the video capture device actually supports video profiles
            //if (MediaCapture.IsVideoProfileSupported(device.Id))
            //{
            //    // Get the kind of known video profile selected by the user
            //    var videoProfileKindIndex = KnownVideoProfileKindComboBox.SelectedIndex;
            //    if (videoProfileKindIndex < 0)
            //    {
            //        return;
            //    }
            //    var videoProfileKind = (VideoProfileKind)Enum.GetValues(typeof(VideoProfileKind)).GetValue(videoProfileKindIndex);

            //    // List all video profiles for the select device (and kind, if any specified)
            //    IReadOnlyList<MediaCaptureVideoProfile> profiles;
            //    if (videoProfileKind == VideoProfileKind.Unspecified)
            //    {
            //        profiles = MediaCapture.FindAllVideoProfiles(device.Id);
            //    }
            //    else
            //    {
            //        profiles = MediaCapture.FindKnownVideoProfiles(device.Id, (KnownVideoProfile)(videoProfileKind - 1));
            //    }
            //    foreach (var profile in profiles)
            //    {
            //        VideoProfiles.Add(profile);
            //    }
            //    if (profiles.Any())
            //    {
            //        VideoProfileComboBox.SelectedIndex = 0;
            //    }
            //}
            //else
            //{
            //    // Device doesn't support video profiles; fall back on flat list of capture formats.
            //    List<VideoCaptureFormat> formatsList = await PeerConnection.GetVideoCaptureFormatsAsync(device.Id);
            //    foreach (var format in formatsList)
            //    {
            //        VideoCaptureFormats.Add(format);
            //    }

            //    // Default to first format, so that user can start the video capture even without selecting
            //    // explicitly a format in a different application tab.
            //    if (formatsList.Count > 0)
            //    {
            //        VideoCaptureFormatList.SelectedIndex = 0;
            //    }
            //}
        }

        //private void VideoCaptureDeviceList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        //{
        //    // Get the video capture device selected by the user
        //    var deviceIndex = VideoCaptureDeviceList.SelectedIndex;
        //    if (deviceIndex < 0)
        //    {
        //        return;
        //    }
        //    var device = VideoCaptureDevices[deviceIndex];

        //    // Select a default video profile kind
        //    var values = Enum.GetValues(typeof(VideoProfileKind));
        //    if (MediaCapture.IsVideoProfileSupported(device.Id))
        //    {
        //        var defaultProfile = VideoProfileKind.VideoConferencing;
        //        var profiles = MediaCapture.FindKnownVideoProfiles(device.Id, (KnownVideoProfile)(defaultProfile - 1));
        //        if (!profiles.Any())
        //        {
        //            // Fall back to VideoRecording if VideoConferencing has no profiles (e.g. HoloLens).
        //            defaultProfile = VideoProfileKind.VideoRecording;
        //        }
        //        KnownVideoProfileKindComboBox.SelectedIndex = Array.IndexOf(values, defaultProfile);

        //        KnownVideoProfileKindComboBox.IsEnabled = true; //< TODO - Use binding
        //        VideoProfileComboBox.IsEnabled = true;
        //        RecordMediaDescList.IsEnabled = true;
        //        VideoCaptureFormatList.IsEnabled = false;
        //    }
        //    else
        //    {
        //        KnownVideoProfileKindComboBox.SelectedIndex = Array.IndexOf(values, VideoProfileKind.Unspecified);
        //        KnownVideoProfileKindComboBox.IsEnabled = false;
        //        VideoProfileComboBox.IsEnabled = false;
        //        RecordMediaDescList.IsEnabled = false;
        //        VideoCaptureFormatList.IsEnabled = true;
        //    }

        //    UpdateVideoProfiles();
        //}

        //private void KnownVideoProfileKindComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        //{
        //    UpdateVideoProfiles();
        //}

        //private void VideoProfileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        //{
        //    RecordMediaDescs.Clear();

        //    var profile = SelectedVideoProfile;
        //    if (profile == null)
        //    {
        //        return;
        //    }

        //    foreach (var desc in profile.SupportedRecordMediaDescription)
        //    {
        //        RecordMediaDescs.Add(desc);
        //    }
        //}

    }
}
