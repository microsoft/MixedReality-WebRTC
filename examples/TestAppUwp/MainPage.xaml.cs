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

    public class VideoCaptureDevice
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

        private PeerConnection _peerConnection;
        private DataChannel _chatDataChannel = null;

        private const int ChatChannelID = 2;

        private bool isDssPolling = false;
        private NodeDssSignaler dssSignaler = new NodeDssSignaler();
        private DispatcherTimer dssStatsTimer = new DispatcherTimer();
        private string remotePeerId; // local copy of remotePeerUidTextBox.Text accessible from non-UI thread

        public ObservableCollection<VideoCaptureDevice> VideoCaptureDevices { get; private set; }
            = new ObservableCollection<VideoCaptureDevice>();

        public VideoCaptureDevice SelectedVideoCaptureDevice
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

        private void OnPeerRenegotiationNeeded()
        {
            // If already connected, update the connection on the fly.
            // If not, wait for user action and don't automatically connect.
            if (_peerConnection.IsConnected)
            {
                _peerConnection.CreateOffer();
            }
        }

        private void Peer_RemoteTrackAdeed(PeerConnection.TrackKind trackKind)
        {
            LogMessage($"Added remote {trackKind} track.");
        }

        private void Peer_RemoteTrackRemoved(PeerConnection.TrackKind trackKind)
        {
            LogMessage($"Removed remote {trackKind} track.");
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

                    List<VideoCaptureDevice> vcds = new List<VideoCaptureDevice>(devices.Count);
                    foreach (var device in devices)
                    {
                        vcds.Add(new VideoCaptureDevice()
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
            _peerConnection.Servers.Clear(); // We use only one server in this demo
            _peerConnection.Servers.Add("stun:" + stunServer.Text);

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
                        _peerConnection.InitializeAsync().ContinueWith((initTask) =>
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

            // Get the video capture device selected by the user
            var deviceIndex = VideoCaptureDeviceList.SelectedIndex;
            if (deviceIndex < 0)
            {
                return;
            }
            var device = VideoCaptureDevices[deviceIndex];

            // Ensure that the video capture device actually supports video profiles
            if (!MediaCapture.IsVideoProfileSupported(device.Id))
            {
                return;
            }

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
            }
            else
            {
                KnownVideoProfileKindComboBox.SelectedIndex = Array.IndexOf(values, PeerConnection.VideoProfileKind.Unspecified);
                KnownVideoProfileKindComboBox.IsEnabled = false;
                VideoProfileComboBox.IsEnabled = false;
                RecordMediaDescList.IsEnabled = false;
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

        private void LogMessage(string message)
        {
            Debugger.Log(4, "TestAppUWP", message);
            RunOnMainThread(() =>
            {
                debugMessages.Text += message + "\n";
            });
        }

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

        private void OnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
        {
            LogMessage($"MediaElement reported an error: \"{args.ErrorMessage}\" (\"{args.ExtendedErrorCode.Message}\")");
        }

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
                    _peerConnection.RemoveLocalAudioTrack();
                    _peerConnection.RemoveLocalVideoTrack();
                    _isLocalVideoPlaying = false;
                }
            }
        }

        private void Peer_RemoteTrackAdded(PeerConnection.TrackKind trackKind)
        {
            //if (trackKind == PeerConnection.TrackKind.Video)
            //{
            //    lock (_isRemoteVideoPlayingLock)
            //    {
            //        if (!_isRemoteVideoPlaying)
            //        {
            //            _isRemoteVideoPlaying = true;
            //        }
            //    }
            //}
        }

        private void Peer_RemoteTrackRemoved(PeerConnection.TrackKind trackKind)
        {
            if (trackKind == PeerConnection.TrackKind.Video)
            {
                lock (_isRemoteVideoPlayingLock)
                {
                    if (_isRemoteVideoPlaying)
                    {
                        remoteVideo.MediaPlayer.Pause();
                        _isRemoteVideoPlaying = false;
                    }
                }
            }
        }

        private void Peer_LocalI420FrameReady(I420AVideoFrame frame)
        {
            localVideoBridge.HandleIncomingVideoFrame(frame);
        }

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
                    remoteVideoSource = CreateVideoStreamSource(frame.width, frame.height);
                    remoteMediaSource = MediaSource.CreateFromMediaStreamSource(remoteVideoSource);
                    remoteVideoPlayer.Source = remoteMediaSource;

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

        private void Peer_LocalAudioFrameReady(AudioFrame frame)
        {
            // The current underlying WebRTC implementation does not support
            // local audio frame callbacks, so this will never be called until
            // that implementation is changed.
            throw new NotImplementedException();
        }

        private void Peer_RemoteAudioFrameReady(AudioFrame frame)
        {
            lock (_isRemoteAudioPlayingLock)
            {
                uint channelCount = frame.channelCount;
                uint sampleRate = frame.sampleRate;

                bool changed = false;
                if (!_isRemoteAudioPlaying)
                {
                    _isRemoteAudioPlaying = true;
                    changed = true;
                }
                else if ((_remoteAudioChannelCount != channelCount) || (_remoteAudioSampleRate != sampleRate))
                {
                    changed = true;
                }

                if (changed)
                {
                    _remoteAudioChannelCount = channelCount;
                    _remoteAudioSampleRate = sampleRate;
                    RunOnMainThread(() => UpdateRemoteAudioStats(channelCount, sampleRate));
                }
            }
        }

        private void UpdateRemoteAudioStats(uint channelCount, uint sampleRate)
        {
            remoteAudioChannelCount.Text = channelCount.ToString();
            remoteAudioSampleRate.Text = $"{sampleRate} Hz";
        }

        private void StartLocalVideoClicked(object sender, RoutedEventArgs e)
        {
            // Toggle between start and stop local video feed
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
                var captureDeviceInfo = new PeerConnection.VideoCaptureDevice()
                {
                    id = captureDevice?.Id,
                    name = captureDevice?.DisplayName
                };
                var videoProfile = SelectedVideoProfile;
                string videoProfileId = videoProfile?.Id;
                uint width = 640; //< TODO - replace with non-profile resolution from querying the capture device
                uint height = 480; //< TODO - replace with non-profile resolution from querying the capture device
                double framerate = 0.0;
                if (videoProfile != null)
                {
                    var recordMediaDesc = SelectedRecordMediaDesc ?? videoProfile.SupportedRecordMediaDescription[0];
                    width = recordMediaDesc.Width;
                    height = recordMediaDesc.Height;
                    framerate = recordMediaDesc.FrameRate;
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
                _peerConnection.AddLocalAudioTrackAsync().ContinueWith(addAudioTask =>
                {
                    // Continue on worker thread here
                    if (addAudioTask.Exception != null)
                    {
                        LogMessage(addAudioTask.Exception.Message);
                        return;
                    }

                    _peerConnection.AddLocalVideoTrackAsync(captureDeviceInfo, videoProfileId, SelectedVideoProfileKind,
                    (int)width, (int)height, framerate, /* mrcEnabled = */ false).ContinueWith(prevTask =>
                    {
                        // Continue inside UI thread here
                        if (prevTask.Exception != null)
                        {
                            LogMessage(prevTask.Exception.Message);
                            return;
                        }
                        dssStatsTimer.Interval = TimeSpan.FromSeconds(1.0);
                        dssStatsTimer.Start();
                        startLocalVideo.Content = "Stop local video";
                        var idx = HACK_GetVideoDeviceIndex(); //< HACK
                        localPeerUidTextBox.Text = GetDeviceUniqueIdLikeUnity((byte)idx); //< HACK
                        remotePeerUidTextBox.Text = "030098210300865F0300E0980300C2A0"; // GetDeviceUniqueIdLikeUnity((byte)(1 - idx)); //< HACK
                        dssServer.Text = "http://10.164.30.167:3000/";
                        localVideoSourceName.Text = $"({VideoCaptureDevices[idx].DisplayName})"; //< HACK
                        //localVideo.MediaPlayer.Play();
                        lock (_isLocalVideoPlayingLock)
                        {
                            // Continue inside UI thread here
                            if (addVideoTask.Exception != null)
                            {
                                LogMessage(addVideoTask.Exception.Message);
                                return;
                            }
                            dssStatsTimer.Interval = TimeSpan.FromSeconds(1.0);
                            dssStatsTimer.Start();
                            startLocalVideo.Content = "Stop local video";
                            var idx = HACK_GetVideoDeviceIndex(); //< HACK
                            localPeerUidTextBox.Text = GetDeviceUniqueIdLikeUnity((byte)idx); //< HACK
                            remotePeerUidTextBox.Text = GetDeviceUniqueIdLikeUnity((byte)(1 - idx)); //< HACK
                            localVideoSourceName.Text = $"({VideoCaptureDevices[idx].DisplayName})"; //< HACK
                            localVideo.MediaPlayer.Play();
                            lock (_isLocalVideoPlayingLock)
                            {
                                _isLocalVideoPlaying = true;
                            }
                        }, uiThreadScheduler);
                    });
                });
            }
        }

        private void OnDssStatsTimerTick(object sender, object e)
        {
            //localLoadText.Text = $"Load: {localVideoBridge.FrameLoad.Value:F2}";
            //localPresentText.Text = $"Present: {localVideoBridge.FramePresent.Value:F2}";
            //localSkipText.Text = $"Skip: {localVideoBridge.FrameSkip.Value:F2}";
            //localLateText.Text = $"Late: {localVideoBridge.LateFrame.Value:F2}";

            //remoteLoadText.Text = $"Load: {remoteVideoBridge.FrameLoad.Value:F2}";
            //remotePresentText.Text = $"Present: {remoteVideoBridge.FramePresent.Value:F2}";
            //remoteSkipText.Text = $"Skip: {remoteVideoBridge.FrameSkip.Value:F2}";
            //remoteLateText.Text = $"Late: {remoteVideoBridge.LateFrame.Value:F2}";
        }

        private void StunServerTextChanged(object sender, TextChangedEventArgs e)
        {
            _peerConnection.Servers.Clear(); // We use only one server in this demo
            _peerConnection.Servers.Add("stun:" + stunServer.Text);
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

        private void OnChatKeyDown(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                ChatSendButton_Click(this, null);
            }
        }
    }
}
