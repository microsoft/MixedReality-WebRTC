// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// This component represents a local audio source added as an audio track to an
    /// existing WebRTC peer connection and sent to the remote peer. The audio track
    /// can optionally be rendered locally with a <see cref="MediaPlayer"/>.
    /// </summary>
    [AddComponentMenu("MixedReality-WebRTC/Local Audio Source")]
    public class LocalAudioSource : AudioSource
    {
        /// <summary>
        /// Automatically start local audio capture when this component is enabled.
        /// </summary>
        [Header("Local audio capture")]
        [Tooltip("Automatically start local audio capture when this component is enabled")]
        public bool AutoStartCapture = true;

        /// <summary>
        /// Name of the preferred audio codec, or empty to let WebRTC decide.
        /// See https://en.wikipedia.org/wiki/RTP_audio_video_profile for the standard SDP names.
        /// </summary>
        [Tooltip("SDP name of the preferred audio codec to use if supported")]
        public string PreferredAudioCodec = string.Empty;

        /// <summary>
        /// Peer connection this local audio source will add an audio track to.
        /// </summary>
        [Header("Video track")]
        public PeerConnection PeerConnection;

        /// <summary>
        /// Automatically register as an audio track when the peer connection is ready.
        /// </summary>
        public bool AutoAddTrack = true;

        protected void Awake()
        {
            PeerConnection.OnInitialized.AddListener(OnPeerInitialized);
            PeerConnection.OnShutdown.AddListener(OnPeerShutdown);
        }

        protected void OnDestroy()
        {
            PeerConnection.OnInitialized.RemoveListener(OnPeerInitialized);
            PeerConnection.OnShutdown.RemoveListener(OnPeerShutdown);
        }

        protected async void OnEnable()
        {
            if (AutoAddTrack)
            {
                var nativePeer = PeerConnection?.Peer;
                if ((nativePeer != null) && nativePeer.Initialized)
                {
                    await nativePeer.AddLocalAudioTrackAsync();
                    AudioStreamStarted.Invoke();
                }
            }
        }

        protected void OnDisable()
        {
            var nativePeer = PeerConnection.Peer;
            if ((nativePeer != null) && nativePeer.Initialized)
            {
                AudioStreamStopped.Invoke();
                nativePeer.RemoveLocalAudioTrack();
            }
        }

        private void OnPeerInitialized()
        {
            var nativePeer = PeerConnection.Peer;

            nativePeer.PreferredAudioCodec = PreferredAudioCodec;

            if (AutoStartCapture)
            {
                //nativePeer.I420LocalVideoFrameReady += I420LocalVideoFrameReady;

                // TODO - Currently AddLocalVideoTrackAsync() both open the capture device AND add a video track
            }

            if (AutoAddTrack)
            {
                nativePeer.AddLocalAudioTrackAsync();
                AudioStreamStarted.Invoke();
            }
        }

        private void OnPeerShutdown()
        {
            AudioStreamStopped.Invoke();
            var nativePeer = PeerConnection.Peer;
            nativePeer.RemoveLocalAudioTrack();
            //nativePeer.I420LocalVideoFrameReady -= I420LocalVideoFrameReady;
        }

        //private void I420LocalVideoFrameReady(I420AVideoFrame frame)
        //{
        //    FrameQueue.Enqueue(frame);
        //}
    }
}
