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

    internal class LocalVideoTrackInterop
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        internal ref struct TrackInitConfig
        {
            [MarshalAs(UnmanagedType.LPStr)]
            public string TrackName;
        }

        #region Unmanaged delegates

        // Note - none of those method arguments can be SafeHandle; use IntPtr instead.

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void I420AVideoFrameUnmanagedCallback(IntPtr userData, in I420AVideoFrame frame);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void Argb32VideoFrameUnmanagedCallback(IntPtr userData, in Argb32VideoFrame frame);

        #endregion


        #region Native functions

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsLocalVideoTrackCreateFromSource")]
        public static unsafe extern uint LocalVideoTrack_CreateFromSource(in TrackInitConfig initSettings,
            VideoTrackSourceHandle sourceHandle, out LocalVideoTrackHandle trackHandle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsLocalVideoTrackRegisterI420AFrameCallback")]
        public static extern void LocalVideoTrack_RegisterI420AFrameCallback(LocalVideoTrackHandle trackHandle,
            I420AVideoFrameUnmanagedCallback callback, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsLocalVideoTrackRegisterArgb32FrameCallback")]
        public static extern void LocalVideoTrack_RegisterArgb32FrameCallback(LocalVideoTrackHandle trackHandle,
            Argb32VideoFrameUnmanagedCallback callback, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsLocalVideoTrackIsEnabled")]
        public static extern int LocalVideoTrack_IsEnabled(LocalVideoTrackHandle trackHandle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsLocalVideoTrackSetEnabled")]
        public static extern uint LocalVideoTrack_SetEnabled(LocalVideoTrackHandle trackHandle, int enabled);

        #endregion

        public class InteropCallbackArgs
        {
            public LocalVideoTrack Track;
            public I420AVideoFrameUnmanagedCallback I420AFrameCallback;
            public Argb32VideoFrameUnmanagedCallback Argb32FrameCallback;
        }

        [MonoPInvokeCallback(typeof(I420AVideoFrameUnmanagedCallback))]
        public static void I420AFrameCallback(IntPtr userData, in I420AVideoFrame frame)
        {
            var track = Utils.ToWrapper<LocalVideoTrack>(userData);
            track.OnI420AFrameReady(frame);
        }

        [MonoPInvokeCallback(typeof(Argb32VideoFrameUnmanagedCallback))]
        public static void Argb32FrameCallback(IntPtr userData, in Argb32VideoFrame frame)
        {
            var track = Utils.ToWrapper<LocalVideoTrack>(userData);
            track.OnArgb32FrameReady(frame);
        }
    }
}
