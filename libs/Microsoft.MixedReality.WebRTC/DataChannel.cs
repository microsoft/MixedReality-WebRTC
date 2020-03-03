// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;
using Microsoft.MixedReality.WebRTC.Interop;
using Microsoft.MixedReality.WebRTC.Tracing;

namespace Microsoft.MixedReality.WebRTC
{
    /// <summary>
    /// Encapsulates a data channel of a peer connection.
    /// 
    /// A data channel is a pipe allowing to send and receive arbitrary data to the
    /// remote peer. Data channels are based on DTLS-SRTP, and are therefore secure (encrypted).
    /// Exact security guarantees are provided by the underlying WebRTC core implementation
    /// and the WebRTC standard itself.
    /// 
    /// https://tools.ietf.org/wg/rtcweb/
    /// 
    /// An instance of <see cref="DataChannel"/> is created by calling <see cref="PeerConnection.AddDataChannelAsync(string,bool,bool)"/>
    /// or one of its variants. <see cref="DataChannel"/> cannot be instantiated directly.
    /// </summary>
    public class DataChannel : IDisposable
    {
        /// <summary>
        /// Connecting state of a data channel, when adding it to a peer connection
        /// or removing it from a peer connection.
        /// </summary>
        public enum ChannelState
        {
            /// <summary>
            /// The data channel has just been created, and negotiating is underway to establish
            /// a track between the peers.
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
        /// Delegate for the <see cref="BufferingChanged"/> event.
        /// </summary>
        /// <param name="previous">Previous buffering size, in bytes.</param>
        /// <param name="current">New buffering size, in bytes.</param>
        /// <param name="limit">Maximum buffering size, in bytes.</param>
        public delegate void BufferingChangedDelegate(ulong previous, ulong current, ulong limit);

        /// <value>The <see cref="PeerConnection"/> object this data channel was created from.</value>
        public PeerConnection PeerConnection { get; }

        /// <value>The unique identifier of the data channel in the current connection.</value>
        public int ID { get; }

        /// <value>The data channel name in the current connection.</value>
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
        /// The channel connection state represents the connection status when creating or closing the
        /// data channel. Changes to this state are notified via the <see cref="StateChanged"/> event.
        /// </summary>
        /// <value>The channel connection state.</value>
        /// <seealso cref="StateChanged"/>
        public ChannelState State { get; private set; }

        /// <summary>
        /// Event fired when the data channel state changes, as reported by <see cref="State"/>.
        /// </summary>
        /// <seealso cref="State"/>
        public event Action StateChanged;

        /// <summary>
        /// Event fired when the data channel buffering changes. Monitor this to ensure calls to
        /// <see cref="SendMessage(byte[])"/> do not fail. Internally the data channel contains
        /// a buffer of messages to send that could not be sent immediately, for example due to
        /// congestion control. Once this buffer is full, any further call to <see cref="SendMessage(byte[])"/>
        /// will fail until some mesages are processed and removed to make space.
        /// </summary>
        /// <seealso cref="SendMessage(byte[])"/>.
        public event BufferingChangedDelegate BufferingChanged;

        /// <summary>
        /// Event fires when a message is received through the data channel.
        /// </summary>
        /// <seealso cref="SendMessage(byte[])"/>
        public event Action<byte[]> MessageReceived;

        /// <summary>
        /// Event fires when a message is received through the data channel.
        /// </summary>
        /// <seealso cref="SendMessage(IntPtr,ulong)"/>
        public event Action<IntPtr, ulong> MessageReceivedUnsafe;

        /// <summary>
        /// GC handle keeping the internal delegates alive while they are registered
        /// as callbacks with the native code.
        /// </summary>
        private GCHandle _handle;

        /// <summary>
        /// Handle to the native object this wrapper is associated with.
        /// </summary>
        internal IntPtr _interopHandle = IntPtr.Zero;

        internal DataChannel(PeerConnection peer, GCHandle handle,
            int id, string label, bool ordered, bool reliable)
        {
            _handle = handle;
            PeerConnection = peer;
            ID = id;
            Label = label;
            Ordered = ordered;
            Reliable = reliable;
            State = ChannelState.Connecting; // see PeerConnection.AddDataChannelImpl()
        }

        /// <summary>
        /// Finalizer to ensure the data track is removed from the peer connection
        /// and the managed resources are cleaned-up.
        /// </summary>
        ~DataChannel()
        {
            Dispose();
        }

        /// <summary>
        /// Remove the data track from the peer connection and destroy it.
        /// </summary>
        public void Dispose()
        {
            State = ChannelState.Closing;
            PeerConnection.RemoveDataChannel(_interopHandle);
            _interopHandle = IntPtr.Zero;
            State = ChannelState.Closed;
            _handle.Free();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Send a message through the data channel. If the message cannot be sent, for example because of congestion
        /// control, it is buffered internally. If this buffer gets full, an exception is thrown and this call is aborted.
        /// The internal buffering is monitored via the <see cref="BufferingChanged"/> event.
        /// </summary>
        /// <param name="message">The message to send to the remote peer.</param>
        /// <exception xref="InvalidOperationException">The native data channel is not initialized.</exception>
        /// <exception xref="Exception">The internal buffer is full.</exception>
        /// <seealso cref="PeerConnection.InitializeAsync"/>
        /// <seealso cref="PeerConnection.Initialized"/>
        /// <seealso cref="BufferingChanged"/>
        public void SendMessage(byte[] message)
        {
            MainEventSource.Log.DataChannelSendMessage(ID, message.Length);
            uint res = DataChannelInterop.DataChannel_SendMessage(_interopHandle, message, (ulong)message.LongLength);
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
        /// <seealso cref="PeerConnection.Initialized"/>
        /// <seealso cref="BufferingChanged"/>
        public void SendMessage(IntPtr message, ulong size)
        {
            MainEventSource.Log.DataChannelSendMessage(ID, (int)size);
            uint res = DataChannelInterop.DataChannel_SendMessage(_interopHandle, message, size);
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
