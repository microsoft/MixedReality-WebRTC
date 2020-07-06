// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using Microsoft.MixedReality.WebRTC.Interop;
using Microsoft.MixedReality.WebRTC.Tracing;

namespace Microsoft.MixedReality.WebRTC
{
    /// <summary>
    /// Audio track receiving audio frames from the remote peer.
    /// </summary>
    /// <remarks>
    /// Instances of this class are created by <see cref="PeerConnection"/> when a negotiation
    /// adds tracks sent by the remote peer.
    ///
    /// New tracks are automatically played on the system audio device after
    /// <see cref="PeerConnection.AudioTrackAdded"/> is fired on track creation. To avoid the track
    /// being played, call <see cref="OutputToDevice(bool)"/> in a <see cref="PeerConnection.AudioTrackAdded"/>
    /// handler (or later).
    /// </remarks>
    public class RemoteAudioTrack : MediaTrack, IAudioSource
    {
        /// <summary>
        /// Enabled status of the track. If enabled, receives audio frames from the remote peer as
        /// expected. If disabled, does not receive anything (silence).
        /// </summary>
        /// <remarks>
        /// Reading the value of this property after the track has been disposed is valid, and returns
        /// <c>false</c>.
        /// The remote audio track enabled status is controlled by the remote peer only.
        /// </remarks>
        public bool Enabled
        {
            get
            {
                return (RemoteAudioTrackInterop.RemoteAudioTrack_IsEnabled(_nativeHandle) != 0);
            }
        }

        /// <inheritdoc/>
        public event AudioFrameDelegate AudioFrameReady;

        /// <summary>
        /// Output the audio track to the WebRTC audio device.
        /// </summary>
        /// <remarks>
        /// The default behavior is for every remote audio frame to be passed to
        /// remote audio frame callbacks, as well as output automatically to the
        /// audio device used by WebRTC. If |false| is passed to this function, remote
        /// audio frames will still be received and passed to callbacks, but won't be
        /// output to the audio device.
        ///
        /// NOTE: Changing the default behavior is not supported on UWP.
        /// </remarks>
        public void OutputToDevice(bool output)
        {
            RemoteAudioTrackInterop.RemoteAudioTrack_OutputToDevice(_nativeHandle, (mrsBool)output);
        }

        /// <summary>
        /// Returns whether the track is output directly to the system audio device.
        /// </summary>
        public bool IsOutputToDevice()
        {
            return (bool)RemoteAudioTrackInterop.RemoteAudioTrack_IsOutputToDevice(_nativeHandle);
        }

        /// <inheritdoc/>
        public AudioTrackReadBuffer CreateReadBuffer()
        {
            uint res = RemoteAudioTrackInterop.RemoteAudioTrack_CreateReadBuffer(_nativeHandle,
                out RemoteAudioTrackInterop.ReadBufferHandle readBufferHandle);
            Utils.ThrowOnErrorCode(res);
            return new AudioTrackReadBuffer(readBufferHandle);
        }

        /// <summary>
        /// Handle to the native RemoteAudioTrack object.
        /// </summary>
        /// <remarks>
        /// In native land this is a <code>mrsRemoteAudioTrackHandle</code>.
        /// </remarks>
        internal RemoteAudioTrackInterop.RemoteAudioTrackHandle _nativeHandle = null;

        /// <summary>
        /// Handle to self for interop callbacks. This adds a reference to the current object, preventing
        /// it from being garbage-collected.
        /// </summary>
        private IntPtr _selfHandle = IntPtr.Zero;

        /// <summary>
        /// Callback arguments to ensure delegates registered with the native layer don't go out of scope.
        /// </summary>
        private RemoteAudioTrackInterop.InteropCallbackArgs _interopCallbackArgs;

        // Constructor for interop-based creation; SetHandle() will be called later
        internal RemoteAudioTrack(RemoteAudioTrackInterop.RemoteAudioTrackHandle handle, PeerConnection peer, string trackName)
            : base(peer, trackName)
        {
            Debug.Assert(!handle.IsClosed);
            _nativeHandle = handle;
            RegisterInteropCallbacks();
        }

        private void RegisterInteropCallbacks()
        {
            _interopCallbackArgs = new RemoteAudioTrackInterop.InteropCallbackArgs()
            {
                Track = this,
                FrameCallback = RemoteAudioTrackInterop.FrameCallback,
            };
            _selfHandle = Utils.MakeWrapperRef(this);
            RemoteAudioTrackInterop.RemoteAudioTrack_RegisterFrameCallback(
                _nativeHandle, _interopCallbackArgs.FrameCallback, _selfHandle);
        }

        private void UnregisterInteropCallbacks()
        {
            if (_selfHandle != IntPtr.Zero)
            {
                RemoteAudioTrackInterop.RemoteAudioTrack_RegisterFrameCallback(_nativeHandle, null, IntPtr.Zero);
                Utils.ReleaseWrapperRef(_selfHandle);
                _selfHandle = IntPtr.Zero;
                _interopCallbackArgs = null;
            }
        }

        /// <summary>
        /// Dispose of the native track. Invoked by its owner (<see cref="PeerConnection"/>).
        /// </summary>
        internal void DestroyNative()
        {
            if (_nativeHandle.IsClosed)
            {
                return;
            }

            Debug.Assert(PeerConnection == null); // see OnTrackRemoved

            UnregisterInteropCallbacks();

            _nativeHandle.Dispose();
        }

        internal void OnFrameReady(AudioFrame frame)
        {
            MainEventSource.Log.RemoteAudioFrameReady(frame.bitsPerSample, frame.sampleRate, frame.channelCount, frame.sampleCount);
            AudioFrameReady?.Invoke(frame);
        }

        internal override void OnMute(bool muted)
        {

        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"(RemoteAudioTrack)\"{Name}\"";
        }
    }
}
