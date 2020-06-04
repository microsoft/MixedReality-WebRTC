// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using UnityEngine;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// This component represents a local audio source added as an audio track to an
    /// existing WebRTC peer connection and sent to the remote peer. It wraps an audio
    /// track source and bridges it with an audio transceiver. Internally it manages
    /// a local audio track (<see cref="LocalAudioTrack"/>).
    /// 
    /// This class is typically instantiated and managed by the peer connection automatically
    /// where needed, and users typically do not have to interact directly with it.
    /// </summary>
    public class AudioSender : MediaSender, IDisposable
    {
        /// <summary>
        /// Name of the preferred audio codec, or empty to let WebRTC decide.
        /// See https://en.wikipedia.org/wiki/RTP_audio_video_profile for the standard SDP names.
        /// </summary>
        [Tooltip("SDP name of the preferred audio codec to use if supported")]
        [SdpToken(allowEmpty: true)]
        public string PreferredAudioCodec = string.Empty;

        /// <summary>
        /// Audio source providing frames to this audio sender.
        /// </summary>
        public AudioTrackSource Source { get; private set; } = null;

        /// <summary>
        /// Local audio track that this component encapsulates, which if paired sends data to
        /// the remote peer.
        /// </summary>
        public LocalAudioTrack Track { get; protected set; } = null;

        public static AudioSender CreateFromSource(AudioTrackSource source, string trackName)
        {
            var initConfig = new LocalAudioTrackInitConfig
            {
                trackName = trackName
            };
            var track = LocalAudioTrack.CreateFromSource(source.Source, initConfig);
            return new AudioSender(source, track);
        }

        public void Dispose()
        {
            if (Track != null)
            {
                // Detach the local track from the transceiver
                if ((Transceiver != null) && (Transceiver.LocalAudioTrack == Track))
                {
                    Transceiver.LocalAudioTrack = null;
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

        protected AudioSender(AudioTrackSource source, LocalAudioTrack track) : base(MediaKind.Audio)
        {
            Source = source;
            Track = track;
            Source.OnSenderAdded(this);
        }

        /// <summary>
        /// Internal callback invoked when a peer connection is about to create an offer,
        /// and needs to create the audio transceivers and senders. The audio sender must
        /// create a local audio track and attach it to the given transceiver.
        /// </summary>
        internal override void AttachTrack()
        {
            Debug.Assert(Transceiver != null);
            Debug.Assert(Track != null);

            // Force again PreferredAudioCodec right before starting the local capture,
            // so that modifications to the property done after OnPeerInitialized() are
            // accounted for.
            //< FIXME - Multi-track override!!!
            if (!string.IsNullOrWhiteSpace(PreferredAudioCodec))
            {
                Transceiver.PeerConnection.PreferredAudioCodec = PreferredAudioCodec;
                Debug.LogWarning("PreferredAudioCodec is currently a per-PeerConnection setting; overriding the value for peer"
                    + $" connection '{Transceiver.PeerConnection.Name}' with track's value of '{PreferredAudioCodec}'.");
            }

            // Attach the local track to the transceiver
            Transceiver.LocalAudioTrack = Track;
        }

        internal override void DetachTrack()
        {
            Debug.Assert(Transceiver != null);
            if (Track != null)
            {
                Debug.Assert(Transceiver.LocalAudioTrack == Track);
                Transceiver.LocalAudioTrack = null;
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
