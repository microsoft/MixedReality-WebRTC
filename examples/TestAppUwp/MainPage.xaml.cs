// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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

        private MediaStreamSource remoteVideoSource = null;
        private MediaSource remoteMediaSource = null;
        private MediaPlayer remoteVideoPlayer = new MediaPlayer();
        private bool _isRemoteVideoPlaying = false;
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
        /// Data channel used to send and receive text messages, as an example use.
        /// </summary>
        private DataChannel _chatDataChannel = null;

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
        private DispatcherTimer dssStatsTimer = new DispatcherTimer();
        private string remotePeerId; // local copy of remotePeerUidTextBox.Text accessible from non-UI thread

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

        public PeerConnection.VideoProfileKind SelectedVideoProfileKind
        {
            get
            {
                var videoProfileKindIndex = KnownVideoProfileKindComboBox.SelectedIndex;
                if (videoProfileKindIndex < 0)
                {
                    return PeerConnection.VideoProfileKind.Unspecified;
                }
                return (PeerConnection.VideoProfileKind)Enum.GetValues(typeof(PeerConnection.VideoProfileKind)).GetValue(videoProfileKindIndex);
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

        public VideoCaptureFormat SelectedVideoCaptureFormat
        {
            get
            {
                var profileIndex = VideoCaptureFormatList.SelectedIndex;
                if ((profileIndex < 0) || (profileIndex >= VideoCaptureFormats.Count))
                {
                    return default(VideoCaptureFormat);
                }
                return VideoCaptureFormats[profileIndex];
            }
        }

        public ObservableCollection<NavLink> NavLinks { get; }
            = new ObservableCollection<NavLink>();

        private VideoBridge localVideoBridge = new VideoBridge(3);
        private VideoBridge remoteVideoBridge = new VideoBridge(5);

        // HACK - For debugging 2 instances on the same machine and 2 webcams
        private int HACK_GetVideoDeviceIndex()
        {
            var firstInstance = AppInstance.FindOrRegisterInstanceForKey("{44CD414E-B604-482E-8CFD-A9E09076CABD}");
            int idx = 0;
            if (!firstInstance.IsCurrentInstance)
            {
                idx++;
            }
            return idx;
        }

        public MainPage()
        {
            this.InitializeComponent();

            // TEMP - For debugging
            //remotePeerUidTextBox.Text = "cc937a60143e2a5d4dbb2b2003b3f0177a49d337";

            // HACK - For debugging: deterministic 2-instance value paired with each other
            var idx = HACK_GetVideoDeviceIndex();
            localPeerUidTextBox.Text = GetDeviceUniqueIdLikeUnity((byte)idx);
            remotePeerUidTextBox.Text = GetDeviceUniqueIdLikeUnity((byte)(1 - idx));

            dssSignaler.OnFailure += DssSignaler_OnFailure;
            dssSignaler.OnPollingDone += DssSignaler_OnPollingDone;

            dssStatsTimer.Tick += OnDssStatsTimerTick;

            _peerConnection = new PeerConnection(dssSignaler);
            _peerConnection.Connected += OnPeerConnected;
            _peerConnection.IceStateChanged += OnIceStateChanged;
            _peerConnection.RenegotiationNeeded += OnPeerRenegotiationNeeded;
            _peerConnection.TrackAdded += Peer_RemoteTrackAdded;
            _peerConnection.TrackRemoved += Peer_RemoteTrackRemoved;
            _peerConnection.I420LocalVideoFrameReady += Peer_LocalI420FrameReady;
            _peerConnection.I420RemoteVideoFrameReady += Peer_RemoteI420FrameReady;
            _peerConnection.LocalAudioFrameReady += Peer_LocalAudioFrameReady;
            _peerConnection.RemoteAudioFrameReady += Peer_RemoteAudioFrameReady;

            //Window.Current.Closed += Shutdown; // doesn't work

            this.Loaded += OnLoaded;
        }

        private void OnIceStateChanged(IceConnectionState newState)
        {
            RunOnMainThread(() =>
            {
                LogMessage($"ICE state changed to {newState}.");
                iceStateText.Text = newState.ToString();
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

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            LogMessage("Initializing the WebRTC native plugin...");

            // Populate the combo box with the PeerConnection.VideoProfileKind enum
            {
                var values = Enum.GetValues(typeof(PeerConnection.VideoProfileKind));
                KnownVideoProfileKindComboBox.ItemsSource = values.Cast<PeerConnection.VideoProfileKind>();
                KnownVideoProfileKindComboBox.SelectedIndex = Array.IndexOf(values, PeerConnection.VideoProfileKind.Unspecified);
            }

            VideoCaptureDeviceList.SelectionChanged += VideoCaptureDeviceList_SelectionChanged;
            KnownVideoProfileKindComboBox.SelectionChanged += KnownVideoProfileKindComboBox_SelectionChanged;
            VideoProfileComboBox.SelectionChanged += VideoProfileComboBox_SelectionChanged;

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
                // Use a local list accessible from a background thread
                PeerConnection.GetVideoCaptureDevicesAsync().ContinueWith((prevTask) =>
                {
                    if (prevTask.Exception != null)
                    {
                        throw prevTask.Exception;
                    }

                    var devices = prevTask.Result;

                    List<VideoCaptureDeviceInfo> vcds = new List<VideoCaptureDeviceInfo>(devices.Count);
                    foreach (var device in devices)
                    {
                        vcds.Add(new VideoCaptureDeviceInfo()
                        {
                            Id = device.id,
                            DisplayName = device.name,
                            Symbol = Symbol.Video
                        });
                    }

                    // Assign on main UI thread because of XAML binding; otherwise it fails.
                    RunOnMainThread(() =>
                    {
                        VideoCaptureDevices.Clear();
                        foreach (var vcd in vcds)
                        {
                            VideoCaptureDevices.Add(vcd);
                            LogMessage($"VCD id={vcd.Id} name={vcd.DisplayName}");
                        }

                        // Select first entry by default
                        if (vcds.Count > 0)
                        {
                            VideoCaptureDeviceList.SelectedIndex = 0;
                        }
                    });
                });
            }

            //localVideo.TransportControls = localVideoControls;

            PluginInitialized = false;

            // Assign STUN server(s) before calling InitializeAsync()
            var config = new PeerConnectionConfiguration();
            config.IceServers.Add(new IceServer { Urls = { "stun:" + stunServer.Text } });

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
                var uiThreadScheduler = TaskScheduler.FromCurrentSynchronizationContext();
                mediaAccessRequester.InitializeAsync(mediaSettings).AsTask()
                    .ContinueWith((accessTask) =>
                    {
                        if (accessTask.Exception != null)
                        {
                            LogMessage($"Access to A/V denied, check app permissions: {accessTask.Exception.Message}");
                            throw accessTask.Exception;
                        }
                        _peerConnection.InitializeAsync(config).ContinueWith((initTask) =>
                        {
                            if (initTask.Exception != null)
                            {
                                LogMessage($"WebRTC native plugin init failed: {initTask.Exception.Message}");
                                throw initTask.Exception;
                            }
                            OnPluginPostInit();
                        }, uiThreadScheduler); // run task on caller (UI) thread
                    });
            }
            //< TODO - This below shouldn't do anything since exceptions are caught and stored inside Task.Exception...
            catch (UnauthorizedAccessException uae)
            {
                LogMessage("Access to A/V denied: " + uae.Message);
            }
            catch (Exception ex)
            {
                if (ex.InnerException is UnauthorizedAccessException uae)
                {
                    LogMessage("Access to A/V denied: " + uae.Message);
                }
                else
                {
                    LogMessage("Failed to initialize A/V with unknown exception: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// Update the list of video profiles stored in <cref>VideoProfiles</cref>
        /// when the selected video capture device or known video profile kind change.
        /// </summary>
        private void UpdateVideoProfiles()
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
                var videoProfileKind = (PeerConnection.VideoProfileKind)Enum.GetValues(typeof(PeerConnection.VideoProfileKind)).GetValue(videoProfileKindIndex);

                // List all video profiles for the select device (and kind, if any specified)
                IReadOnlyList<MediaCaptureVideoProfile> profiles;
                if (videoProfileKind == PeerConnection.VideoProfileKind.Unspecified)
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
            }
            else
            {
                // Device doesn't support video profiles; fall back on flat list of capture formats.

                // List resolutions
                var uiThreadScheduler = TaskScheduler.FromCurrentSynchronizationContext();
                PeerConnection.GetVideoCaptureFormatsAsync(device.Id).ContinueWith((listTask) =>
                {
                    if (listTask.Exception != null)
                    {
                        throw listTask.Exception;
                    }

                    // Populate the capture format list
                    foreach (var format in listTask.Result)
                    {
                        VideoCaptureFormats.Add(format);
                    }
                }, uiThreadScheduler);
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
            var values = Enum.GetValues(typeof(PeerConnection.VideoProfileKind));
            if (MediaCapture.IsVideoProfileSupported(device.Id))
            {
                KnownVideoProfileKindComboBox.SelectedIndex = Array.IndexOf(values, PeerConnection.VideoProfileKind.VideoConferencing);
                KnownVideoProfileKindComboBox.IsEnabled = true; //< TODO - Use binding
                VideoProfileComboBox.IsEnabled = true;
                RecordMediaDescList.IsEnabled = true;
                VideoCaptureFormatList.IsEnabled = false;
            }
            else
            {
                KnownVideoProfileKindComboBox.SelectedIndex = Array.IndexOf(values, PeerConnection.VideoProfileKind.Unspecified);
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
            RunOnMainThread(() =>
            {
                sessionStatusText.Text = "(session joined)";
                chatTextBox.IsEnabled = true;
            });
        }

        private void DssSignaler_OnFailure(Exception e)
        {
            RunOnMainThread(() =>
            {
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
            RunOnMainThread(() =>
            {
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
            RunOnMainThread(() =>
            {
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
        private MediaStreamSource CreateVideoStreamSource(uint width, uint height)
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
            videoStreamDesc.EncodingProperties.FrameRate.Numerator = 30;
            videoStreamDesc.EncodingProperties.FrameRate.Denominator = 1;
            videoStreamDesc.EncodingProperties.Bitrate = (30 * width * height * 8 * 8 / 12); // 30-fps 8bits/byte NV12=12bpp
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

        /// <summary>
        /// Main UI thread callback on WebRTC native plugin initialized.
        /// </summary>
        private void OnPluginPostInit()
        {
            PluginInitialized = true;
            LogMessage("WebRTC native plugin initialized.");

            // It is CRUCIAL to add any data channel BEFORE the SDP offer is sent, if data channels are
            // to be used at all. Otherwise the SCTP will not be negotiated, and then all channels will
            // stay forever in the kConnecting state.
            // https://stackoverflow.com/questions/43788872/how-are-data-channels-negotiated-between-two-peers-with-webrtc
            _peerConnection.AddDataChannelAsync(ChatChannelID, "chat", true, true).ContinueWith((prevTask) =>
            {
                if (prevTask.Exception != null)
                {
                    throw prevTask.Exception;
                }
                var newDataChannel = prevTask.Result;
                RunOnMainThread(() =>
                {
                    _chatDataChannel = newDataChannel;
                    _chatDataChannel.MessageReceived += ChatMessageReceived;
                    chatInputBox.IsEnabled = true;
                    chatSendButton.IsEnabled = true;
                });
            });

            createOfferButton.IsEnabled = true;

            ////// Do not allow starting the local video before the MediaElement told us it was
            ////// safe to do so (see OnMediaOpened). Otherwise Play() will silently fail.
            startLocalVideo.IsEnabled = true;

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

            // Bind the XAML UI control (localVideo) to the MediaFoundation rendering pipeline (localVideoPlayer)
            // so that the former can render in the UI the video frames produced in the background by the later.
            localVideo.SetMediaPlayer(localVideoPlayer);
            remoteVideo.SetMediaPlayer(remoteVideoPlayer);
        }

        private void OnMediaStateChanged(Windows.Media.Playback.MediaPlayer sender, object args)
        {
            RunOnMainThread(() =>
            {
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
                RunOnMainThread(() =>
                {
                    localVideo.MediaPlayer.Play();
                    //startLocalVideo.IsEnabled = true;
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
            RunOnMainThread(() =>
            {
                LogMessage("Local MediaElement video playback ended.");
                //StopLocalVideo();
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
        private void StopLocalVideo()
        {
            lock (_isLocalVideoPlayingLock)
            {
                if (_isLocalVideoPlaying)
                {
                    localVideo.MediaPlayer.Pause();
                    localVideo.SetMediaPlayer(null);
                    localVideoSource = null;
                    //localMediaSource.Reset();
                    _isLocalVideoPlaying = false;

                    // Avoid deadlock in audio processing stack, as this call is delegated to the WebRTC
                    // signaling thread (and will block the caller thread), and audio processing will
                    // delegate to the UI thread for UWP operations (and will block the signaling thread).
                    RunOnWorkerThread(() =>
                    {
                        _peerConnection.RemoveLocalAudioTrack();
                        _peerConnection.RemoveLocalVideoTrack();
                    });
                }
            }
        }

        /// <summary>
        /// Callback on remote media (audio or video) track added.
        /// Currently does nothing, as starting the media pipeline is done lazily in the
        /// per-frame callback.
        /// </summary>
        /// <param name="trackKind">The kind of media track added (audio or video only).</param>
        /// <seealso cref="Peer_RemoteI420FrameReady"/>
        private void Peer_RemoteTrackAdded(PeerConnection.TrackKind trackKind)
        {
            LogMessage($"Added remote {trackKind} track.");
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
                        RunOnMainThread(() =>
                        {
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
        private void Peer_LocalI420FrameReady(I420AVideoFrame frame)
        {
            localVideoBridge.HandleIncomingVideoFrame(frame);
        }

        /// <summary>
        /// Callback on video frame received from the remote peer, for local rendering
        /// (or any other use).
        /// </summary>
        /// <param name="frame">The newly received video frame.</param>
        private void Peer_RemoteI420FrameReady(I420AVideoFrame frame)
        {
            // Lazily start the remote video media player when receiving
            // the first video frame from the remote peer. Currently there
            // is no exposed API to tell once connected that the remote peer
            // will be sending some video track.
            //< TODO - See if we can add an API to enumerate the remote channels,
            //         or an On(Audio|Video|Data)Channel(Added|Removed) event?
            lock (_isRemoteVideoPlayingLock)
            {
                if (!_isRemoteVideoPlaying)
                {
                    _isRemoteVideoPlaying = true;
                    uint width = frame.width;
                    uint height = frame.height;
                    RunOnMainThread(() =>
                    {
                        remoteVideoSource = CreateVideoStreamSource(width, height);
                        remoteVideoPlayer.Source = MediaSource.CreateFromMediaStreamSource(remoteVideoSource);
                        remoteVideoPlayer.Play();
                    });
                }
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

        /// <summary>
        /// Toggle local audio and video playback on/off, adding or removing tracks as needed.
        /// </summary>
        /// <param name="sender">The object which invoked the event.</param>
        /// <param name="e">Event arguments.</param>
        private void StartLocalVideoClicked(object sender, RoutedEventArgs e)
        {
            // Toggle between start and stop local audio/video feeds
            //< TODO dssStatsTimer.IsEnabled used for toggle, but dssStatsTimer should be
            // used also for remote statistics display (so even when no local video active)
            if (dssStatsTimer.IsEnabled)
            {
                StopLocalVideo();
                dssStatsTimer.Stop();
                localLoadText.Text = "Load: -";
                localPresentText.Text = "Present: -";
                localSkipText.Text = "Skip: -";
                localLateText.Text = "Late: -";
                remoteLoadText.Text = "Load: -";
                remotePresentText.Text = "Present: -";
                remoteSkipText.Text = "Skip: -";
                remoteLateText.Text = "Late: -";
                startLocalVideo.Content = "Start local video";
            }
            else
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
                uint width = 0;
                uint height = 0;
                double framerate = 0.0;
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
                    width = captureFormat.width;
                    height = captureFormat.height;
                    framerate = captureFormat.framerate;
                }

                localVideoPlayer.Source = null;
                localMediaSource?.Reset();
                localVideo.SetMediaPlayer(null);
                localVideoSource = null;
                localVideoSource = CreateVideoStreamSource(width, height);
                localMediaSource = MediaSource.CreateFromMediaStreamSource(localVideoSource);
                localVideoPlayer.Source = localMediaSource;
                localVideo.SetMediaPlayer(localVideoPlayer);

                var uiThreadScheduler = TaskScheduler.FromCurrentSynchronizationContext();
                var videoProfileKind = SelectedVideoProfileKind; // capture on UI thread

                // Disable auto-offer on renegotiation needed event, to bundle the audio + video tracks
                // change together into a single SDP offer. Otherwise each added track will send a
                // separate offer message, which is currently not handled correctly (the second message
                // will arrive too soon, before the core implementation finished processing the first one
                // and is in kStable state, and it will get discarded so remote video will not start).
                _renegotiationOfferEnabled = false;

                _peerConnection.AddLocalAudioTrackAsync().ContinueWith(addAudioTask =>
                {
                    // Continue on worker thread here
                    if (addAudioTask.Exception != null)
                    {
                        LogMessage(addAudioTask.Exception.Message);
                        _renegotiationOfferEnabled = true;
                        return;
                    }

                    var trackConfig = new PeerConnection.LocalVideoTrackSettings
                    {
                        videoDevice = captureDeviceInfo,
                        videoProfileId = videoProfileId,
                        videoProfileKind = videoProfileKind,
                        width = width,
                        height = height,
                        framerate = framerate,
                        enableMrc = false
                    };
                    _peerConnection.AddLocalVideoTrackAsync(trackConfig).ContinueWith(addVideoTask =>
                    {
                        // Continue inside UI thread here
                        if (addVideoTask.Exception != null)
                        {
                            LogMessage(addVideoTask.Exception.Message);
                            _renegotiationOfferEnabled = true;
                            return;
                        }
                        dssStatsTimer.Interval = TimeSpan.FromSeconds(1.0);
                        dssStatsTimer.Start();
                        startLocalVideo.Content = "Stop local video";
                        var idx = HACK_GetVideoDeviceIndex(); //< HACK
                        localPeerUidTextBox.Text = GetDeviceUniqueIdLikeUnity((byte)idx); //< HACK
                        remotePeerUidTextBox.Text = GetDeviceUniqueIdLikeUnity((byte)(1 - idx)); //< HACK
                        localVideoSourceName.Text = $"({VideoCaptureDevices[idx].DisplayName})"; //< HACK
                        //localVideo.MediaPlayer.Play();
                        lock (_isLocalVideoPlayingLock)
                        {
                            _isLocalVideoPlaying = true;
                        }

                        // Re-enable auto-offer on track change, and manually apply above changes.
                        _renegotiationOfferEnabled = true;
                        if (_peerConnection.IsConnected)
                        {
                            _peerConnection.CreateOffer();
                        }

                    }, uiThreadScheduler);
                });
            }
        }

        private void OnDssStatsTimerTick(object sender, object e)
        {
            //localLoadText.Text = $"Load: {localVideoBridge.FrameLoad:F2}";
            //localPresentText.Text = $"Present: {localVideoBridge.FramePresent:F2}";
            //localSkipText.Text = $"Skip: {localVideoBridge.FrameSkip:F2}";
            //localLateText.Text = $"Late: {localVideoBridge.LateFrame:F2}";

            //remoteLoadText.Text = $"Load: {remoteVideoBridge.FrameLoad:F2}";
            //remotePresentText.Text = $"Present: {remoteVideoBridge.FramePresent:F2}";
            //remoteSkipText.Text = $"Skip: {remoteVideoBridge.FrameSkip:F2}";
            //remoteLateText.Text = $"Late: {remoteVideoBridge.LateFrame:F2}";
        }

        private void CreateOfferButtonClicked(object sender, RoutedEventArgs e)
        {
            if (!PluginInitialized)
            {
                return;
            }

            createOfferButton.IsEnabled = false;
            createOfferButton.Content = "Joining...";

            _peerConnection.PreferredVideoCodec = PreferredVideoCodec.Text;
            _peerConnection.CreateOffer();
        }

        /// <summary>
        /// Retrieve a unique ID that is stable across application instances, similar to what
        /// Unity does with SystemInfo.deviceUniqueIdentifier.
        /// </summary>
        /// <param name="variant">Optional variation quantity to modify the unique ID,
        /// to allow multiple instances of the application to run in parallel with different IDs.</param>
        /// <returns>A unique string ID stable across multiple runs of the application</returns>
        /// <remarks>
        /// This is a debugging utility useful to generate deterministic WebRTC peers for node-dss
        /// signaling, to avoid manual input during testing. This is not a production-level solution.
        /// </remarks>
        private string GetDeviceUniqueIdLikeUnity(byte variant = 0)
        {
            // More or less like Unity, which can use HardwareIdentification.GetPackageSpecificToken() in some cases,
            // although it's unclear how they convert that to a string.
            Windows.Storage.Streams.IBuffer buffer = HardwareIdentification.GetPackageSpecificToken(null).Id;
            using (var dataReader = Windows.Storage.Streams.DataReader.FromBuffer(buffer))
            {
                byte[] bytes = new byte[buffer.Length];
                dataReader.ReadBytes(bytes);
                bytes[0] += variant;
                return BitConverter.ToString(bytes).Replace("-", string.Empty).Remove(32);
            }
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
                return;
            }

            // If not polling, try to start if the poll time is valid
            if (!float.TryParse(dssPollTimeMs.Text, out float pollTimeMs))
            {
                // Invalid time format, cannot start polling
                return;
            }

            dssSignaler.HttpServerAddress = dssServer.Text;
            dssSignaler.LocalPeerId = localPeerUidTextBox.Text;
            dssSignaler.PollTimeMs = pollTimeMs;
            remotePeerId = remotePeerUidTextBox.Text;
            dssSignaler.RemotePeerId = remotePeerId;

            if (dssSignaler.StartPollingAsync())
            {
                pollDssButton.Content = "Stop polling";
                isDssPolling = true;
            }
        }

        private void NavLinksList_ItemClick(object sender, ItemClickEventArgs e)
        {

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
            byte[] chatMessage = System.Text.Encoding.UTF8.GetBytes(chatInputBox.Text);
            _chatDataChannel.SendMessage(chatMessage);
            chatTextBox.Text += $"[local] {chatInputBox.Text}\n";
            chatScrollViewer.ChangeView(chatScrollViewer.HorizontalOffset,
                chatScrollViewer.ScrollableHeight,
                chatScrollViewer.ZoomFactor); // scroll to end
            chatInputBox.Text = string.Empty;
        }

        /// <summary>
        /// Callback on text message received through the data channel.
        /// </summary>
        /// <param name="message">The raw data channel message.</param>
        private void ChatMessageReceived(byte[] message)
        {
            string text = System.Text.Encoding.UTF8.GetString(message);
            RunOnMainThread(() =>
            {
                chatTextBox.Text += $"[remote] {text}\n";
                chatScrollViewer.ChangeView(chatScrollViewer.HorizontalOffset,
                    chatScrollViewer.ScrollableHeight,
                    chatScrollViewer.ZoomFactor); // scroll to end
            });
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
