//-----------------------------------------------------------------------------
// Filename: Program.cs
//
// Description: Demonstration of receiving and rendering video from a WebRTC
// enabled browser using https://github.com/microsoft/MixedReality-WebRTC 
// dotnet library.
//
// Author(s):
// Aaron Clauson (aaron@sipsorcery.com)
// 
// History:
// 07 Feb 2020	Aaron Clauson	Created, Dublin, Ireland.
//
// License: 
// Licensed under the MIT License.
//-----------------------------------------------------------------------------

using System;
using System.Drawing;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.MixedReality.WebRTC;
using Newtonsoft.Json.Linq;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace TestReceiveAV
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
        private const string WEBSOCKET_CERTIFICATE_PATH = "c:/temp/certs/localhost.pfx";
        private const int WEBSOCKET_PORT = 8081;

        static void Main()
        {
            try
            {
                // Start web socket server.
                Console.WriteLine("Starting web socket server...");
                var webSocketServer = new WebSocketServer(IPAddress.Any, WEBSOCKET_PORT, true);
                webSocketServer.SslConfiguration.ServerCertificate = new System.Security.Cryptography.X509Certificates.X509Certificate2(WEBSOCKET_CERTIFICATE_PATH);
                webSocketServer.SslConfiguration.CheckCertificateRevocation = false;
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

                session.pc.AddIceCandidate((string)jsonMsg["sdpMLineindex"], (int)jsonMsg["sdpMid"], (string)jsonMsg["candidate"]);
            }
            else if ((string)jsonMsg["type"] == "sdp")
            {
                Console.WriteLine("Received remote peer SDP offer.");

                var config = new PeerConnectionConfiguration();

                session.pc.IceCandidateReadytoSend += (string candidate, int sdpMlineindex, string sdpMid) =>
                {
                    Console.WriteLine($"Sending ice candidate: {candidate}");
                    JObject iceCandidate = new JObject {
                        { "type", "ice" },
                        { "candidate", candidate },
                        { "sdpMLineindex", sdpMlineindex },
                        { "sdpMid", sdpMid}
                    };
                    session.Context.WebSocket.Send(iceCandidate.ToString());
                };

                session.pc.IceStateChanged += (newState) =>
                {
                    Console.WriteLine($"ice connection state changed to {newState}.");
                };

                session.pc.LocalSdpReadytoSend += (string type, string sdp) =>
                {
                    Console.WriteLine($"SDP answer ready, sending to remote peer.");

                    // Send our SDP answer to the remote peer.
                    JObject sdpAnswer = new JObject {
                        { "type", "sdp" },
                        { "answer", sdp }
                    };
                    session.Context.WebSocket.Send(sdpAnswer.ToString());
                };

                await session.pc.InitializeAsync(config).ContinueWith((t) =>
                {
                    session.pc.SetRemoteDescription("offer", (string)jsonMsg["offer"]);

                    if (!session.pc.CreateAnswer())
                    {
                        Console.WriteLine("Failed to create peer connection answer, closing peer connection.");
                        session.pc.Close();
                        session.Context.WebSocket.Close();
                    }
                });

                // Create a new form to display the video feed from the WebRTC peer.
                var form = new Form();
                form.AutoSize = true;
                form.BackgroundImageLayout = ImageLayout.Center;
                PictureBox picBox = null;

                form.HandleDestroyed += (object sender, EventArgs e) =>
                {
                    Console.WriteLine("Form closed, closing peer connection.");
                    session.pc.Close();
                    session.Context.WebSocket.Close();
                };

                session.pc.ARGBRemoteVideoFrameReady += (frame) =>
                {
                    var width = frame.width;
                    var height = frame.height;
                    var stride = frame.stride;
                    var data = frame.data;

                    if (picBox == null)
                    {
                        picBox = new PictureBox
                        {
                            Size = new Size((int)width, (int)height),
                            Location = new Point(0, 0),
                            Visible = true
                        };
                        form.BeginInvoke(new Action(() => { form.Controls.Add(picBox); }));
                    }

                    form.BeginInvoke(new Action(() =>
                    {
                        System.Drawing.Bitmap bmpImage = new System.Drawing.Bitmap((int)width, (int)height, (int)stride, System.Drawing.Imaging.PixelFormat.Format32bppArgb, data);
                        picBox.Image = bmpImage;
                    }));
                };

                Application.EnableVisualStyles();
                Application.Run(form);
            }
        }
    }
}
