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
    public sealed class LocalAudioTrackHandle : SafeHandle
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
        public LocalAudioTrackHandle() : base(IntPtr.Zero, ownsHandle: true)
        {
        }

        /// <summary>
        /// Constructor for a valid handle referencing the given native object.
        /// </summary>
        /// <param name="handle">The valid internal handle to the native object.</param>
        public LocalAudioTrackHandle(IntPtr handle) : base(IntPtr.Zero, ownsHandle: true)
        {
            SetHandle(handle);
        }

        /// <summary>
        /// Release the native object while the handle is being closed.
        /// </summary>
        /// <returns>Return <c>true</c> if the native object was successfully released.</returns>
        protected override bool ReleaseHandle()
        {
            LocalAudioTrackInterop.LocalAudioTrack_RemoveRef(handle);
            return true;
        }
    }

    internal class LocalAudioTrackInterop
    {
        #region Native functions

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsLocalAudioTrackAddRef")]
        public static unsafe extern void LocalAudioTrack_AddRef(LocalAudioTrackHandle handle);

        // Note - This is used during SafeHandle.ReleaseHandle(), so cannot use LocalAudioTrackHandle
        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsLocalAudioTrackRemoveRef")]
        public static unsafe extern void LocalAudioTrack_RemoveRef(IntPtr handle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsLocalAudioTrackCreateFromDevice")]
        public static unsafe extern uint LocalAudioTrack_CreateFromDevice(in PeerConnectionInterop.LocalAudioTrackInteropInitConfig config,
            string trackName, out LocalAudioTrackHandle trackHandle);

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
