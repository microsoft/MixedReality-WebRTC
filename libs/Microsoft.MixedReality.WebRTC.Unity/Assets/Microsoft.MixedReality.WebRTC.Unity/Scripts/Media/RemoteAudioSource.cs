// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using UnityEngine;
using UnityEngine.Events;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// Unity event corresponding to a new audio track added to the current connection
    /// by the remote peer.
    /// </summary>
    [Serializable]
    public class AudioTrackAddedEvent : UnityEvent
    { };

    /// <summary>
    /// Unity event corresponding to an existing audio track removed from the current connection
    /// by the remote peer.
    /// </summary>
    [Serializable]
    public class AudioTrackRemovedEvent : UnityEvent
    { };

    /// <summary>
    /// This component represents a remote audio source added as an audio track to an
    /// existing WebRTC peer connection by a remote peer and received locally.
    /// The audio track can optionally be displayed locally with a <see cref="MediaPlayer"/>.
    /// </summary>
    [AddComponentMenu("MixedReality-WebRTC/Remote Audio Source")]
    public class RemoteAudioSource : AudioSource
    {
        /// <summary>
        /// Peer connection this remote audio source is extracted from.
        /// </summary>
        [Header("Audio track")]
        public PeerConnection PeerConnection;

        /// <summary>
        /// Automatically play the remote audio track when it is added.
        /// </summary>
        public bool AutoPlayOnAdded = true;

        /// <summary>
        /// Event triggered when a remote audio track is added remotely and received locally.
        /// </summary>
        public AudioTrackAddedEvent AudioTrackAdded = new AudioTrackAddedEvent();

        /// <summary>
        /// Event triggered when a remote audio track is removed remotely and stops begin received locally.
        /// </summary>
        public AudioTrackRemovedEvent AudioTrackRemoved = new AudioTrackRemovedEvent();

        protected void Awake()
        {
            //FrameQueue = new VideoFrameQueue<I420VideoFrameStorage>(5);
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
                //PeerConnection.Peer.I420RemoteVideoFrameReady += I420RemoteVideoFrameReady;
            }
        }

        private void OnPeerShutdown()
        {
            //PeerConnection.Peer.I420RemoteVideoFrameReady -= I420RemoteVideoFrameReady;
        }

        //private void I420RemoteVideoFrameReady(I420AVideoFrame frame)
        //{
        //    FrameQueue.Enqueue(frame);
        //}
    }
}
