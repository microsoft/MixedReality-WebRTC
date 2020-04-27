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
        [SdpToken(allowEmpty: true)]
        public string PreferredVideoCodec = string.Empty;

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

        public bool IsStreaming { get; protected set; }

        public VideoStreamStartedEvent GetVideoStreamStarted() { return VideoStreamStarted; }
        public VideoStreamStoppedEvent GetVideoStreamStopped() { return VideoStreamStopped; }

        /// <summary>
        /// Video transceiver this sender is paired with, if any.
        /// 
        /// This is <c>null</c> until a remote description is applied which pairs the media line
        /// the sender is associated with to a transceiver.
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

                // Dispatch the event to the main Unity app thread to allow Unity object access
                _mainThreadWorkQueue.Enqueue(() =>
                {
                    VideoStreamStarted.Invoke(this);

                    // Only clear this after the event handlers ran
                    IsStreaming = true;
                });
            }

            // Attach the local track to the transceiver
            if (Transceiver != null)
            {
                Transceiver.LocalVideoTrack = Track;
            }
        }

        protected override void DestroyLocalTrack()
        {
            if (Track != null)
            {
                // Detach the local track from the transceiver
                if ((Transceiver != null) && (Transceiver.LocalVideoTrack == Track))
                {
                    Transceiver.LocalVideoTrack = null;
                }

                // Defer track destruction to derived classes.
                DestroyLocalVideoTrack();
                Debug.Assert(Track == null, "Implementation did not destroy the existing Track property yet did not throw any exception.", this);

                // Clear this already to make sure it is false when the event is raised.
                IsStreaming = false;

                // Dispatch the event to the main Unity app thread to allow Unity object access
                _mainThreadWorkQueue.Enqueue(() =>
                {
                    VideoStreamStopped.Invoke(this);
                });
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

        /// <summary>
        /// Internal callback invoked when the video sender is detached from a transceiver about to be
        /// destroyed by the native implementation.
        /// </summary>
        /// <param name="videoTransceiver">The video transceiver this sender is attached with.</param>
        internal void DetachFromTransceiver(Transceiver videoTransceiver)
        {
            Debug.Assert((Transceiver == null) || (Transceiver == videoTransceiver));
            Transceiver = null;
        }

        internal override async Task AttachTrackAsync()
        {
            Debug.Assert(Transceiver != null);

            // Force again PreferredVideoCodec right before starting the local capture,
            // so that modifications to the property done after OnPeerInitialized() are
            // accounted for.
            //< FIXME - Multi-track override!!!
            if (!string.IsNullOrWhiteSpace(PreferredVideoCodec))
            {
                Transceiver.PeerConnection.PreferredVideoCodec = PreferredVideoCodec;
                Debug.LogWarning("PreferredVideoCodec is currently a per-PeerConnection setting; overriding the value for peer"
                    + $" connection '{Transceiver.PeerConnection.Name}' with track's value of '{PreferredVideoCodec}'.");
            }

            // Ensure the local sender track exists and is ready, but do not create it
            // if the component is not active.
            if (isActiveAndEnabled)
            {
                await StartCaptureAsync();
            }

            // Attach the local track to the transceiver if any
            if (Track != null)
            {
                Transceiver.LocalVideoTrack = Track;
            }
        }

        internal override void DetachTrack()
        {
            Debug.Assert(Transceiver != null);
            if (Track != null)
            {
                Debug.Assert(Transceiver.LocalVideoTrack == Track);
                Transceiver.LocalVideoTrack = null;
            }
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
                // Track may not be added to any transceiver (e.g. no connection), or the
                // transceiver is about to be destroyed so the DetachFromTransceiver() already
                // cleared it.
                var transceiver = Transceiver;
                if (transceiver != null)
                {
                    if (transceiver.LocalVideoTrack != null)
                    {
                        Debug.Assert(transceiver.LocalVideoTrack == Track);
                        transceiver.LocalVideoTrack = null;
                    }
                }

                // Local tracks are disposable objects owned by the user (this component)
                Track.Dispose();
                Track = null;
            }
        }
    }
}
