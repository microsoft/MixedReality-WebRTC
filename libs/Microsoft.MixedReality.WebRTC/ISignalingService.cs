using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.MixedReality.WebRTC
{
    interface ISignalingService : IDisposable
    {
        /// <summary>
        /// Get the currently available remote endpoints. These can be passed to
        /// <see cref="ConnectToRemoteEndpoint(string, PeerConnection)"/>.
        /// </summary>
        IEnumerable<string> RemoteEndPoints { get; }

        /// <summary>
        /// Make a local <see cref="PeerConnection"/> visible to remote endpoints under the given ID.
        /// </summary>
        /// <remarks>
        /// SDP/ICE messages sent to the passed ID will be forwarded to `pc`.
        /// ??? Replies?
        /// </remarks>
        void PublishLocalEndPoint(string id, PeerConnection pc);

        /// <summary>
        /// Remove a previously published local endpoint.
        /// </summary>
        void RemoveLocalEndPoint(string id);

        /// <summary>
        /// Connect the passed <see cref="PeerConnection"/> to the endpoint with the passed ID.
        /// </summary>
        /// <remarks>
        /// The method calls <see cref="PeerConnection.CreateOffer"/> on the passed connection.
        /// The service forwards any following message between the connection and the remote endpoint.
        /// </remarks>
        void ConnectToRemoteEndpoint(string id, PeerConnection pc);

        /// <summary>
        /// Remove the passed connection.
        /// </summary>
        /// <remarks>
        ///
        /// </remarks>
        /// <param name="pc"></param>
        void RemoveConnection(PeerConnection pc);
    }
}
