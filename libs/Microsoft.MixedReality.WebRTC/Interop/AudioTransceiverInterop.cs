// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.MixedReality.WebRTC.Interop
{
    /// <summary>
    /// Handle to a native audio transceiver object.
    /// </summary>
    public sealed class AudioTransceiverHandle : SafeHandle
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
        public AudioTransceiverHandle() : base(IntPtr.Zero, ownsHandle: true)
        {
        }

        /// <summary>
        /// Constructor for a valid handle referencing the given native object.
        /// </summary>
        /// <param name="handle">The valid internal handle to the native object.</param>
        public AudioTransceiverHandle(IntPtr handle) : base(IntPtr.Zero, ownsHandle: true)
        {
            SetHandle(handle);
        }

        /// <summary>
        /// Release the native object while the handle is being closed.
        /// </summary>
        /// <returns>Return <c>true</c> if the native object was successfully released.</returns>
        protected override bool ReleaseHandle()
        {
            AudioTransceiverInterop.AudioTransceiver_RemoveRef(handle);
            return true;
        }
    }

    internal class AudioTransceiverInterop
    {
        #region Native functions

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsAudioTransceiverAddRef")]
        public static unsafe extern void AudioTransceiver_AddRef(AudioTransceiverHandle handle);

        // Note - This is used during SafeHandle.ReleaseHandle(), so cannot use AudioTransceiverHandle
        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsAudioTransceiverRemoveRef")]
        public static unsafe extern void AudioTransceiver_RemoveRef(IntPtr handle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsAudioTransceiverRegisterStateUpdatedCallback")]
        public static unsafe extern uint AudioTransceiver_RegisterStateUpdatedCallback(AudioTransceiverHandle handle,
            StateUpdatedDelegate callback, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsAudioTransceiverSetDirection")]
        public static unsafe extern uint AudioTransceiver_SetDirection(AudioTransceiverHandle handle,
            Transceiver.Direction newDirection);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsAudioTransceiverSetLocalTrack")]
        public static unsafe extern uint AudioTransceiver_SetLocalTrack(AudioTransceiverHandle handle,
            LocalAudioTrackHandle trackHandle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsAudioTransceiverGetLocalTrack")]
        public static unsafe extern uint AudioTransceiver_GetLocalTrack(AudioTransceiverHandle handle,
            ref LocalAudioTrackHandle trackHandle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsAudioTransceiverGetRemoteTrack")]
        public static unsafe extern uint AudioTransceiver_GetRemoteTrack(AudioTransceiverHandle handle,
            ref RemoteAudioTrackHandle trackHandle);

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
            /// Handle to the audio transceiver wrapper.
            /// </summary>
            public IntPtr transceiverHandle;

            public InitConfig(AudioTransceiver transceiver, AudioTransceiverInitSettings settings)
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

        public static readonly StateUpdatedDelegate StateUpdatedCallback = AudioTransceiverStateUpdatedCallback;

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate IntPtr CreateObjectDelegate(IntPtr peer, in CreateConfig config);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void FinishCreateDelegate(IntPtr transceiver, IntPtr interopHandle);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void StateUpdatedDelegate(IntPtr transceiver, StateUpdatedReason reason,
            OptDirection negotiatedDirection, Transceiver.Direction desiredDirection);

        [MonoPInvokeCallback(typeof(CreateObjectDelegate))]
        public static IntPtr AudioTransceiverCreateObjectCallback(IntPtr peer, in CreateConfig config)
        {
            var peerWrapper = Utils.ToWrapper<PeerConnection>(peer);
            var audioTranceiverWrapper = CreateWrapper(peerWrapper, in config);
            return Utils.MakeWrapperRef(audioTranceiverWrapper);
        }

        [MonoPInvokeCallback(typeof(FinishCreateDelegate))]
        public static void AudioTransceiverFinishCreateCallback(IntPtr transceiver, IntPtr interopHandle)
        {
            var transceiverWrapper = Utils.ToWrapper<AudioTransceiver>(transceiver);
            transceiverWrapper.SetHandle(new AudioTransceiverHandle(interopHandle));
            transceiverWrapper.PeerConnection.OnTransceiverAdded(transceiverWrapper);
        }

        [MonoPInvokeCallback(typeof(StateUpdatedDelegate))]
        private static void AudioTransceiverStateUpdatedCallback(IntPtr transceiver, StateUpdatedReason reason,
            OptDirection negotiatedDirection, Transceiver.Direction desiredDirection)
        {
            var audioTranceiverWrapper = Utils.ToWrapper<AudioTransceiver>(transceiver);
            var optDir = negotiatedDirection == OptDirection.NotSet ? null : (Transceiver.Direction?)negotiatedDirection;
            audioTranceiverWrapper.OnStateUpdated(optDir, desiredDirection);
        }

        #endregion


        #region Utilities

        public static AudioTransceiver CreateWrapper(PeerConnection parent, in CreateConfig config)
        {
            return new AudioTransceiver(parent, config.MlineIndex, config.Name, config.DesiredDirection);
        }

        public static void RegisterCallbacks(AudioTransceiver transceiver, out IntPtr argsRef)
        {
            argsRef = Utils.MakeWrapperRef(transceiver);
            AudioTransceiver_RegisterStateUpdatedCallback(transceiver._nativeHandle, StateUpdatedCallback, argsRef);
        }

        public static void UnregisterCallbacks(AudioTransceiverHandle handle, IntPtr argsRef)
        {
            Utils.ReleaseWrapperRef(argsRef);
            AudioTransceiver_RegisterStateUpdatedCallback(handle, null, IntPtr.Zero);
        }

        #endregion
    }
}
