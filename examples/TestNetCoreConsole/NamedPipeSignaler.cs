// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.MixedReality.WebRTC;

namespace NamedPipeSignaler
{
    /// <summary>
    /// Simple WebRTC signaler based on named pipes.
    /// Allows automatic remote peer discovery on localhost, provided that the pipe
    /// name is unique for each peer pair.
    /// Remote connection (non-localhost) is not supported.
    /// </summary>
    /// <remarks>
    /// This is a simple implementation for debugging and testing; this is not a production-ready solution.
    /// </remarks>
    public class NamedPipeSignaler : IDisposable
    {
        /// <summary>
        /// Peer connection this signaler is associated with.
        /// </summary>
        public PeerConnection PeerConnection { get; }

        /// <summary>
        /// Is this signaler acting as a client for the forward pipe (the pipe whose name is specified
        /// in <see cref="NamedPipeSignaler(PeerConnection, string)"/>)?
        /// </summary>
        /// <remarks>
        /// The signaler always has a reverse pipe with reversed roles, to allow bidirectional message
        /// transport. This is mainly used for debugging and/or to disambiguate two peer connections when
        /// automatically connecting to each other, since <see cref="PeerConnection.CreateOffer()"/> must
        /// be called by a single peer only.
        /// </remarks>
        public bool IsClient { get; }

        /// <summary>
        /// Event invoked when an ICE candidate message has been received from the remote peer's signaler.
        /// </summary>
        public PeerConnection.IceCandidateReadytoSendDelegate IceCandidateReceived;

        /// <summary>
        /// Event invoked when an SDP offer or answer message has been received from the remote peer's signaler.
        /// </summary>
        public PeerConnection.LocalSdpReadyToSendDelegate SdpMessageReceived;

        /// <summary>
        /// Client pipe for sending data. This is connected to the remote signaler's server pipe. 
        /// </summary>
        private NamedPipeClientStream _clientPipe = null;

        /// <summary>
        /// Server pipe for receiving data. This is connected to the remote signaler's client pipe. 
        /// </summary>
        private NamedPipeServerStream _serverPipe = null;

        /// <summary>
        /// Base pipe name for the forward pipe. The reverse pipe has an extra "_r" suffix.
        /// </summary>
        private string _basePipeName;

        /// <summary>
        /// Write stream wrapping the client pipe, for writing outgoing messages.
        /// </summary>
        private StreamWriter _sendStream = null;

        /// <summary>
        /// Read stream wrapping the server pipe, for reading incoming messages.
        /// </summary>
        private StreamReader _recvStream = null;

        /// <summary>
        /// The server name is always localhost; remote connection is not supported.
        /// Remote connection would require 2 server names, one for each peer, since
        /// they both have a pipe server and a pipe client. Or refactor this pattern.
        /// </summary>
        private readonly string _serverName = "."; // localhost

        /// <summary>
        /// Thread-safe collection of outgoing message, with automatic blocking read.
        /// </summary>
        private readonly BufferBlock<string> _outgoingMessages = new BufferBlock<string>();

        /// <summary>
        /// Construct a signaler instance for the given peer connection, with an explicit pipe name.
        /// </summary>
        /// <param name="peerConnection">The peer connection to act as a signaler for. This instance
        /// will subscribe to the <see cref="PeerConnection.LocalSdpReadytoSend"/> and
        /// <see cref="PeerConnection.IceCandidateReadytoSend"/> events to manage outgoing signaling messages.</param>
        /// <param name="pipeName">The unique pipe name shared by the local and remote peers.</param>
        public NamedPipeSignaler(PeerConnection peerConnection, string pipeName)
        {
            PeerConnection = peerConnection;
            _basePipeName = pipeName;

            PeerConnection.DataChannelAdded += PeerConnection_DataChannelAdded;
            PeerConnection.DataChannelRemoved += PeerConnection_DataChannelRemoved;
            PeerConnection.LocalSdpReadytoSend += PeerConnection_LocalSdpReadytoSend;
            PeerConnection.IceCandidateReadytoSend += PeerConnection_IceCandidateReadytoSend;

            // Try to create the server, if not already existing
            IsClient = false;
            try
            {
                _serverPipe = new NamedPipeServerStream(pipeName, PipeDirection.In);
                Console.WriteLine("Created pipe server; acting as server.");
            }
            catch (IOException)
            {
                Console.WriteLine("Pipe server already exists; acting as client.");
                IsClient = true;
            }
        }

        /// <summary>
        /// Start the signaler background tasks and connect to the remote signaler.
        /// </summary>
        /// <returns>Asynchronous task completed once the local and remote signalers
        /// are connected with each other, and the background reading and writing tasks
        /// are running and ready to process incoming and outgoing messages.</returns>
        public async Task StartAsync()
        {
            if (IsClient)
            {
                // Connect to the remote peer
                Console.Write("Attempting to connect to the remote peer...");
                _clientPipe = new NamedPipeClientStream(_serverName, _basePipeName, PipeDirection.Out);
                await _clientPipe.ConnectAsync();
                Console.WriteLine("Connected to the remote peer.");
                Console.WriteLine($"There are currently {_clientPipe.NumberOfServerInstances} pipe server instances open.");

                // Create the reverse pipe and wait for the remote peer to connect
                _serverPipe = new NamedPipeServerStream(_basePipeName + "_r", PipeDirection.In);
                Console.Write("Waiting for the remote peer to connect back...");
                await _serverPipe.WaitForConnectionAsync();
            }
            else
            {
                // Wait for the remote peer to connect
                Console.Write("Waiting for the remote peer to connect...");
                await _serverPipe.WaitForConnectionAsync();
                Console.WriteLine("Remote peer connected.");

                // Connect back to it on the reverse pipe
                Console.Write("Attempting to connect back to the remote peer...");
                _clientPipe = new NamedPipeClientStream(_serverName, _basePipeName + "_r", PipeDirection.Out);
                await _clientPipe.ConnectAsync();
            }
            Console.WriteLine("Signaler connection established.");

            // Start signaling
            _sendStream = new StreamWriter(_clientPipe);
            _sendStream.AutoFlush = true;
            _recvStream = new StreamReader(_serverPipe);
            _ = Task.Factory.StartNew(ProcessIncomingMessages, TaskCreationOptions.LongRunning);
            _ = Task.Run(() => WriteOutgoingMessagesAsync(CancellationToken.None));
        }

        /// <summary>
        /// Entry point for the reading task which read incoming messages from the
        /// receiving pipe and dispatch them through events to the WebRTC peer.
        /// </summary>
        private void ProcessIncomingMessages()
        {
            // ReadLine() will block while waiting for a new line
            string line;
            while ((line = _recvStream.ReadLine()) != null)
            {
                Console.WriteLine($"[<-] {line}");
                if (line == "ice")
                {
                    string sdpMid = _recvStream.ReadLine();
                    int sdpMlineindex = int.Parse(_recvStream.ReadLine());

                    // The ICE candidate is a multi-line field, ends with an empty line
                    string candidate = "";
                    while ((line = _recvStream.ReadLine()) != null)
                    {
                        if (line.Length == 0)
                        {
                            break;
                        }
                        candidate += line;
                        candidate += "\n";
                    }

                    Console.WriteLine($"[<-] ICE candidate: {sdpMid} {sdpMlineindex} {candidate}");
                    var iceCandidate = new IceCandidate
                    {
                        SdpMid = sdpMid,
                        SdpMlineIndex = sdpMlineindex,
                        Content = candidate
                    };
                    IceCandidateReceived?.Invoke(iceCandidate);
                }
                else if (line == "sdp")
                {
                    string type = _recvStream.ReadLine();

                    // The SDP message content is a multi-line field, ends with an empty line
                    string sdp = "";
                    while ((line = _recvStream.ReadLine()) != null)
                    {
                        if (line.Length == 0)
                        {
                            break;
                        }
                        sdp += line;
                        sdp += "\n";
                    }

                    Console.WriteLine($"[<-] SDP message: {type} {sdp}");
                    var message = new SdpMessage { Type = SdpMessage.StringToType(type), Content = sdp };
                    SdpMessageReceived?.Invoke(message);
                }
            }
            Console.WriteLine("Finished processing messages");
        }

        /// <summary>
        /// Entry point for the writing task dequeuing outgoing messages and
        /// writing them to the sending pipe.
        /// </summary>
        private async Task WriteOutgoingMessagesAsync(CancellationToken ct)
        {
            // OutputAvailableAsync() will wait until messages are available or
            // until Complete() is called from Dispose().
            while (await _outgoingMessages.OutputAvailableAsync(ct))
            {
                var msg = await _outgoingMessages.ReceiveAsync(ct);

                // Write the message and wait for the stream to be ready again
                // for the next Write() call.
                _sendStream.Write(msg);
            }
        }

        /// <summary>
        /// Send a message to the remote signaler.
        /// </summary>
        /// <param name="msg">The message to send.</param>
        private void SendMessage(string msg)
        {
            try
            {
                // Enqueue the message and immediately return, to avoid blocking the
                // WebRTC signaler thread which is typically invoking this method through
                // the PeerConnection signaling callbacks.
                Console.WriteLine($"[->] {msg}");
                _outgoingMessages.Post(msg);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Exception: {e.Message}");
                Environment.Exit(-1);
            }
        }

        private void PeerConnection_IceCandidateReadytoSend(IceCandidate candidate)
        {
            // See ProcessIncomingMessages() for the message format
            SendMessage($"ice\n{candidate.SdpMid}\n{candidate.SdpMlineIndex}\n{candidate.Content}\n\n");
        }

        private void PeerConnection_LocalSdpReadytoSend(SdpMessage message)
        {
            // See ProcessIncomingMessages() for the message format
            string typeStr = SdpMessage.TypeToString(message.Type);
            SendMessage($"sdp\n{typeStr}\n{message.Content}\n\n");
        }

        private void PeerConnection_DataChannelAdded(DataChannel channel)
        {
            Console.WriteLine($"Event: DataChannel Added {channel.Label}");
            channel.StateChanged += () => { Console.WriteLine($"DataChannel '{channel.Label}':  StateChanged '{channel.State}'"); };
        }

        private void PeerConnection_DataChannelRemoved(DataChannel channel)
        {
            Console.WriteLine($"Event: DataChannel Removed {channel.Label}");
        }

        public void Dispose()
        {
            PeerConnection.LocalSdpReadytoSend -= PeerConnection_LocalSdpReadytoSend;
            PeerConnection.DataChannelAdded -= PeerConnection_DataChannelAdded;
            PeerConnection.DataChannelRemoved -= PeerConnection_DataChannelRemoved;
            PeerConnection.IceCandidateReadytoSend -= PeerConnection_IceCandidateReadytoSend;

            _recvStream.Close();
            _outgoingMessages.Complete();

            // Optional, wait for all messages to be processed.
            _outgoingMessages.Completion.Wait();
            
            _clientPipe?.Dispose();
            _serverPipe?.Dispose();
            _sendStream?.Dispose();
            _recvStream?.Dispose();
        }
    }
}
