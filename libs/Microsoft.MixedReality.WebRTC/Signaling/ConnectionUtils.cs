// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net;

namespace Microsoft.MixedReality.WebRTC.Signaling
{

    static class ConnectionUtils
    {
        // Use the service to connect a local PeerConnection to another chosen by the user among the available ones.
        public static void ChooseAndConnect(PeerConnection pc)
        {
            using (ISignalingService service = new PeerSignalingService(IPAddress.Broadcast, 1234, "MyApp"))
            {
                service.Start("LocalEndpoint#123");

                string target = null;
                while (target == null)
                {
                    target = MakeTheUserChoose(service.RemoteEndPoints);
                }

                string sessionId = Guid.NewGuid().ToString();

                // We have a target
                pc.LocalSdpReadytoSend +=
                    (string type, string sdp) =>
                    {
                        service.SendToRemoteEndpoint(target,
                            new SignalingMessage
                            {
                                SessionId = sessionId,
                                Type = FromSMTString(type),
                                Payload = sdp
                            });
                    };
                pc.IceCandidateReadytoSend +=
                    (string candidate, int sdpMlineindex, string sdpMid) =>
                    {
                        string payload = candidate + '|' + sdpMlineindex + '|' + sdpMid;
                        service.SendToRemoteEndpoint(target,
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
                        if (senderId == target && message.SessionId == sessionId)
                        {
                            if (message.Type == SdpType.SdpAnswer)
                            {
                                pc.SetRemoteDescription("answer", message.Payload);
                            }
                            else if (message.Type == SdpType.IceCandidate)
                            {
                                var parts = message.Payload.Split('|');
                                pc.AddIceCandidate(parts[2], int.Parse(parts[1]), parts[0]);
                            }
                        }
                    };
                pc.CreateOffer();
                // go on and add tracks etc.
            }
        }

        // Publish the peer connection on the service and negotiate with the first endpoint that sends an offer.
        public static void ListenForConnections(PeerConnection pc)
        {
            using (ISignalingService service = new PeerSignalingService(IPAddress.Broadcast, 1234, "MyApp"))
            {
                string endPointId = null;
                string sessionId = null;
                service.MessageReceived +=
                    (senderId, message) =>
                    {
                        if (sessionId == null)
                        {
                            if (message.Type == SdpType.SdpOffer)
                            {
                                endPointId = senderId;
                                sessionId = message.SessionId;
                                pc.SetRemoteDescription("offer", message.Payload);
                            }
                        }
                        else
                        {
                            if (senderId == endPointId && message.SessionId == sessionId && message.Type == SdpType.IceCandidate)
                            {
                                var parts = message.Payload.Split('|');
                                pc.AddIceCandidate(parts[2], int.Parse(parts[1]), parts[0]);

                            }
                        }
                    };

                pc.LocalSdpReadytoSend +=
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
                pc.IceCandidateReadytoSend +=
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

                service.Start("LocalEndpoint#456");
                // go on and add tracks etc.
            }
        }
        private static string MakeTheUserChoose(IEnumerable<string> endpoints) { return null; }

        static SdpType FromSMTString(string s)
        {
            switch (s)
            {
                case "offer": return SdpType.SdpOffer;
                case "answer": return SdpType.SdpAnswer;
                default: throw new Exception();
            }
        }
    }
}
