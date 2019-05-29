using System;
using UnityEngine;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// Provides video frame hooks for WebRTC
    /// </summary>
    public class VideoSource : MonoBehaviour
    {
        /// <summary>
        /// Peer connection this video source is attached to.
        /// </summary>
        [Tooltip("Peer connection this video source is attached to")]
        public PeerConnection PeerConnection;

        /// <summary>
        /// Instance of a local video player which will have frame data written to it.
        /// </summary>
        [Tooltip("Video player instance that local stream data will be written to")]
        public VideoPlayer LocalPlayer;

        /// <summary>
        /// Instance of a remote video player which will have frame data written to it.
        /// </summary>
        [Tooltip("Video player instance that remote stream data will be written to")]
        public VideoPlayer RemotePlayer;

        /// <summary>
        /// The underlying peer
        /// </summary>
        private WebRTC.PeerConnection _nativePeer = null;

        /// <summary>
        /// Internal queue used for storing and processing local video frames
        /// </summary>
        private VideoFrameQueue<I420VideoFrameStorage> localFrameQueue = new VideoFrameQueue<I420VideoFrameStorage>(3);

        /// <summary>
        /// Internal queue used for storing and processing remote video frames
        /// </summary>
        private VideoFrameQueue<I420VideoFrameStorage> remoteFrameQueue = new VideoFrameQueue<I420VideoFrameStorage>(5);


        public void Awake()
        {
            // Bind to the peer events to hook up video frame callbacks
            PeerConnection.OnInitialized.AddListener(OnPeerInitialized);
            PeerConnection.OnShutdown.AddListener(OnPeerShutdown);

            // Setup the video players frame queue references.
            LocalPlayer.FrameQueue = localFrameQueue;
            RemotePlayer.FrameQueue = remoteFrameQueue;
        }

        /// <summary>
        /// Unity Engine OnDestroy() hook
        /// </summary>
        /// <remarks>
        /// https://docs.unity3d.com/ScriptReference/MonoBehaviour.OnDestroy.html
        /// </remarks>
        private void OnDestroy()
        {
            PeerConnection.OnInitialized.RemoveListener(OnPeerInitialized);
            PeerConnection.OnShutdown.RemoveListener(OnPeerShutdown);
        }

        /// <summary>
        /// Initialization handler
        /// </summary>
        private void OnPeerInitialized()
        {
            // Cache the native peer connection object
            _nativePeer = GetComponent<PeerConnection>().Peer;

            // Register the video frame callbacks
            _nativePeer.I420LocalVideoFrameReady += Peer_LocalI420FrameReady;
            _nativePeer.I420RemoteVideoFrameReady += Peer_RemoteI420FrameReady;
        }

        private void OnPeerShutdown()
        {
            if (_nativePeer != null)
            {
                // Unregister the video frame callbacks
                _nativePeer.I420LocalVideoFrameReady -= Peer_LocalI420FrameReady;
                _nativePeer.I420RemoteVideoFrameReady -= Peer_RemoteI420FrameReady;

                // Release the reference to the underlying native peer connection object
                _nativePeer = null;
            }
        }

        private void Peer_LocalI420FrameReady(I420AVideoFrame frame)
        {
            localFrameQueue.Enqueue(frame);
        }

        private void Peer_RemoteI420FrameReady(I420AVideoFrame frame)
        {
            remoteFrameQueue.Enqueue(frame);
        }

        //private void Peer_LocalARGBFrameReady(ARGBVideoFrame frame)
        //{
        //    localFrameQueue.Enqueue(frame);
        //}

        //private void Peer_RemoteARGBFrameReady(ARGBVideoFrame frame)
        //{
        //    remoteFrameQueue.Enqueue(frame);
        //}
    }
}
