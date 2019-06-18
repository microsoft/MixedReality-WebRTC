// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using UnityEngine;
using UnityEngine.Events;

namespace Microsoft.MixedReality.WebRTC.Unity
{
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

        protected void Awake()
        {
            FrameQueue = new VideoFrameQueue<I420VideoFrameStorage>(5);
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
            if (AutoPlayOnAdded)
            {
                PeerConnection.Peer.TrackAdded += TrackAdded;
                PeerConnection.Peer.TrackRemoved += TrackRemoved;
                PeerConnection.Peer.I420RemoteVideoFrameReady += I420RemoteVideoFrameReady;
            }
        }

        private void TrackAdded()
        {
            VideoStreamStarted.Invoke();
        }

        private void TrackRemoved()
        {
            VideoStreamStopped.Invoke();
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
