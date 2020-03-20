// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using UnityEngine;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// This component represents a remote video source added as a video track to an
    /// existing WebRTC peer connection by a remote peer and received locally.
    /// The video track can optionally be displayed locally with a <see cref="MediaPlayer"/>.
    /// </summary>
    [AddComponentMenu("MixedReality-WebRTC/Video Receiver")]
    public class VideoReceiver : MediaReceiver, IVideoSource
    {
        /// <summary>
        /// Video transceiver this receiver is paired with.
        /// This is <c>null</c> until a remote description is applied which pairs the receiver
        /// with the remote track of the transceiver, or until the peer connection associated
        /// with this receiver creates the video receiver right before creating an SDP offer.
        /// </summary>
        public Transceiver Transceiver { get; private set; }

        /// <summary>
        /// Remote video track receiving data from the remote peer.
        /// This is <c>null</c> until a remote description is applied which pairs the receiver
        /// with the remote track of the transceiver.
        /// </summary>
        public RemoteVideoTrack Track { get; private set; }

        /// <summary>
        /// Event invoked from the main Unity thread when the video stream starts.
        /// This means that video frames are available and the renderer should start polling.
        /// </summary>
        public VideoStreamStartedEvent VideoStreamStarted = new VideoStreamStartedEvent();

        /// <summary>
        /// Event invoked from the main Unity thread when the video stream stops.
        /// This means that the video frame queue is not populated anymore, though some frames
        /// may still be present in it that may be rendered.
        /// </summary>
        public VideoStreamStoppedEvent VideoStreamStopped = new VideoStreamStoppedEvent();

        public bool IsStreaming { get; protected set; }

        public VideoStreamStartedEvent GetVideoStreamStarted() { return VideoStreamStarted; }
        public VideoStreamStoppedEvent GetVideoStreamStopped() { return VideoStreamStopped; }

        /// <inheritdoc/>
        public VideoEncoding FrameEncoding { get; } = VideoEncoding.I420A;

        public VideoReceiver() : base(MediaKind.Video)
        {
        }

        /// <summary>
        /// Register a frame callback to listen to incoming video data receiving through this
        /// video receiver from the remote peer.
        /// The callback can only be registered once the <see cref="Track"/> is valid, that is
        /// once the <see cref="VideoStreamStarted"/> event was triggered.
        /// </summary>
        /// <param name="callback">The new frame callback to register.</param>
        /// <remarks>
        /// A typical application uses this callback to display the received video.
        /// </remarks>
        public void RegisterCallback(I420AVideoFrameDelegate callback)
        {
            if (Track != null)
            {
                Track.I420AVideoFrameReady += callback;
            }
        }

        /// <summary>
        /// Register a frame callback to listen to incoming video data receiving through this
        /// video receiver from the remote peer.
        /// The callback can only be registered once the <see cref="Track"/> is valid, that is
        /// once the <see cref="VideoStreamStarted"/> event was triggered.
        /// </summary>
        /// <param name="callback">The new frame callback to register.</param>
        /// <remarks>
        /// A typical application uses this callback to display the received video.
        /// </remarks>
        public void RegisterCallback(Argb32VideoFrameDelegate callback)
        {
            if (Track != null)
            {
                Track.Argb32VideoFrameReady += callback;
            }
        }

        /// <summary>
        /// Unregister an existing frame callback registered with <see cref="RegisterCallback(I420AVideoFrameDelegate)"/>.
        /// </summary>
        /// <param name="callback">The frame callback to unregister.</param>
        public void UnregisterCallback(I420AVideoFrameDelegate callback)
        {
            if (Track != null)
            {
                Track.I420AVideoFrameReady -= callback;
            }
        }

        /// <summary>
        /// Unregister an existing frame callback registered with <see cref="RegisterCallback(Argb32VideoFrameDelegate)"/>.
        /// </summary>
        /// <param name="callback">The frame callback to unregister.</param>
        public void UnregisterCallback(Argb32VideoFrameDelegate callback)
        {
            if (Track != null)
            {
                Track.Argb32VideoFrameReady -= callback;
            }
        }

        /// <summary>
        /// Internal callback invoked when the video receiver is attached to a transceiver created
        /// just before the peer connection creates an SDP offer.
        /// </summary>
        /// <param name="videoTransceiver">The video transceiver this receiver is attached with.</param>
        /// <remarks>
        /// At this time the transceiver does not yet contain a remote track. The remote track will be
        /// created when receiving an answer from the remote peer, if it agreed to send media data through
        /// that transceiver, and <see cref="OnPaired"/> will be invoked at that time.
        /// </remarks>
        internal void AttachToTransceiver(Transceiver videoTransceiver)
        {
            Debug.Assert((Transceiver == null) || (Transceiver == videoTransceiver));
            Transceiver = videoTransceiver;
        }

        /// <summary>
        /// Free-threaded callback invoked by the owning peer connection when a track is paired
        /// with this receiver, which enqueues the <see cref="VideoSource.VideoStreamStarted"/>
        /// event to be fired from the main Unity thread.
        /// </summary>
        internal override void OnPaired(MediaTrack track)
        {
            Debug.Assert(track is RemoteVideoTrack);
            var remoteVideoTrack = (RemoteVideoTrack)track;

            // Enqueue invoking from the main Unity app thread, both to avoid locks on public
            // properties and so that listeners of the event can directly access Unity objects
            // from their handler function.
            _mainThreadWorkQueue.Enqueue(() =>
            {
                Debug.Assert(Track == null);
                Track = remoteVideoTrack;
                IsLive = true;
                VideoStreamStarted.Invoke(this);
                IsStreaming = true;
            });
        }

        /// <summary>
        /// Free-threaded callback invoked by the owning peer connection when a track is unpaired
        /// from this receiver, which enqueues the <see cref="VideoSource.VideoStreamStopped"/>
        /// event to be fired from the main Unity thread.
        /// </summary>
        internal override void OnUnpaired(MediaTrack track)
        {
            Debug.Assert(track is RemoteVideoTrack);

            // Enqueue invoking from the main Unity app thread, both to avoid locks on public
            // properties and so that listeners of the event can directly access Unity objects
            // from their handler function.
            _mainThreadWorkQueue.Enqueue(() =>
            {
                Debug.Assert(Track == track);
                IsStreaming = false;
                VideoStreamStopped.Invoke(this);
                Track = null;
                IsLive = false;
            });
        }
    }
}
