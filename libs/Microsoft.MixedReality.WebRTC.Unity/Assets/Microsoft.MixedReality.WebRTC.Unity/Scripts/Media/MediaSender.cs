// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;
using UnityEngine;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// Base class for media sources generating their media frames locally,
    /// with the intention to send them to the remote peer.
    /// </summary>
    public abstract class MediaSender : MediaSource
    {
        /// <summary>
        /// Name of the local media track this component will create when calling <see cref="StartCaptureAsync"/>.
        /// If left empty, the implementation will generate a unique name for the track (generally a GUID).
        /// </summary>
        /// <remarks>
        /// This value must comply with the 'msid' attribute rules as defined in
        /// https://tools.ietf.org/html/draft-ietf-mmusic-msid-05#section-2, which in
        /// particular constraints the set of allowed characters to those allowed for a
        /// 'token' element as specified in https://tools.ietf.org/html/rfc4566#page-43:
        /// - Symbols [!#$%'*+-.^_`{|}~] and ampersand &amp;
        /// - Alphanumerical characters [A-Za-z0-9]
        /// 
        /// Users can manually test if a string is a valid SDP token with the utility
        /// method <see cref="SdpTokenAttribute.Validate(string, bool)"/>.
        /// </remarks>
        /// <seealso cref="SdpTokenAttribute.Validate(string, bool)"/>
        [Tooltip("SDP track name")]
        [SdpToken(allowEmpty: true)]
        public string TrackName;

        /// <summary>
        /// Automatically start media capture when the component is enabled.
        /// 
        /// If <c>true</c>, then <see cref="StartCaptureAsync"/> is automatically called
        /// when the <see xref="UnityEngine.MonoBehaviour.OnEnabled"/> callback is invoked
        /// by Unity.
        /// </summary>
        [Tooltip("Automatically start media capture when the component is enabled")]
        public bool AutoStartOnEnabled = true;

        /// <summary>
        /// Automatically stop media capture when the component is disabled.
        /// 
        /// If <c>true</c>, then <see cref="StopCapture"/> is automatically called when the
        /// <see xref="UnityEngine.MonoBehaviour.OnDisabled"/> callback is invoked by Unity.
        /// </summary>
        [Tooltip("Automatically stop media capture when the component is disabled")]
        public bool AutoStopOnDisabled = true;

        /// <summary>
        /// Is the media source currently generating frames from local capture?
        /// The concept of _capture_ is described in the <see cref="StartCaptureAsync"/> function.
        /// </summary>
        /// <seealso cref="StartCaptureAsync"/>
        /// <seealso cref="StopCapture()"/>
        public bool IsCapturing { get; private set; }

        /// <inheritdoc/>
        public MediaSender(MediaKind mediaKind) : base(mediaKind)
        {
        }

        /// <summary>
        /// Manually start capture of the local media by creating a local media track.
        /// 
        /// If <see cref="AutoStartOnEnabled"/> is <c>true</c> then this is called automatically
        /// as soon as the component is enabled. Otherwise the user must call this method to create
        /// the underlying local media track.
        /// </summary>
        /// <seealso cref="StopCapture()"/>
        /// <seealso cref="IsCapturing"/>
        /// <seealso cref="AutoStartOnEnabled"/>
        public async Task StartCaptureAsync()
        {
            if (!IsCapturing)
            {
                await CreateLocalTrackAsync();
                IsCapturing = true;
            }
        }

        /// <summary>
        /// Stop capture of the local video track and destroy it.
        /// </summary>
        /// <seealso cref="StartCaptureAsync()"/>
        /// <seealso cref="IsCapturing"/>
        public void StopCapture()
        {
            if (IsCapturing)
            {
                DestroyLocalTrack();
                IsCapturing = false;
            }
        }

        /// <summary>
        /// Mute or unmute the media. For audio, muting turns the source to silence. For video, this
        /// produces black frames. This is transparent to the SDP session and does not requite any
        /// renegotiation.
        /// </summary>
        /// <param name="mute"><c>true</c> to mute the local media track, or <c>false</c> to unmute
        /// it and resume producing media frames.</param>
        public void Mute(bool mute = true)
        {
            MuteImpl(mute);
        }

        /// <summary>
        /// Unmute the media and resume normal source playback.
        /// This is equivalent to <c>Mute(false)</c>, and provided for code clarity.
        /// </summary>
        public void Unmute()
        {
            MuteImpl(false);
        }

        /// <inheritdoc/>
        protected async Task OnEnable()
        {
            if (AutoStartOnEnabled)
            {
                await StartCaptureAsync();
            }
        }

        /// <inheritdoc/>
        protected void OnDisable()
        {
            if (AutoStopOnDisabled)
            {
                StopCapture();
            }
        }

        protected abstract Task CreateLocalTrackAsync();
        protected abstract void DestroyLocalTrack();

        /// <summary>
        /// Derived classes implement the mute/unmute action on the track.
        /// </summary>
        /// <param name="mute"><c>true</c> to mute the track, or <c>false</c> to unmute it.</param>
        protected abstract void MuteImpl(bool mute);
    }
}
