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
    /// This component represents a remote video source added as a video track to an
    /// existing WebRTC peer connection by a remote peer and received locally.
    /// The video track can optionally be displayed locally with a <see cref="VideoRenderer"/>.
    /// </summary>
    [AddComponentMenu("MixedReality-WebRTC/Video Receiver")]
    public class VideoReceiver : MediaReceiver
    {
        /// <summary>
        /// Remote video track receiving data from the remote peer.
        ///
        /// This is <c>null</c> until <see cref="IMediaReceiver.Transceiver"/> is set to a non-null value
        /// and a remote track is added to that transceiver.
        /// </summary>
        public RemoteVideoTrack VideoTrack { get; private set; }

        /// <summary>
        /// Event raised when the video stream started.
        ///
        /// When this event is raised, the followings are true:
        /// - The <see cref="Track"/> property is a valid remote video track.
        /// - The <see cref="MediaReceiver.IsLive"/> property is <c>true</c>.
        /// - The <see cref="IsStreaming"/> will become <c>true</c> just after the event
        ///   is raised, by design.
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
        /// - The <see cref="IsStreaming"/> has just become <c>false</c> right before the event
        ///   was raised, by design.
        /// </summary>
        /// <remarks>
        /// This event is raised from the main Unity thread to allow Unity object access.
        /// </remarks>
        public VideoStreamStoppedEvent VideoStreamStopped = new VideoStreamStoppedEvent();

        #region IMediaReceiver interface

        /// <inheritdoc/>
        public override MediaKind MediaKind => MediaKind.Video;

        public override MediaTrack Track => VideoTrack;

        #endregion

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
