// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;
using UnityEngine;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// This component represents a local video source added as a video track to an
    /// existing WebRTC peer connection and sent to the remote peer. The video track
    /// can optionally be displayed locally with a <see cref="MediaPlayer"/>.
    /// </summary>
    /// <seealso cref="WebcamSource"/>
    /// <seealso cref="CustomVideoSender{T}"/>
    /// <seealso cref="SceneVideoSender"/>
    public abstract class VideoSender : MediaSender, IVideoSource
    {
        /// <summary>
        /// Name of the preferred video codec, or empty to let WebRTC decide.
        /// See https://en.wikipedia.org/wiki/RTP_audio_video_profile for the standard SDP names.
        /// </summary>
        [Tooltip("SDP name of the preferred video codec to use if supported")]
        public string PreferredVideoCodec = string.Empty;

        /// <summary>
        /// Event invoked from the main Unity app thread when the video stream starts.
        /// This means that video frames are available and any renderer should start polling.
        /// </summary>
        public VideoStreamStartedEvent VideoStreamStarted = new VideoStreamStartedEvent();

        /// <summary>
        /// Event invoked from the main Unity app thread when the video stream stops.
        /// This means that video frames are not produced anymore and any renderer should stop
        /// trying to poll the track to render them.
        /// </summary>
        public VideoStreamStoppedEvent VideoStreamStopped = new VideoStreamStoppedEvent();

        public bool IsStreaming { get; protected set; }

        public VideoStreamStartedEvent GetVideoStreamStarted() { return VideoStreamStarted; }
        public VideoStreamStoppedEvent GetVideoStreamStopped() { return VideoStreamStopped; }

        /// <summary>
        /// Video transceiver the local video track <see cref="Track"/> this components owns
        /// is added to, if any. If this is non-<c>null</c> and the peer connection the transceiver
        /// is owned by is connected, then the video frames produced by the local <see cref="Track"/>
        /// are sent through the <see cref="Transceiver"/> to the remote peer. That is,
        /// <see cref="Track"/> is attached as <see cref="VideoTransceiver.LocalTrack"/>.
        /// </summary>
        public Transceiver Transceiver { get; private set; }

        /// <inheritdoc/>
        public VideoEncoding FrameEncoding { get; } = VideoEncoding.I420A;

        /// <summary>
        /// Video track that this component encapsulates.
        /// </summary>
        public LocalVideoTrack Track { get; protected set; } = null;

        public VideoSender(VideoEncoding frameEncoding) : base(MediaKind.Video)
        {
            FrameEncoding = frameEncoding;
        }

        public void RegisterCallback(I420AVideoFrameDelegate callback)
        {
            if (Track != null)
            {
                Track.I420AVideoFrameReady += callback;
            }
        }

        public void UnregisterCallback(I420AVideoFrameDelegate callback)
        {
            if (Track != null)
            {
                Track.I420AVideoFrameReady -= callback;
            }
        }

        public void RegisterCallback(Argb32VideoFrameDelegate callback)
        {
            if (Track != null)
            {
                Track.Argb32VideoFrameReady += callback;
            }
        }

        public void UnregisterCallback(Argb32VideoFrameDelegate callback)
        {
            if (Track != null)
            {
                Track.Argb32VideoFrameReady -= callback;
            }
        }

        protected override async Task CreateLocalTrackAsync()
        {
            if (Track == null)
            {
                // Defer track creation to derived classes, which will invoke some methods like
                // LocalVideoTrack.CreateFromDeviceAsync() or LocalVideoTrack.CreateFromExternalSourceAsync().
                await CreateLocalVideoTrackAsync();
                Debug.Assert(Track != null, "Implementation did not create a valid Track property yet did not throw any exception.", this);

                VideoStreamStarted.Invoke(this);
                IsStreaming = true;
            }
        }

        protected override void DestroyLocalTrack()
        {
            if (Track != null)
            {
                IsStreaming = true;
                VideoStreamStopped.Invoke(this);

                // Defer track destruction to derived classes.
                DestroyLocalVideoTrack();
                Debug.Assert(Track == null, "Implementation did not destroy the existing Track property yet did not throw any exception.", this);
            }
        }

        /// <summary>
        /// Internal callback invoked when the video sender is attached to a transceiver created
        /// just before the peer connection creates an SDP offer.
        /// </summary>
        /// <param name="videoTransceiver">The video transceiver this sender is attached with.</param>
        internal void AttachToTransceiver(Transceiver videoTransceiver)
        {
            Debug.Assert((Transceiver == null) || (Transceiver == videoTransceiver));
            Transceiver = videoTransceiver;
        }

        internal async Task AttachTrackAsync()
        {
            Debug.Assert(Transceiver != null);

            // Ensure the local sender track exists
            if (Track == null)
            {
                await CreateLocalTrackAsync();
            }

            // Attach the local track to the transceiver
            if (Track != null)
            {
                Transceiver.LocalVideoTrack = Track;
            }
        }

        internal void DetachTrack()
        {
            Debug.Assert(Transceiver != null);
            Debug.Assert(Transceiver.LocalTrack == Track);
            Transceiver.LocalVideoTrack = null;
        }

        /// <inheritdoc/>
        protected override void MuteImpl(bool mute)
        {
            if (Track != null)
            {
                Track.Enabled = mute;
            }
        }

        /// <summary>
        /// Implement this callback to create the <see cref="Track"/> instance.
        /// On failure, this method must throw an exception. Otherwise it must set the <see cref="Track"/>
        /// property to a non-<c>null</c> instance.
        /// </summary>
        protected abstract Task CreateLocalVideoTrackAsync();

        /// <summary>
        /// Re-implement this callback to destroy the <see cref="Track"/> instance
        /// and other associated resources.
        /// </summary>
        protected virtual void DestroyLocalVideoTrack()
        {
            if (Track != null)
            {
                // Track may not be added to any transceiver (e.g. no connection)
                var transceiver = Track.Transceiver;
                if (transceiver != null)
                {
                    Debug.Assert(transceiver.LocalVideoTrack == Track);
                    transceiver.LocalVideoTrack = null;
                }

                // Local tracks are disposable objects owned by the user (this component)
                Track.Dispose();
                Track = null;
            }
        }
    }
}
