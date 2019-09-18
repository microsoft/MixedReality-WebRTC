// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Runtime.InteropServices;

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
        /// Event fires when a message is received through the data channel.
        /// </summary>
        /// <seealso cref="SendMessage(byte[])"/>
        public event Action<byte[]> MessageReceived;

        /// <summary>
        /// GC handle keeping the internal delegates alive while they are registered
        /// as callbacks with the native code.
        /// </summary>
        private GCHandle _handle;

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
            PeerConnection.RemoveDataChannel(this);
            State = ChannelState.Closed;
            _handle.Free();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Send a message through the data channel.
        /// </summary>
        /// <param name="message">The message to send to the remote peer.</param>
        /// <exception xref="InvalidOperationException">The peer connection is not initialized.</exception>
        /// <seealso cref="PeerConnection.InitializeAsync"/>
        /// <seealso cref="PeerConnection.Initialized"/>
        public void SendMessage(byte[] message)
        {
            PeerConnection.SendDataChannelMessage(ID, message);
        }

        internal void OnMessageReceived(IntPtr data, ulong size)
        {
            //< TODO - .NET Standard 2.1
            //https://docs.microsoft.com/en-us/dotnet/api/system.readonlyspan-1.-ctor?view=netstandard-2.1#System_ReadOnlySpan_1__ctor_System_Void__System_Int32_
            //var span = new ReadOnlySpan<byte>(data, size);
            //MessageReceived?.Invoke(span);

            if (MessageReceived != null)
            {
                byte[] msg = new byte[size];
                unsafe
                {
                    fixed (void* ptr = msg)
                    {
                        PeerConnection.MemCpy(ptr, (void*)data, size);
                    }
                }
                MessageReceived.Invoke(msg);
            }
        }

        internal void OnBufferingChanged(ulong previous, ulong current, ulong limit)
        {

        }

        internal void OnStateChanged(int state, int id)
        {
            State = (ChannelState)state;
            StateChanged?.Invoke();
        }
    }
}
