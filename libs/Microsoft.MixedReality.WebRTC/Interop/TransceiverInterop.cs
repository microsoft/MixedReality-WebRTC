// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Microsoft.MixedReality.WebRTC.Interop
{
    internal class TransceiverInterop
    {
        internal sealed class TransceiverHandle : ObjectHandle
        {
            public TransceiverHandle(IntPtr value) : base(value) { }
        }

        #region Native functions

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsTransceiverRegisterAssociatedCallback")]
        public static unsafe extern void Transceiver_RegisterAssociatedCallback(TransceiverHandle handle,
            AssociatedDelegate callback, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsTransceiverRegisterStateUpdatedCallback")]
        public static unsafe extern void Transceiver_RegisterStateUpdatedCallback(TransceiverHandle handle,
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
        public static unsafe extern uint Transceiver_GetRemoteAudioTrack(TransceiverHandle handle, out IntPtr trackHandle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsTransceiverGetRemoteVideoTrack")]
        public static unsafe extern uint Transceiver_GetRemoteVideoTrack(TransceiverHandle handle, out IntPtr trackHandle);

        #endregion


        #region Marshaling data structures

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

            public InitConfig(MediaKind mediaKind, TransceiverInitSettings settings)
            {
                name = settings?.Name;
                this.mediaKind = mediaKind;
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

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void AssociatedDelegate(IntPtr transceiver, int mlineIndex);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void StateUpdatedDelegate(IntPtr transceiver, StateUpdatedReason reason,
            OptDirection negotiatedDirection, Transceiver.Direction desiredDirection);

        public static readonly AssociatedDelegate AssociatedCallback = TransceiverAssociatedCallback;
        public static readonly StateUpdatedDelegate StateUpdatedCallback = TransceiverStateUpdatedCallback;

        [MonoPInvokeCallback(typeof(AssociatedDelegate))]
        private static void TransceiverAssociatedCallback(IntPtr transceiver, int mlineIndex)
        {
            var videoTranceiverWrapper = Utils.ToWrapper<Transceiver>(transceiver);
            videoTranceiverWrapper.OnAssociated(mlineIndex);
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

        public static Transceiver CreateWrapper(PeerConnection parent, in PeerConnectionInterop.TransceiverAddedInfo info)
        {
            // Create a new wrapper
            var streamIDs = info.encodedStreamIDs.Split(';');
            Transceiver wrapper = new Transceiver(new TransceiverHandle(info.transceiverHandle), info.mediaKind, parent,
                info.mlineIndex, info.name, streamIDs, info.desiredDirection);

            // Assign a reference to it inside the UserData of the native object so it can be retrieved whenever needed
            IntPtr wrapperRef = Utils.MakeWrapperRef(wrapper);
            ObjectInterop.Object_SetUserData(wrapper._nativeHandle, wrapperRef);

            return wrapper;
        }

        public static void RegisterCallbacks(Transceiver transceiver, out IntPtr argsRef)
        {
            argsRef = Utils.MakeWrapperRef(transceiver);
            Transceiver_RegisterAssociatedCallback(transceiver._nativeHandle, AssociatedCallback, argsRef);
            Transceiver_RegisterStateUpdatedCallback(transceiver._nativeHandle, StateUpdatedCallback, argsRef);
        }

        public static void UnregisterCallbacks(TransceiverHandle handle, IntPtr argsRef)
        {
            Utils.ReleaseWrapperRef(argsRef);
            Transceiver_RegisterAssociatedCallback(handle, null, IntPtr.Zero);
            Transceiver_RegisterStateUpdatedCallback(handle, null, IntPtr.Zero);
        }

        #endregion
    }
}
