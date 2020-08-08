// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.MixedReality.WebRTC.Interop
{
    /// <summary>
    /// Handle to a native local video track object.
    /// </summary>
    internal sealed class LocalVideoTrackHandle : RefCountedObjectHandle { }

    internal static class LocalVideoTrackInterop
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        internal ref struct TrackInitConfig
        {
            [MarshalAs(UnmanagedType.LPStr)]
            public string TrackName;
        }


        #region Native functions

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsLocalVideoTrackCreateFromSource")]
        public static unsafe extern uint LocalVideoTrack_CreateFromSource(in TrackInitConfig initSettings,
            VideoTrackSourceHandle sourceHandle, out LocalVideoTrackHandle trackHandle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsLocalVideoTrackRegisterI420AFrameCallback")]
        public static extern void LocalVideoTrack_RegisterI420AFrameCallback(LocalVideoTrackHandle trackHandle,
            VideoTrackSourceInterop.I420AVideoFrameUnmanagedCallback callback, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsLocalVideoTrackRegisterArgb32FrameCallback")]
        public static extern void LocalVideoTrack_RegisterArgb32FrameCallback(LocalVideoTrackHandle trackHandle,
            VideoTrackSourceInterop.Argb32VideoFrameUnmanagedCallback callback, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsLocalVideoTrackIsEnabled")]
        public static extern int LocalVideoTrack_IsEnabled(LocalVideoTrackHandle trackHandle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsLocalVideoTrackSetEnabled")]
        public static extern uint LocalVideoTrack_SetEnabled(LocalVideoTrackHandle trackHandle, int enabled);

        #endregion
    }
}
