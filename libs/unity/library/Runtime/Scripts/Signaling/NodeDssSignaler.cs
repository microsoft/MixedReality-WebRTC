// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// Simple signaler for debug and testing.
    /// This is based on https://github.com/bengreenier/node-dss and SHOULD NOT BE USED FOR PRODUCTION.
    /// </summary>
    [AddComponentMenu("MixedReality-WebRTC/NodeDSS Signaler")]
    public class NodeDssSignaler : Signaler
    {
        /// <summary>
        /// Automatically log all errors to the Unity console.
        /// </summary>
        [Tooltip("Automatically log all errors to the Unity console")]
        public bool AutoLogErrors = true;

        /// <summary>
        /// Unique identifier of the local peer.
        /// </summary>
        [Tooltip("Unique identifier of the local peer")]
        public string LocalPeerId;

        /// <summary>
        /// Unique identifier of the remote peer.
        /// </summary>
        [Tooltip("Unique identifier of the remote peer")]
        public string RemotePeerId;

        /// <summary>
        /// The https://github.com/bengreenier/node-dss HTTP service address to connect to
        /// </summary>
        [Header("Server")]
        [Tooltip("The node-dss server to connect to")]
        public string HttpServerAddress = "http://127.0.0.1:3000/";

        /// <summary>
        /// The interval (in ms) that the server is polled at
        /// </summary>
        [Tooltip("The interval (in ms) that the server is polled at")]
        public float PollTimeMs = 500f;

        /// <summary>
        /// Message exchanged with a <c>node-dss</c> server, serialized as JSON.
        /// </summary>
        /// <remarks>
        /// The names of the fields is critical here for proper JSON serialization.
        /// </remarks>
        [Serializable]
        private class NodeDssMessage
        {
            /// <summary>
            /// Separator for ICE messages.
            /// </summary>
            public const string IceSeparatorChar = "|";

            /// <summary>
            /// Possible message types as-serialized on the wire to <c>node-dss</c>.
            /// </summary>
            public enum Type
            {
                /// <summary>
                /// An unrecognized message.
                /// </summary>
                Unknown = 0,

                /// <summary>
                /// A SDP offer message.
                /// </summary>
                Offer,

                /// <summary>
                /// A SDP answer message.
                /// </summary>
                Answer,

                /// <summary>
                /// A trickle-ice or ice message.
                /// </summary>
                Ice
            }

            /// <summary>
            /// Convert a message type from <see xref="string"/> to <see cref="Type"/>.
            /// </summary>
            /// <param name="stringType">The message type as <see xref="string"/>.</param>
            /// <returns>The message type as a <see cref="Type"/> object.</returns>
            public static Type MessageTypeFromString(string stringType)
            {
                if (string.Equals(stringType, "offer", StringComparison.OrdinalIgnoreCase))
                {
                    return Type.Offer;
                }
                else if (string.Equals(stringType, "answer", StringComparison.OrdinalIgnoreCase))
                {
                    return Type.Answer;
                }
                throw new ArgumentException($"Unkown signaler message type '{stringType}'", "stringType");
            }

            public static Type MessageTypeFromSdpMessageType(SdpMessageType type)
            {
                switch (type)
                {
                case SdpMessageType.Offer: return Type.Offer;
                case SdpMessageType.Answer: return Type.Answer;
                default: return Type.Unknown;
                }
            }

            public IceCandidate ToIceCandidate()
            {
                if (MessageType != Type.Ice)
                {
                    throw new InvalidOperationException("The node-dss message it not an ICE candidate message.");
                }
                var parts = Data.Split(new string[] { IceSeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
                // Note the inverted arguments; candidate is last in IceCandidate, but first in the node-dss wire message
                return new IceCandidate
                {
                    SdpMid = parts[2],
                    SdpMlineIndex = int.Parse(parts[1]),
                    Content = parts[0]
                };
            }

            public NodeDssMessage(SdpMessage message)
            {
                MessageType = MessageTypeFromSdpMessageType(message.Type);
                Data = message.Content;
                IceDataSeparator = string.Empty;
            }

            public NodeDssMessage(IceCandidate candidate)
            {
                MessageType = Type.Ice;
                Data = string.Join(IceSeparatorChar, candidate.Content, candidate.SdpMlineIndex.ToString(), candidate.SdpMid);
                IceDataSeparator = IceSeparatorChar;
            }

            /// <summary>
            /// The message type.
            /// </summary>
            public Type MessageType = Type.Unknown;

            /// <summary>
            /// The primary message contents.
            /// </summary>
            public string Data;

            /// <summary>
            /// The data separator needed for proper ICE serialization.
            /// </summary>
            public string IceDataSeparator;
        }

        /// <summary>
        /// Internal timing helper
        /// </summary>
        private float timeSincePollMs = 0f;

        /// <summary>
        /// Internal last poll response status flag
        /// </summary>
        private bool lastGetComplete = true;


        #region ISignaler interface

        /// <inheritdoc/>
        public override Task SendMessageAsync(SdpMessage message)
        {
            return SendMessageImplAsync(new NodeDssMessage(message));
        }

        /// <inheritdoc/>
        public override Task SendMessageAsync(IceCandidate candidate)
        {
            return SendMessageImplAsync(new NodeDssMessage(candidate));
        }

        #endregion

        private Task SendMessageImplAsync(NodeDssMessage message)
        {
            // This method needs to return a Task object which gets completed once the signaler message
            // has been sent. Because the implementation uses a Unity coroutine, use a reset event to
            // signal the task to complete from the coroutine after the message is sent.
            // Note that the coroutine is a Unity object so needs to be started from the main Unity app thread.
            // Also note that TaskCompletionSource<bool> is used as a no-result variant; there is no meaning
            // to the bool value.
            // https://stackoverflow.com/questions/11969208/non-generic-taskcompletionsource-or-alternative
            var tcs = new TaskCompletionSource<bool>();
            _mainThreadWorkQueue.Enqueue(() => StartCoroutine(PostToServerAndWait(message, tcs)));
            return tcs.Task;
        }

        /// <summary>
        /// Unity Engine Start() hook
        /// </summary>
        /// <remarks>
        /// https://docs.unity3d.com/ScriptReference/MonoBehaviour.Start.html
        /// </remarks>
        private void Start()
        {
            if (string.IsNullOrEmpty(HttpServerAddress))
            {
                throw new ArgumentNullException("HttpServerAddress");
            }
            if (!HttpServerAddress.EndsWith("/"))
            {
                HttpServerAddress += "/";
            }

            // If not explicitly set, default local ID to some unique ID generated by Unity
            if (string.IsNullOrEmpty(LocalPeerId))
            {
                LocalPeerId = SystemInfo.deviceName;
            }
        }

        /// <summary>
        /// Internal helper for sending HTTP data to the node-dss server using POST
        /// </summary>
        /// <param name="msg">the message to send</param>
        private IEnumerator PostToServer(NodeDssMessage msg)
        {
            if (RemotePeerId.Length == 0)
            {
                throw new InvalidOperationException("Cannot send SDP message to remote peer; invalid empty remote peer ID.");
            }

            var data = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(msg));
            var www = new UnityWebRequest($"{HttpServerAddress}data/{RemotePeerId}", UnityWebRequest.kHttpVerbPOST);
            www.uploadHandler = new UploadHandlerRaw(data);

            yield return www.SendWebRequest();

            if (AutoLogErrors && (www.isNetworkError || www.isHttpError))
            {
                Debug.Log($"Failed to send message to remote peer {RemotePeerId}: {www.error}");
            }
        }

        /// <summary>
        /// Internal helper to wrap a coroutine into a synchronous call for use inside
        /// a <see cref="Task"/> object.
        /// </summary>
        /// <param name="msg">the message to send</param>
        private IEnumerator PostToServerAndWait(NodeDssMessage message, TaskCompletionSource<bool> tcs)
        {
            yield return StartCoroutine(PostToServer(message));
            const bool dummy = true; // unused
            tcs.SetResult(dummy);
        }

        /// <summary>
        /// Internal coroutine helper for receiving HTTP data from the DSS server using GET
        /// and processing it as needed
        /// </summary>
        /// <returns>the message</returns>
        private IEnumerator CO_GetAndProcessFromServer()
        {
            if (HttpServerAddress.Length == 0)
            {
                throw new InvalidOperationException("Cannot receive SDP messages from remote peer; invalid empty HTTP server address.");
            }
            if (LocalPeerId.Length == 0)
            {
                throw new InvalidOperationException("Cannot receive SDP messages from remote peer; invalid empty local peer ID.");
            }

            var www = UnityWebRequest.Get($"{HttpServerAddress}data/{LocalPeerId}");
            yield return www.SendWebRequest();

            if (!www.isNetworkError && !www.isHttpError)
            {
                var json = www.downloadHandler.text;

                var msg = JsonUtility.FromJson<NodeDssMessage>(json);

                // if the message is good
                if (msg != null)
                {
                    // depending on what type of message we get, we'll handle it differently
                    // this is the "glue" that allows two peers to establish a connection.
                    DebugLogLong($"Received SDP message: type={msg.MessageType} data={msg.Data}");
                    switch (msg.MessageType)
                    {
                    case NodeDssMessage.Type.Offer:
                        // Apply the offer coming from the remote peer to the local peer
                        var sdpOffer = new WebRTC.SdpMessage { Type = SdpMessageType.Offer, Content = msg.Data };
                        PeerConnection.HandleConnectionMessageAsync(sdpOffer).ContinueWith(_ =>
                        {
                            // If the remote description was successfully applied then immediately send
                            // back an answer to the remote peer to acccept the offer.
                            _nativePeer.CreateAnswer();
                        }, TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.RunContinuationsAsynchronously);
                        break;

                    case NodeDssMessage.Type.Answer:
                        // No need to wait for completion; there is nothing interesting to do after it.
                        var sdpAnswer = new WebRTC.SdpMessage { Type = SdpMessageType.Answer, Content = msg.Data };
                        _ = PeerConnection.HandleConnectionMessageAsync(sdpAnswer);
                        break;

                    case NodeDssMessage.Type.Ice:
                        // this "parts" protocol is defined above, in OnIceCandidateReadyToSend listener
                        _nativePeer.AddIceCandidate(msg.ToIceCandidate());
                        break;

                    default:
                        Debug.Log("Unknown message: " + msg.MessageType + ": " + msg.Data);
                        break;
                    }

                    timeSincePollMs = PollTimeMs + 1f; //fast forward next request
                }
                else if (AutoLogErrors)
                {
                    Debug.LogError($"Failed to deserialize JSON message : {json}");
                }
            }
            else if (AutoLogErrors && www.isNetworkError)
            {
                Debug.LogError($"Network error trying to send data to {HttpServerAddress}: {www.error}");
            }
            else
            {
                // This is very spammy because the node-dss protocol uses 404 as regular "no data yet" message, which is an HTTP error
                //Debug.LogError($"HTTP error: {www.error}");
            }

            lastGetComplete = true;
        }

        /// <inheritdoc/>
        protected override void Update()
        {
            // Do not forget to call the base class Update(), which processes events from background
            // threads to fire the callbacks implemented in this class.
            base.Update();

            // If we have not reached our PollTimeMs value...
            if (timeSincePollMs <= PollTimeMs)
            {
                // ...then we keep incrementing our local counter until we do.
                timeSincePollMs += Time.deltaTime * 1000.0f;
                return;
            }

            // If we have a pending request still going, don't queue another yet.
            if (!lastGetComplete)
            {
                return;
            }

            // When we have reached our PollTimeMs value...
            timeSincePollMs = 0f;

            // ...begin the poll and process.
            lastGetComplete = false;
            StartCoroutine(CO_GetAndProcessFromServer());
        }

        private void DebugLogLong(string str)
        {
#if !UNITY_EDITOR && UNITY_ANDROID
            // On Android, logcat truncates to ~1000 characters, so split manually instead.
            const int maxLineSize = 1000;
            int totalLength = str.Length;
            int numLines = (totalLength + maxLineSize - 1) / maxLineSize;
            for (int i = 0; i < numLines; ++i)
            {
                int start = i * maxLineSize;
                int length = Math.Min(start + maxLineSize, totalLength) - start;
                Debug.Log(str.Substring(start, length));
            }
#else
            Debug.Log(str);
#endif
        }
    }
}
