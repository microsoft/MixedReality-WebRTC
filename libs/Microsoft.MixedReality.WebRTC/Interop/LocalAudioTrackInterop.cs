// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;
using static Microsoft.MixedReality.WebRTC.Interop.AudioTrackInterop;

namespace Microsoft.MixedReality.WebRTC.Interop
{
    /// <summary>
    /// Handle to a native local audio track object.
    /// </summary>
    internal sealed class LocalAudioTrackHandle : RefCountedObjectHandle { }

    internal class LocalAudioTrackInterop
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        internal ref struct TrackInitConfig
        {
            [MarshalAs(UnmanagedType.LPStr)]
            public string TrackName;
        }


        #region Native functions

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsLocalAudioTrackCreateFromSource")]
        public static unsafe extern uint LocalAudioTrack_CreateFromSource(in TrackInitConfig initSettings,
            AudioTrackSourceHandle sourceHandle, out LocalAudioTrackHandle trackHandle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsLocalAudioTrackRegisterFrameCallback")]
        public static extern void LocalAudioTrack_RegisterFrameCallback(LocalAudioTrackHandle trackHandle,
            AudioFrameUnmanagedCallback callback, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsLocalAudioTrackIsEnabled")]
        public static extern int LocalAudioTrack_IsEnabled(LocalAudioTrackHandle trackHandle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsLocalAudioTrackSetEnabled")]
        public static extern uint LocalAudioTrack_SetEnabled(LocalAudioTrackHandle trackHandle, int enabled);

        #endregion


        public class InteropCallbackArgs
        {
            public LocalAudioTrack Track;
            public AudioFrameUnmanagedCallback FrameCallback;
        }

        [MonoPInvokeCallback(typeof(AudioFrameUnmanagedCallback))]
        public static void FrameCallback(IntPtr userData, in AudioFrame frame)
        {
            var track = Utils.ToWrapper<LocalAudioTrack>(userData);
            track.OnFrameReady(frame);
        }
    }
}
