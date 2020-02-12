using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Microsoft.MixedReality.WebRTC
{
    public enum SignalingType
    {
        SdpOffer,
        SdpAnswer,
        IceCandidate
    }

    public class SignalingMessage
    {
        public string SessionId;
        public SignalingType Type;
        public string Payload;
    }

    interface ISignalingService : IDisposable
    {
        /// <summary>
        /// Start the service and make the local process visible as `localEndPointId`.
        /// </summary>
        void Start(string localEndPointId);

        /// <summary>
        /// Stop the service.
        /// </summary>
        void Stop();

        /// <summary>
        /// Get the endpoints that are currently active.
        /// </summary>
        IEnumerable<string> RemoteEndPoints { get; }

        /// <summary>
        /// Raised every time there is a change in the list of remote endpoints.
        /// Every delegate added to this is called once even if there are no updates.
        /// </summary>
        event Action RemoteEndPointsChanged;

        /// <summary>
        /// Send a message to the remote endpoint.
        /// </summary>
        void SendToRemoteEndpoint(string targetEndPointId, SignalingMessage message);

        /// <summary>
        /// Raised when a message is received.
        /// </summary>
        event Action<string, SignalingMessage> MessageReceived;
    }

    public class PeerSignalingService : ISignalingService
    {
        public IEnumerable<string> RemoteEndPoints => throw new NotImplementedException();

        public event Action RemoteEndPointsChanged;
        public event Action<string, SignalingMessage> MessageReceived;

        public PeerSignalingService(IPAddress broadcast, ushort port, string category)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public void SendToRemoteEndpoint(string targetEndPointId, SignalingMessage message)
        {
            throw new NotImplementedException();
        }

        public void Start(string localEndPointId)
        {
            throw new NotImplementedException();
        }

        public void Stop()
        {
            throw new NotImplementedException();
        }
    }

    static class SignalingExamples
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
                                Type = SignalingType.IceCandidate,
                                Payload = payload
                            });
                    };
                service.MessageReceived +=
                    (senderId, message) =>
                    {
                        if (senderId == target && message.SessionId == sessionId)
                        {
                            if (message.Type == SignalingType.SdpAnswer)
                            {
                                pc.SetRemoteDescription("answer", message.Payload);
                            }
                            else if (message.Type == SignalingType.IceCandidate)
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
                            if (message.Type == SignalingType.SdpOffer)
                            {
                                endPointId = senderId;
                                sessionId = message.SessionId;
                                pc.SetRemoteDescription("offer", message.Payload);
                            }
                        }
                        else
                        {
                            if (senderId == endPointId && message.SessionId == sessionId && message.Type == SignalingType.IceCandidate)
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
                                Type = SignalingType.IceCandidate,
                                Payload = payload
                            });
                    };

                service.Start("LocalEndpoint#456");
                // go on and add tracks etc.
            }
        }
        private static string MakeTheUserChoose(IEnumerable<string> endpoints) { return null; }

        static SignalingType FromSMTString(string s)
        {
            switch (s)
            {
                case "offer": return SignalingType.SdpOffer;
                case "answer": return SignalingType.SdpAnswer;
                default: throw new Exception();
            }
        }
    }
}
