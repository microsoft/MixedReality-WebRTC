using System;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;
using Microsoft.MixedReality.WebRTC;

namespace NamedPipeSignaler
{
    /// <summary>
    /// Simple WebRTC signaler based on named pipes.
    /// </summary>
    public class NamedPipeSignaler
    {
        public PeerConnection PeerConnection { get; }
        public bool IsClient { get; }

        public Action<string, int, string> IceCandidateReceived;
        public Action<string, string> SdpMessageReceived;

        private NamedPipeClientStream _clientPipe = null;
        private NamedPipeServerStream _serverPipe = null;
        private string _basePipeName;
        private string _serverName;
        private StreamWriter _sendStream = null;
        private StreamReader _recvStream = null;

        public NamedPipeSignaler(PeerConnection peerConnection, string pipeName, string serverName = ".")
        {
            PeerConnection = peerConnection;
            _basePipeName = pipeName;
            _serverName = serverName;

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
            _recvStream = new StreamReader(_serverPipe);
            PeerConnection.LocalSdpReadytoSend += PeerConnection_LocalSdpReadytoSend;
            PeerConnection.IceCandidateReadytoSend += PeerConnection_IceCandidateReadytoSend;
            _ = Task.Run(ProcessIncomingMessages);
        }

        private void ProcessIncomingMessages()
        {
            string line;
            while ((line = _recvStream.ReadLine()) != null)
            {
                Console.WriteLine($"[remote] {line}");
                if (line == "ice")
                {
                    string sdpMid = _recvStream.ReadLine();
                    int sdpMlineindex = int.Parse(_recvStream.ReadLine());
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
                    Console.WriteLine($"[remote] ICE candidate: {sdpMid} {sdpMlineindex} {candidate}");
                    IceCandidateReceived?.Invoke(sdpMid, sdpMlineindex, candidate);
                }
                else if (line == "sdp")
                {
                    string type = _recvStream.ReadLine();
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
                    Console.WriteLine($"[remote] SDP message: {type} {sdp}");
                    SdpMessageReceived?.Invoke(type, sdp);
                }
            }
            Console.WriteLine("Finished processing messages");
        }

        private void SendMessage(string msg)
        {
            _sendStream.Write(msg);
        }

        private void PeerConnection_IceCandidateReadytoSend(string candidate, int sdpMlineindex, string sdpMid)
        {
            SendMessage($"ice\n{sdpMid}\n{sdpMlineindex}\n{candidate}\n\n");
        }

        private void PeerConnection_LocalSdpReadytoSend(string type, string sdp)
        {
            SendMessage($"sdp\n{type}\n{sdp}\n\n");
        }
    }
}
