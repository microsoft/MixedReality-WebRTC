// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

namespace Microsoft.MixedReality.WebRTC.Unity
{
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
        protected async void OnEnable()
        {
            if (AutoAddTrack)
            {
                var nativePeer = PeerConnection?.Peer;
                if ((nativePeer != null) && nativePeer.Initialized)
                {
                    await nativePeer.AddLocalVideoTrackAsync(default, EnableMixedRealityCapture);
                    VideoStreamStarted.Invoke();
                }
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
                nativePeer.RemoveLocalVideoTrack();
            }
        }

        private void OnPeerInitialized()
        {
            var nativePeer = PeerConnection.Peer;

            nativePeer.PreferredVideoCodec = PreferredVideoCodec;

            if (AutoStartCapture)
            {
                nativePeer.I420LocalVideoFrameReady += I420LocalVideoFrameReady;

                // TODO - Currently AddLocalVideoTrackAsync() both open the capture device AND add a video track
            }

            if (AutoAddTrack)
            {
                nativePeer.AddLocalVideoTrackAsync(default, EnableMixedRealityCapture);
                VideoStreamStarted.Invoke();
            }
        }

        private void OnPeerShutdown()
        {
            VideoStreamStopped.Invoke();
            var nativePeer = PeerConnection.Peer;
            nativePeer.RemoveLocalVideoTrack();
            nativePeer.I420LocalVideoFrameReady -= I420LocalVideoFrameReady;
        }

        private void I420LocalVideoFrameReady(I420AVideoFrame frame)
        {
            FrameQueue.Enqueue(frame);
        }
    }
}
