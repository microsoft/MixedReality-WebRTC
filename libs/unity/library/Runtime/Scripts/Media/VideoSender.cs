// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// This component represents a local video source added as a video track to an
    /// existing WebRTC peer connection and sent to the remote peer. It wraps a video
    /// track source and bridges it with a video transceiver. Internally it manages
    /// a local video track (<see cref="LocalVideoTrack"/>).
    /// 
    /// This class is typically instantiated and managed by the peer connection automatically
    /// where needed, and users typically do not have to interact directly with it.
    /// </summary>
    public class VideoSender : MediaSender, IDisposable
    {
        /// <summary>
        /// Name of the preferred video codec, or empty to let WebRTC decide.
        /// See https://en.wikipedia.org/wiki/RTP_audio_video_profile for the standard SDP names.
        /// </summary>
        [Tooltip("SDP name of the preferred video codec to use if supported")]
        [SdpToken(allowEmpty: true)]
        public string PreferredVideoCodec = string.Empty;

        /// <summary>
        /// Video source providing frames to this video sender.
        /// </summary>
        public VideoTrackSource Source { get; private set; } = null;

        /// <summary>
        /// Video track that this component encapsulates.
        /// </summary>
        public LocalVideoTrack Track { get; protected set; } = null;

        public static VideoSender CreateFromSource(VideoTrackSource source, string trackName)
        {
            var initConfig = new LocalVideoTrackInitConfig
            {
                trackName = trackName
            };
            var track = LocalVideoTrack.CreateFromSource(source.Source, initConfig);
            return new VideoSender(source, track);
        }

        protected VideoSender(VideoTrackSource source, LocalVideoTrack track) : base(MediaKind.Video)
        {
            Source = source;
            Track = track;
            Source.OnSenderAdded(this);
        }

        public void Dispose()
        {
            if (Track != null)
            {
                // Detach the local track from the transceiver
                if ((Transceiver != null) && (Transceiver.LocalVideoTrack == Track))
                {
                    Transceiver.LocalVideoTrack = null;
                }

                // Detach from source
                Debug.Assert(Source != null);
                Source.OnSenderRemoved(this);
                Source = null;

                // Local tracks are disposable objects owned by the user (this component)
                Track.Dispose();
                Track = null;
            }

            Source = null;
        }

        internal override void AttachTrack()
        {
            Debug.Assert(Transceiver != null);
            Debug.Assert(Track != null);

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

            // Attach the local track to the transceiver
            Transceiver.LocalVideoTrack = Track;
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
    }
}
