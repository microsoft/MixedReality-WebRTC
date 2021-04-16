// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using Microsoft.MixedReality.WebRTC.Interop;
using Microsoft.MixedReality.WebRTC.Tracing;

namespace Microsoft.MixedReality.WebRTC
{
    /// <summary>
    /// Encapsulates a data channel of a peer connection.
    ///
    /// A data channel is a "pipe" allowing to send and receive arbitrary data to the
    /// remote peer. Data channels are based on DTLS-SRTP, and are therefore secure (encrypted).
    /// Exact security guarantees are provided by the underlying WebRTC core implementation
    /// and the WebRTC standard itself.
    ///
    /// https://tools.ietf.org/wg/rtcweb/
    /// https://www.w3.org/TR/webrtc/
    ///
    /// An instance of <see cref="DataChannel"/> is created either by manually calling
    /// <see cref="PeerConnection.AddDataChannelAsync(string,bool,bool,System.Threading.CancellationToken)"/>
    /// or one of its variants, or automatically by the implementation when a new data channel
    /// is created in-band by the remote peer (<see cref="PeerConnection.DataChannelAdded"/>).
    /// <see cref="DataChannel"/> cannot be instantiated directly.
    /// </summary>
    /// <seealso cref="PeerConnection.AddDataChannelAsync(string, bool, bool, System.Threading.CancellationToken)"/>
    /// <seealso cref="PeerConnection.AddDataChannelAsync(ushort, string, bool, bool, System.Threading.CancellationToken)"/>
    /// <seealso cref="PeerConnection.DataChannelAdded"/>
    public class DataChannel
    {
        /// <summary>
        /// Connection state of a data channel.
        /// </summary>
        public enum ChannelState
        {
            /// <summary>
            /// The data channel has just been created, and negotiating is underway to establish
            /// a link between the peers. The data channel cannot be used to send/receive yet.
            /// </summary>
            Connecting = 0,

            /// <summary>
            /// The data channel is open and ready to send and receive messages.
            /// </summary>
            Open = 1,

            /// <summary>
            /// The data channel is being closed, and is not available anymore for data exchange.
            /// </summary>
            Closing = 2,

            /// <summary>
            /// The data channel reached end of life and can be destroyed.
            /// It cannot be re-connected; instead a new data channel must be created.
            /// </summary>
            Closed = 3
        }

        /// <summary>
        /// Type of message sent or received through the data channel.
        /// </summary>
        public enum MessageKind
        {
            /// <summary>
            /// The message is a binary message.
            /// </summary>
            Binary = 1,

            /// <summary>
            /// The message is a text message (UTF-8 encoded string).
            /// </summary>
            Text = 2
        }

        /// <summary>
        /// Delegate for the <see cref="BufferingChanged"/> event.
        /// </summary>
        /// <param name="previous">Previous buffering size, in bytes.</param>
        /// <param name="current">New buffering size, in bytes.</param>
        /// <param name="limit">Maximum buffering size, in bytes.</param>
        public delegate void BufferingChangedDelegate(ulong previous, ulong current, ulong limit);

        /// <summary>
        /// The <see cref="PeerConnection"/> object this data channel was created from and is attached to.
        /// </summary>
        public PeerConnection PeerConnection { get; }

        /// <summary>
        /// The unique identifier of the data channel in the current connection.
        /// </summary>
        public int ID { get; }

        /// <summary>
        /// The data channel name in the current connection.
        /// </summary>
        public string Label { get; }

        /// <summary>
        /// Indicates whether the data channel messages are ordered or not.
        /// Ordered messages are delivered in the order they are sent, at the cost of delaying later messages
        /// delivery to the application (via <see cref="MessageReceived"/>) when internally arriving out of order.
        /// </summary>
        /// <value><c>true</c> if messages are ordered.</value>
        /// <seealso cref="Reliable"/>
        public bool Ordered { get; }

        /// <summary>
        /// Indicates whether the data channel messages are reliably delivered.
        /// Reliable messages are guaranteed to be delivered as long as the connection is not dropped.
        /// Unreliable messages may be silently dropped for whatever reason, and the implementation will
        /// not try to detect this nor resend them.
        /// </summary>
        /// <value><c>true</c> if messages are reliable.</value>
        /// <seealso cref="Ordered"/>
        public bool Reliable { get; }

        /// <summary>
        /// The channel connection state represents the connection status.
        /// Changes to this state are notified via the <see cref="StateChanged"/> event.
        /// </summary>
        /// <remarks>
        /// The code handling this event should unwind the stack before
        /// using any other MR-WebRTC APIs; re-entrancy is not supported.
        /// </remarks>
        /// <value>The channel connection state.</value>
        /// <seealso cref="StateChanged"/>
        public ChannelState State { get; internal set; }

        /// <summary>
        /// Event triggered when the data channel state changes.
        /// The new state is available in <see cref="State"/>.
        /// </summary>
        /// <remarks>
        /// The code handling this event should unwind the stack before
        /// using any other MR-WebRTC APIs; re-entrancy is not supported.
        /// </remarks>
        /// <seealso cref="State"/>
        public event Action StateChanged;

        /// <summary>
        /// Event triggered when the data channel buffering changes. Users should monitor this to ensure
        /// calls to <see cref="SendMessage(byte[])"/> do not fail. Internally the data channel contains
        /// a buffer of messages to send that could not be sent immediately, for example due to
        /// congestion control. Once this buffer is full, any further call to <see cref="SendMessage(byte[])"/>
        /// will fail until some messages are processed and removed to make space.
        /// </summary>
        /// <remarks>
        /// The code handling this event should unwind the stack before
        /// using any other MR-WebRTC APIs; re-entrancy is not supported.
        /// </remarks>
        /// <seealso cref="SendMessage(byte[])"/>.
        public event BufferingChangedDelegate BufferingChanged;

        /// <summary>
        /// Event triggered when a message is received through the data channel.
        /// </summary>
        /// <seealso cref="SendMessage(byte[])"/>
        public event Action<byte[]> MessageReceived;

        /// <summary>
        /// Event triggered when a message is received through the data channel,
        /// includes information if the message is binary or text.
        /// </summary>
        /// <seealso cref="SendMessage(byte[])"/>
        public event Action<MessageKind, byte[]> MessageReceivedEx;

        /// <summary>
        /// Event fires when a message is received through the data channel.
        /// </summary>
        /// <seealso cref="SendMessage(IntPtr,ulong)"/>
        public event Action<IntPtr, ulong> MessageReceivedUnsafe;

        /// <summary>
        /// Reference (GC handle) keeping the internal delegates alive while they are registered
        /// as callbacks with the native code.
        /// </summary>
        /// <seealso cref="Utils.MakeWrapperRef(object)"/>
        private IntPtr _argsRef;

        /// <summary>
        /// Handle to the native DataChannel object.
        /// </summary>
        /// <remarks>
        /// In native land this is a <code>mrsDataChannelHandle</code>.
        /// </remarks>
        internal IntPtr _nativeHandle = IntPtr.Zero;

        internal DataChannel(IntPtr nativeHandle, PeerConnection peer, IntPtr argsRef, int id, string label, bool ordered, bool reliable)
        {
            Debug.Assert(nativeHandle != IntPtr.Zero);
            _nativeHandle = nativeHandle;
            _argsRef = argsRef;
            PeerConnection = peer;
            ID = id;
            Label = label;
            Ordered = ordered;
            Reliable = reliable;
            State = ChannelState.Connecting; // see PeerConnection.AddDataChannelImpl()
        }

        /// <summary>
        /// Dispose of the native data channel. Invoked by its owner (<see cref="PeerConnection"/>).
        /// </summary>
        internal void DestroyNative()
        {
            _nativeHandle = IntPtr.Zero;
            State = ChannelState.Closed;
            Utils.ReleaseWrapperRef(_argsRef);
            _argsRef = IntPtr.Zero;
        }

        /// <summary>
        /// Send a message through the data channel. If the message cannot be sent, for example because of congestion
        /// control, it is buffered internally. If this buffer gets full, an exception is thrown and this call is aborted.
        /// The internal buffering is monitored via the <see cref="BufferingChanged"/> event.
        /// </summary>
        /// <param name="message">The message to send to the remote peer.</param>
        /// <exception xref="System.InvalidOperationException">The native data channel is not initialized.</exception>
        /// <exception xref="System.Exception">The internal buffer is full.</exception>
        /// <exception cref="DataChannelNotOpenException">The data channel is not open yet.</exception>
        /// <seealso cref="PeerConnection.InitializeAsync"/>
        /// <seealso cref="BufferingChanged"/>
        public void SendMessage(byte[] message)
        {
            MainEventSource.Log.DataChannelSendMessage(ID, message.Length);
            // Check channel state before doing a P/Invoke call which would anyway fail
            if (State != ChannelState.Open)
            {
                throw new DataChannelNotOpenException();
            }
            uint res = DataChannelInterop.DataChannel_SendMessage(_nativeHandle, message, (ulong)message.LongLength);
            Utils.ThrowOnErrorCode(res);
        }

        /// <summary>
        /// Send a message through the data channel. If the message cannot be sent, for example because of congestion
        /// control, it is buffered internally. If this buffer gets full, an exception is thrown and this call is aborted.
        /// The internal buffering is monitored via the <see cref="BufferingChanged"/> event.
        /// </summary>
        /// <param name="message">The message to send to the remote peer.</param>
        /// <param name="size">The size of the message to send in octects.</param>
        /// <exception xref="InvalidOperationException">The native data channel is not initialized.</exception>
        /// <exception xref="Exception">The internal buffer is full.</exception>
        /// <seealso cref="PeerConnection.InitializeAsync"/>
        /// <seealso cref="BufferingChanged"/>
        public void SendMessage(IntPtr message, ulong size)
        {
            MainEventSource.Log.DataChannelSendMessage(ID, (int)size);
            uint res = DataChannelInterop.DataChannel_SendMessage(_nativeHandle, message, size);
            Utils.ThrowOnErrorCode(res);
        }

        /// <summary>
        /// Send a message through the data channel with the specified kind. If the message cannot be sent, for example because of congestion
        /// control, it is buffered internally. If this buffer gets full, an exception is thrown and this call is aborted.
        /// The internal buffering is monitored via the <see cref="BufferingChanged"/> event.
        /// </summary>
        /// <param name="messageKind">The kind of message to send to the remote peer.</param>
        /// <param name="message">The message to send to the remote peer.</param>
        /// <param name="size">The size of the message to send in octects.</param>
        /// <exception xref="System.InvalidOperationException">The native data channel is not initialized.</exception>
        /// <exception xref="System.Exception">The internal buffer is full.</exception>
        /// <exception cref="DataChannelNotOpenException">The data channel is not open yet.</exception>
        /// <seealso cref="PeerConnection.InitializeAsync"/>
        /// <seealso cref="BufferingChanged"/>
        public void SendMessageEx(MessageKind messageKind, IntPtr message, ulong size)
        {
            MainEventSource.Log.DataChannelSendMessage(ID, (int)size);
            // Check channel state before doing a P/Invoke call which would anyway fail
            if (State != ChannelState.Open)
            {
                throw new DataChannelNotOpenException();
            }

            uint res = DataChannelInterop.DataChannel_SendMessageEx(_nativeHandle, messageKind, message, size);
            Utils.ThrowOnErrorCode(res);
        }

        internal void OnMessageReceived(IntPtr data, ulong size)
        {
            MainEventSource.Log.DataChannelMessageReceived(ID, (int)size);

            //< TODO - .NET Standard 2.1
            //https://docs.microsoft.com/en-us/dotnet/api/system.readonlyspan-1.-ctor?view=netstandard-2.1#System_ReadOnlySpan_1__ctor_System_Void__System_Int32_
            //var span = new ReadOnlySpan<byte>(data, size);
            //MessageReceived?.Invoke(span);

            var callback = MessageReceived;
            if (callback != null)
            {
                byte[] msg = new byte[size];
                unsafe
                {
                    fixed (void* ptr = msg)
                    {
                        Utils.MemCpy(ptr, (void*)data, size);
                    }
                }
                callback.Invoke(msg);
            }

            var unsafeCallback = MessageReceivedUnsafe;
            if (unsafeCallback != null)
            {
                unsafeCallback.Invoke(data, size);
            }
        }

        internal void OnMessageExReceived(int messageKind, IntPtr data, ulong size)
        {
            MainEventSource.Log.DataChannelMessageReceived(ID, (int)size);

            var callback = MessageReceivedEx;
            if (callback != null)
            {
                byte[] msg = new byte[size];
                unsafe
                {
                    fixed (void* ptr = msg)
                    {
                        Utils.MemCpy(ptr, (void*)data, size);
                    }
                }
                callback.Invoke((MessageKind)messageKind, msg);
            }
        }

        internal void OnBufferingChanged(ulong previous, ulong current, ulong limit)
        {
            MainEventSource.Log.DataChannelBufferingChanged(ID, previous, current, limit);
            BufferingChanged?.Invoke(previous, current, limit);
        }

        internal void OnStateChanged(int state, int id)
        {
            State = (ChannelState)state;
            MainEventSource.Log.DataChannelStateChanged(ID, State);
            StateChanged?.Invoke();
        }
    }
}
