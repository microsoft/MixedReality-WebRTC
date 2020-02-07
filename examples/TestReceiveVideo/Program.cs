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
using WebSocketSharp;
using WebSocketSharp.Net.WebSockets;
using WebSocketSharp.Server;

namespace TestNetCoreConsole
{
    public class SDPExchange : WebSocketBehavior
    {
        public event Action<WebSocketContext, string> MessageReceived;

        public SDPExchange()
        { }

        protected override void OnMessage(MessageEventArgs e)
        {
            MessageReceived(this.Context, e.Data);
        }
    }

    class Program
    {
        private const string WEBSOCKET_CERTIFICATE_PATH = "certs/localhost.pfx";
        private const int WEBSOCKET_PORT = 8081;

        static void Main()
        {
            try
            {
                Console.WriteLine("Starting...");

                // Start web socket.
                Console.WriteLine("Starting web socket server...");
                var webSocketServer = new WebSocketServer(IPAddress.Any, WEBSOCKET_PORT, true);
                webSocketServer.SslConfiguration.ServerCertificate = new System.Security.Cryptography.X509Certificates.X509Certificate2(WEBSOCKET_CERTIFICATE_PATH);
                webSocketServer.SslConfiguration.CheckCertificateRevocation = false;
                webSocketServer.AddWebSocketService<SDPExchange>("/", (sdpExchanger) =>
                {
                    sdpExchanger.MessageReceived += MessageReceived;
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

        private static async void MessageReceived(WebSocketContext context, string msg)
        {
            Console.WriteLine($"websocket recv: {msg}");

            // Set up the peer connection.
            var pc = new PeerConnection();

            var config = new PeerConnectionConfiguration();
            await pc.InitializeAsync(config);

            // Create a new form to display the video feed from the WebRTC peer.
            var form = new Form();
            form.AutoSize = true;
            form.BackgroundImageLayout = ImageLayout.Center;
            PictureBox picBox = null;

            pc.SetRemoteDescription("offer", msg);

            pc.LocalSdpReadytoSend += (string type, string sdp) =>
            {
                Console.WriteLine($"Local SDP ready {type}");

                // Send our SDP answer to the remote peer.
                context.WebSocket.Send(sdp);
            };

            if (pc.CreateAnswer())
            {
                Console.WriteLine("Peer connection answer successfully created.");
            }
            else
            {
                Console.WriteLine("Failed to create peer connection answer.");
                pc.Close();
            }

            pc.ARGBRemoteVideoFrameReady += (frame) =>
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
