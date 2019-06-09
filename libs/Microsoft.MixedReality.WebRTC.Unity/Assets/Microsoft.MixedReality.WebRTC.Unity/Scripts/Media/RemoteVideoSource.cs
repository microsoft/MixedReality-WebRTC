using System;
using UnityEngine;
using UnityEngine.Events;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    [Serializable]
    public class VideoTrackAddedEvent : UnityEvent
    {};

    [Serializable]
    public class VideoTrackRemovedEvent : UnityEvent
    { };

    /// <summary>
    /// This component represents a remote video source added as a video track to an
    /// existing WebRTC peer connection by a remote peer and received locally.
    /// The video track can optionally be displayed locally with a <see cref="VideoTrackPlayer"/>.
    /// </summary>
    public class RemoteVideoSource : VideoSource
    {
        /// <summary>
        /// Peer connection this remote video source is extracted from.
        /// </summary>
        [Header("Video track")]
        public PeerConnection PeerConnection;

        /// <summary>
        /// Automatically play the remote video track when it is added.
        /// </summary>
        public bool AutoPlayOnAdded = true;

        /// <summary>
        /// Event triggered when a remote video track is added remotely and received locally.
        /// </summary>
        public VideoTrackAddedEvent VideoTrackAdded = new VideoTrackAddedEvent();

        /// <summary>
        /// Event triggered when a remote video track is removed remotely and stops begin received locally.
        /// </summary>
        public VideoTrackRemovedEvent VideoTrackRemoved = new VideoTrackRemovedEvent();

        protected void Awake()
        {
            FrameQueue = new VideoFrameQueue<I420VideoFrameStorage>(5);
            PeerConnection.OnInitialized.AddListener(OnPeerInitialized);
            PeerConnection.OnShutdown.AddListener(OnPeerShutdown);
        }

        protected void OnDestroy()
        {
            PeerConnection.Peer.I420RemoteVideoFrameReady -= I420RemoteVideoFrameReady;
            PeerConnection.OnInitialized.RemoveListener(OnPeerInitialized);
            PeerConnection.OnShutdown.RemoveListener(OnPeerShutdown);
        }

        private void OnPeerInitialized()
        {
            if (AutoPlayOnAdded)
            {
                PeerConnection.Peer.I420RemoteVideoFrameReady += I420RemoteVideoFrameReady;
            }
        }

        private void OnPeerShutdown()
        {
            PeerConnection.Peer.I420RemoteVideoFrameReady -= I420RemoteVideoFrameReady;
        }

        private void I420RemoteVideoFrameReady(I420AVideoFrame frame)
        {
            FrameQueue.Enqueue(frame);
        }
    }
}
