// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using UnityEngine;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// This component represents a video track source, an entity which produces raw video
    /// frames for one or more tracks. The source can be added on a peer connection media
    /// line to be sent through that peer connection. It is a standalone object, independent
    /// of any peer connection, and can be shared with multiple of them.
    /// </summary>
    /// <seealso cref="WebcamSource"/>
    /// <seealso cref="CustomVideoSource{T}"/>
    /// <seealso cref="SceneVideoSource"/>
    public abstract class VideoTrackSource : MediaTrackSource
    {
        /// <summary>
        /// Video track source object from the underlying C# library that this component encapsulates.
        ///
        /// The object is owned by this component, which will create it and dispose of it automatically.
        /// </summary>
        public WebRTC.VideoTrackSource Source { get; private set; } = null;

        /// <summary>
        /// Event raised when the video stream started.
        ///
        /// When this event is raised, the followings are true:
        /// - The <see cref="Track"/> property is a valid local video track.
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
        /// - The <see cref="IsStreaming"/> has just become <c>false</c> right before the event
        ///   was raised, by design.
        /// </summary>
        /// <remarks>
        /// This event is raised from the main Unity thread to allow Unity object access.
        /// </remarks>
        public VideoStreamStoppedEvent VideoStreamStopped = new VideoStreamStoppedEvent();

        /// <inheritdoc/>
        public override bool IsLive => Source != null;

        /// <inheritdoc/>
        public override MediaKind MediaKind => MediaKind.Video;

        protected void AttachSource(WebRTC.VideoTrackSource source)
        {
            Source = source;
            AttachToMediaLines();
            VideoStreamStarted.Invoke(Source);
        }

        protected void DisposeSource()
        {
            if (Source != null)
            {
                VideoStreamStopped.Invoke(Source);
                DetachFromMediaLines();

                // Video track sources are disposable objects owned by the user (this component)
                Source.Dispose();
                Source = null;
            }
        }
    }
}
