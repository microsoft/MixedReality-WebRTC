// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.MixedReality.WebRTC.Interop
{
    /// <summary>
    /// Handle to a native video transceiver object.
    /// </summary>
    public sealed class VideoTransceiverHandle : SafeHandle
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
        public VideoTransceiverHandle() : base(IntPtr.Zero, ownsHandle: true)
        {
        }

        /// <summary>
        /// Constructor for a valid handle referencing the given native object.
        /// </summary>
        /// <param name="handle">The valid internal handle to the native object.</param>
        public VideoTransceiverHandle(IntPtr handle) : base(IntPtr.Zero, ownsHandle: true)
        {
            SetHandle(handle);
        }

        /// <summary>
        /// Release the native object while the handle is being closed.
        /// </summary>
        /// <returns>Return <c>true</c> if the native object was successfully released.</returns>
        protected override bool ReleaseHandle()
        {
            VideoTransceiverInterop.VideoTransceiver_RemoveRef(handle);
            return true;
        }
    }

    internal class VideoTransceiverInterop
    {
        #region Native functions

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsVideoTransceiverAddRef")]
        public static unsafe extern void VideoTransceiver_AddRef(VideoTransceiverHandle handle);

        // Note - This is used during SafeHandle.ReleaseHandle(), so cannot use VideoTransceiverHandle
        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsVideoTransceiverRemoveRef")]
        public static unsafe extern void VideoTransceiver_RemoveRef(IntPtr handle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsVideoTransceiverRegisterStateUpdatedCallback")]
        public static unsafe extern uint VideoTransceiver_RegisterStateUpdatedCallback(VideoTransceiverHandle handle,
            StateUpdatedDelegate callback, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsVideoTransceiverSetDirection")]
        public static unsafe extern uint VideoTransceiver_SetDirection(VideoTransceiverHandle handle,
            Transceiver.Direction newDirection);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsVideoTransceiverSetLocalTrack")]
        public static unsafe extern uint VideoTransceiver_SetLocalTrack(VideoTransceiverHandle handle,
            LocalVideoTrackHandle trackHandle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsVideoTransceiverGetLocalTrack")]
        public static unsafe extern uint VideoTransceiver_GetLocalTrack(VideoTransceiverHandle handle,
            out LocalVideoTrackHandle trackHandle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsVideoTransceiverGetRemoteTrack")]
        public static unsafe extern uint VideoTransceiver_GetRemoteTrack(VideoTransceiverHandle handle,
            out RemoteVideoTrackHandle trackHandle);

        #endregion


        #region Marshaling data structures

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public ref struct CreateConfig
        {
            /// <summary>
            /// Transceiver name.
            /// </summary>
            [MarshalAs(UnmanagedType.LPStr)]
            public string Name;

            /// <summary>
            /// Media line index of the transceiver.
            /// </summary>
            public int MlineIndex;

            /// <summary>
            /// Initial desired direction of the transceiver on creation.
            /// </summary>
            public Transceiver.Direction DesiredDirection;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        internal ref struct InitConfig
        {
            /// <summary>
            /// Name of the transceiver, for logging and debugging.
            /// </summary>
            [MarshalAs(UnmanagedType.LPStr)]
            public string name;

            /// <summary>
            /// Initial desired direction.
            /// </summary>
            public Transceiver.Direction desiredDirection;

            /// <summary>
            /// Stream IDs of the transceiver, encoded as a semi-colon separated list of IDs.
            /// </summary>
            [MarshalAs(UnmanagedType.LPStr)]
            public string encodedStreamIds;

            /// <summary>
            /// Handle to the video transceiver wrapper.
            /// </summary>
            public IntPtr transceiverHandle;

            public InitConfig(VideoTransceiver transceiver, VideoTransceiverInitSettings settings)
            {
                name = settings?.Name;
                transceiverHandle = Utils.MakeWrapperRef(transceiver);
                desiredDirection = (settings != null ? settings.InitialDesiredDirection : new VideoTransceiverInitSettings().InitialDesiredDirection);
                encodedStreamIds = Utils.EncodeTransceiverStreamIDs(settings?.StreamIDs);
            }
        }

        public enum StateUpdatedReason : int
        {
            LocalDesc,
            RemoteDesc,
            SetDirection
        }

        public enum OptDirection : int
        {
            NotSet = -1,
            SendReceive = 0,
            SendOnly = 1,
            ReceiveOnly = 2,
            Inactive = 3,
        }

        #endregion


        #region Native callbacks

        public static readonly StateUpdatedDelegate StateUpdatedCallback = VideoTransceiverStateUpdatedCallback;

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate IntPtr CreateObjectDelegate(IntPtr peer, in CreateConfig config);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void FinishCreateDelegate(IntPtr transceiver, IntPtr interopHandle);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void StateUpdatedDelegate(IntPtr transceiver, StateUpdatedReason reason,
            OptDirection negotiatedDirection, Transceiver.Direction desiredDirection);

        [MonoPInvokeCallback(typeof(CreateObjectDelegate))]
        public static IntPtr VideoTransceiverCreateObjectCallback(IntPtr peer, in CreateConfig config)
        {
            var peerWrapper = Utils.ToWrapper<PeerConnection>(peer);
            var videoTranceiverWrapper = CreateWrapper(peerWrapper, in config);
            return Utils.MakeWrapperRef(videoTranceiverWrapper);
        }

        [MonoPInvokeCallback(typeof(FinishCreateDelegate))]
        public static void VideoTransceiverFinishCreateCallback(IntPtr transceiver, IntPtr interopHandle)
        {
            var transceiverWrapper = Utils.ToWrapper<VideoTransceiver>(transceiver);
            transceiverWrapper.SetHandle(new VideoTransceiverHandle(interopHandle));
            transceiverWrapper.PeerConnection.OnTransceiverAdded(transceiverWrapper);
        }

        [MonoPInvokeCallback(typeof(StateUpdatedDelegate))]
        private static void VideoTransceiverStateUpdatedCallback(IntPtr transceiver, StateUpdatedReason reason,
            OptDirection negotiatedDirection, Transceiver.Direction desiredDirection)
        {
            var videoTranceiverWrapper = Utils.ToWrapper<VideoTransceiver>(transceiver);
            var optDir = negotiatedDirection == OptDirection.NotSet ? null : (Transceiver.Direction?)negotiatedDirection;
            videoTranceiverWrapper.OnStateUpdated(optDir, desiredDirection);
        }

        #endregion


        #region Utilities

        public static VideoTransceiver CreateWrapper(PeerConnection parent, in CreateConfig config)
        {
            return new VideoTransceiver(parent, config.MlineIndex, config.Name, config.DesiredDirection);
        }

        public static void RegisterCallbacks(VideoTransceiver transceiver, out IntPtr argsRef)
        {
            argsRef = Utils.MakeWrapperRef(transceiver);
            VideoTransceiver_RegisterStateUpdatedCallback(transceiver._nativeHandle, StateUpdatedCallback, argsRef);
        }

        public static void UnregisterCallbacks(VideoTransceiverHandle handle, IntPtr argsRef)
        {
            Utils.ReleaseWrapperRef(argsRef);
            VideoTransceiver_RegisterStateUpdatedCallback(handle, null, IntPtr.Zero);
        }

        #endregion
    }
}
