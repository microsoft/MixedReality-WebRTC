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
        public CollectionViewModel<MediaCaptureVideoProfile> VideoProfiles
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

        public CollectionViewModel<MediaCaptureVideoProfileMediaDescription> RecordMediaDescs { get; private set; }
            = new CollectionViewModel<MediaCaptureVideoProfileMediaDescription>();

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
        private CollectionViewModel<MediaCaptureVideoProfile> _videoProfiles
            = new CollectionViewModel<MediaCaptureVideoProfile>();
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

        public void RefreshVideoProfiles(VideoCaptureDeviceInfo item, VideoProfileKind kind)
        {
            var videoProfiles = new CollectionViewModel<MediaCaptureVideoProfile>();
            if (item != null)
            {
                IReadOnlyList<MediaCaptureVideoProfile> profiles;
                if (kind == VideoProfileKind.Unspecified)
                {
                    profiles = MediaCapture.FindAllVideoProfiles(item.Id);
                }
                else
                {
                    // VideoProfileKind and KnownVideoProfile are the same with the exception of
                    // `Unspecified` that takes value 0.
                    var profile = (KnownVideoProfile)((int)kind - 1);
                    profiles = MediaCapture.FindKnownVideoProfiles(item.Id, profile);
                }
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
                if (MediaCapture.IsVideoProfileSupported(item.Id))
                {
                    foreach (var desc in VideoProfiles.SelectedItem?.SupportedRecordMediaDescription)
                    {
                        var formatVM = new VideoCaptureFormatViewModel();
                        formatVM.Format.width = desc.Width;
                        formatVM.Format.height = desc.Height;
                        formatVM.Format.framerate = desc.FrameRate;
                        //formatVM.Format.fourcc = desc.Subtype; // TODO: string => FOURCC
                        formatVM.FormatEncodingDisplayName = desc.Subtype;
                        formats.Add(formatVM);
                    }
                }
                else
                {
                    // Device doesn't support video profiles; fall back on flat list of capture formats.
                    List<VideoCaptureFormat> formatsList = await PeerConnection.GetVideoCaptureFormatsAsync(item.Id);
                    foreach (var format in formatsList)
                    {
                        formats.Add(new VideoCaptureFormatViewModel
                        {
                            Format = format,
                            FormatEncodingDisplayName = FourCCToString(format.fourcc)
                        });
                    }
                }
            }
            VideoCaptureFormats = formats;

            // Select first item for convenience
            VideoCaptureFormats.SelectFirstItemIfAny();
        }

        public async Task AddVideoTrackFromDeviceAsync(string trackName)
        {
            await RequestMediaAccessAsync(StreamingCaptureMode.Video);

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
            var source = await VideoTrackSource.CreateFromDeviceAsync(deviceConfig);
            // FIXME - this leaks the source, never disposed

            // Crate the track
            var settings = new LocalVideoTrackInitConfig
            {
                trackName = trackName,
            };
            VideoCaptureFormatViewModel formatInfo = VideoCaptureFormats.SelectedItem;
            if (formatInfo != null)
            {
                settings.width = formatInfo.Format.width;
                settings.height = formatInfo.Format.height;
                settings.framerate = formatInfo.Format.framerate;
            }
            var track = LocalVideoTrack.CreateFromSource(source, settings);
            // FIXME - this probably leaks the track, never disposed

            SessionModel.Current.VideoTracks.Add(new VideoTrackViewModel
            {
                Source = source,
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
    }
}
