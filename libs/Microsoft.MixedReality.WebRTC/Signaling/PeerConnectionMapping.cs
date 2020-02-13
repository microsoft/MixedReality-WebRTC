// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.WebRTC.Signaling
{
    /// <summary>
    /// Maps a local <see cref="PeerConnection"/> to a remote signaling endpoint.
    /// SDP sent from the PeerConnection will be forwarded to the remote endpoint and viceversa
    /// until this is disposed.
    /// </summary>
    public class PeerConnectionMapping : IDisposable
    {
        public PeerConnection LocalPeerConnection { get; }
        public string RemoteEndPoint => _connection.RemoteEndPoint;

        private ISignalingConnection _connection;

        /// <summary>
        /// ID assigned on creation. Messages going back and forth are marked with this ID.
        /// Messages that do not have this ID are ignored by this connection.
        /// </summary>
        public string SessionId { get; }


        /// <summary>
        /// Use the service to connect a local PeerConnection to a remote endpoint
        /// </summary>
        public static PeerConnectionMapping Connect(ISignalingEndPoint service, PeerConnection pc, string remoteEndPoint)
        {
            string sessionId = Guid.NewGuid().ToString();
            return new PeerConnectionMapping(sessionId, remoteEndPoint, service, pc);
        }

        /// <summary>
        /// Publish the peer connection on the service and negotiate with the first endpoint that sends an offer.
        /// </summary>
        public static Task<PeerConnectionMapping> ListenForOfferAsync(ISignalingEndPoint service, PeerConnection pc)
        {
            var res = new TaskCompletionSource<PeerConnectionMapping>();

            Action<string, SignalingMessage> handler = null;
            handler =
                (senderId, message) =>
                {
                    if (message.Type == SdpType.SdpOffer)
                    {
                        service.MessageReceived -= handler;
                        pc.SetRemoteDescription("offer", message.Payload);
                        res.SetResult(new PeerConnectionMapping(message.SessionId, senderId, service, pc));
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

        private PeerConnectionMapping(string sessionId, string endPointId, ISignalingEndPoint service, PeerConnection localConnection)
        {
            SessionId = sessionId;
            LocalPeerConnection = localConnection;

            _connection = service.Connect(endPointId);

            // TODO store handlers to remove them later.

            localConnection.LocalSdpReadytoSend +=
                (string type, string sdp) =>
                {
                    _connection.SendToRemoteEndpoint(
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
                    _connection.SendToRemoteEndpoint(
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
                ISignalingEndPoint svc1 = MakeService("EndPoint 1");

                var sessionTask = PeerConnectionMapping.ListenForOfferAsync(svc1, pc1);
                using (var session = sessionTask.Result)
                {
                    Console.WriteLine($"Session established with {session.RemoteEndPoint}");
                    DoWork(pc1);
                }
            }

            // Connect to EndPoint 1
            using (PeerConnection pc2 = MakeConnection())
            {
                ISignalingEndPoint svc2 = MakeService("EndPoint 2");
                using (var session = PeerConnectionMapping.Connect(svc2, pc2, "EndPoint 1"))
                {
                    DoWork(pc2);
                }
            }

            // Choose an endpoint to connect to among the ones available
            using (PeerConnection pc3 = MakeConnection())
            {
                ISignalingEndPoint svc3 = MakeService("EndPoint 3");
                string selectedEndPoint = null;
                while (selectedEndPoint == null)
                {
                    selectedEndPoint = MakeTheUserChoose(svc3.RemoteEndPoints);
                }

                using (var session = PeerConnectionMapping.Connect(svc3, pc3, selectedEndPoint))
                {
                    DoWork(pc3);
                }
            }
        }

        // Placeholders
        internal static PeerConnection MakeConnection() { return null; }
        internal static ISignalingEndPoint MakeService(string localEndPoint) { return null; }
        internal static void DoWork(PeerConnection pc) { }
        internal static string MakeTheUserChoose(IEnumerable<string> endpoints) { return null; }
    }
}
