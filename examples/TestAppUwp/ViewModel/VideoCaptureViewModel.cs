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

    /// <summary>
    /// View model abstracting the video capture selection process, including:
    /// - selecting a video capture device
    /// - optionally selecting a video profile or profile kind (if supported by device)
    /// - optionally selecting a video capture format
    /// </summary>
    public class VideoCaptureViewModel : NotifierBase
    {
        /// <summary>
        /// Collection of video capture devices available on the current host device.
        /// The selected item is the currently selected video capture device, and affects
        /// the list of video profiles and capture formats.
        /// </summary>
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

        /// <summary>
        /// Collection of video capture formats for the currently selected video capture device.
        /// The capture formats are further filtered based on the selected video profile.
        /// </summary>
        public CollectionViewModel<VideoCaptureFormatViewModel> VideoCaptureFormats
        {
            get { return _videoCaptureFormats; }
            set { SetProperty(ref _videoCaptureFormats, value); }
        }

        /// <summary>
        /// List of video profile kinds.
        /// </summary>
        public VideoProfileKind[] VideoProfileKinds { get; }

        /// <summary>
        /// Currently selected video profile kind. This affects the available video profiles,
        /// which are filtered based on the current kind.
        /// </summary>
        public VideoProfileKind SelectedVideoProfileKind
        {
            get { return _selectedVideoProfileKind; }
            set
            {
                if (SetProperty(ref _selectedVideoProfileKind, value))
                {
                    // If video profile kind changed, refresh the list of profiles which
                    // are associated with the currently selected profile kind.
                    var device = VideoCaptureDevices.SelectedItem;
                    if (device != null)
                    {
                        RefreshVideoProfiles(device, _selectedVideoProfileKind);
                    }
                }
            }
        }

        /// <summary>
        /// Collection of video profiles for the currently selected video capture device
        /// and video profile kind.
        /// </summary>
        public CollectionViewModel<VideoProfile> VideoProfiles
        {
            get { return _videoProfiles; }
            private set
            {
                if (SetProperty(ref _videoProfiles, value))
                {
                    // If the list of video profiles changed, select the first one automatically,
                    // and refresh the capture formats associated with it.
                    _videoProfiles.SelectionChanged += () =>
                    {
                        _ = RefreshVideoCaptureFormatsAsync(VideoCaptureDevices.SelectedItem);
                    };
                    _videoProfiles.SelectFirstItemIfAny();
                }
            }
        }

        /// <summary>
        /// Property indicating whether a track can be created based on the currently selected items.
        /// </summary>
        public bool CanCreateTrack
        {
            get { return _canCreateTrack; }
            set { SetProperty(ref _canCreateTrack, value); }
        }

        /// <summary>
        /// Error message to report to user when non-<c>null</c>.
        /// </summary>
        public string ErrorMessage
        {
            get { return _errorMessage; }
            set { SetProperty(ref _errorMessage, value); }
        }

        private CollectionViewModel<VideoCaptureDeviceInfo> _videoCaptureDevices
            = new CollectionViewModel<VideoCaptureDeviceInfo>();
        private CollectionViewModel<VideoCaptureFormatViewModel> _videoCaptureFormats
            = new CollectionViewModel<VideoCaptureFormatViewModel>();
        private CollectionViewModel<VideoProfile> _videoProfiles
            = new CollectionViewModel<VideoProfile>();
        private VideoProfileKind _selectedVideoProfileKind = VideoProfileKind.Unspecified;
        private bool _canCreateTrack = false;
        private string _errorMessage;

        public VideoCaptureViewModel()
        {
            VideoProfileKinds = (VideoProfileKind[])Enum.GetValues(typeof(VideoProfileKind));
        }

        public async Task RefreshVideoCaptureDevicesAsync()
        {
            Logger.Log($"Refreshing list of video capture devices");

            ErrorMessage = null;
            try
            {
                await Utils.RequestMediaAccessAsync(StreamingCaptureMode.Video);
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
            var devices = await DeviceVideoTrackSource.GetCaptureDevicesAsync();
            var deviceList = new CollectionViewModel<VideoCaptureDeviceInfo>();
            foreach (var device in devices)
            {
                Logger.Log($"Found video capture device: id={device.id} name={device.name}");
                deviceList.Add(new VideoCaptureDeviceInfo(id: device.id, displayName: device.name));
            }
            VideoCaptureDevices = deviceList;

            // Auto-select first device for convenience
            VideoCaptureDevices.SelectFirstItemIfAny();
        }

        private void VideoCaptureDevices_SelectionChanged()
        {
            CanCreateTrack = (VideoCaptureDevices.SelectedItem != null);
            if (MediaCapture.IsVideoProfileSupported(VideoCaptureDevices.SelectedItem.Id))
            {
                // Refresh the video profiles, which wil automatically refresh the video capture
                // formats associated with the selected profile.
                RefreshVideoProfiles(VideoCaptureDevices.SelectedItem, SelectedVideoProfileKind);
            }
            else
            {
                _ = RefreshVideoCaptureFormatsAsync(VideoCaptureDevices.SelectedItem);
            }
        }

        public async void RefreshVideoProfiles(VideoCaptureDeviceInfo item, VideoProfileKind kind)
        {
            // Clear formats, which are profile-dependent. This ensures the former list doesn't
            // stay visible if the current profile kind is not supported (does not return any profile).
            VideoCaptureFormats.Clear();

            var videoProfiles = new CollectionViewModel<VideoProfile>();
            if (item != null)
            {
                IReadOnlyList<VideoProfile> profiles = await DeviceVideoTrackSource.GetCaptureProfilesAsync(item.Id, kind);
                foreach (var profile in profiles)
                {
                    videoProfiles.Add(profile);
                }
            }
            VideoProfiles = videoProfiles;

            // Select first item for convenience
            VideoProfiles.SelectFirstItemIfAny();
        }

        public static string FourCCToString(uint fourcc)
        {
            byte[] str = new byte[4];
            str[0] = (byte)(fourcc & 0xFF);
            str[1] = (byte)((fourcc & 0xFF00) >> 8);
            str[2] = (byte)((fourcc & 0xFF0000) >> 16);
            str[3] = (byte)((fourcc & 0xFF000000) >> 24);
            return System.Text.Encoding.ASCII.GetString(str);
        }

        public async Task RefreshVideoCaptureFormatsAsync(VideoCaptureDeviceInfo item)
        {
            var formats = new CollectionViewModel<VideoCaptureFormatViewModel>();
            if (item != null)
            {
                IReadOnlyList<VideoCaptureFormat> formatsList;
                string profileId = VideoProfiles.SelectedItem?.uniqueId;
                if (string.IsNullOrEmpty(profileId))
                {
                    // Device doesn't support video profiles; fall back on flat list of capture formats.
                    formatsList = await DeviceVideoTrackSource.GetCaptureFormatsAsync(item.Id);
                }
                else
                {
                    // Enumerate formats for the specified profile only
                    formatsList = await DeviceVideoTrackSource.GetCaptureFormatsAsync(item.Id, profileId);
                }
                foreach (var format in formatsList)
                {
                    formats.Add(new VideoCaptureFormatViewModel
                    {
                        Format = format,
                        FormatEncodingDisplayName = FourCCToString(format.fourcc)
                    });
                }
            }
            VideoCaptureFormats = formats;

            // Select first item for convenience
            VideoCaptureFormats.SelectFirstItemIfAny();
        }

        public async Task AddVideoTrackFromDeviceAsync(string trackName)
        {
            await Utils.RequestMediaAccessAsync(StreamingCaptureMode.Video);

            // Create the source
            VideoCaptureDeviceInfo deviceInfo = VideoCaptureDevices.SelectedItem;
            if (deviceInfo == null)
            {
                throw new InvalidOperationException("No video capture device selected");
            }
            var deviceConfig = new LocalVideoDeviceInitConfig
            {
                videoDevice = new VideoCaptureDevice { id = deviceInfo.Id },
            };
            VideoCaptureFormatViewModel formatInfo = VideoCaptureFormats.SelectedItem;
            if (formatInfo != null)
            {
                deviceConfig.width = formatInfo.Format.width;
                deviceConfig.height = formatInfo.Format.height;
                deviceConfig.framerate = formatInfo.Format.framerate;
            }
            if (deviceInfo.SupportsVideoProfiles)
            {
                VideoProfile profile = VideoProfiles.SelectedItem;
                deviceConfig.videoProfileId = profile?.uniqueId;
                deviceConfig.videoProfileKind = SelectedVideoProfileKind;
            }
            var source = await DeviceVideoTrackSource.CreateAsync(deviceConfig);
            // FIXME - this leaks the source, never disposed

            // Crate the track
            var trackConfig = new LocalVideoTrackInitConfig
            {
                trackName = trackName,
            };
            var track = LocalVideoTrack.CreateFromSource(source, trackConfig);
            // FIXME - this probably leaks the track, never disposed

            SessionModel.Current.AddVideoTrack(track, deviceInfo.DisplayName);
        }
    }
}
