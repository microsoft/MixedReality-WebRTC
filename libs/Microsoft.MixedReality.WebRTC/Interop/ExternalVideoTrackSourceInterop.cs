// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.MixedReality.WebRTC.Interop
{
    /// <summary>
    /// Handle to a native external video track source object.
    /// </summary>
    public sealed class ExternalVideoTrackSourceHandle : SafeHandle
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
        public ExternalVideoTrackSourceHandle() : base(IntPtr.Zero, ownsHandle: true)
        {
        }

        /// <summary>
        /// Constructor for a valid handle referencing the given native object.
        /// </summary>
        /// <param name="handle">The valid internal handle to the native object.</param>
        public ExternalVideoTrackSourceHandle(IntPtr handle) : base(IntPtr.Zero, ownsHandle: true)
        {
            SetHandle(handle);
        }

        /// <summary>
        /// Release the native object while the handle is being closed.
        /// </summary>
        /// <returns>Return <c>true</c> if the native object was successfully released.</returns>
        protected override bool ReleaseHandle()
        {
            ExternalVideoTrackSourceInterop.ExternalVideoTrackSource_RemoveRef(handle);
            return true;
        }
    }

    internal class ExternalVideoTrackSourceInterop
    {
        #region Native functions

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsExternalVideoTrackSourceAddRef")]
        public static unsafe extern void ExternalVideoTrackSource_AddRef(ExternalVideoTrackSourceHandle handle);

        // Note - This is used during SafeHandle.ReleaseHandle(), so cannot use ExternalVideoTrackSourceHandle
        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsExternalVideoTrackSourceRemoveRef")]
        public static unsafe extern void ExternalVideoTrackSource_RemoveRef(IntPtr handle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsExternalVideoTrackSourceCompleteI420AFrameRequest")]
        public static unsafe extern uint ExternalVideoTrackSource_CompleteFrameRequest(ExternalVideoTrackSourceHandle handle,
            uint requestId, long timestampMs, in I420AVideoFrame frame);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsExternalVideoTrackSourceCompleteArgb32FrameRequest")]
        public static unsafe extern uint ExternalVideoTrackSource_CompleteFrameRequest(ExternalVideoTrackSourceHandle handle,
            uint requestId, long timestampMs, in Argb32VideoFrame frame);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsExternalVideoTrackSourceShutdown")]
        public static extern void ExternalVideoTrackSource_Shutdown(ExternalVideoTrackSourceHandle handle);

        #endregion


        #region Helpers

        public static void CompleteFrameRequest(ExternalVideoTrackSourceHandle sourceHandle, uint requestId,
            long timestampMs, in I420AVideoFrame frame)
        {
            uint res = ExternalVideoTrackSource_CompleteFrameRequest(sourceHandle, requestId, timestampMs, frame);
            Utils.ThrowOnErrorCode(res);
        }

        public static void CompleteFrameRequest(ExternalVideoTrackSourceHandle sourceHandle, uint requestId,
            long timestampMs, in Argb32VideoFrame frame)
        {
            uint res = ExternalVideoTrackSource_CompleteFrameRequest(sourceHandle, requestId, timestampMs, frame);
            Utils.ThrowOnErrorCode(res);
        }

        #endregion
    }
}
