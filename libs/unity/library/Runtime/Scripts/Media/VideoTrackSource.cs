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
    public abstract class VideoTrackSource : VideoRendererSource, IVideoSource, IMediaTrackSource, IMediaTrackSourceInternal
    {
        /// <summary>
        /// Video track source object from the underlying C# library that this component encapsulates.
        ///
        /// The object is owned by this component, which will create it and dispose of it automatically.
        /// </summary>
        public WebRTC.VideoTrackSource Source { get; protected set; } = null;

        /// <summary>
        /// List of video media lines using this source.
        /// </summary>
        public IReadOnlyList<MediaLine> MediaLines => _mediaLines;

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


        #region IVideoSource interface

        /// <inheritdoc/>
        public bool IsStreaming { get; protected set; }

        /// <inheritdoc/>
        public VideoStreamStartedEvent GetVideoStreamStarted() { return VideoStreamStarted; }

        /// <inheritdoc/>
        public VideoStreamStoppedEvent GetVideoStreamStopped() { return VideoStreamStopped; }

        /// <inheritdoc/>
        public VideoEncoding FrameEncoding { get; } = VideoEncoding.I420A;

        /// <inheritdoc/>
        public void RegisterCallback(I420AVideoFrameDelegate callback)
        {
            if (Source != null)
            {
                Source.VideoFrameReady += callback;
            }
        }

        /// <inheritdoc/>
        public void UnregisterCallback(I420AVideoFrameDelegate callback)
        {
            if (Source != null)
            {
                Source.VideoFrameReady -= callback;
            }
        }

        /// <inheritdoc/>
        public void RegisterCallback(Argb32VideoFrameDelegate callback)
        {
            if (Source != null)
            {
                Source.ARGB32VideoFrameReady += callback;
            }
        }

        /// <inheritdoc/>
        public void UnregisterCallback(Argb32VideoFrameDelegate callback)
        {
            if (Source != null)
            {
                Source.ARGB32VideoFrameReady -= callback;
            }
        }

        #endregion


        #region IMediaTrackSource

        /// <inheritdoc/>
        MediaKind IMediaTrackSource.MediaKind => MediaKind.Video;

        #endregion



        public VideoTrackSource(VideoEncoding frameEncoding)
        {
            FrameEncoding = frameEncoding;
        }

        private readonly List<MediaLine> _mediaLines = new List<MediaLine>();

        protected virtual void OnDisable()
        {
            if (Source != null)
            {
                // Notify media lines using this source.
                foreach (var ml in _mediaLines)
                {
                    ml.OnSourceDestroyed();
                }
                _mediaLines.Clear();

                // Video track sources are disposable objects owned by the user (this component)
                Source.Dispose();
                Source = null;

                VideoStreamStopped.Invoke(this);
                IsStreaming = false;
            }
        }

        void IMediaTrackSourceInternal.OnAddedToMediaLine(MediaLine mediaLine)
        {
            Debug.Assert(!_mediaLines.Contains(mediaLine));
            _mediaLines.Add(mediaLine);
        }

        void IMediaTrackSourceInternal.OnRemoveFromMediaLine(MediaLine mediaLine)
        {
            bool removed = _mediaLines.Remove(mediaLine);
            Debug.Assert(removed);
        }
    }
}
