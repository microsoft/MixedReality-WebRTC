// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.WebRTC.Signaling
{
    public class SignalingSession : IDisposable
    {
        public ISignalingService Service { get; }
        public PeerConnection LocalConnection { get; }
        public string RemoteEndPoint { get; private set; }

        private readonly string _sessionId;


        /// <summary>
        /// Use the service to connect a local PeerConnection to a remote endpoint
        /// SDP sent from the PeerConnection will be forwarded to the remote endpoint and viceversa.
        /// </summary>
        public static SignalingSession Connect(ISignalingService service, PeerConnection pc, string remoteEndPoint)
        {
            string sessionId = Guid.NewGuid().ToString();
            return new SignalingSession(sessionId, remoteEndPoint, service, pc);
        }

        // Publish the peer connection on the service and negotiate with the first endpoint that sends an offer.
        public static Task<SignalingSession> ListenForOfferAsync(ISignalingService service, PeerConnection pc)
        {
            var res = new TaskCompletionSource<SignalingSession>();

            Action<string, SignalingMessage> handler = null;
            handler =
                (senderId, message) =>
                {
                    if (message.Type == SdpType.SdpOffer)
                    {
                        service.MessageReceived -= handler;
                        pc.SetRemoteDescription("offer", message.Payload);
                        res.SetResult(new SignalingSession(message.SessionId, senderId, service, pc));
                    }
                };
            service.MessageReceived += handler;
            return res.Task;
        }

        public void Dispose()
        {
            // TODO remove the handlers
            throw new NotImplementedException();
        }

        private SignalingSession(string sessionId, string endPointId, ISignalingService service, PeerConnection localConnection)
        {
            _sessionId = sessionId;
            Service = service;
            LocalConnection = localConnection;

            // TODO store handlers to remove them later.

            localConnection.LocalSdpReadytoSend +=
                (string type, string sdp) =>
                {
                    service.SendToRemoteEndpoint(endPointId,
                        new SignalingMessage
                        {
                            SessionId = sessionId,
                            Type = FromSMTString(type),
                            Payload = sdp
                        });
                };
            localConnection.IceCandidateReadytoSend +=
                (string candidate, int sdpMlineindex, string sdpMid) =>
                {
                    string payload = candidate + '|' + sdpMlineindex + '|' + sdpMid;
                    service.SendToRemoteEndpoint(endPointId,
                        new SignalingMessage
                        {
                            SessionId = sessionId,
                            Type = SdpType.IceCandidate,
                            Payload = payload
                        });
                };
            service.MessageReceived +=
                (senderId, message) =>
                {
                    if (senderId == endPointId && message.SessionId == sessionId)
                    {
                        if (message.Type == SdpType.SdpOffer)
                        {
                            localConnection.SetRemoteDescription("offer", message.Payload);
                        }
                        else if (message.Type == SdpType.SdpAnswer)
                        {
                            localConnection.SetRemoteDescription("answer", message.Payload);
                        }
                        else if (message.Type == SdpType.IceCandidate)
                        {
                            var parts = message.Payload.Split('|');
                            localConnection.AddIceCandidate(parts[2], int.Parse(parts[1]), parts[0]);
                        }
                    }
                };

        }

        internal static SdpType FromSMTString(string s)
        {
            switch (s)
            {
                case "offer": return SdpType.SdpOffer;
                case "answer": return SdpType.SdpAnswer;
                default: throw new Exception();
            }
        }
    }

    internal static class Examples
    {
        internal static void Example()
        {
            // Wait for any peer to send an offer to this process.
            using(PeerConnection pc1 = MakeConnection())
            {
                ISignalingService svc1 = MakeService("EndPoint 1");

                var sessionTask = SignalingSession.ListenForOfferAsync(svc1, pc1);
                using (var session = sessionTask.Result)
                {
                    Console.WriteLine($"Session established with {session.RemoteEndPoint}");
                    DoWork(pc1);
                }
            }

            // Connect to EndPoint 1
            using (PeerConnection pc2 = MakeConnection())
            {
                ISignalingService svc2 = MakeService("EndPoint 2");
                using (var session = SignalingSession.Connect(svc2, pc2, "EndPoint 1"))
                {
                    DoWork(pc2);
                }
            }

            // Choose an endpoint to connect to among the ones available
            using (PeerConnection pc3 = MakeConnection())
            {
                ISignalingService svc3 = MakeService("EndPoint 3");
                string selectedEndPoint = null;
                while (selectedEndPoint == null)
                {
                    selectedEndPoint = MakeTheUserChoose(svc3.RemoteEndPoints);
                }

                using (var session = SignalingSession.Connect(svc3, pc3, selectedEndPoint))
                {
                    DoWork(pc3);
                }
            }
        }

        // Placeholders
        internal static PeerConnection MakeConnection() { return null; }
        internal static ISignalingService MakeService(string localEndPoint) { return null; }
        internal static void DoWork(PeerConnection pc) { }
        internal static string MakeTheUserChoose(IEnumerable<string> endpoints) { return null; }
    }
}
