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
            EntryPoint = "mrsLocalVideoTrackRegisterI420FrameCallback")]
        public static extern void LocalVideoTrack_RegisterI420FrameCallback(IntPtr trackHandle,
            I420VideoFrameCallback callback, IntPtr userData);

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
        public delegate void I420VideoFrameCallback(IntPtr userData,
            IntPtr ydata, IntPtr udata, IntPtr vdata, IntPtr adata,
            int ystride, int ustride, int vstride, int astride,
            int frameWidth, int frameHeight);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void ARGBVideoFrameCallback(IntPtr userData,
            IntPtr data, int stride, int frameWidth, int frameHeight);

        #endregion

        public class InteropCallbackArgs
        {
            public LocalVideoTrack Track;
            public I420VideoFrameCallback I420FrameCallback;
            public ARGBVideoFrameCallback ARGBFrameCallback;
        }

        [MonoPInvokeCallback(typeof(I420VideoFrameDelegate))]
        public static void I420FrameCallback(IntPtr userData,
            IntPtr dataY, IntPtr dataU, IntPtr dataV, IntPtr dataA,
            int strideY, int strideU, int strideV, int strideA,
            int width, int height)
        {
            var track = Utils.ToWrapper<LocalVideoTrack>(userData);
            var frame = new I420AVideoFrame()
            {
                width = (uint)width,
                height = (uint)height,
                dataY = dataY,
                dataU = dataU,
                dataV = dataV,
                dataA = dataA,
                strideY = strideY,
                strideU = strideU,
                strideV = strideV,
                strideA = strideA
            };
            track.OnI420FrameReady(frame);
        }

        [MonoPInvokeCallback(typeof(ARGBVideoFrameDelegate))]
        public static void ARGBFrameCallback(IntPtr userData,
            IntPtr data, int stride, int width, int height)
        {
            var track = Utils.ToWrapper<LocalVideoTrack>(userData);
            var frame = new ARGBVideoFrame()
            {
                width = (uint)width,
                height = (uint)height,
                data = data,
                stride = stride
            };
            track.OnARGBFrameReady(frame);
        }
    }
}
