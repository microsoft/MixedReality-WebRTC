// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using UnityEngine;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// Abstract base class to simplify implementing a WebRTC signaling solution in Unity.
    /// 
    /// There is no requirement to use this class as a base class for a custom implementation,
    /// but it handles automatically registering the necessary <see cref="Unity.PeerConnection"/>
    /// event handlers, as well as dispatching free-threaded callbacks to the main Unity app thread
    /// for simplicity and safety, and leaves the implementation with instead with a single sending
    /// method <see cref="SendMessageAsync(Message)"/> to implement, as well as handling received
    /// messages.
    /// </summary>
    public abstract class Signaler : MonoBehaviour
    {
        /// <summary>
        /// The <see cref="PeerConnection"/> this signaler needs to work for.
        /// </summary>
        public PeerConnection PeerConnection;


        #region Signaler interface

        // TODO - These messages will move into the C# library API eventually (#188)

        [Serializable]
        public class Message
        {
            /// <summary>
            /// Message types.
            /// </summary>
            public enum MessageType
            {
                /// <summary>
                /// An unrecognized message
                /// </summary>
                Unknown = 0,

                /// <summary>
                /// A SDP offer message
                /// </summary>
                Offer,

                /// <summary>
                /// A SDP answer message
                /// </summary>
                Answer,

                /// <summary>
                /// A trickle-ice or ice message
                /// </summary>
                Ice
            }

            /// <summary>
            /// The message type.
            /// </summary>
            public readonly MessageType Type;

            public Message(MessageType type)
            {
                Type = type;
            }
        }

        [Serializable]
        public class SdpMessage : Message
        {
            /// <summary>
            /// The SDP message content.
            /// </summary>
            public string Data;

            public SdpMessage(string type, string data) : base(TypeFromString(type))
            {
                Data = data;
            }

            public static MessageType TypeFromString(string type)
            {
                if (type == "offer")
                {
                    return MessageType.Offer;
                }
                else if (type == "answer")
                {
                    return MessageType.Answer;
                }
                throw new ArgumentException($"Invalid SDP message type: \"{type}\"", "type");
            }
        }

        [Serializable]
        public class IceMessage : Message
        {
            /// <summary>
            /// The ICE message content.
            /// </summary>
            public string Data;

            /// <summary>
            /// The data separator needed for proper ICE serialization.
            /// </summary>
            public string IceDataSeparator;

            public IceMessage(string sdpMid, int sdpMlineIndex, string candidate) : base(MessageType.Ice)
            {
                IceDataSeparator = "|";
                Data = string.Join(IceDataSeparator, new string[] { candidate, sdpMlineIndex.ToString(), sdpMid });
            }
        }

        /// <summary>
        /// Asynchronously send a signaling message to the remote peer.
        /// </summary>
        /// <param name="message">The signaling message to send to the remote peer.</param>
        /// <returns>
        /// A <see cref="Task"/> object completed once the message has been sent,
        /// but not necessarily delivered.
        /// </returns>
        public abstract Task SendMessageAsync(Message message);

        #endregion


        /// <summary>
        /// Native <xref href="Microsoft.MixedReality.WebRTC.PeerConnection"/> object from the underlying
        /// WebRTC C# library, available once the peer has been initialized.
        /// </summary>
        protected WebRTC.PeerConnection _nativePeer = null;

        /// <summary>
        /// Task queue used to defer actions to the main Unity app thread, which is the only thread
        /// with access to Unity objects.
        /// </summary>
        protected ConcurrentQueue<Action> _mainThreadWorkQueue = new ConcurrentQueue<Action>();

        /// <summary>
        /// Callback fired from the <see cref="PeerConnection"/> when it finished
        /// initializing, to subscribe to signaling-related events.
        /// </summary>
        /// <param name="peer">The peer connection to attach to</param>
        public void OnPeerInitialized()
        {
            _nativePeer = PeerConnection.Peer;

            // Register handlers for the SDP events
            _nativePeer.IceCandidateReadytoSend += OnIceCandidateReadyToSend_Listener;
            _nativePeer.LocalSdpReadytoSend += OnLocalSdpReadyToSend_Listener;
        }

        /// <summary>
        /// Callback fired from the <see cref="PeerConnection"/> before it starts
        /// uninitializing itself and disposing of the underlying implementation object.
        /// </summary>
        /// <param name="peer">The peer connection about to be deinitialized</param>
        public void OnPeerUninitializing()
        {
            // Unregister handlers for the SDP events
            //_nativePeer.IceCandidateReadytoSend -= OnIceCandidateReadyToSend_Listener;
            //_nativePeer.LocalSdpReadytoSend -= OnLocalSdpReadyToSend_Listener;
        }

        private void OnIceCandidateReadyToSend_Listener(string candidate, int sdpMlineIndex, string sdpMid)
        {
            _mainThreadWorkQueue.Enqueue(() => OnIceCandidateReadyToSend(candidate, sdpMlineIndex, sdpMid));
        }

        /// <summary>
        /// Helper to split SDP offer and answer messages and dispatch to the appropriate handler.
        /// </summary>
        /// <param name="message">The SDP message ready to be sent to the remote peer.</param>
        private void OnLocalSdpReadyToSend_Listener(WebRTC.SdpMessage message)
        {
            if (message.Type == SdpMessageType.Offer)
            {
                _mainThreadWorkQueue.Enqueue(() => OnSdpOfferReadyToSend(message.Content));
            }
            else if (message.Type == SdpMessageType.Answer)
            {
                _mainThreadWorkQueue.Enqueue(() => OnSdpAnswerReadyToSend(message.Content));
            }
        }

        protected virtual void OnEnable()
        {
            PeerConnection.OnInitialized.AddListener(OnPeerInitialized);
            PeerConnection.OnShutdown.AddListener(OnPeerUninitializing);
        }

        /// <summary>
        /// Unity Engine Update() hook
        /// </summary>
        /// <remarks>
        /// https://docs.unity3d.com/ScriptReference/MonoBehaviour.Update.html
        /// </remarks>
        protected virtual void Update()
        {
            // Process workloads queued from background threads
            while (_mainThreadWorkQueue.TryDequeue(out Action action))
            {
                action();
            }
        }

        protected virtual void OnDisable()
        {
            PeerConnection.OnInitialized.RemoveListener(OnPeerInitialized);
            PeerConnection.OnShutdown.RemoveListener(OnPeerUninitializing);
        }

        /// <summary>
        /// Callback invoked when an ICE candidate message has been generated and is ready to
        /// be sent to the remote peer by the signaling object.
        /// </summary>
        /// <param name="candidate"></param>
        /// <param name="sdpMlineIndex"></param>
        /// <param name="sdpMid"></param>
        protected virtual void OnIceCandidateReadyToSend(string candidate, int sdpMlineIndex, string sdpMid)
        {
            var message = new IceMessage(sdpMid, sdpMlineIndex, candidate);
            SendMessageAsync(message);
        }

        /// <summary>
        /// Callback invoked when a local SDP offer has been generated and is ready to
        /// be sent to the remote peer by the signaling object.
        /// </summary>
        /// <param name="offer">The SDP offer message to send.</param>
        protected virtual void OnSdpOfferReadyToSend(string offer)
        {
            var message = new SdpMessage("offer", offer);
            SendMessageAsync(message);
        }

        /// <summary>
        /// Callback invoked when a local SDP answer has been generated and is ready to
        /// be sent to the remote peer by the signaling object.
        /// </summary>
        /// <param name="answer">The SDP answer message to send.</param>
        protected virtual void OnSdpAnswerReadyToSend(string answer)
        {
            var message = new SdpMessage("answer", answer);
            SendMessageAsync(message);
        }
    }
}
