// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.MixedReality.WebRTC.Interop
{
    internal class LocalVideoTrackInterop
    {
        #region Native functions

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsLocalVideoTrackAddRef")]
        public static unsafe extern void LocalVideoTrack_AddRef(IntPtr handle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsLocalVideoTrackRemoveRef")]
        public static unsafe extern void LocalVideoTrack_RemoveRef(IntPtr handle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsLocalVideoTrackRegisterI420AFrameCallback")]
        public static extern void LocalVideoTrack_RegisterI420AFrameCallback(IntPtr trackHandle,
            I420AVideoFrameCallback callback, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsLocalVideoTrackRegisterARGBFrameCallback")]
        public static extern void LocalVideoTrack_RegisterARGBFrameCallback(IntPtr trackHandle,
            ARGBVideoFrameCallback callback, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsLocalVideoTrackIsEnabled")]
        public static extern int LocalVideoTrack_IsEnabled(IntPtr trackHandle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsLocalVideoTrackSetEnabled")]
        public static extern uint LocalVideoTrack_SetEnabled(IntPtr trackHandle, int enabled);

        #endregion

        #region Native callbacks

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void I420AVideoFrameCallback(IntPtr userData, I420AVideoFrame frame);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void ARGBVideoFrameCallback(IntPtr userData, ARGBVideoFrame frame);

        #endregion

        public class InteropCallbackArgs
        {
            public LocalVideoTrack Track;
            public I420AVideoFrameCallback I420AFrameCallback;
            public ARGBVideoFrameCallback ARGBFrameCallback;
        }

        [MonoPInvokeCallback(typeof(I420AVideoFrameDelegate))]
        public static void I420AFrameCallback(IntPtr userData, I420AVideoFrame frame)
        {
            var track = Utils.ToWrapper<LocalVideoTrack>(userData);
            track.OnI420AFrameReady(frame);
        }

        [MonoPInvokeCallback(typeof(ARGBVideoFrameDelegate))]
        public static void ARGBFrameCallback(IntPtr userData, ARGBVideoFrame frame)
        {
            var track = Utils.ToWrapper<LocalVideoTrack>(userData);
            track.OnARGBFrameReady(frame);
        }
    }
}
