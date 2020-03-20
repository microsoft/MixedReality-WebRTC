// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;
using UnityEngine;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// This component represents a local audio source added as an audio track to an
    /// existing WebRTC peer connection and sent to the remote peer.
    /// 
    /// <div class="WARNING alert alert-warning">
    /// <h5>WARNING</h5>
    /// <p>
    /// Currently the low-level WebRTC implementation does not support registering
    /// local audio callbacks, therefore the audio track cannot be rendered locally
    /// with a <see cref="MediaPlayer"/>.
    /// </p>
    /// </div>
    /// </summary>
    /// <seealso cref="MicrophoneSource"/>
    /// <seealso cref="MediaPlayer"/>
    public abstract class AudioSender : MediaSender, IAudioSource
    {
        /// <summary>
        /// Name of the preferred audio codec, or empty to let WebRTC decide.
        /// See https://en.wikipedia.org/wiki/RTP_audio_video_profile for the standard SDP names.
        /// </summary>
        [Tooltip("SDP name of the preferred audio codec to use if supported")]
        public string PreferredAudioCodec = string.Empty;

        /// <inheritdoc/>
        public bool IsStreaming { get; protected set; }

        /// <summary>
        /// Event raised when the audio stream started.
        /// 
        /// After this event is raised, the <see cref="IsStreaming"/> will become <c>true</c>.
        /// </summary>
        public AudioStreamStartedEvent AudioStreamStarted = new AudioStreamStartedEvent();

        /// <summary>
        /// Event raised when the audio stream stopped.
        /// 
        /// Before this event is raised, the <see cref="IsStreaming"/> becomes <c>false</c>.
        /// </summary>
        public AudioStreamStoppedEvent AudioStreamStopped = new AudioStreamStoppedEvent();

        /// <inheritdoc/>
        public AudioStreamStartedEvent GetAudioStreamStarted() { return AudioStreamStarted; }

        /// <inheritdoc/>
        public AudioStreamStoppedEvent GetAudioStreamStopped() { return AudioStreamStopped; }

        /// <summary>
        /// Audio transceiver this sender is paired with, if any.
        /// 
        /// This is <c>null</c> until a remote description is applied which pairs the sender
        /// with the local track of the transceiver.
        /// </summary>
        public Transceiver Transceiver { get; private set; }

        /// <summary>
        /// Local audio track that this component encapsulates, which if paired sends data to
        /// the remote peer.
        /// </summary>
        public LocalAudioTrack Track { get; protected set; } = null;

        public AudioSender() : base(MediaKind.Audio)
        {
        }

        /// <summary>
        /// Register a frame callback to listen to outgoing audio data produced by this audio sender
        /// and sent to the remote peer.
        /// 
        /// <div class="WARNING alert alert-warning">
        /// <h5>WARNING</h5>
        /// <p>
        /// Currently the low-level WebRTC implementation does not support registering local audio callbacks,
        /// therefore this is not implemented and will throw a <see cref="System.NotImplementedException"/>.
        /// </p>
        /// </div>
        /// </summary>
        /// <param name="callback">The new frame callback to register.</param>
        /// <remarks>
        /// Unlike for video, where a typical application might display some local feedback of a local
        /// webcam recording, local audio feedback is rare, so this callback is not typically used.
        /// One possible use case would be to display some visual feedback, like an audio spectrum analyzer.
        /// 
        /// Note that registering a callback does not influence the audio capture and sending to the
        /// remote peer, which occur whether or not a callback is registered.
        /// </remarks>
        public void RegisterCallback(AudioFrameDelegate callback)
        {
            throw new System.NotImplementedException("Local audio callbacks are not currently implemented.");
        }

        /// <summary>
        /// Unregister a frame callback previously registered with <see cref="RegisterCallback(AudioFrameDelegate)"/>.
        /// </summary>
        /// <param name="callback">The frame callback to unregister.</param>
        public void UnregisterCallback(AudioFrameDelegate callback)
        {
            throw new System.NotImplementedException("Local audio callbacks are not currently implemented.");
        }

        protected override async Task CreateLocalTrackAsync()
        {
            if (Track == null)
            {
                // Defer track creation to derived classes, which will invoke some methods like
                // LocalAudioTrack.CreateFromDeviceAsync().
                await CreateLocalAudioTrackAsync();
                Debug.Assert(Track != null, "Implementation did not create a valid Track property yet did not throw any exception.", this);

                AudioStreamStarted.Invoke(this);
                IsStreaming = true;
            }
        }

        protected override void DestroyLocalTrack()
        {
            if (Track != null)
            {
                IsStreaming = false;
                AudioStreamStopped.Invoke(this);

                // Defer track destruction to derived classes.
                DestroyLocalAudioTrack();
                Debug.Assert(Track == null, "Implementation did not destroy the existing Track property yet did not throw any exception.", this);
            }
        }

        /// <summary>
        /// Internal callback invoked when the audio sender is attached to a transceiver created
        /// just before the peer connection creates an SDP offer.
        /// </summary>
        /// <param name="audioTransceiver">The audio transceiver this sender is attached with.</param>
        internal void AttachToTransceiver(Transceiver audioTransceiver)
        {
            Debug.Assert((Transceiver == null) || (Transceiver == audioTransceiver));
            Transceiver = audioTransceiver;
        }

        /// <summary>
        /// Internal callback invoked when a peer connection is about to create an offer,
        /// and needs to create the audio transceivers and senders. The audio sender must
        /// create a local audio track and attach it to the given transceiver.
        /// </summary>
        /// <returns>Once the asynchronous operation is completed, the <see cref="Track"/> property
        /// must reference it.</returns>
        internal async Task AttachTrackAsync()
        {
            Debug.Assert(Transceiver != null);

            // Force again PreferredAudioCodec right before starting the local capture,
            // so that modifications to the property done after OnPeerInitialized() are
            // accounted for.
            //< FIXME - Multi-track override!!!
            Transceiver.PeerConnection.PreferredAudioCodec = PreferredAudioCodec;

            // Ensure the local sender track exists
            if (Track == null)
            {
                await CreateLocalTrackAsync();
            }

            // Attach the local track to the transceiver
            if (Track != null)
            {
                Transceiver.LocalAudioTrack = Track;
            }
        }

        internal void DetachTrack()
        {
            Debug.Assert(Transceiver != null);
            Debug.Assert(Transceiver.LocalTrack == Track);
            Transceiver.LocalAudioTrack = null;
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
        protected abstract Task CreateLocalAudioTrackAsync();

        /// <summary>
        /// Re-implement this callback to destroy the <see cref="Track"/> instance
        /// and other associated resources.
        /// </summary>
        protected virtual void DestroyLocalAudioTrack()
        {
            if (Track != null)
            {
                // Track may not be added to any transceiver (e.g. no connection)
                var transceiver = Track.Transceiver;
                if (transceiver != null)
                {
                    Debug.Assert(transceiver.LocalAudioTrack == Track);
                    transceiver.LocalAudioTrack = null;
                }

                // Local tracks are disposable objects owned by the user (this component)
                Track.Dispose();
                Track = null;
            }
        }
    }
}
