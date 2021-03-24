// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.MixedReality.WebRTC.Interop
{
    internal class DataChannelInterop
    {
        #region Native functions

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsDataChannelSetUserData")]
        public static unsafe extern void DataChannel_SetUserData(IntPtr handle, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsDataChannelGetUserData")]
        public static unsafe extern IntPtr DataChannel_GetUserData(IntPtr handle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsDataChannelRegisterCallbacks")]
        public static extern void DataChannel_RegisterCallbacks(IntPtr dataChannelHandle, in Callbacks callbacks);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsDataChannelSendMessage")]
        public static extern uint DataChannel_SendMessage(IntPtr dataChannelHandle, byte[] data, ulong size);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsDataChannelSendMessage")]
        public static extern uint DataChannel_SendMessage(IntPtr dataChannelHandle, IntPtr data, ulong size);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsDataChannelSendMessageEx")]
        public static extern uint DataChannel_SendMessageEx(
            IntPtr dataChannelHandle, DataChannel.MessageKind messageKind, IntPtr data, ulong size);
        #endregion


        #region Marshaling data structures

        [Flags]
        public enum Flags : uint
        {
            None = 0x0,
            Ordered = 0x1,
            Reliable = 0x2
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public ref struct CreateConfig
        {
            public int id;
            public uint flags;
            [MarshalAs(UnmanagedType.LPStr)]
            public string label;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public ref struct Callbacks
        {
            public MessageCallback messageCallback;
            public IntPtr messageUserData;
            public MessageExCallback messageExCallback;
            public IntPtr messageExUserData;
            public BufferingCallback bufferingCallback;
            public IntPtr bufferingUserData;
            public StateCallback stateCallback;
            public IntPtr stateUserData;
        }

        #endregion


        #region Native callbacks

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void MessageCallback(IntPtr userData, IntPtr data, ulong size);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void MessageExCallback(IntPtr userData, int messageKind, IntPtr data, ulong size);

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
            public MessageExCallback MessageExCallback;
            public BufferingCallback BufferingCallback;
            public StateCallback StateCallback;
        }

        [MonoPInvokeCallback(typeof(MessageCallback))]
        public static void DataChannelMessageCallback(IntPtr userData, IntPtr data, ulong size)
        {
            var args = Utils.ToWrapper<CallbackArgs>(userData);
            args.DataChannel.OnMessageReceived(data, size);
        }

        [MonoPInvokeCallback(typeof(MessageExCallback))]
        public static void DataChannelMessageExCallback(IntPtr userData, int messageKind, IntPtr data, ulong size)
        {
            var args = Utils.ToWrapper<CallbackArgs>(userData);
            args.DataChannel.OnMessageExReceived(messageKind, data, size);
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

        public static DataChannel CreateWrapper(PeerConnection parent, in PeerConnectionInterop.DataChannelAddedInfo info)
        {
            // Create the callback args for the data channel
            var args = new CallbackArgs()
            {
                Peer = parent,
                DataChannel = null, // set below
                MessageCallback = DataChannelMessageCallback,
                MessageExCallback = DataChannelMessageExCallback,
                BufferingCallback = DataChannelBufferingCallback,
                StateCallback = DataChannelStateCallback
            };

            // Pin the args to pin the delegates while they're registered with the native code
            IntPtr argsRef = Utils.MakeWrapperRef(args);

            // Create a new data channel wrapper. It will hold the lock for its args while alive.
            bool ordered = (info.flags & 0x1) != 0;
            bool reliable = (info.flags & 0x2) != 0;
            var dataChannel = new DataChannel(info.dataChannelHandle, parent, argsRef, info.id, info.label, ordered, reliable);
            args.DataChannel = dataChannel;

            // Assign a reference to it inside the UserData of the native object so it can be retrieved whenever needed
            IntPtr wrapperRef = Utils.MakeWrapperRef(dataChannel);
            DataChannel_SetUserData(info.dataChannelHandle, wrapperRef);

            // Register the callbacks
            var callbacks = new Callbacks()
            {
                messageCallback = args.MessageCallback,
                messageUserData = argsRef,
                messageExCallback = args.MessageExCallback,
                messageExUserData = argsRef,
                bufferingCallback = args.BufferingCallback,
                bufferingUserData = argsRef,
                stateCallback = args.StateCallback,
                stateUserData = argsRef
            };
            DataChannel_RegisterCallbacks(info.dataChannelHandle, callbacks);

            return dataChannel;
        }

        #endregion
    }
}
