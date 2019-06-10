using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// This component represents a local video source added as a video track to an
    /// existing WebRTC peer connection and sent to the remote peer. The video track
    /// can optionally be displayed locally with a <see cref="VideoTrackPlayer"/>.
    /// </summary>
    public class LocalVideoSource : VideoSource
    {
        /// <summary>
        /// Automatically start local video capture when this component is enabled.
        /// </summary>
        [Header("Local video capture")]
        [Tooltip("Automatically start local video capture when this component is enabled")]
        public bool AutoStartCapture = true;

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

        private void OnPeerInitialized()
        {
            var nativePeer = PeerConnection.Peer;

            if (AutoStartCapture)
            {
                nativePeer.I420LocalVideoFrameReady += I420LocalVideoFrameReady;

                // TODO - Currently AddLocalVideoTrackAsync() both open the capture device AND add a video track
            }

            if (AutoAddTrack)
            {
                nativePeer.AddLocalVideoTrackAsync(default, EnableMixedRealityCapture);
            }
        }

        private void OnPeerShutdown()
        {
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
