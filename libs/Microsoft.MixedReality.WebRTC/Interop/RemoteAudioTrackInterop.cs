// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using static Microsoft.MixedReality.WebRTC.Interop.AudioTrackInterop;

namespace Microsoft.MixedReality.WebRTC.Interop
{
    internal class RemoteAudioTrackInterop
    {
        internal class ReadBufferHandle : SafeHandle
        {
            /// <summary>
            /// Used internally by <see cref="RemoteAudioTrack_CreateReadBuffer(IntPtr, out ReadBufferHandle)"/>.
            /// </summary>
            internal ReadBufferHandle() : base(IntPtr.Zero, true) { }

            public override bool IsInvalid => handle == IntPtr.Zero;
            protected override bool ReleaseHandle()
            {
                AudioTrackReadBuffer_Destroy(handle);
                return true;
            }
        }

        #region Native functions

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsRemoteAudioTrackSetUserData")]
        public static unsafe extern void RemoteAudioTrack_SetUserData(IntPtr handle, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsRemoteAudioTrackGetUserData")]
        public static unsafe extern IntPtr RemoteAudioTrack_GetUserData(IntPtr handle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsRemoteAudioTrackRegisterFrameCallback")]
        public static extern void RemoteAudioTrack_RegisterFrameCallback(IntPtr trackHandle,
            AudioFrameUnmanagedCallback callback, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsRemoteAudioTrackIsEnabled")]
        public static extern int RemoteAudioTrack_IsEnabled(IntPtr trackHandle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsRemoteAudioTrackOutputToDevice")]
        public static extern void RemoteAudioTrack_OutputToDevice(IntPtr trackHandle, mrsBool output);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsRemoteAudioTrackIsOutputToDevice")]
        public static extern mrsBool RemoteAudioTrack_IsOutputToDevice(IntPtr trackHandle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsRemoteAudioTrackCreateReadBuffer")]
        public static extern uint RemoteAudioTrack_CreateReadBuffer(IntPtr trackHandle, out ReadBufferHandle audioTrackReadBufferOut);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsAudioTrackReadBufferRead")]
        public static extern uint AudioTrackReadBuffer_Read(ReadBufferHandle audioTrackReadBuffer,
                            int sampleRate, int numChannels, AudioTrackReadBuffer.PadBehavior padBehavior,
                            float[] samplesOut, int numSamplesMax, out int numSamplesRead, out mrsBool hasOverrun);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsAudioTrackReadBufferDestroy")]
        public static extern void AudioTrackReadBuffer_Destroy(IntPtr audioTrackReadBuffer);

        #endregion


        #region Marshaling data structures

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct CreateConfig
        {
            public string TrackName;
        }

        #endregion


        #region Native callbacks

        public class InteropCallbackArgs
        {
            public RemoteAudioTrack Track;
            public AudioFrameUnmanagedCallback FrameCallback;
        }

        [MonoPInvokeCallback(typeof(AudioFrameUnmanagedCallback))]
        public static void FrameCallback(IntPtr userData, in AudioFrame frame)
        {
            var track = Utils.ToWrapper<RemoteAudioTrack>(userData);
            track.OnFrameReady(frame);
        }

        #endregion


        #region Utilities

        public static RemoteAudioTrack CreateWrapper(PeerConnection parent, in PeerConnectionInterop.RemoteAudioTrackAddedInfo config)
        {
            // Create a new wrapper
            var wrapper = new RemoteAudioTrack(config.trackHandle, parent, config.trackName);

            // Assign a reference to it inside the UserData of the native object so it can be retrieved whenever needed
            IntPtr wrapperRef = Utils.MakeWrapperRef(wrapper);
            RemoteAudioTrack_SetUserData(config.trackHandle, wrapperRef);

            return wrapper;
        }

        #endregion
    }
}
