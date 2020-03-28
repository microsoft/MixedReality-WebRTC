// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.MixedReality.WebRTC;
using TestAppUwp.Video;
using Windows.ApplicationModel;
using Windows.Media.Capture;
using Windows.Media.Core;
using Windows.Media.MediaProperties;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.System.Profile;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace TestAppUwp
{
    public class NavLink
    {
        public string Id { get; set; }
        public string Label { get; set; }
        public Symbol Symbol { get; set; }
    }

    public class VideoCaptureDeviceInfo
    {
        public string Id;
        public string DisplayName;
        public Symbol Symbol;

        public bool SupportsVideoProfiles
        {
            get
            {
                if (Id != null)
                {
                    return MediaCapture.IsVideoProfileSupported(Id);
                }
                return false;
            }
        }
    }

    public class ChatChannel
    {
        public DataChannel DataChannel;
        public string Text = "";
        public string Label { get { return DataChannel?.Label; } }
    }

    /// <summary>
    /// The main application page.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        /// <summary>
        /// Indicates whether the native plugin has been successfully initialized,
        /// and the <see cref="_peerConnection"/> object can be used.
        /// </summary>
        public bool PluginInitialized { get; private set; } = false;

        private MediaStreamSource localVideoSource = null;
        private MediaSource localMediaSource = null;
        private MediaPlayer localVideoPlayer = new MediaPlayer();
        private bool _isLocalVideoPlaying = false;
        private object _isLocalVideoPlayingLock = new object();
        private LocalVideoTrack _localVideoTrack = null;

        private MediaStreamSource remoteVideoSource = null;
        private MediaSource remoteMediaSource = null;
        private MediaPlayer remoteVideoPlayer = new MediaPlayer();
        private bool _isRemoteVideoPlaying = false;
        private uint _remoteVideoWidth = 0;
        private uint _remoteVideoHeight = 0;
        private object _isRemoteVideoPlayingLock = new object();

        private uint _remoteAudioChannelCount = 0;
        private uint _remoteAudioSampleRate = 0;
        private bool _isRemoteAudioPlaying = false;
        private object _isRemoteAudioPlayingLock = new object();

        /// <summary>
        /// The underlying <see cref="PeerConnection"/> object.
        /// </summary>
        private PeerConnection _peerConnection;

        /// <summary>
        /// Enable automatically creating a new SDP offer when the renegotiation event is fired.
        /// This can be disabled when adding multiple tracks, to bundle all changes together and
        /// avoid multiple round trips to the signaler and remote peer.
        /// </summary>
        private bool _renegotiationOfferEnabled = true;

        /// <summary>
        /// Predetermined chat data channel ID, negotiated out of band.
        /// </summary>
        private const int ChatChannelID = 42;

        private bool isDssPolling = false;
        private NodeDssSignaler dssSignaler = new NodeDssSignaler();

        private DispatcherTimer localVideoStatsTimer = new DispatcherTimer();
        private DispatcherTimer remoteVideoStatsTimer = new DispatcherTimer();

        /// <summary>
        /// Get the string representing the preferred audio codec the user selected.
        /// </summary>
        public string PreferredAudioCodec
        {
            get
            {
                if (PreferredAudioCodec_Custom.IsChecked.GetValueOrDefault(false))
                {
                    return CustomPreferredAudioCodec.Text;
                }
                else if (PreferredAudioCodec_OPUS.IsChecked.GetValueOrDefault(false))
                {
                    return "opus";
                }
                return string.Empty;
            }
        }

        /// <summary>
        /// Get the string representing the preferred video codec the user selected.
        /// </summary>
        public string PreferredVideoCodec
        {
            get
            {
                if (PreferredVideoCodec_Custom.IsChecked.GetValueOrDefault(false))
                {
                    return CustomPreferredVideoCodec.Text;
                }
                else if (PreferredVideoCodec_H264.IsChecked.GetValueOrDefault(false))
                {
                    return "H264";
                }
                else if (PreferredVideoCodec_VP8.IsChecked.GetValueOrDefault(false))
                {
                    return "VP8";
                }
                return string.Empty;
            }
        }

        public ObservableCollection<VideoCaptureDeviceInfo> VideoCaptureDevices { get; private set; }
            = new ObservableCollection<VideoCaptureDeviceInfo>();

        public VideoCaptureDeviceInfo SelectedVideoCaptureDevice
        {
            get
            {
                var deviceIndex = VideoCaptureDeviceList.SelectedIndex;
                if ((deviceIndex < 0) || (deviceIndex >= VideoCaptureDevices.Count))
                {
                    return null;
                }
                return VideoCaptureDevices[deviceIndex];
            }
        }

        public VideoProfileKind SelectedVideoProfileKind
        {
            get
            {
                var videoProfileKindIndex = KnownVideoProfileKindComboBox.SelectedIndex;
                if (videoProfileKindIndex < 0)
                {
                    return VideoProfileKind.Unspecified;
                }
                return (VideoProfileKind)Enum.GetValues(typeof(VideoProfileKind)).GetValue(videoProfileKindIndex);
            }
        }

        public ObservableCollection<MediaCaptureVideoProfile> VideoProfiles { get; private set; }
            = new ObservableCollection<MediaCaptureVideoProfile>();

        public MediaCaptureVideoProfile SelectedVideoProfile
        {
            get
            {
                var profileIndex = VideoProfileComboBox.SelectedIndex;
                if ((profileIndex < 0) || (profileIndex >= VideoProfiles.Count))
                {
                    return null;
                }
                return VideoProfiles[profileIndex];
            }
        }

        public ObservableCollection<MediaCaptureVideoProfileMediaDescription> RecordMediaDescs { get; private set; }
            = new ObservableCollection<MediaCaptureVideoProfileMediaDescription>();

        public MediaCaptureVideoProfileMediaDescription SelectedRecordMediaDesc
        {
            get
            {
                var descIndex = RecordMediaDescList.SelectedIndex;
                if ((descIndex < 0) || (descIndex >= RecordMediaDescs.Count))
                {
                    return null;
                }
                return RecordMediaDescs[descIndex];

            }
        }

        public ObservableCollection<VideoCaptureFormat> VideoCaptureFormats { get; private set; }
            = new ObservableCollection<VideoCaptureFormat>();

        public VideoCaptureFormat? SelectedVideoCaptureFormat
        {
            get
            {
                var profileIndex = VideoCaptureFormatList.SelectedIndex;
                if ((profileIndex < 0) || (profileIndex >= VideoCaptureFormats.Count))
                {
                    return null;
                }
                return VideoCaptureFormats[profileIndex];
            }
        }

        public ObservableCollection<NavLink> NavLinks { get; }
            = new ObservableCollection<NavLink>();

        public ObservableCollection<ChatChannel> ChatChannels { get; private set; }
            = new ObservableCollection<ChatChannel>();

        public ChatChannel SelectedChatChannel
        {
            get
            {
                var chatIndex = chatList.SelectedIndex;
                if ((chatIndex < 0) || (chatIndex >= ChatChannels.Count))
                {
                    return null;
                }
                return ChatChannels[chatIndex];
            }
        }

        private VideoBridge localVideoBridge = new VideoBridge(3);
        private VideoBridge remoteVideoBridge = new VideoBridge(5);

        public static string GetDeviceName()
        {
            return Environment.MachineName;
        }

        public MainPage()
        {
            this.InitializeComponent();
            muteLocalVideoStroke.Visibility = Visibility.Collapsed;
            muteLocalAudioStroke.Visibility = Visibility.Collapsed;

            // This will be enabled once the signaling is initialized and ready to send data
            createOfferButton.IsEnabled = false;

            // Those are called during InitializeComponent() but before the controls are initialized (!).
            // Force-call again to actually initialize the panels correctly.
            PreferredAudioCodecChecked(null, null);
            PreferredVideoCodecChecked(null, null);

            RestoreParams();

            dssSignaler.OnMessage += DssSignaler_OnMessage;
            dssSignaler.OnFailure += DssSignaler_OnFailure;
            dssSignaler.OnPollingDone += DssSignaler_OnPollingDone;

            localVideoStatsTimer.Tick += OnLocalVideoStatsTimerTicked;
            remoteVideoStatsTimer.Tick += OnRemoteVideoStatsTimerTicked;

            _peerConnection = new PeerConnection();
            _peerConnection.Connected += OnPeerConnected;
            _peerConnection.DataChannelAdded += OnDataChannelAdded;
            _peerConnection.DataChannelRemoved += OnDataChannelRemoved;
            _peerConnection.LocalSdpReadytoSend += OnLocalSdpReadyToSend;
            _peerConnection.IceCandidateReadytoSend += OnIceCandidateReadyToSend;
            _peerConnection.IceStateChanged += OnIceStateChanged;
            _peerConnection.IceGatheringStateChanged += OnIceGatheringStateChanged;
            _peerConnection.RenegotiationNeeded += OnPeerRenegotiationNeeded;
            _peerConnection.TrackAdded += Peer_RemoteTrackAdded;
            _peerConnection.TrackRemoved += Peer_RemoteTrackRemoved;
            _peerConnection.I420ARemoteVideoFrameReady += Peer_RemoteI420AFrameReady;
            _peerConnection.LocalAudioFrameReady += Peer_LocalAudioFrameReady;
            _peerConnection.RemoteAudioFrameReady += Peer_RemoteAudioFrameReady;

            //Window.Current.Closed += Shutdown; // doesn't work

            // Start polling automatically.
            PollDssButtonClicked(this, null);

            this.Loaded += OnLoaded;
            Application.Current.Suspending += App_Suspending;
            Application.Current.Resuming += App_Resuming;
        }

        private void App_Suspending(object sender, SuspendingEventArgs e)
        {
            // Save local and remote peer IDs for next launch for convenience
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            localSettings.Values["DssServerAddress"] = dssServer.Text;
            localSettings.Values["LocalPeerID"] = localPeerUidTextBox.Text;
            localSettings.Values["RemotePeerID"] = remotePeerUidTextBox.Text;
            localSettings.Values["PreferredAudioCodec"] = PreferredAudioCodec;
            localSettings.Values["PreferredAudioCodecExtraParamsLocal"] = PreferredAudioCodecExtraParamsLocalTextBox.Text;
            localSettings.Values["PreferredAudioCodecExtraParamsRemote"] = PreferredAudioCodecExtraParamsRemoteTextBox.Text;
            localSettings.Values["PreferredAudioCodec_Custom"] = PreferredAudioCodec_Custom.IsChecked.GetValueOrDefault() ? CustomPreferredAudioCodec.Text : "";
            localSettings.Values["PreferredVideoCodec"] = PreferredVideoCodec;
            localSettings.Values["PreferredVideoCodecExtraParamsLocal"] = PreferredVideoCodecExtraParamsLocalTextBox.Text;
            localSettings.Values["PreferredVideoCodecExtraParamsRemote"] = PreferredVideoCodecExtraParamsRemoteTextBox.Text;
            localSettings.Values["PreferredVideoCodec_Custom"] = PreferredVideoCodec_Custom.IsChecked.GetValueOrDefault() ? CustomPreferredVideoCodec.Text : "";
        }

        private void App_Resuming(object sender, object e)
        {
            RestoreParams();
        }

        private static bool IsFirstInstance()
        {
            var firstInstance = AppInstance.FindOrRegisterInstanceForKey("{44CD414E-B604-482E-8CFD-A9E09076CABD}");
            return firstInstance.IsCurrentInstance;
        }

        private void RestoreParams()
        {
            // Uncomment these lines if you want to connect a HoloLens (or any non-x64 device) to a
            // x64 PC.
            //var arch = System.Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE");
            //if (arch == "AMD64")
            //{
            //    localPeerUidTextBox.Text = "Pc";
            //    remotePeerUidTextBox.Text = "Device";
            //}
            //else
            //{
            //    localPeerUidTextBox.Text = "Device";
            //    remotePeerUidTextBox.Text = "Pc";
            //}

            // Get server address and peer ID from local settings if available.
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            if (localSettings.Values.TryGetValue("DssServerAddress", out object dssServerAddress))
            {
                if (dssServerAddress is string str)
                {
                    dssServer.Text = str;
                }
            }

            if (localSettings.Values.TryGetValue("LocalPeerID", out object localObj))
            {
                if (localObj is string str)
                {
                    localPeerUidTextBox.Text = str;
                }
            }
            if (localPeerUidTextBox.Text.Length == 0)
            {
                localPeerUidTextBox.Text = GetDeviceName();
            }
            if (localSettings.Values.TryGetValue("RemotePeerID", out object remoteObj))
            {
                if (remoteObj is string str)
                {
                    remotePeerUidTextBox.Text = str;
                }
            }

            if (!IsFirstInstance())
            {
                // Swap the peer IDs. This way two instances launched on the same machine connect
                // to each other by default.
                var tmp = localPeerUidTextBox.Text;
                localPeerUidTextBox.Text = remotePeerUidTextBox.Text;
                remotePeerUidTextBox.Text = tmp;
            }

            if (localSettings.Values.TryGetValue("PreferredAudioCodec", out object preferredAudioObj))
            {
                if (preferredAudioObj is string str)
                {
                    switch(str)
                    {
                        case "":
                        {
                            PreferredAudioCodec_Default.IsChecked = true;
                            break;
                        }
                        case "opus":
                        {
                            PreferredAudioCodec_OPUS.IsChecked = true;
                            break;
                        }
                        default:
                        {
                            PreferredAudioCodec_Custom.IsChecked = true;
                            CustomPreferredAudioCodec.Text = str;
                            break;
                        }
                    }
                }
            }
            if (localSettings.Values.TryGetValue("PreferredAudioCodecExtraParamsLocal", out object preferredAudioParamsLocalObj))
            {
                if (preferredAudioParamsLocalObj is string str)
                {
                    PreferredAudioCodecExtraParamsLocalTextBox.Text = str;
                }
            }
            if (localSettings.Values.TryGetValue("PreferredAudioCodecExtraParamsRemote", out object preferredAudioParamsRemoteObj))
            {
                if (preferredAudioParamsRemoteObj is string str)
                {
                    PreferredAudioCodecExtraParamsRemoteTextBox.Text = str;
                }
            }

            if (localSettings.Values.TryGetValue("PreferredVideoCodec", out object preferredVideoObj))
            {
                if (preferredVideoObj is string str)
                {
                    switch (str)
                    {
                        case "":
                        {
                            PreferredVideoCodec_Default.IsChecked = true;
                            break;
                        }
                        case "H264":
                        {
                            PreferredVideoCodec_H264.IsChecked = true;
                            break;
                        }
                        case "VP8":
                        {
                            PreferredVideoCodec_VP8.IsChecked = true;
                            break;
                        }
                        default:
                        {
                            PreferredVideoCodec_Custom.IsChecked = true;
                            CustomPreferredVideoCodec.Text = str;
                            break;
                        }
                    }
                }
            }
            if (localSettings.Values.TryGetValue("PreferredVideoCodecExtraParamsLocal", out object preferredVideoParamsLocalObj))
            {
                if (preferredVideoParamsLocalObj is string str)
                {
                    PreferredVideoCodecExtraParamsLocalTextBox.Text = str;
                }
            }
            if (localSettings.Values.TryGetValue("PreferredVideoCodecExtraParamsRemote", out object preferredVideoParamsRemoteObj))
            {
                if (preferredVideoParamsRemoteObj is string str)
                {
                    PreferredVideoCodecExtraParamsRemoteTextBox.Text = str;
                }
            }
        }

        private void OnDataChannelAdded(DataChannel channel)
        {
            LogMessage($"Added data channel '{channel.Label}' (#{channel.ID}).");
            RunOnMainThread(() => {
                var chat = new ChatChannel { DataChannel = channel };
                ChatChannels.Add(chat);
                if (ChatChannels.Count == 1)
                {
                    chatList.SelectedIndex = 0;
                }
                channel.MessageReceived += (byte[] message) =>
                    RunOnMainThread(() => {
                        string text = System.Text.Encoding.UTF8.GetString(message);
                        ChatMessageReceived(chat, text);
                    });
            });
        }

        private void OnDataChannelRemoved(DataChannel channel)
        {
            LogMessage($"Removed data channel '{channel.Label}' (#{channel.ID}).");
            RunOnMainThread(() => {
                var chat = ChatChannels.Where((c) => c.DataChannel == channel).First();
                ChatChannels.Remove(chat);
            });
        }

        private void OnLocalSdpReadyToSend(string type, string sdp)
        {
            var message = new NodeDssSignaler.Message
            {
                MessageType = NodeDssSignaler.Message.WireMessageTypeFromString(type),
                Data = sdp,
                IceDataSeparator = "|"
            };
            dssSignaler.SendMessageAsync(message);
        }

        private void OnIceCandidateReadyToSend(string candidate, int sdpMlineindex, string sdpMid)
        {
            var message = new NodeDssSignaler.Message
            {
                MessageType = NodeDssSignaler.Message.WireMessageType.Ice,
                Data = $"{candidate}|{sdpMlineindex}|{sdpMid}", // see DssSignaler_OnMessage
                IceDataSeparator = "|"
            };
            dssSignaler.SendMessageAsync(message);
        }

        private void OnIceStateChanged(IceConnectionState newState)
        {
            RunOnMainThread(() => {
                LogMessage($"ICE state changed to {newState}.");
                iceStateText.Text = newState.ToString();
            });
        }

        private void OnIceGatheringStateChanged(IceGatheringState newState)
        {
            RunOnMainThread(() => {
                LogMessage($"ICE gathering changed to {newState}.");
                iceGatheringStateText.Text = newState.ToString();
            });
        }

        private void OnPeerRenegotiationNeeded()
        {
            // If already connected, update the connection on the fly.
            // If not, wait for user action and don't automatically connect.
            if (_peerConnection.IsConnected && _renegotiationOfferEnabled)
            {
                _peerConnection.CreateOffer();
            }
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            LogMessage("Initializing the WebRTC native plugin...");

            // Populate the combo box with the VideoProfileKind enum
            {
                var values = Enum.GetValues(typeof(VideoProfileKind));
                KnownVideoProfileKindComboBox.ItemsSource = values.Cast<VideoProfileKind>();
                KnownVideoProfileKindComboBox.SelectedIndex = Array.IndexOf(values, VideoProfileKind.Unspecified);
            }

            VideoCaptureDeviceList.SelectionChanged += VideoCaptureDeviceList_SelectionChanged;
            KnownVideoProfileKindComboBox.SelectionChanged += KnownVideoProfileKindComboBox_SelectionChanged;
            VideoProfileComboBox.SelectionChanged += VideoProfileComboBox_SelectionChanged;

            //localVideo.TransportControls = localVideoControls;

            PluginInitialized = false;

            // Ensure that the UWP app was authorized to capture audio (cap:microphone)
            // and video (cap:webcam), otherwise the native plugin will fail.
            try
            {
                MediaCapture mediaAccessRequester = new MediaCapture();
                var mediaSettings = new MediaCaptureInitializationSettings
                {
                    AudioDeviceId = "",
                    VideoDeviceId = "",
                    StreamingCaptureMode = StreamingCaptureMode.AudioAndVideo,
                    PhotoCaptureSource = PhotoCaptureSource.VideoPreview
                };
                await mediaAccessRequester.InitializeAsync(mediaSettings);
            }
            catch (UnauthorizedAccessException uae)
            {
                LogMessage("Access to A/V denied, check app permissions:: " + uae.Message);
                return;
            }
            catch (Exception ex)
            {
                LogMessage("Failed to initialize A/V with unknown exception: " + ex.Message);
                return;
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
            {
                var devices = await PeerConnection.GetVideoCaptureDevicesAsync();
                VideoCaptureDevices.Clear();
                List<VideoCaptureDeviceInfo> vcds = new List<VideoCaptureDeviceInfo>(devices.Count);
                foreach (var device in devices)
                {
                    LogMessage($"VCD id={device.id} name={device.name}");
                    VideoCaptureDevices.Add(new VideoCaptureDeviceInfo()
                    {
                        Id = device.id,
                        DisplayName = device.name,
                        Symbol = Symbol.Video
                    });
                }

                // Select first entry by default
                if (VideoCaptureDevices.Count > 0)
                {
                    VideoCaptureDeviceList.SelectedIndex = 0;
                }
            }

            // Initialize the native peer connection object
            try
            {
                var config = new PeerConnectionConfiguration();
                config.IceServers.Add(new IceServer { Urls = { "stun:" + stunServer.Text } });
                config.SdpSemantic = (sdpSemanticUnifiedPlan.IsChecked.GetValueOrDefault(true)
                    ? SdpSemantic.UnifiedPlan : SdpSemantic.PlanB);
                await _peerConnection.InitializeAsync(config);
            }
            catch(Exception ex)
            {
                LogMessage($"WebRTC native plugin init failed: {ex.Message}");
                return;
            }

            PluginInitialized = true;
            LogMessage("WebRTC native plugin initialized.");

            // It is CRUCIAL to add any data channel BEFORE the SDP offer is sent, if data channels are
            // to be used at all. Otherwise the SCTP will not be negotiated, and then all channels will
            // stay forever in the kConnecting state.
            // https://stackoverflow.com/questions/43788872/how-are-data-channels-negotiated-between-two-peers-with-webrtc
            var newDataChannel = await _peerConnection.AddDataChannelAsync(ChatChannelID, "chat", true, true);
            chatInputBox.IsEnabled = true;
            chatSendButton.IsEnabled = true;

            startLocalMedia.IsEnabled = true;

            localVideoPlayer.CurrentStateChanged += OnMediaStateChanged;
            localVideoPlayer.MediaOpened += OnMediaOpened;
            localVideoPlayer.MediaFailed += OnMediaFailed;
            localVideoPlayer.MediaEnded += OnMediaEnded;
            localVideoPlayer.RealTimePlayback = true;
            localVideoPlayer.AutoPlay = false;

            remoteVideoPlayer.CurrentStateChanged += OnMediaStateChanged;
            remoteVideoPlayer.MediaOpened += OnMediaOpened;
            remoteVideoPlayer.MediaFailed += OnMediaFailed;
            remoteVideoPlayer.MediaEnded += OnMediaEnded;
            remoteVideoPlayer.RealTimePlayback = true;
            remoteVideoPlayer.AutoPlay = false;

            // Bind the XAML UI control (localVideo) to the MediaFoundation rendering pipeline (localVideoPlayer)
            // so that the former can render in the UI the video frames produced in the background by the later.
            localVideo.SetMediaPlayer(localVideoPlayer);
            remoteVideo.SetMediaPlayer(remoteVideoPlayer);
        }

        private void PreferredAudioCodecChecked(object sender, RoutedEventArgs args)
        {
            // Ignore calls during startup, before components are initialized
            if (PreferredAudioCodec_Custom == null)
            {
                return;
            }

            if (PreferredAudioCodec_Custom.IsChecked.GetValueOrDefault(false))
            {
                CustomPreferredAudioCodecHelpText.Visibility = Visibility.Visible;
                CustomPreferredAudioCodec.Visibility = Visibility.Visible;
            }
            else
            {
                CustomPreferredAudioCodecHelpText.Visibility = Visibility.Collapsed;
                CustomPreferredAudioCodec.Visibility = Visibility.Collapsed;
            }
        }

        private void PreferredVideoCodecChecked(object sender, RoutedEventArgs args)
        {
            // Ignore calls during startup, before components are initialized
            if (PreferredVideoCodec_Custom == null)
            {
                return;
            }

            if (PreferredVideoCodec_Custom.IsChecked.GetValueOrDefault(false))
            {
                CustomPreferredVideoCodecHelpText.Visibility = Visibility.Visible;
                CustomPreferredVideoCodec.Visibility = Visibility.Visible;
            }
            else
            {
                CustomPreferredVideoCodecHelpText.Visibility = Visibility.Collapsed;
                CustomPreferredVideoCodec.Visibility = Visibility.Collapsed;
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

            // Get the video capture device selected by the user
            var deviceIndex = VideoCaptureDeviceList.SelectedIndex;
            if (deviceIndex < 0)
            {
                return;
            }
            var device = VideoCaptureDevices[deviceIndex];

            // Ensure that the video capture device actually supports video profiles
            if (MediaCapture.IsVideoProfileSupported(device.Id))
            {
                // Get the kind of known video profile selected by the user
                var videoProfileKindIndex = KnownVideoProfileKindComboBox.SelectedIndex;
                if (videoProfileKindIndex < 0)
                {
                    return;
                }
                var videoProfileKind = (VideoProfileKind)Enum.GetValues(typeof(VideoProfileKind)).GetValue(videoProfileKindIndex);

                // List all video profiles for the select device (and kind, if any specified)
                IReadOnlyList<MediaCaptureVideoProfile> profiles;
                if (videoProfileKind == VideoProfileKind.Unspecified)
                {
                    profiles = MediaCapture.FindAllVideoProfiles(device.Id);
                }
                else
                {
                    profiles = MediaCapture.FindKnownVideoProfiles(device.Id, (KnownVideoProfile)(videoProfileKind - 1));
                }
                foreach (var profile in profiles)
                {
                    VideoProfiles.Add(profile);
                }
                if (profiles.Any())
                {
                    VideoProfileComboBox.SelectedIndex = 0;
                }
            }
            else
            {
                // Device doesn't support video profiles; fall back on flat list of capture formats.
                List<VideoCaptureFormat> formatsList = await PeerConnection.GetVideoCaptureFormatsAsync(device.Id);
                foreach (var format in formatsList)
                {
                    VideoCaptureFormats.Add(format);
                }

                // Default to first format, so that user can start the video capture even without selecting
                // explicitly a format in a different application tab.
                if (formatsList.Count > 0)
                {
                    VideoCaptureFormatList.SelectedIndex = 0;
                }
            }
        }

        private void VideoCaptureDeviceList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Get the video capture device selected by the user
            var deviceIndex = VideoCaptureDeviceList.SelectedIndex;
            if (deviceIndex < 0)
            {
                return;
            }
            var device = VideoCaptureDevices[deviceIndex];

            // Select a default video profile kind
            var values = Enum.GetValues(typeof(VideoProfileKind));
            if (MediaCapture.IsVideoProfileSupported(device.Id))
            {
                var defaultProfile = VideoProfileKind.VideoConferencing;
                var profiles = MediaCapture.FindKnownVideoProfiles(device.Id, (KnownVideoProfile)(defaultProfile - 1));
                if (!profiles.Any())
                {
                    // Fall back to VideoRecording if VideoConferencing has no profiles (e.g. HoloLens).
                    defaultProfile = VideoProfileKind.VideoRecording;
                }
                KnownVideoProfileKindComboBox.SelectedIndex = Array.IndexOf(values, defaultProfile);

                KnownVideoProfileKindComboBox.IsEnabled = true; //< TODO - Use binding
                VideoProfileComboBox.IsEnabled = true;
                RecordMediaDescList.IsEnabled = true;
                VideoCaptureFormatList.IsEnabled = false;
            }
            else
            {
                KnownVideoProfileKindComboBox.SelectedIndex = Array.IndexOf(values, VideoProfileKind.Unspecified);
                KnownVideoProfileKindComboBox.IsEnabled = false;
                VideoProfileComboBox.IsEnabled = false;
                RecordMediaDescList.IsEnabled = false;
                VideoCaptureFormatList.IsEnabled = true;
            }

            UpdateVideoProfiles();
        }

        private void KnownVideoProfileKindComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateVideoProfiles();
        }

        private void VideoProfileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RecordMediaDescs.Clear();

            var profile = SelectedVideoProfile;
            if (profile == null)
            {
                return;
            }

            foreach (var desc in profile.SupportedRecordMediaDescription)
            {
                RecordMediaDescs.Add(desc);
            }
        }

        //private void Shutdown(object sender, Windows.UI.Core.CoreWindowEventArgs e)
        //{
        //    webRTCNativePlugin.Uninitialize();
        //}

        /// <summary>
        /// Callback invoked when the peer connection is established.
        /// </summary>
        private void OnPeerConnected()
        {
            RunOnMainThread(() => {
                sessionStatusText.Text = "(session joined)";
                chatTextBox.IsEnabled = true;

                // Reset "Create Offer" button, and re-enable if signaling is available
                createOfferButton.Content = "Create Offer";
                createOfferButton.IsEnabled = isDssPolling;
            });
        }

        private void DssSignaler_OnMessage(NodeDssSignaler.Message message)
        {
            Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                // Ensure that the filtering values are up to date before passing the message on.
                UpdateCodecFilters();
            }).AsTask().ContinueWith(async _ =>
            {
                switch (message.MessageType)
                {
                    case NodeDssSignaler.Message.WireMessageType.Offer:
                        await _peerConnection.SetRemoteDescriptionAsync("offer", message.Data);
                        // If we get an offer, we immediately send an answer back once the offer is applied
                        _peerConnection.CreateAnswer();
                        break;

                    case NodeDssSignaler.Message.WireMessageType.Answer:
                        _ = _peerConnection.SetRemoteDescriptionAsync("answer", message.Data);
                        break;

                    case NodeDssSignaler.Message.WireMessageType.Ice:
                        // TODO - This is NodeDSS-specific
                        // this "parts" protocol is defined above, in OnIceCandiateReadyToSend listener
                        var parts = message.Data.Split(new string[] { message.IceDataSeparator }, StringSplitOptions.RemoveEmptyEntries);
                        // Note the inverted arguments; candidate is last here, but first in OnIceCandiateReadyToSend
                        _peerConnection.AddIceCandidate(parts[2], int.Parse(parts[1]), parts[0]);
                        break;

                    default:
                        throw new InvalidOperationException($"Unhandled signaler message type '{message.MessageType}'");
                }
            }, TaskScheduler.Default);
        }

        private void DssSignaler_OnFailure(Exception e)
        {
            RunOnMainThread(() => {
                LogMessage($"DSS polling failed: {e.Message}");
                // TODO - differentiate between StartPollingAsync() failure and SendMessageAsync() ones!
                if (dssSignaler.IsPolling)
                {
                    LogMessage($"Cancelling polling DSS server...");
                    //pollDssButton.IsEnabled = false; // will be re-enabled when cancellation is completed, see DssSignaler_OnPollingDone
                    pollDssButton.Content = "Start polling";
                    dssSignaler.StopPollingAsync();
                }
            });
        }

        private void DssSignaler_OnPollingDone()
        {
            RunOnMainThread(() => {
                isDssPolling = false;
                pollDssButton.IsEnabled = true;
                LogMessage($"Polling DSS server stopped.");
            });
        }

        /// <summary>
        /// Log a message to the debugger and to the Debug tab in the UI.
        /// This can be called from any thread.
        /// </summary>
        /// <param name="message">The message to log.</param>
        private void LogMessage(string message)
        {
            Debugger.Log(4, "TestAppUWP", message);
            RunOnMainThread(() => {
                debugMessages.Text += message + "\n";
            });
        }

        /// <summary>
        /// Utility to run some random workload on the main UI thread.
        /// </summary>
        /// <param name="handler">The workload to run.</param>
        private void RunOnMainThread(Windows.UI.Core.DispatchedHandler handler)
        {
            if (Dispatcher.HasThreadAccess)
            {
                handler.Invoke();
            }
            else
            {
                // Note: use a discard "_" to silence CS4014 warning; we don't need to await the result here
                _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, handler);
            }
        }

        /// <summary>
        /// Utility to run some random workload on a worker thread (not the main UI thread).
        /// </summary>
        /// <param name="handler">The workload to run.</param>
        private Task RunOnWorkerThread(Action handler)
        {
            if (Dispatcher.HasThreadAccess)
            {
                return Task.Run(handler);
            }
            else
            {
                handler.Invoke();
                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// Create a new 30-fps NV12-encoded video source for the specified video size.
        /// </summary>
        /// <param name="width">The width of the video in pixels.</param>
        /// <param name="height">The height of the video in pixels.</param>
        /// <returns>The newly created video source.</returns>
        private MediaStreamSource CreateVideoStreamSource(uint width, uint height, uint framerate)
        {
            if (width == 0)
            {
                throw new ArgumentException("Invalid zero width for video stream source.", "width");
            }
            if (height == 0)
            {
                throw new ArgumentException("Invalid zero height for video stream source.", "height");
            }

            // Note: IYUV and I420 have same memory layout (though different FOURCC)
            // https://docs.microsoft.com/en-us/windows/desktop/medfound/video-subtype-guids
            var videoProperties = VideoEncodingProperties.CreateUncompressed(MediaEncodingSubtypes.Iyuv, width, height);
            var videoStreamDesc = new VideoStreamDescriptor(videoProperties);
            videoStreamDesc.EncodingProperties.FrameRate.Numerator = framerate;
            videoStreamDesc.EncodingProperties.FrameRate.Denominator = 1;
            videoStreamDesc.EncodingProperties.Bitrate = (framerate * width * height * 12); // NV12=12bpp
            var videoStreamSource = new MediaStreamSource(videoStreamDesc);
            videoStreamSource.BufferTime = TimeSpan.Zero; // TODO : playback breaks if buffering, need to investigate
            videoStreamSource.Starting += OnMediaStreamSourceStarting;
            videoStreamSource.Closed += OnMediaStreamSourceClosed;
            videoStreamSource.Paused += OnMediaStreamSourcePaused;
            videoStreamSource.SampleRequested += OnMediaStreamSourceRequested;
            videoStreamSource.IsLive = true; // Enables optimizations for live sources
            videoStreamSource.CanSeek = false; // Cannot seek live WebRTC video stream
            return videoStreamSource;
        }

        private void OnMediaStreamSourceStarting(MediaStreamSource sender, MediaStreamSourceStartingEventArgs args)
        {
            Debug.Assert(Dispatcher.HasThreadAccess == false);
            if (sender == localVideoSource)
            {
                LogMessage("Starting local A/V stream...");
                //webRTCNativePlugin.Peer.AddStream(audio: true, video_device_index: _video_device_index);
            }
            else if (sender == remoteVideoSource)
            {
                LogMessage("Starting remote A/V stream...");
            }
            args.Request.SetActualStartPosition(TimeSpan.Zero);
        }

        private void OnMediaStreamSourceClosed(MediaStreamSource sender, MediaStreamSourceClosedEventArgs args)
        {
            Debug.Assert(Dispatcher.HasThreadAccess == false);
            if (sender == localVideoSource)
            {
                LogMessage("Closing local A/V stream...");
                //webRTCNativePlugin.Peer.RemoveStream(audio: true, video: true);
            }
            else if (sender == remoteVideoSource)
            {
                LogMessage("Closing remote A/V stream...");
            }
        }

        private void OnMediaStreamSourcePaused(MediaStreamSource sender, object args)
        {
            Debug.Assert(Dispatcher.HasThreadAccess == false);
            if (sender == localVideoSource)
            {
                LogMessage("Pausing local A/V stream...");
                //webRTCNativePlugin.Peer.RemoveStream(audio: true, video: true); // NOT YET!
            }
            else if (sender == remoteVideoSource)
            {
                LogMessage("Pausing remote A/V stream...");
            }
        }

        /// <summary>
        /// Callback from the Media Foundation pipeline when a new video frame is needed.
        /// </summary>
        /// <param name="sender">The stream source requesting a new sample.</param>
        /// <param name="args">The sample request to fullfil.</param>
        private void OnMediaStreamSourceRequested(MediaStreamSource sender, MediaStreamSourceSampleRequestedEventArgs args)
        {
            VideoBridge videoBridge;
            if (sender == localVideoSource)
                videoBridge = localVideoBridge;
            else if (sender == remoteVideoSource)
                videoBridge = remoteVideoBridge;
            else
                return;
            videoBridge.TryServeVideoFrame(args);
        }

        private void OnMediaStateChanged(Windows.Media.Playback.MediaPlayer sender, object args)
        {
            RunOnMainThread(() => {
                if (sender == localVideoPlayer)
                {
                    localVideoStateText.Text = $"State: {sender.PlaybackSession.PlaybackState}";
                }
                else if (sender == remoteVideoPlayer)
                {
                    remoteVideoStateText.Text = $"State: {sender.PlaybackSession.PlaybackState}";
                }
            });
        }

        /// <summary>
        /// Callback on Media Foundation pipeline media successfully opened and
        /// ready to be played in a local media player.
        /// </summary>
        /// <param name="sender">The <see xref="MediaPlayer"/> source object owning the media.</param>
        /// <param name="args">(unused)</param>
        private void OnMediaOpened(MediaPlayer sender, object args)
        {
            // Now it is safe to call Play() on the MediaElement
            if (sender == localVideoPlayer)
            {
                RunOnMainThread(() => {
                    localVideo.MediaPlayer.Play();
                });
            }
            else if (sender == remoteVideoPlayer)
            {
                RunOnMainThread(() => {
                    remoteVideo.MediaPlayer.Play();
                });
            }
        }

        /// <summary>
        /// Callback on Media Foundation pipeline media failed to open or to continue playback.
        /// </summary>
        /// <param name="sender">The <see xref="MediaPlayer"/> source object owning the media.</param>
        /// <param name="args">(unused)</param>
        private void OnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
        {
            LogMessage($"MediaElement reported an error: \"{args.ErrorMessage}\" (\"{args.ExtendedErrorCode.Message}\")");
        }

        /// <summary>
        /// Callback on Media Foundation pipeline media ended playback.
        /// </summary>
        /// <param name="sender">The <see xref="MediaPlayer"/> source object owning the media.</param>
        /// <param name="args">(unused)</param>
        /// <remarks>This appears to never be called for live sources.</remarks>
        private void OnMediaEnded(MediaPlayer sender, object args)
        {
            RunOnMainThread(() => {
                LogMessage("Local MediaElement video playback ended.");
                //StopLocalMedia();
                sender.Pause();
                sender.Source = null;
                if (sender == localVideoPlayer)
                {
                    //< TODO - This should never happen. But what to do with
                    //         local channels if it happens?
                    lock (_isLocalVideoPlayingLock)
                    {
                        _isLocalVideoPlaying = false;
                    }
                }
            });
        }

        /// <summary>
        /// Stop playback of the local video from the local webcam, and remove the local
        /// audio and video tracks from the peer connection.
        /// This is called on the UI thread.
        /// </summary>
        private async void StopLocalMedia()
        {
            lock (_isLocalVideoPlayingLock)
            {
                if (_isLocalVideoPlaying)
                {
                    localVideo.MediaPlayer.Pause();
                    localVideo.MediaPlayer.Source = null;
                    localVideo.SetMediaPlayer(null);
                    localVideoSource.NotifyError(MediaStreamSourceErrorStatus.Other);
                    localVideoSource = null;
                    localMediaSource.Dispose();
                    localMediaSource = null;
                    _isLocalVideoPlaying = false;
                    _localVideoTrack.I420AVideoFrameReady -= LocalVideoTrack_I420AFrameReady;
                }
            }

            // Avoid deadlock in audio processing stack, as this call is delegated to the WebRTC
            // signaling thread (and will block the caller thread), and audio processing will
            // delegate to the UI thread for UWP operations (and will block the signaling thread).
            await RunOnWorkerThread(() => {
                lock (_isLocalVideoPlayingLock)
                {
                    _peerConnection.RemoveLocalAudioTrack();
                    _peerConnection.RemoveLocalVideoTrack(_localVideoTrack); // TODO - this doesn't unregister the callbacks...
                    _renegotiationOfferEnabled = true;
                    if (_peerConnection.IsConnected)
                    {
                        _peerConnection.CreateOffer();
                    }
                    _localVideoTrack.Dispose();
                    _localVideoTrack = null;
                }
            });

            startLocalMedia.IsEnabled = true;
        }

        /// <summary>
        /// Callback on remote media (audio or video) track added.
        /// Currently does nothing, as starting the media pipeline is done lazily in the
        /// per-frame callback.
        /// </summary>
        /// <param name="trackKind">The kind of media track added (audio or video only).</param>
        /// <seealso cref="Peer_RemoteI420AFrameReady"/>
        private void Peer_RemoteTrackAdded(PeerConnection.TrackKind trackKind)
        {
            LogMessage($"Added remote {trackKind} track.");
            RunOnMainThread(() =>
            {
                remoteVideoStatsTimer.Interval = TimeSpan.FromSeconds(1.0);
                remoteVideoStatsTimer.Start();
            });
        }

        /// <summary>
        /// Callback on remote media (audio or video) track removed.
        /// </summary>
        /// <param name="trackKind">The kind of media track added (audio or video only).</param>
        private void Peer_RemoteTrackRemoved(PeerConnection.TrackKind trackKind)
        {
            LogMessage($"Removed remote {trackKind} track.");

            if (trackKind == PeerConnection.TrackKind.Video)
            {
                // Just double-check that the remote video track is indeed playing
                // before scheduling a task to stop it. Currently the remote video
                // playback is exclusively controlled by the remote track being present
                // or not, so these should be always in sync.
                lock (_isRemoteVideoPlayingLock)
                {
                    if (_isRemoteVideoPlaying)
                    {
                        // Schedule on the main UI thread to access STA objects.
                        RunOnMainThread(() => {
                            remoteVideoStatsTimer.Stop();
                            // Check that the remote video is still playing.
                            // This ensures that rapid calls to add/remove the video track
                            // are serialized, and an earlier remove call doesn't remove the
                            // track added by a later call, due to delays in scheduling the task.
                            lock (_isRemoteVideoPlayingLock)
                            {
                                if (_isRemoteVideoPlaying)
                                {
                                    remoteVideo.MediaPlayer.Pause();
                                    _isRemoteVideoPlaying = false;
                                }
                            }
                        });
                    }
                }
            }
        }

        /// <summary>
        /// Callback on video frame received from the local video capture device,
        /// for local rendering before (or in parallel of) being sent to the remote peer.
        /// </summary>
        /// <param name="frame">The newly captured video frame.</param>
        private void LocalVideoTrack_I420AFrameReady(I420AVideoFrame frame)
        {
            localVideoBridge.HandleIncomingVideoFrame(frame);
        }

        /// <summary>
        /// Callback on video frame received from the remote peer, for local rendering
        /// (or any other use).
        /// </summary>
        /// <param name="frame">The newly received video frame.</param>
        private void Peer_RemoteI420AFrameReady(I420AVideoFrame frame)
        {
            // Lazily start the remote video media player when receiving
            // the first video frame from the remote peer. Currently there
            // is no exposed API to tell once connected that the remote peer
            // will be sending some video track.
            //< TODO - See if we can add an API to enumerate the remote channels,
            //         or an On(Audio|Video|Data)Channel(Added|Removed) event?
            bool needNewSource = false;
            uint width = frame.width;
            uint height = frame.height;
            lock (_isRemoteVideoPlayingLock)
            {
                if (!_isRemoteVideoPlaying)
                {
                    _isRemoteVideoPlaying = true;
                    needNewSource = true;
                }
                else if ((width != _remoteVideoWidth) || (height != _remoteVideoHeight))
                {
                    _remoteVideoWidth = width;
                    _remoteVideoHeight = height;
                    needNewSource = true;
                }
            }
            if (needNewSource)
            {
                // We don't know the remote video framerate yet, so use a default.
                uint framerate = 30;
                RunOnMainThread(() => {
                    remoteVideoPlayer.Pause();
                    remoteVideoPlayer.Source = null;
                    remoteVideo.SetMediaPlayer(null);
                    remoteVideoSource?.NotifyError(MediaStreamSourceErrorStatus.Other);
                    remoteMediaSource?.Dispose();
                    remoteVideoSource = CreateVideoStreamSource(width, height, framerate);
                    remoteMediaSource = MediaSource.CreateFromMediaStreamSource(remoteVideoSource);
                    remoteVideoPlayer.Source = remoteMediaSource;
                    remoteVideo.SetMediaPlayer(remoteVideoPlayer);
                });
            }

            remoteVideoBridge.HandleIncomingVideoFrame(frame);
        }

        /// <summary>
        /// Callback on audio frame received from the local audio capture device (microphone),
        /// for local output before (or in parallel of) being sent to the remote peer.
        /// </summary>
        /// <param name="frame">The newly captured audio frame.</param>
        /// <remarks>This is currently never called due to implementation limitations.</remarks>
        private void Peer_LocalAudioFrameReady(AudioFrame frame)
        {
            // The current underlying WebRTC implementation does not support
            // local audio frame callbacks, so THIS WILL NEVER BE CALLED until
            // that implementation is changed.
            throw new NotImplementedException();
        }

        /// <summary>
        /// Callback on audio frame received from the remote peer, for local output
        /// (or any other use).
        /// </summary>
        /// <param name="frame">The newly received audio frame.</param>
        private void Peer_RemoteAudioFrameReady(AudioFrame frame)
        {
            lock (_isRemoteAudioPlayingLock)
            {
                uint channelCount = frame.channelCount;
                uint sampleRate = frame.sampleRate;
                if (!_isRemoteAudioPlaying || (_remoteAudioChannelCount != channelCount)
                    || (_remoteAudioSampleRate != sampleRate))
                {
                    _isRemoteAudioPlaying = true;
                    _remoteAudioChannelCount = channelCount;
                    _remoteAudioSampleRate = sampleRate;

                    // As an example of handling, update the UI to display the number of audio
                    // channels and the sample rate of the audio track.
                    RunOnMainThread(() => UpdateRemoteAudioStats(channelCount, sampleRate));
                }
            }
        }

        private void UpdateRemoteAudioStats(uint channelCount, uint sampleRate)
        {
            remoteAudioChannelCount.Text = channelCount.ToString();
            remoteAudioSampleRate.Text = $"{sampleRate} Hz";
        }

        private void MuteLocalVideoClicked(object sender, RoutedEventArgs e)
        {
            if (_localVideoTrack.Enabled)
            {
                _localVideoTrack.Enabled = false;
                muteLocalVideoStroke.Visibility = Visibility.Visible;
            }
            else
            {
                _localVideoTrack.Enabled = true;
                muteLocalVideoStroke.Visibility = Visibility.Collapsed;
            }
        }

        private void MuteLocalAudioClicked(object sender, RoutedEventArgs e)
        {
            if (_peerConnection.IsLocalAudioTrackEnabled())
            {
                _peerConnection.SetLocalAudioTrackEnabled(false);
                muteLocalAudioStroke.Visibility = Visibility.Visible;
            }
            else
            {
                _peerConnection.SetLocalAudioTrackEnabled(true);
                muteLocalAudioStroke.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Toggle local audio and video playback on/off, adding or removing tracks as needed.
        /// </summary>
        /// <param name="sender">The object which invoked the event.</param>
        /// <param name="e">Event arguments.</param>
        private async void StartLocalMediaClicked(object sender, RoutedEventArgs e)
        {
            // The button will be re-enabled once the current action is finished
            startLocalMedia.IsEnabled = false;

            // Toggle between start and stop local audio/video feeds
            if (localVideoStatsTimer.IsEnabled && (_localVideoTrack != null))
            {
                StopLocalMedia();
                localVideoStatsTimer.Stop();
                localLoadText.Text = "Produce: -";
                localPresentText.Text = "Render: -";
                localSkipText.Text = "Skip: -";
                localLateText.Text = "Late: -";
                muteLocalVideo.IsEnabled = false;
                muteLocalAudio.IsEnabled = false;
                startLocalMediaIcon.Symbol = Symbol.Play;
                muteLocalAudioStroke.Visibility = Visibility.Collapsed;
                muteLocalVideoStroke.Visibility = Visibility.Collapsed;
                return;
            }

            // Only start video if previous track was completely shutdown
            if (_localVideoTrack == null)
            {
                LogMessage("Opening local A/V stream...");

                var captureDevice = SelectedVideoCaptureDevice;
                var captureDeviceInfo = new VideoCaptureDevice()
                {
                    id = captureDevice?.Id,
                    name = captureDevice?.DisplayName
                };
                var videoProfile = SelectedVideoProfile;
                string videoProfileId = videoProfile?.Id;
                uint width;
                uint height;
                double framerate;
                if (videoProfile != null)
                {
                    var recordMediaDesc = SelectedRecordMediaDesc ?? videoProfile.SupportedRecordMediaDescription[0];
                    width = recordMediaDesc.Width;
                    height = recordMediaDesc.Height;
                    framerate = recordMediaDesc.FrameRate;
                }
                else
                {
                    var captureFormat = SelectedVideoCaptureFormat;
                    if (captureFormat.HasValue)
                    {
                        width = captureFormat.Value.width;
                        height = captureFormat.Value.height;
                        framerate = captureFormat.Value.framerate;
                    }
                    else
                    {
                        LogMessage("Cannot start video capture; no capture format selected.");
                        return;
                    }
                }

                localVideoPlayer.Source = null;
                localVideoSource?.NotifyError(MediaStreamSourceErrorStatus.Other);
                localMediaSource?.Dispose();
                localVideo.SetMediaPlayer(null);
                localVideoSource = CreateVideoStreamSource(width, height, (uint)framerate);
                localMediaSource = MediaSource.CreateFromMediaStreamSource(localVideoSource);
                localVideoPlayer.Source = localMediaSource;
                localVideo.SetMediaPlayer(localVideoPlayer);

                // Disable auto-offer on renegotiation needed event, to bundle the audio + video tracks
                // change together into a single SDP offer. Otherwise each added track will send a
                // separate offer message, which is currently not handled correctly (the second message
                // will arrive too soon, before the core implementation finished processing the first one
                // and is in kStable state, and it will get discarded so remote video will not start).
                _renegotiationOfferEnabled = false;

                // Ensure that the filtering values are up to date before creating new tracks.
                UpdateCodecFilters();

                // The default start bitrate is quite low (300 kbps); use a higher value to get
                // better quality on local network.
                _peerConnection.SetBitrate(startBitrateBps: (uint)(width * height * framerate / 20));

                try
                {
                    // Add the local audio track captured from the local microphone
                    await _peerConnection.AddLocalAudioTrackAsync();

                    // Add the local video track captured from the local webcam
                    var trackConfig = new LocalVideoTrackSettings
                    {
                        trackName = "local_video",
                        videoDevice = captureDeviceInfo,
                        videoProfileId = videoProfileId,
                        videoProfileKind = SelectedVideoProfileKind,
                        width = width,
                        height = height,
                        framerate = framerate,
                        enableMrc = false // TestAppUWP is a shared app, MRC will not get permission anyway
                    };
                    _localVideoTrack = await _peerConnection.AddLocalVideoTrackAsync(trackConfig);
                    _localVideoTrack.I420AVideoFrameReady += LocalVideoTrack_I420AFrameReady;
                }
                catch (Exception ex)
                {
                    LogMessage(ex.Message);
                    _renegotiationOfferEnabled = true;
                    return;
                }

                localVideoStatsTimer.Interval = TimeSpan.FromSeconds(1.0);
                localVideoStatsTimer.Start();
                muteLocalVideo.IsEnabled = true;
                muteLocalAudio.IsEnabled = true;
                startLocalMediaIcon.Symbol = Symbol.Stop;
                muteLocalAudioStroke.Visibility = Visibility.Collapsed;
                muteLocalVideoStroke.Visibility = Visibility.Collapsed;

                localVideoSourceName.Text = $"({SelectedVideoCaptureDevice?.DisplayName})";
                lock (_isLocalVideoPlayingLock)
                {
                    _isLocalVideoPlaying = true;
                }

                // Re-enable auto-offer on track change, and manually apply above changes
                // by creating a manual SDP offer to negotiate the new tracks.
                _renegotiationOfferEnabled = true;
                if (_peerConnection.IsConnected)
                {
                    _peerConnection.CreateOffer();
                }

                // Enable stopping the local audio and video
                startLocalMedia.IsEnabled = true;
            }
        }

        private void OnLocalVideoStatsTimerTicked(object sender, object e)
        {
            localLoadText.Text = $"Produce: {localVideoBridge.FrameLoad:F2}";
            localPresentText.Text = $"Render: {localVideoBridge.FramePresent:F2}";
            localSkipText.Text = $"Skip: {localVideoBridge.FrameSkip:F2}";
            //localLateText.Text = $"Late: {localVideoBridge.LateFrame:F2}";
        }

        private void OnRemoteVideoStatsTimerTicked(object sender, object e)
        {
            remoteLoadText.Text = $"Receive: {remoteVideoBridge.FrameLoad:F2}";
            remotePresentText.Text = $"Render: {remoteVideoBridge.FramePresent:F2}";
            remoteSkipText.Text = $"Skip: {remoteVideoBridge.FrameSkip:F2}";
            //remoteLateText.Text = $"Late: {remoteVideoBridge.LateFrame:F2}";
        }

        void UpdateCodecFilters()
        {
            _peerConnection.PreferredAudioCodec = PreferredAudioCodec;
            _peerConnection.PreferredAudioCodecExtraParamsRemote = PreferredAudioCodecExtraParamsRemoteTextBox.Text;
            _peerConnection.PreferredAudioCodecExtraParamsLocal = PreferredAudioCodecExtraParamsLocalTextBox.Text;
            _peerConnection.PreferredVideoCodec = PreferredVideoCodec;
            _peerConnection.PreferredVideoCodecExtraParamsRemote = PreferredVideoCodecExtraParamsRemoteTextBox.Text;
            _peerConnection.PreferredVideoCodecExtraParamsLocal = PreferredVideoCodecExtraParamsLocalTextBox.Text;
        }

        private void CreateOfferButtonClicked(object sender, RoutedEventArgs e)
        {
            if (!PluginInitialized)
            {
                return;
            }

            createOfferButton.IsEnabled = false;
            createOfferButton.Content = "Joining...";

            // Ensure that the filtering values are up to date before starting to create messages.
            UpdateCodecFilters();
            _peerConnection.CreateOffer();
        }

        private async void AddExtraDataChannelButtonClicked(object sender, RoutedEventArgs e)
        {
            if (!PluginInitialized)
            {
                return;
            }
            await _peerConnection.AddDataChannelAsync("extra_channel", true, true);
        }

        private void PollDssButtonClicked(object sender, RoutedEventArgs e)
        {
            // If already polling, stop
            if (isDssPolling)
            {
                LogMessage($"Cancelling polling DSS server...");
                pollDssButton.IsEnabled = false; // will be re-enabled when cancellation is completed, see DssSignaler_OnPollingDone
                pollDssButton.Content = "Start polling";
                dssSignaler.StopPollingAsync();
                // Cannot create offer before signaling is ready
                createOfferButton.IsEnabled = false;
                return;
            }

            // If not polling, try to start if the poll parameters are valid
            if (!float.TryParse(dssPollTimeMs.Text, out float pollTimeMs))
            {
                // Invalid time format, cannot start polling
                return;
            }
            if (string.IsNullOrEmpty(localPeerUidTextBox.Text) || string.IsNullOrEmpty(remotePeerUidTextBox.Text))
            {
                // Invalid peer ID, cannot start polling
                return;
            }

            dssSignaler.HttpServerAddress = dssServer.Text;
            dssSignaler.LocalPeerId = localPeerUidTextBox.Text;
            dssSignaler.RemotePeerId = remotePeerUidTextBox.Text;
            dssSignaler.PollTimeMs = pollTimeMs;
            if (dssSignaler.StartPollingAsync())
            {
                pollDssButton.Content = "Stop polling";
                isDssPolling = true;
                // Cannot create offer before signaling is ready
                createOfferButton.IsEnabled = true;
            }
        }

        private void NavLinksList_ItemClick(object sender, ItemClickEventArgs e)
        {

        }

        private void ChatList_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ChatChannel chat)
            {
                chatTextBox.Text = chat.Text;
            }
        }

        /// <summary>
        /// Callback on Send button from text chat clicker.
        /// If connected, this sends the text message to the remote peer using
        /// the previously opened data channel.
        /// </summary>
        /// <param name="sender">The object which invoked the event.</param>
        /// <param name="e">Event arguments.</param>
        private void ChatSendButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(chatInputBox.Text))
                return;

            var chat = SelectedChatChannel;
            if (chat == null)
                return;

            // Send the message through the data channel
            byte[] chatMessage = System.Text.Encoding.UTF8.GetBytes(chatInputBox.Text);
            chat.DataChannel.SendMessage(chatMessage);

            // Save and display in the UI
            var newLine = $"[local] {chatInputBox.Text}\n";
            chat.Text += newLine;
            chatTextBox.Text = chat.Text; // reassign or append? not sure...
            chatScrollViewer.ChangeView(chatScrollViewer.HorizontalOffset,
                chatScrollViewer.ScrollableHeight,
                chatScrollViewer.ZoomFactor); // scroll to end
            chatInputBox.Text = string.Empty;
        }

        /// <summary>
        /// Callback on text message received through the data channel.
        /// </summary>
        /// <param name="message">The raw data channel message.</param>
        private void ChatMessageReceived(ChatChannel chat, string text)
        {
            chat.Text += $"[remote] {text}\n";
            if (SelectedChatChannel == chat)
            {
                chatTextBox.Text = chat.Text; // reassign or append? not sure...
                chatScrollViewer.ChangeView(chatScrollViewer.HorizontalOffset,
                    chatScrollViewer.ScrollableHeight,
                    chatScrollViewer.ZoomFactor); // scroll to end
            }
        }

        /// <summary>
        /// Callback on key down event invoked in the chat window, to handle
        /// the "press Enter to send" text chat functionality.
        /// </summary>
        /// <param name="sender">The object which invoked the event.</param>
        /// <param name="e">Event arguments.</param>
        private void OnChatKeyDown(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                ChatSendButton_Click(this, null);
            }
        }
    }
}
