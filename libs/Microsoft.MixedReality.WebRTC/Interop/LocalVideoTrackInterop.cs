// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.MixedReality.WebRTC.Interop
{
    /// <summary>
    /// Handle to a native local video track object.
    /// </summary>
    internal sealed class LocalVideoTrackHandle : SafeHandle
    {
        /// <summary>
        /// Check if the current handle is invalid, which means it is not referencing
        /// an actual native object. Note that a valid handle only means that the internal
        /// handle references a native object, but does not guarantee that the native
        /// object is still accessible. It is only safe to access the native object if
        /// the handle is not closed, which implies it being valid.
        /// </summary>
        public override bool IsInvalid
        {
            get
            {
                return (handle == IntPtr.Zero);
            }
        }

        /// <summary>
        /// Default constructor for an invalid handle.
        /// </summary>
        public LocalVideoTrackHandle() : base(IntPtr.Zero, ownsHandle: true)
        {
        }

        /// <summary>
        /// Constructor for a valid handle referencing the given native object.
        /// </summary>
        /// <param name="handle">The valid internal handle to the native object.</param>
        public LocalVideoTrackHandle(IntPtr handle) : base(IntPtr.Zero, ownsHandle: true)
        {
            SetHandle(handle);
        }

        /// <summary>
        /// Release the native object while the handle is being closed.
        /// </summary>
        /// <returns>Return <c>true</c> if the native object was successfully released.</returns>
        protected override bool ReleaseHandle()
        {
            LocalVideoTrackInterop.LocalVideoTrack_RemoveRef(handle);
            return true;
        }
    }

    internal class LocalVideoTrackInterop
    {
        #region Unmanaged delegates

        // Note - none of those method arguments can be SafeHandle; use IntPtr instead.

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void I420AVideoFrameUnmanagedCallback(IntPtr userData, in I420AVideoFrame frame);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void Argb32VideoFrameUnmanagedCallback(IntPtr userData, in Argb32VideoFrame frame);

        #endregion


        #region Native functions

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsLocalVideoTrackAddRef")]
        public static unsafe extern void LocalVideoTrack_AddRef(LocalVideoTrackHandle handle);

        // Note - This is used during SafeHandle.ReleaseHandle(), so cannot use LocalVideoTrackHandle
        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsLocalVideoTrackRemoveRef")]
        public static unsafe extern void LocalVideoTrack_RemoveRef(IntPtr handle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsLocalVideoTrackCreateFromDevice")]
        public static unsafe extern uint LocalVideoTrack_CreateFromDevice(in PeerConnectionInterop.LocalVideoTrackInteropInitConfig config,
            string trackName, out LocalVideoTrackHandle trackHandle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsLocalVideoTrackCreateFromExternalSource")]
        public static unsafe extern uint LocalVideoTrack_CreateFromExternalSource(
            in PeerConnectionInterop.LocalVideoTrackFromExternalSourceInteropInitConfig config,
            out LocalVideoTrackHandle trackHandle);

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
