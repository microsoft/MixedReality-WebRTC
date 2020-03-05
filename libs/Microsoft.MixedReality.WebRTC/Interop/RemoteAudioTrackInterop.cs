// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using static Microsoft.MixedReality.WebRTC.Interop.AudioTrackInterop;

namespace Microsoft.MixedReality.WebRTC.Interop
{
    /// <summary>
    /// Handle to a native remote audio track object.
    /// </summary>
    public sealed class RemoteAudioTrackHandle : SafeHandle
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
        public RemoteAudioTrackHandle() : base(IntPtr.Zero, ownsHandle: true)
        {
        }

        /// <summary>
        /// Constructor for a valid handle referencing the given native object.
        /// </summary>
        /// <param name="handle">The valid internal handle to the native object.</param>
        public RemoteAudioTrackHandle(IntPtr handle) : base(IntPtr.Zero, ownsHandle: true)
        {
            SetHandle(handle);
        }

        /// <summary>
        /// Release the native object while the handle is being closed.
        /// </summary>
        /// <returns>Return <c>true</c> if the native object was successfully released.</returns>
        protected override bool ReleaseHandle()
        {
            RemoteAudioTrackInterop.RemoteAudioTrack_RemoveRef(handle);
            return true;
        }
    }

    internal class RemoteAudioTrackInterop
    {
        #region Native functions

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsRemoteAudioTrackAddRef")]
        public static unsafe extern void RemoteAudioTrack_AddRef(RemoteAudioTrackHandle handle);

        // Note - This is used during SafeHandle.ReleaseHandle(), so cannot use RemoteAudioTrackHandle
        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsRemoteAudioTrackRemoveRef")]
        public static unsafe extern void RemoteAudioTrack_RemoveRef(IntPtr handle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsRemoteAudioTrackRegisterFrameCallback")]
        public static extern void RemoteAudioTrack_RegisterFrameCallback(RemoteAudioTrackHandle trackHandle,
            AudioFrameUnmanagedCallback callback, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsRemoteAudioTrackIsEnabled")]
        public static extern int RemoteAudioTrack_IsEnabled(RemoteAudioTrackHandle trackHandle);

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
        public static IntPtr RemoteAudioTrackCreateObjectCallback(IntPtr peer, in CreateConfig config)
        {
            var peerWrapper = Utils.ToWrapper<PeerConnection>(peer);
            var remoteAudioTrackWrapper = CreateWrapper(peerWrapper, in config);
            return Utils.MakeWrapperRef(remoteAudioTrackWrapper);
        }

        public class InteropCallbackArgs
        {
            public RemoteAudioTrack Track;
            public AudioFrameUnmanagedCallback FrameCallback;
        }

        [MonoPInvokeCallback(typeof(AudioFrameUnmanagedCallback))]
        public static void FrameCallback(IntPtr userData, ref AudioFrame frame)
        {
            var track = Utils.ToWrapper<RemoteAudioTrack>(userData);
            track.OnFrameReady(frame);
        }

        #endregion


        #region Utilities

        public static RemoteAudioTrack CreateWrapper(PeerConnection parent, in CreateConfig config)
        {
            return new RemoteAudioTrack(parent, config.TrackName);
        }

        #endregion
    }
}
