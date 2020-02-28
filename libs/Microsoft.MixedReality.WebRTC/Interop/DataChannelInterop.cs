// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Microsoft.MixedReality.WebRTC.Interop
{
    internal class DataChannelInterop
    {
        #region Native functions

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsDataChannelSendMessage")]
        public static extern uint DataChannel_SendMessage(IntPtr dataChannelHandle, byte[] data, ulong size);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsDataChannelSendMessage")]
        public static extern uint DataChannel_SendMessage(IntPtr dataChannelHandle, IntPtr data, ulong size);

        #endregion


        #region Marshaling data structures

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public ref struct CreateConfig
        {
            public int id;
            public string label;
            public uint flags;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public ref struct Callbacks
        {
            public MessageCallback messageCallback;
            public IntPtr messageUserData;
            public BufferingCallback bufferingCallback;
            public IntPtr bufferingUserData;
            public StateCallback stateCallback;
            public IntPtr stateUserData;
        }

        #endregion


        #region Native callbacks

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate IntPtr CreateObjectDelegate(IntPtr peer, CreateConfig config,
            out Callbacks callbacks);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void MessageCallback(IntPtr userData, IntPtr data, ulong size);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void BufferingCallback(IntPtr userData,
            ulong previous, ulong current, ulong limit);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void StateCallback(IntPtr userData, int state, int id);

        /// <summary>
        /// Utility to lock all data channel delegates registered with the native plugin and prevent their
        /// garbage collection while registered.
        /// </summary>
        public class CallbackArgs
        {
            public PeerConnection Peer;
            public DataChannel DataChannel;
            public MessageCallback MessageCallback;
            public BufferingCallback BufferingCallback;
            public StateCallback StateCallback;
        }

        [MonoPInvokeCallback(typeof(CreateObjectDelegate))]
        public static IntPtr DataChannelCreateObjectCallback(IntPtr peer, CreateConfig config,
            out Callbacks callbacks)
        {
            var peerWrapper = Utils.ToWrapper<PeerConnection>(peer);
            var dataChannelWrapper = CreateWrapper(peerWrapper, config, out callbacks);
            return Utils.MakeWrapperRef(dataChannelWrapper);
        }

        [MonoPInvokeCallback(typeof(MessageCallback))]
        public static void DataChannelMessageCallback(IntPtr userData, IntPtr data, ulong size)
        {
            var args = Utils.ToWrapper<CallbackArgs>(userData);
            args.DataChannel.OnMessageReceived(data, size);
        }

        [MonoPInvokeCallback(typeof(BufferingCallback))]
        public static void DataChannelBufferingCallback(IntPtr userData, ulong previous, ulong current, ulong limit)
        {
            var args = Utils.ToWrapper<CallbackArgs>(userData);
            args.DataChannel.OnBufferingChanged(previous, current, limit);
        }

        [MonoPInvokeCallback(typeof(StateCallback))]
        public static void DataChannelStateCallback(IntPtr userData, int state, int id)
        {
            var args = Utils.ToWrapper<CallbackArgs>(userData);
            args.DataChannel.OnStateChanged(state, id);
        }

        #endregion


        #region Utilities

        public static DataChannel CreateWrapper(PeerConnection parent, CreateConfig config, out Callbacks callbacks)
        {
            // Create the callback args for the data channel
            var args = new CallbackArgs()
            {
                Peer = parent,
                DataChannel = null, // set below
                MessageCallback = DataChannelMessageCallback,
                BufferingCallback = DataChannelBufferingCallback,
                StateCallback = DataChannelStateCallback
            };

            // Pin the args to pin the delegates while they're registered with the native code
            var handle = GCHandle.Alloc(args, GCHandleType.Normal);
            IntPtr userData = GCHandle.ToIntPtr(handle);

            // Create a new data channel. It will hold the lock for its args while alive.
            bool ordered = (config.flags & 0x1) != 0;
            bool reliable = (config.flags & 0x2) != 0;
            var dataChannel = new DataChannel(parent, handle, config.id, config.label, ordered, reliable);
            args.DataChannel = dataChannel;

            // Fill out the callbacks
            callbacks = new Callbacks()
            {
                messageCallback = args.MessageCallback,
                messageUserData = userData,
                bufferingCallback = args.BufferingCallback,
                bufferingUserData = userData,
                stateCallback = args.StateCallback,
                stateUserData = userData
            };

            return dataChannel;
        }

        public static void SetHandle(DataChannel dataChannel, IntPtr handle)
        {
            Debug.Assert(handle != IntPtr.Zero);
            // Either first-time assign or no-op (assign same value again)
            Debug.Assert((dataChannel._interopHandle == IntPtr.Zero) || (dataChannel._interopHandle == handle));
            dataChannel._interopHandle = handle;
        }

        #endregion
    }
}
