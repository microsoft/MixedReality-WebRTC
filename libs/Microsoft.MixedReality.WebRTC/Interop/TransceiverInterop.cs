// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Microsoft.MixedReality.WebRTC.Interop
{
    /// <summary>
    /// Handle to a native transceiver object.
    /// </summary>
    public sealed class TransceiverHandle : SafeHandle
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
        public TransceiverHandle() : base(IntPtr.Zero, ownsHandle: true)
        {
        }

        /// <summary>
        /// Constructor for a valid handle referencing the given native object.
        /// </summary>
        /// <param name="handle">The valid internal handle to the native object.</param>
        public TransceiverHandle(IntPtr handle) : base(IntPtr.Zero, ownsHandle: true)
        {
            SetHandle(handle);
        }

        /// <summary>
        /// Release the native object while the handle is being closed.
        /// </summary>
        /// <returns>Return <c>true</c> if the native object was successfully released.</returns>
        protected override bool ReleaseHandle()
        {
            TransceiverInterop.Transceiver_RemoveRef(handle);
            return true;
        }
    }

    internal class TransceiverInterop
    {
        #region Native functions

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsTransceiverAddRef")]
        public static unsafe extern void Transceiver_AddRef(TransceiverHandle handle);

        // Note - This is used during SafeHandle.ReleaseHandle(), so cannot use TransceiverHandle
        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsTransceiverRemoveRef")]
        public static unsafe extern void Transceiver_RemoveRef(IntPtr handle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsTransceiverRegisterStateUpdatedCallback")]
        public static unsafe extern uint Transceiver_RegisterStateUpdatedCallback(TransceiverHandle handle,
            StateUpdatedDelegate callback, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsTransceiverSetDirection")]
        public static unsafe extern uint Transceiver_SetDirection(TransceiverHandle handle,
            Transceiver.Direction newDirection);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsTransceiverSetLocalAudioTrack")]
        public static unsafe extern uint Transceiver_SetLocalAudioTrack(TransceiverHandle handle,
            LocalAudioTrackHandle trackHandle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsTransceiverSetLocalVideoTrack")]
        public static unsafe extern uint Transceiver_SetLocalVideoTrack(TransceiverHandle handle,
            LocalVideoTrackHandle trackHandle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsTransceiverGetLocalAudioTrack")]
        public static unsafe extern uint Transceiver_GetLocalAudioTrack(TransceiverHandle handle,
            out LocalAudioTrackHandle trackHandle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsTransceiverGetLocalVideoTrack")]
        public static unsafe extern uint Transceiver_GetLocalVideoTrack(TransceiverHandle handle,
            out LocalVideoTrackHandle trackHandle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsTransceiverGetRemoteAudioTrack")]
        public static unsafe extern uint Transceiver_GetRemoteAudioTrack(TransceiverHandle handle,
            out RemoteAudioTrackHandle trackHandle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsTransceiverGetRemoteVideoTrack")]
        public static unsafe extern uint Transceiver_GetRemoteVideoTrack(TransceiverHandle handle,
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
            /// Kind of media the transceiver transports.
            /// </summary>
            public MediaKind MediaKind;

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
            /// Kind of media the new transceiver transports.
            /// </summary>
            public MediaKind mediaKind;

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

            public InitConfig(Transceiver transceiver, TransceiverInitSettings settings)
            {
                name = settings?.Name;
                mediaKind = transceiver.MediaKind;
                transceiverHandle = Utils.MakeWrapperRef(transceiver);
                desiredDirection = (settings != null ? settings.InitialDesiredDirection : new TransceiverInitSettings().InitialDesiredDirection);
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

        public static readonly StateUpdatedDelegate StateUpdatedCallback = TransceiverStateUpdatedCallback;

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate IntPtr CreateObjectDelegate(IntPtr peer, in CreateConfig config);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void FinishCreateDelegate(IntPtr transceiver, IntPtr interopHandle);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void StateUpdatedDelegate(IntPtr transceiver, StateUpdatedReason reason,
            OptDirection negotiatedDirection, Transceiver.Direction desiredDirection);

        [MonoPInvokeCallback(typeof(CreateObjectDelegate))]
        public static IntPtr TransceiverCreateObjectCallback(IntPtr peer, in CreateConfig config)
        {
            var peerWrapper = Utils.ToWrapper<PeerConnection>(peer);
            var videoTranceiverWrapper = CreateWrapper(peerWrapper, in config);
            return Utils.MakeWrapperRef(videoTranceiverWrapper);
        }

        [MonoPInvokeCallback(typeof(FinishCreateDelegate))]
        public static void TransceiverFinishCreateCallback(IntPtr transceiver, IntPtr interopHandle)
        {
            var transceiverWrapper = Utils.ToWrapper<Transceiver>(transceiver);
            transceiverWrapper.SetHandle(new TransceiverHandle(interopHandle));
            transceiverWrapper.PeerConnection.OnTransceiverAdded(transceiverWrapper);
        }

        [MonoPInvokeCallback(typeof(StateUpdatedDelegate))]
        private static void TransceiverStateUpdatedCallback(IntPtr transceiver, StateUpdatedReason reason,
            OptDirection negotiatedDirection, Transceiver.Direction desiredDirection)
        {
            var videoTranceiverWrapper = Utils.ToWrapper<Transceiver>(transceiver);
            var optDir = negotiatedDirection == OptDirection.NotSet ? null : (Transceiver.Direction?)negotiatedDirection;
            videoTranceiverWrapper.OnStateUpdated(optDir, desiredDirection);
        }

        #endregion


        #region Utilities

        public static Transceiver CreateWrapper(PeerConnection parent, in CreateConfig config)
        {
            if (config.MediaKind == MediaKind.Audio)
            {
                return new AudioTransceiver(parent, config.MlineIndex, config.Name, config.DesiredDirection);
            }
            else
            {
                Debug.Assert(config.MediaKind == MediaKind.Video);
                return new VideoTransceiver(parent, config.MlineIndex, config.Name, config.DesiredDirection);
            }
        }

        public static void RegisterCallbacks(Transceiver transceiver, out IntPtr argsRef)
        {
            argsRef = Utils.MakeWrapperRef(transceiver);
            Transceiver_RegisterStateUpdatedCallback(transceiver._nativeHandle, StateUpdatedCallback, argsRef);
        }

        public static void UnregisterCallbacks(TransceiverHandle handle, IntPtr argsRef)
        {
            Utils.ReleaseWrapperRef(argsRef);
            Transceiver_RegisterStateUpdatedCallback(handle, null, IntPtr.Zero);
        }

        #endregion
    }
}
