// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.MixedReality.WebRTC.Interop
{
    /// <summary>
    /// Handle to a native video track source object.
    /// </summary>
    internal class VideoTrackSourceHandle : RefCountedObjectHandle { }

    internal class VideoTrackSourceInterop
    {
        #region Unmanaged delegates

        // Note - none of those method arguments can be SafeHandle; use IntPtr instead.

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void I420AVideoFrameUnmanagedCallback(IntPtr userData, in I420AVideoFrame frame);

        #endregion

        #region P/Invoke static functions

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsVideoTrackSourceRegisterFrameCallback")]
        public static extern void VideoTrackSource_RegisterFrameCallback(VideoTrackSourceHandle trackHandle,
            I420AVideoFrameUnmanagedCallback callback, IntPtr userData);

        #endregion

        public class InteropCallbackArgs
        {
            public VideoTrackSource Source;
            public I420AVideoFrameUnmanagedCallback I420AFrameCallback;
        }

        [MonoPInvokeCallback(typeof(I420AVideoFrameUnmanagedCallback))]
        public static void I420AFrameCallback(IntPtr userData, in I420AVideoFrame frame)
        {
            var source = Utils.ToWrapper<VideoTrackSource>(userData);
            source.OnI420AFrameReady(frame);
        }
    }
}
