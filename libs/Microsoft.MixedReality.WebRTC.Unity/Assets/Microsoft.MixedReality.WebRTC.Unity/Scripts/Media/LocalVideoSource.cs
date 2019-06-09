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
            PeerConnection.Peer.I420LocalVideoFrameReady -= I420LocalVideoFrameReady;
            PeerConnection.OnInitialized.RemoveListener(OnPeerInitialized);
            PeerConnection.OnShutdown.RemoveListener(OnPeerShutdown);
        }

        protected void OnEnable()
        {
            if (AutoStartCapture)
            {
                // TODO - Currently AddLocalVideoTrackAsync() both open the capture device AND add a video track
            }

            if (AutoAddTrack)
            {
                var nativePeer = PeerConnection.Peer;
                if (nativePeer.Initialized)
                {
                    nativePeer.AddLocalVideoTrackAsync(default, EnableMixedRealityCapture);
                }
                else
                {
                    //< TODO - Race condition here if PeerConnection finished intializing after the if() test above
                    PeerConnection.OnInitialized.AddListener(() =>
                    {
                        PeerConnection.Peer.AddLocalVideoTrackAsync(default, EnableMixedRealityCapture);
                    });
                }
            }
        }

        protected void OnDisable()
        {
            PeerConnection.Peer.RemoveLocalVideoTrack();
        }

        private void OnPeerInitialized()
        {
            if (AutoStartCapture)
            {
                PeerConnection.Peer.I420LocalVideoFrameReady += I420LocalVideoFrameReady;
            }
        }

        private void OnPeerShutdown()
        {
            PeerConnection.Peer.I420LocalVideoFrameReady -= I420LocalVideoFrameReady;
        }

        private void I420LocalVideoFrameReady(I420AVideoFrame frame)
        {
            FrameQueue.Enqueue(frame);
        }
    }
}
