// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net;

namespace Microsoft.MixedReality.WebRTC.Signaling
{
    // TODO: implement on top of PeerDiscoveryAgent
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

}

