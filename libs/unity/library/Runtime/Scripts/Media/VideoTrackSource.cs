// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
    /// <seealso cref="CustomVideoSender{T}"/>
    /// <seealso cref="SceneVideoSender"/>
    public abstract class VideoTrackSource : VideoRendererSource, IVideoSource, IMediaTrackSource
    {
        /// <summary>
        /// Video track source object from the underlying C# library that this component encapsulates.
        /// 
        /// The object is owned by this component, which will create it and dispose of it automatically.
        /// </summary>
        public WebRTC.VideoTrackSource Source { get; protected set; } = null;

        /// <summary>
        /// List of video senders (tracks) using this source.
        /// </summary>
        public List<VideoSender> Senders { get; } = new List<VideoSender>();

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

        protected virtual async Task OnEnable()
        {
            if (Source == null)
            {
                // Defer track creation to derived classes, which will invoke some methods like
                // VideoTrackSource.CreateFromDeviceAsync() or VideoTrackSource.CreateFromExternalSourceAsync().
                await CreateVideoTrackSourceAsync();
                Debug.Assert(Source != null, "Implementation did not create a valid Source property yet did not throw any exception.", this);

                // Dispatch the event to the main Unity app thread to allow Unity object access
                InvokeOnAppThread(() =>
                {
                    VideoStreamStarted.Invoke(this);

                    // Only clear this after the event handlers ran
                    IsStreaming = true;
                });
            }
        }

        protected virtual void OnDisable()
        {
            if (Source != null)
            {
                // Defer track destruction to derived classes.
                DestroyVideoTrackSource();
                Debug.Assert(Source == null, "Implementation did not destroy the existing Source property yet did not throw any exception.", this);

                // Clear this already to make sure it is false when the event is raised.
                IsStreaming = false;

                // Dispatch the event to the main Unity app thread to allow Unity object access
                InvokeOnAppThread(() => VideoStreamStopped.Invoke(this));
            }
        }


        /// <summary>
        /// Implement this callback to create the <see cref="Track"/> instance.
        /// On failure, this method must throw an exception. Otherwise it must set the <see cref="Track"/>
        /// property to a non-<c>null</c> instance.
        /// </summary>
        protected abstract Task CreateVideoTrackSourceAsync();

        /// <summary>
        /// Re-implement this callback to destroy the <see cref="Track"/> instance
        /// and other associated resources.
        /// </summary>
        protected virtual void DestroyVideoTrackSource()
        {
            if (Source != null)
            {
                // Notify senders using that source
                while (Senders.Count > 0) // Dispose() calls OnSenderRemoved() which will modify the collection
                {
                    Senders[Senders.Count - 1].Dispose();
                }

                // Video track sources are disposable objects owned by the user (this component)
                Source.Dispose();
                Source = null;
            }
        }

        internal void OnSenderAdded(VideoSender sender)
        {
            Debug.Assert(!Senders.Contains(sender));
            Senders.Add(sender);
        }

        internal void OnSenderRemoved(VideoSender sender)
        {
            bool removed = Senders.Remove(sender);
            Debug.Assert(removed);
        }
    }
}
