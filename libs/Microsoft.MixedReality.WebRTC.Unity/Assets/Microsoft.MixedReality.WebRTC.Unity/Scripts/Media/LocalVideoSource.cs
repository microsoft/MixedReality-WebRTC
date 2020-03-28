// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR;

#if ENABLE_WINMD_SUPPORT
using global::Windows.Graphics.Holographic;
#endif

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// Video capture format selection mode for a local video source.
    /// </summary>
    public enum LocalVideoSourceFormatMode
    {
        /// <summary>
        /// Automatically select a good resolution and framerate based on the runtime detection
        /// of the device the application is running on.
        /// This currently overwrites the default WebRTC selection only on HoloLens devices.
        /// </summary>
        Automatic,

        /// <summary>
        /// Manually specify a video profile unique ID and/or a kind of video profile to use,
        /// and additional optional constraints on the resolution and framerate of that profile.
        /// </summary>
        Manual
    }

    /// <summary>
    /// Additional optional constraints applied to the resolution and framerate when selecting
    /// a video capture format.
    /// </summary>
    [Serializable]
    public struct VideoCaptureConstraints
    {
        /// <summary>
        /// Desired resolution width, in pixels, or zero for unconstrained.
        /// </summary>
        public int width;

        /// <summary>
        /// Desired resolution height, in pixels, or zero for unconstrained.
        /// </summary>
        public int height;

        /// <summary>
        /// Desired framerate, in frame-per-second, or zero for unconstrained.
        /// Note: the comparison is exact, and floating point imprecision may
        /// prevent finding a matching format. Use with caution.
        /// </summary>
        public double framerate;
    }

    /// <summary>
    /// This component represents a local video source added as a video track to an
    /// existing WebRTC peer connection and sent to the remote peer. The video track
    /// can optionally be displayed locally with a <see cref="MediaPlayer"/>.
    /// </summary>
    [AddComponentMenu("MixedReality-WebRTC/Local Video Source")]
    public class LocalVideoSource : VideoSource
    {
        /// <summary>
        /// Automatically start local video capture when this component is enabled.
        /// </summary>
        [Header("Local video capture")]
        [Tooltip("Automatically start local video capture when this component is enabled")]
        public bool AutoStartCapture = true;

        /// <summary>
        /// Name of the preferred video codec, or empty to let WebRTC decide.
        /// See https://en.wikipedia.org/wiki/RTP_audio_video_profile for the standard SDP names.
        /// </summary>
        [Tooltip("SDP name of the preferred video codec to use if supported")]
        public string PreferredVideoCodec = string.Empty;

        /// <summary>
        /// Enable Mixed Reality Capture (MRC) if available on the local device.
        /// This option has no effect on devices not supporting MRC, and is silently ignored.
        /// </summary>
        [Tooltip("Enable Mixed Reality Capture (MRC) if available on the local device")]
        public bool EnableMixedRealityCapture = true;

        /// <summary>
        /// Enable the on-screen recording indicator when Mixed Reality Capture (MRC) is
        /// available and enabled.
        /// This option has no effect on devices not supporting MRC, or if MRC is not enabled.
        /// </summary>
        [Tooltip("Enable the on-screen recording indicator when MRC is enabled")]
        public bool EnableMRCRecordingIndicator = true;

        /// <summary>
        /// Peer connection this local video source will add a video track to.
        /// </summary>
        [Header("Video track")]
        public PeerConnection PeerConnection;

        /// <summary>
        /// Name of the track. This will be sent in the SDP messages.
        /// </summary>
        [Tooltip("SDP track name.")]
        [SdpToken(allowEmpty: true)]
        public string TrackName;

        /// <summary>
        /// Automatically register as a video track when the peer connection is ready.
        /// </summary>
        /// <remarks>
        /// If this is <c>false</c> then the user needs to manually call
        /// <xref href="Microsoft.MixedReality.WebRTC.PeerConnection.AddLocalVideoTrackAsync(Microsoft.MixedReality.WebRTC.PeerConnection.VideoCaptureDevice,bool)"/>
        /// to add a video track to the peer connection and start sending video data to the remote peer.
        /// </remarks>
        public bool AutoAddTrack = true;

        /// <summary>
        /// Selection mode for the video capture format.
        /// </summary>
        public LocalVideoSourceFormatMode FormatMode = LocalVideoSourceFormatMode.Automatic;

        /// <summary>
        /// For manual <see cref="FormatMode"/>, unique identifier of the video profile to use,
        /// or an empty string to leave unconstrained.
        /// </summary>
        public string VideoProfileId = string.Empty;

        /// <summary>
        /// For manual <see cref="FormatMode"/>, kind of video profile to use among a list of predefined
        /// ones, or an empty string to leave unconstrained.
        /// </summary>
        public VideoProfileKind VideoProfileKind = VideoProfileKind.Unspecified;

        /// <summary>
        /// Video track added to the peer connection that this component encapsulates.
        /// </summary>
        public LocalVideoTrack Track { get; private set; }

        /// <summary>
        /// Frame queue holding the pending frames enqueued by the video source itself,
        /// which a video renderer needs to read and display.
        /// </summary>
        private VideoFrameQueue<I420AVideoFrameStorage> _frameQueue;

        /// <summary>
        /// For manual <see cref="FormatMode"/>, optional constraints on the resolution and framerate of
        /// the capture format. These constraints are additive, meaning a matching format must satisfy
        /// all of them at once, in addition of being restricted to the formats supported by the selected
        /// video profile or kind of profile.
        /// </summary>
        public VideoCaptureConstraints Constraints = new VideoCaptureConstraints()
        {
            width = 0,
            height = 0,
            framerate = 0.0
        };

        protected void Awake()
        {
            _frameQueue = new VideoFrameQueue<I420AVideoFrameStorage>(3);
            FrameQueue = _frameQueue;
            PeerConnection.OnInitialized.AddListener(OnPeerInitialized);
            PeerConnection.OnShutdown.AddListener(OnPeerShutdown);
        }

        protected void OnDestroy()
        {
            PeerConnection.OnInitialized.RemoveListener(OnPeerInitialized);
            PeerConnection.OnShutdown.RemoveListener(OnPeerShutdown);
        }

        /// <summary>
        /// Callback when the Unity component is enabled. This is the proper way to enable the
        /// video source and get it to start video capture and enqueue video frames.
        /// </summary>
        protected void OnEnable()
        {
            var nativePeer = PeerConnection?.Peer;
            if ((nativePeer != null) && nativePeer.Initialized)
            {
                DoAutoStartActions(nativePeer);
            }
        }

        /// <summary>
        /// Callback when the Unity component is disabled. This is the proper way to disable the
        /// video source and get it to stop video capture.
        /// </summary>
        protected void OnDisable()
        {
            var nativePeer = PeerConnection.Peer;
            if ((nativePeer != null) && nativePeer.Initialized)
            {
                if (Track != null)
                {
                    VideoStreamStopped.Invoke();
                    Track.I420AVideoFrameReady -= I420ALocalVideoFrameReady;
                    nativePeer.RemoveLocalVideoTrack(Track);
                    Track.Dispose();
                    Track = null;
                }
                _frameQueue.Clear();
            }
        }

        private void OnPeerInitialized()
        {
            var nativePeer = PeerConnection.Peer;
            nativePeer.PreferredVideoCodec = PreferredVideoCodec;

            // Only perform auto-start actions (add track, start capture) if the component
            // is enabled. Otherwise just do nothing, this component is idle.
            if (enabled)
            {
                DoAutoStartActions(nativePeer);
            }
        }

        private async void DoAutoStartActions(WebRTC.PeerConnection nativePeer)
        {
            // If a headset is active then do not capture local streams.
            if (XRDevice.isPresent)
            {
                return;
            }

            if (AutoAddTrack)
            {
                // This needs to be awaited because it will initialize Track, used below
                await AddLocalVideoTrackImplAsync(nativePeer);
            }

            if (AutoStartCapture && (Track != null))
            {
                Track.I420AVideoFrameReady += I420ALocalVideoFrameReady;
                // TODO - Currently AddLocalVideoTrackAsync() both open the capture device AND add a video track
            }
        }

        private async Task AddLocalVideoTrackImplAsync(WebRTC.PeerConnection nativePeer)
        {
            string videoProfileId = VideoProfileId;
            var videoProfileKind = VideoProfileKind;
            int width = Constraints.width;
            int height = Constraints.height;
            double framerate = Constraints.framerate;
#if ENABLE_WINMD_SUPPORT
            if (FormatMode == LocalVideoSourceFormatMode.Automatic)
            {
                // Do not constrain resolution by default, unless the device calls for it (see below).
                width = 0; // auto
                height = 0; // auto

                // Avoid constraining the framerate; this is generally not necessary (formats are listed
                // with higher framerates first) and is error-prone as some formats report 30.0 FPS while
                // others report 29.97 FPS.
                framerate = 0; // auto

                // For HoloLens, use video profile to reduce resolution and save power/CPU/bandwidth
                if (global::Windows.Graphics.Holographic.HolographicSpace.IsAvailable)
                {
                    if (!global::Windows.Graphics.Holographic.HolographicDisplay.GetDefault().IsOpaque)
                    {
                        if (global::Windows.ApplicationModel.Package.Current.Id.Architecture == global::Windows.System.ProcessorArchitecture.X86)
                        {
                            // Holographic AR (transparent) x86 platform - Assume HoloLens 1
                            videoProfileKind = WebRTC.VideoProfileKind.VideoRecording; // No profile in VideoConferencing
                            width = 896; // Target 896 x 504
                        }
                        else
                        {
                            // Holographic AR (transparent) non-x86 platform - Assume HoloLens 2
                            videoProfileKind = WebRTC.VideoProfileKind.VideoConferencing;
                            width = 1280; // Target 1280 x 720
                        }
                    }
                }
            }
#endif
            // Force again PreferredVideoCodec right before starting the local capture,
            // so that modifications to the property done after OnPeerInitialized() are
            // accounted for.
            nativePeer.PreferredVideoCodec = PreferredVideoCodec;

            // Ensure the track has a valid name
            string trackName = TrackName;
            if (trackName.Length == 0)
            {
                trackName = Guid.NewGuid().ToString();
                TrackName = trackName;
            }
            SdpTokenAttribute.Validate(trackName, allowEmpty: false);

            _frameQueue.Clear();

            var trackSettings = new LocalVideoTrackSettings
            {
                trackName = trackName,
                videoDevice = default,
                videoProfileId = videoProfileId,
                videoProfileKind = videoProfileKind,
                width = (width > 0 ? (uint?)width : null),
                height = (height > 0 ? (uint?)height : null),
                framerate = (framerate > 0 ? (double?)framerate : null),
                enableMrc = EnableMixedRealityCapture,
                enableMrcRecordingIndicator = EnableMRCRecordingIndicator
            };
            Track = await nativePeer.AddLocalVideoTrackAsync(trackSettings);
            if (Track != null)
            {
                VideoStreamStarted.Invoke();
            }
        }

        private void OnPeerShutdown()
        {
            var nativePeer = PeerConnection.Peer;
            if (Track != null)
            {
                VideoStreamStopped.Invoke();
                Track.I420AVideoFrameReady -= I420ALocalVideoFrameReady;
                nativePeer.RemoveLocalVideoTrack(Track);
                Track.Dispose();
                Track = null;
            }
            _frameQueue.Clear();
        }

        private void I420ALocalVideoFrameReady(I420AVideoFrame frame)
        {
            _frameQueue.Enqueue(frame);
        }
    }
}
