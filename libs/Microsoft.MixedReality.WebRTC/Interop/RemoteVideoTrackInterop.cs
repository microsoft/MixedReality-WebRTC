// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Microsoft.MixedReality.WebRTC.Interop
{
    /// <summary>
    /// Handle to a native remote video track object.
    /// </summary>
    public sealed class RemoteVideoTrackHandle : SafeHandle
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
        public RemoteVideoTrackHandle() : base(IntPtr.Zero, ownsHandle: true)
        {
        }

        /// <summary>
        /// Constructor for a valid handle referencing the given native object.
        /// </summary>
        /// <param name="handle">The valid internal handle to the native object.</param>
        public RemoteVideoTrackHandle(IntPtr handle) : base(IntPtr.Zero, ownsHandle: true)
        {
            SetHandle(handle);
        }

        /// <summary>
        /// Release the native object while the handle is being closed.
        /// </summary>
        /// <returns>Return <c>true</c> if the native object was successfully released.</returns>
        protected override bool ReleaseHandle()
        {
            RemoteVideoTrackInterop.RemoteVideoTrack_RemoveRef(handle);
            return true;
        }
    }

    internal class RemoteVideoTrackInterop
    {
        #region Native functions

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsRemoteVideoTrackAddRef")]
        public static unsafe extern void RemoteVideoTrack_AddRef(RemoteVideoTrackHandle handle);

        // Note - This is used during SafeHandle.ReleaseHandle(), so cannot use RemoteVideoTrackHandle
        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsRemoteVideoTrackRemoveRef")]
        public static unsafe extern void RemoteVideoTrack_RemoveRef(IntPtr handle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsRemoteVideoTrackRegisterI420AFrameCallback")]
        public static extern void RemoteVideoTrack_RegisterI420AFrameCallback(RemoteVideoTrackHandle trackHandle,
            LocalVideoTrackInterop.I420AVideoFrameUnmanagedCallback callback, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsRemoteVideoTrackRegisterArgb32FrameCallback")]
        public static extern void RemoteVideoTrack_RegisterArgb32FrameCallback(RemoteVideoTrackHandle trackHandle,
            LocalVideoTrackInterop.Argb32VideoFrameUnmanagedCallback callback, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsRemoteVideoTrackIsEnabled")]
        public static extern int RemoteVideoTrack_IsEnabled(RemoteVideoTrackHandle trackHandle);

        #endregion


        #region Marshaling data structures

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct CreateConfig
        {
            public string TrackName;
        }

        #endregion


        #region Native callbacks

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate IntPtr CreateObjectDelegate(IntPtr peer, in CreateConfig config);

        [MonoPInvokeCallback(typeof(CreateObjectDelegate))]
        public static IntPtr RemoteVideoTrackCreateObjectCallback(IntPtr peer, in CreateConfig config)
        {
            var peerWrapper = Utils.ToWrapper<PeerConnection>(peer);
            var remoteVideoTrackWrapper = CreateWrapper(peerWrapper, in config);
            return Utils.MakeWrapperRef(remoteVideoTrackWrapper);
        }

        public class InteropCallbackArgs
        {
            public RemoteVideoTrack Track;
            public LocalVideoTrackInterop.I420AVideoFrameUnmanagedCallback I420AFrameCallback;
            public LocalVideoTrackInterop.Argb32VideoFrameUnmanagedCallback Argb32FrameCallback;
        }

        [MonoPInvokeCallback(typeof(LocalVideoTrackInterop.I420AVideoFrameUnmanagedCallback))]
        public static void I420AFrameCallback(IntPtr userData, ref I420AVideoFrame frame)
        {
            var track = Utils.ToWrapper<RemoteVideoTrack>(userData);
            track.OnI420AFrameReady(frame);
        }

        [MonoPInvokeCallback(typeof(LocalVideoTrackInterop.Argb32VideoFrameUnmanagedCallback))]
        public static void Argb32FrameCallback(IntPtr userData, ref Argb32VideoFrame frame)
        {
            var track = Utils.ToWrapper<RemoteVideoTrack>(userData);
            track.OnArgb32FrameReady(frame);
        }

        #endregion


        #region Utilities

        public static RemoteVideoTrack CreateWrapper(PeerConnection parent, in CreateConfig config)
        {
            return new RemoteVideoTrack(parent, config.TrackName);
        }

        #endregion
    }
}
