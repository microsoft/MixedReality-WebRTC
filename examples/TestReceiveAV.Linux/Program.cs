using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.MixedReality.WebRTC;
using Newtonsoft.Json.Linq;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace TestReceiveAV.Linux
{
    public class WebRtcSession : WebSocketBehavior
    {
        public PeerConnection pc { get; private set; }

        public event Action<WebRtcSession, string> MessageReceived;

        public WebRtcSession()
        {
            pc = new PeerConnection();
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            MessageReceived(this, e.Data);
        }
    }

    class Program
    {
        private const int WEBSOCKET_PORT = 8081;

        static void Main()
        {
            try
            {
                NativeAssemblyResolver.RegisterResolver(typeof(Program).Assembly);

                var processStart = new ProcessStartInfo
                {
                    FileName = "pulseaudio",
                    Arguments = "-D --verbose --exit-idle-time=-1 --system --disallow-exit",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                var process = Process.Start(processStart);
                process.OutputDataReceived += (object sender, DataReceivedEventArgs e) =>
                {
                    Console.WriteLine(e.Data);
                };
                process.ErrorDataReceived += (object sender, DataReceivedEventArgs e) =>
                {
                    Console.WriteLine(e.Data);
                };

                // Start web socket server.
                Console.WriteLine("Starting web socket server...");
                var webSocketServer = new WebSocketServer(IPAddress.Any, WEBSOCKET_PORT, false);
                //webSocketServer.Log.Level = WebSocketSharp.LogLevel.Debug;
                webSocketServer.AddWebSocketService<WebRtcSession>("/", (session) =>
                {
                    session.MessageReceived += MessageReceived;
                });
                webSocketServer.Start();

                Console.WriteLine($"Waiting for browser web socket connection to {webSocketServer.Address}:{webSocketServer.Port}...");

                ManualResetEvent mre = new ManualResetEvent(false);
                mre.WaitOne();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        private static async void MessageReceived(WebRtcSession session, string msg)
        {
            Console.WriteLine($"web socket recv: {msg.Length} bytes");

            JObject jsonMsg = JObject.Parse(msg);

            if ((string)jsonMsg["type"] == "ice")
            {
                Console.WriteLine($"Adding remote ICE candidate {msg}.");

                while (!session.pc.Initialized)
                {
                    // This delay is needed due to an initialise bug in the Microsoft.MixedReality.WebRTC
                    // nuget packages up to version 0.2.3. On master awaiting pc.InitializeAsync does end 
                    // up with the pc object being ready.
                    Console.WriteLine("Sleeping for 1s while peer connection is initialising...");
                    await Task.Delay(1000);
                }

                session.pc.AddIceCandidate(new IceCandidate
                {
                    SdpMlineIndex = (int)jsonMsg["sdpMLineindex"],
                    SdpMid = (string)jsonMsg["sdpMid"],
                    Content = (string)jsonMsg["candidate"]
                });
            }
            else if ((string)jsonMsg["type"] == "sdp")
            {
                Console.WriteLine("Received remote peer SDP offer.");

                var config = new PeerConnectionConfiguration();

                session.pc.IceCandidateReadytoSend += (candidate) =>
                {
                    Console.WriteLine($"Sending ice candidate: {candidate.Content}");
                    JObject iceCandidate = new JObject {
                        { "type", "ice" },
                        { "candidate", candidate.Content },
                        { "sdpMLineindex", candidate.SdpMlineIndex },
                        { "sdpMid", candidate.SdpMid }
                    };
                    session.Context.WebSocket.Send(iceCandidate.ToString());
                };

                session.pc.IceStateChanged += (newState) =>
                {
                    Console.WriteLine($"ice connection state changed to {newState}.");
                };

                session.pc.LocalSdpReadytoSend += (sdp) =>
                {
                    Console.WriteLine($"SDP answer ready, sending to remote peer.");

                    // Send our SDP answer to the remote peer.
                    JObject sdpAnswer = new JObject {
                        { "type", "sdp" },
                        { "answer", sdp.Content }
                    };
                    session.Context.WebSocket.Send(sdpAnswer.ToString());
                };

                await session.pc.InitializeAsync(config).ContinueWith(async (t) =>
                {
                    await session.pc.SetRemoteDescriptionAsync(new SdpMessage
                    {
                        Type = SdpMessageType.Offer,
                        Content = (string)jsonMsg["offer"]
                    });

                    if (!session.pc.CreateAnswer())
                    {
                        Console.WriteLine("Failed to create peer connection answer, closing peer connection.");
                        session.pc.Close();
                        session.Context.WebSocket.Close();
                    }
                });

                session.pc.VideoTrackAdded += (track) =>
                {
                    track.Argb32VideoFrameReady += (frame) =>
                    {
                        Console.WriteLine("New video frame");
                    };
                };
            }
        }
    }
}
