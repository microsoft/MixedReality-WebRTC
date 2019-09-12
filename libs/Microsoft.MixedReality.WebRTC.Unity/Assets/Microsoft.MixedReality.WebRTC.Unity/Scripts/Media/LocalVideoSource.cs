// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using UnityEngine;

#if ENABLE_WINMD_SUPPORT
using Windows.Graphics.Holographic;
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
        /// This option has no effect on devices not supporting MRC.
        /// </summary>
        [Tooltip("Enable Mixed Reality Capture (MRC) if available on the local device")]
        public bool EnableMixedRealityCapture = true;

        /// <summary>
        /// Peer connection this local video source will add a video track to.
        /// </summary>
        [Header("Video track")]
        public PeerConnection PeerConnection;

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
        public LocalVideoSourceFormatMode Mode = LocalVideoSourceFormatMode.Automatic;

        /// <summary>
        /// For manual <see cref="Mode"/>, unique identifier of the video profile to use,
        /// or an empty string to leave unconstrained.
        /// </summary>
        public string VideoProfileId = string.Empty;

        /// <summary>
        /// For manual <see cref="Mode"/>, kind of video profile to use among a list of predefined
        /// ones, or an empty string to leave unconstrained.
        /// </summary>
        public WebRTC.PeerConnection.VideoProfileKind VideoProfileKind = WebRTC.PeerConnection.VideoProfileKind.Unspecified;

        /// <summary>
        /// For manual <see cref="Mode"/>, optional constraints on the resolution and framerate of
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
            FrameQueue = new VideoFrameQueue<I420VideoFrameStorage>(3);
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
                VideoStreamStopped.Invoke();
                nativePeer.I420LocalVideoFrameReady -= I420LocalVideoFrameReady;
                nativePeer.RemoveLocalVideoTrack();
                FrameQueue.Clear();
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

        private void DoAutoStartActions(WebRTC.PeerConnection nativePeer)
        {
            if (AutoStartCapture)
            {
                nativePeer.I420LocalVideoFrameReady += I420LocalVideoFrameReady;

                // TODO - Currently AddLocalVideoTrackAsync() both open the capture device AND add a video track
            }

            if (AutoAddTrack)
            {
                AddLocalVideoTrackImpl(nativePeer);
            }
        }

        private void AddLocalVideoTrackImpl(WebRTC.PeerConnection nativePeer)
        {
            string videoProfileId = VideoProfileId;
            var videoProfileKind = VideoProfileKind;
            int width = Constraints.width;
            int height = Constraints.height;
            double framerate = Constraints.framerate;
#if ENABLE_WINMD_SUPPORT
            if (Mode == LocalVideoSourceFormatMode.Automatic)
            {
                // Do not constrain resolution by default, unless the device calls for it (see below).
                width = 0; // auto
                height = 0; // auto

                // Avoid constraining the framerate; this is generally not necessary (formats are listed
                // with higher framerates first) and is error-prone as some formats report 30.0 FPS while
                // others report 29.97 FPS.
                framerate = 0; // auto

                // For HoloLens, use video profile to reduce resolution and save power/CPU/bandwidth
                if (!Windows.Graphics.Holographic.HolographicDisplay.GetDefault().IsOpaque)
                {
                    if (Windows.ApplicationModel.Package.Current.Id.Architecture == Windows.System.ProcessorArchitecture.X86)
                    {
                        // Holographic AR (transparent) x86 platform - Assume HoloLens 1
                        videoProfileKind = WebRTC.PeerConnection.VideoProfileKind.VideoRecording; // No profile in VideoConferencing
                        width = 896; // Target 896 x 504
                    }
                    else
                    {
                        // Holographic AR (transparent) non-x86 platform - Assume HoloLens 2
                        videoProfileKind = WebRTC.PeerConnection.VideoProfileKind.VideoConferencing;
                        width = 1280; // Target 1280 x 720
                    }
                }
            }
#endif
            // Force again PreferredVideoCodec right before starting the local capture,
            // so that modifications to the property done after OnPeerInitialized() are
            // accounted for.
            nativePeer.PreferredVideoCodec = PreferredVideoCodec;

            FrameQueue.Clear();
            nativePeer.AddLocalVideoTrackAsync(default, videoProfileId: videoProfileId, videoProfileKind: videoProfileKind,
                width: width, height: height, framerate: framerate, enableMrc: EnableMixedRealityCapture);
            VideoStreamStarted.Invoke();
        }

        private void OnPeerShutdown()
        {
            VideoStreamStopped.Invoke();
            var nativePeer = PeerConnection.Peer;
            nativePeer.I420LocalVideoFrameReady -= I420LocalVideoFrameReady;
            nativePeer.RemoveLocalVideoTrack();
            FrameQueue.Clear();
        }

        private void I420LocalVideoFrameReady(I420AVideoFrame frame)
        {
            FrameQueue.Enqueue(frame);
        }
    }
}
