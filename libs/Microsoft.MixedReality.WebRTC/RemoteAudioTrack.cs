// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.MixedReality.WebRTC.Interop;
using Microsoft.MixedReality.WebRTC.Tracing;

namespace Microsoft.MixedReality.WebRTC
{
    /// <summary>
    /// Audio track receiving audio frames from the remote peer.
    /// </summary>
    public class RemoteAudioTrack
    {
        /// <summary>
        /// Peer connection this audio track is added to, if any.
        /// This is <c>null</c> after the track has been removed from the peer connection.
        /// </summary>
        public PeerConnection PeerConnection { get; private set; }

        /// <summary>
        /// Audio transceiver this track is part of.
        /// </summary>
        public AudioTransceiver Transceiver { get; private set; }

        /// <summary>
        /// Track name as specified during creation. This property is immutable.
        /// For remote audio track the property is specified by the remote peer.
        /// </summary>
        public string Name { get; }

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

        /// <summary>
        /// Event that occurs when a audio frame has been received from the remote peer.
        /// </summary>
        public event AudioFrameDelegate AudioFrameReady;

        /// <summary>
        /// Handle to the native RemoteAudioTrack object.
        /// </summary>
        /// <remarks>
        /// In native land this is a <code>Microsoft::MixedReality::WebRTC::RemoteAudioTrackHandle</code>.
        /// </remarks>
        internal RemoteAudioTrackHandle _nativeHandle = new RemoteAudioTrackHandle();

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
        internal RemoteAudioTrack(PeerConnection peer, string trackName)
        {
            PeerConnection = peer;
            Name = trackName;
        }

        internal void SetHandle(RemoteAudioTrackHandle handle)
        {
            Debug.Assert(!handle.IsClosed);
            // Either first-time assign or no-op (assign same value again)
            Debug.Assert(_nativeHandle.IsInvalid || (_nativeHandle == handle));
            if (_nativeHandle != handle)
            {
                _nativeHandle = handle;
                RegisterInteropCallbacks();
            }
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
        internal void Dispose()
        {
            if (_nativeHandle.IsClosed)
            {
                return;
            }

            // Remove the track from the peer connection, if any
            //Transceiver?.SetRemoteTrack(null);
            Debug.Assert(PeerConnection == null); // see OnTrackRemoved

            UnregisterInteropCallbacks();

            // Destroy the native object. This may be delayed if a P/Invoke callback is underway,
            // but will be handled at some point anyway, even if the managed instance is gone.
            _nativeHandle.Dispose();
        }

        internal void OnFrameReady(AudioFrame frame)
        {
            MainEventSource.Log.RemoteAudioFrameReady(frame.bitsPerSample, frame.sampleRate, frame.channelCount, frame.sampleCount);
            AudioFrameReady?.Invoke(frame);
        }

        internal void OnTrackAddedToTransceiver(AudioTransceiver transceiver)
        {
            Debug.Assert(PeerConnection == transceiver.PeerConnection);
            Debug.Assert(!_nativeHandle.IsClosed);
            Debug.Assert(Transceiver == null);
            Debug.Assert(transceiver != null);
            Transceiver = transceiver;
            transceiver.OnRemoteTrackAdded(this);
        }

        internal void OnTrackRemoved(PeerConnection previousConnection)
        {
            Debug.Assert(PeerConnection == previousConnection);
            Debug.Assert(!_nativeHandle.IsClosed);
            PeerConnection = null;
            Transceiver.OnRemoteTrackRemoved(this);
            Transceiver = null;
        }

        internal void OnMute(bool muted)
        {

        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"(RemoteAudioTrack)\"{Name}\"";
        }
    }
}
