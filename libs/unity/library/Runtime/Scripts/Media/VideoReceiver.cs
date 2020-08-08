// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using UnityEngine;
using UnityEngine.Events;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// Unity event corresponding to a new video stream being started.
    /// </summary>
    [Serializable]
    public class VideoStreamStartedEvent : UnityEvent<IVideoSource>
    { };

    /// <summary>
    /// Unity event corresponding to an on-going video stream being stopped.
    /// </summary>
    [Serializable]
    public class VideoStreamStoppedEvent : UnityEvent<IVideoSource>
    { };

    /// <summary>
    /// Endpoint for a WebRTC remote video track.
    /// </summary>
    /// <remarks>
    /// Setting this on a video <see cref="MediaLine"/> will enable the corresponding transceiver to receive.
    /// A remote track will be exposed through <see cref="VideoTrack"/> once a connection is established.
    /// The video track can optionally be displayed locally with a <see cref="VideoRenderer"/>.
    /// </remarks>
    [AddComponentMenu("MixedReality-WebRTC/Video Receiver")]
    public class VideoReceiver : MediaReceiver
    {
        /// <summary>
        /// Remote video track receiving data from the remote peer.
        ///
        /// This is <c>null</c> until <see cref="MediaLine.Transceiver"/> is set to a non-null value
        /// and a remote track is added to that transceiver.
        /// </summary>
        public RemoteVideoTrack VideoTrack { get; private set; }

        /// <summary>
        /// Event raised when the video stream started.
        ///
        /// When this event is raised, the followings are true:
        /// - The <see cref="Track"/> property is a valid remote video track.
        /// - The <see cref="MediaReceiver.IsLive"/> property is <c>true</c>.
        /// </summary>
        /// <remarks>
        /// This event is raised from the main Unity thread to allow Unity object access.
        /// </remarks>
        public VideoStreamStartedEvent VideoStreamStarted = new VideoStreamStartedEvent();

        /// <summary>
        /// Event raised when the video stream stopped.
        ///
        /// When this event is raised, the followings are true:
        /// - The <see cref="Track"/> property is <c>null</c>.
        /// - The <see cref="MediaReceiver.IsLive"/> property is <c>false</c>.
        /// </summary>
        /// <remarks>
        /// This event is raised from the main Unity thread to allow Unity object access.
        /// </remarks>
        public VideoStreamStoppedEvent VideoStreamStopped = new VideoStreamStoppedEvent();


        /// <inheritdoc/>
        public override MediaKind MediaKind => MediaKind.Video;

        /// <inheritdoc/>
        public override MediaTrack Track => VideoTrack;

        /// <inheritdoc/>
        protected internal override void OnPaired(MediaTrack track)
        {
            var remoteVideoTrack = (RemoteVideoTrack)track;
            Debug.Assert(VideoTrack == null);
            VideoTrack = remoteVideoTrack;
            VideoStreamStarted.Invoke(VideoTrack);
        }

        /// <inheritdoc/>
        protected internal override void OnUnpaired(MediaTrack track)
        {
            Debug.Assert(track is RemoteVideoTrack);
            Debug.Assert(VideoTrack == track);
            VideoTrack = null;
            VideoStreamStopped.Invoke(VideoTrack);
        }
    }
}
