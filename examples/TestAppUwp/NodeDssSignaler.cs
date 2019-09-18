// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Newtonsoft.Json;
using System;
using System.Collections;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.MixedReality.WebRTC;

namespace TestAppUwp
{
    /// <summary>
    /// A signaler implementation for reference and prototyping.
    /// </summary>
    /// <remarks>
    /// This is based on https://github.com/bengreenier/node-dss and
    /// is not a production ready signaling solution. It is included
    /// here to demonstrate how one might implement such a solution,
    /// as well as allow rapid prototyping with the <c>node-dss</c>
    /// server solution.
    /// </remarks>
    public class NodeDssSignaler
    {
        /// <summary>
        /// Data that makes up a signaler message
        /// </summary>
        /// <remarks>
        /// Note: the same data is used for transmitting and receiving
        /// </remarks>
        [Serializable]
        public class Message
        {
            /// <summary>
            /// Possible message types as-serialized on the wire
            /// </summary>
            public enum WireMessageType
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
            /// Convert a message type from <see xref="string"/> to <see cref="WireMessageType"/>.
            /// </summary>
            /// <param name="stringType">The message type as <see xref="string"/>.</param>
            /// <returns>The message type as a <see cref="WireMessageType"/> object.</returns>
            public static WireMessageType WireMessageTypeFromString(string stringType)
            {
                if (string.Equals(stringType, "offer", StringComparison.OrdinalIgnoreCase))
                {
                    return WireMessageType.Offer;
                }
                else if (string.Equals(stringType, "answer", StringComparison.OrdinalIgnoreCase))
                {
                    return WireMessageType.Answer;
                }
                throw new ArgumentException($"Unkown signaler message type '{stringType}'");
            }

            /// <summary>
            /// The message type
            /// </summary>
            public WireMessageType MessageType;

            /// <summary>
            /// The primary message contents
            /// </summary>
            public string Data;

            /// <summary>
            /// The data separator needed for proper ICE serialization
            /// </summary>
            public string IceDataSeparator;
        }

        /// <summary>
        /// The https://github.com/bengreenier/node-dss HTTP service address to connect to.
        /// </summary>
        /// <remarks>
        /// This value should end with a forward-slash (/).
        /// </remarks>
        public string HttpServerAddress
        {
            get { return _httpServerAddress; }
            set
            {
                // TODO - More validation?
                if (!value.EndsWith('/'))
                {
                    throw new ArgumentException("HTTP server address must end with a forward slash '/', e.g. 'http://ip:port/'.");
                }
                _httpServerAddress = value;
            }
        }

        /// <summary>
        /// Unique identifier of the local peer, used as part of message exchange
        /// by the <c>node-dss</c> server to identify senders and receivers.
        /// </summary>
        /// <remarks>
        /// This must be set before calling <see cref="StartPollingAsync"/>.
        /// </remarks>
        public string LocalPeerId;

        /// <summary>
        /// Unique identifier of the remote peer, used as part of message exchange
        /// by the <c>node-dss</c> server to identify senders and receivers.
        /// </summary>
        /// <remarks>
        /// This must be set before calling <see cref="StartPollingAsync"/>.
        /// </remarks>
        public string RemotePeerId;

        /// <summary>
        /// The interval (in ms) that the server is polled at.
        /// </summary>
        /// <remarks>
        /// This is approximate, and in any case will never allow multiple
        /// polling requests to be in flight at the same time, so actual
        /// interval between polling requests may be longer depending on
        /// network conditions.
        /// </remarks>
        public float PollTimeMs = 5f;

        /// <summary>
        /// Indicate whether the <c>node-dss</c> server polling is active,
        /// including if <see cref="StopPollingAsync"/> was called but
        /// cancellation did not complete yet.
        /// </summary>
        /// <remarks>
        /// Because the cancellation process is asynchronous, this value is
        /// only informational.
        /// </remarks>
        public bool IsPolling
        {
            get
            {
                // Atomic read
                return (Interlocked.CompareExchange(ref _isPolling, 1, 1) == 1);
            }
        }

        /// <summary>
        /// Event that occurs when polling is effectively completed, and can
        /// be restarted with <see cref="StartPollingAsync"/>.
        /// </summary>
        public event Action OnPollingDone;
        
        /// <summary>
        /// Event that occurs when signaling is connected.
        /// </summary>
        public event Action OnConnect;

        /// <summary>
        /// Event that occurs when signaling is disconnected.
        /// </summary>
#pragma warning disable 67
        public event Action OnDisconnect;
#pragma warning restore 67

        /// <summary>
        /// Event that occurs when the signaler receives a new message.
        /// </summary>
        public event Action<Message> OnMessage;

        /// <summary>
        /// Event that occurs when the signaler experiences some failure.
        /// </summary>
        public event Action<Exception> OnFailure;

        /// <summary>
        /// Property storage of the <c>node-dss</c> server HTTP address.
        /// </summary>
        private string _httpServerAddress;

        /// <summary>
        /// The <see cref="System.Net.Http.HttpClient"/> object used to communicate
        /// with the <c>node-dss</c> server.
        /// </summary>
        private HttpClient _httpClient = new HttpClient();

        /// <summary>
        /// Atomic boolean indicating whether the <see cref="OnConnect"/> event was fired.
        /// Because <c>node-dss</c> does not have a concept of connection, the event
        /// is fired on the first successfully transmitted message.
        /// </summary>
        /// <remarks>
        /// This field is an <code>int</code> because it is modified atomically, and
        /// <see cref="Interlocked"/> does not provide a <code>bool</code> overload.
        /// </remarks>
        private int _connectedEventFired = 0;

        /// <summary>
        /// Cancellation token source for the recursive polling task.
        /// </summary>
        private CancellationTokenSource _cancellationTokenSource;

        /// <summary>
        /// Atomic boolean indicating whether polling is underway, including being cancelled.
        /// </summary>
        /// <remarks>
        /// This field is an <code>int</code> because it is modified atomically, and
        /// <see cref="Interlocked"/> does not provide a <code>bool</code> overload.
        /// </remarks>
        private int _isPolling = 0;

        public NodeDssSignaler()
        {
            // Set "Keep-Alive=true" on requests to DSS server to improve performance
            _httpClient.DefaultRequestHeaders.Connection.Clear();
            _httpClient.DefaultRequestHeaders.ConnectionClose = false;
            _httpClient.DefaultRequestHeaders.Connection.Add("Keep-Alive");
        }

        /// <summary>
        /// Send a message to the signaling server to be dispatched to the remote
        /// endpoint specified by <see cref="SignalerMessage.TargetId"/>.
        /// </summary>
        /// <param name="message">Message to send</param>
        public Task SendMessageAsync(Message message)
        {
            if (string.IsNullOrWhiteSpace(_httpServerAddress))
            {
                throw new ArgumentException("Invalid empty HTTP server address.");
            }

            // Send a POST request to the server with the JSON-serialized message.
            var jsonMsg = JsonConvert.SerializeObject(message);
            string requestUri = $"{_httpServerAddress}data/{RemotePeerId}";
            HttpContent content = new StringContent(jsonMsg);
            var task = _httpClient.PostAsync(requestUri, content).ContinueWith((postTask) =>
            {
                if (postTask.Exception != null)
                {
                    OnFailure?.Invoke(postTask.Exception);
                }
            });

            // Atomic read
            if (Interlocked.CompareExchange(ref _connectedEventFired, 1, 1) == 1)
            {
                return task;
            }

            // On first successful message, fire the OnConnect event
            return task.ContinueWith((prevTask) =>
            {
                if (prevTask.Exception != null)
                {
                    OnFailure?.Invoke(prevTask.Exception);
                }

                if (prevTask.IsCompletedSuccessfully)
                {
                    // Only invoke if this task was the first one to change the value, because
                    // another task may have completed faster in the meantime and already invoked.
                    if (0 == Interlocked.Exchange(ref _connectedEventFired, 1))
                    {
                        OnConnect?.Invoke();
                    }
                }
            });
        }

        /// <summary>
        /// Start polling the node-dss server with a GET request, and continue to do so
        /// until <see cref="StopPollingAsync"/> is called.
        /// </summary>
        /// <returns>Returns <c>true</c> if polling effectively started with this call.</returns>
        /// <remarks>
        /// The <see cref="LocalPeerId"/> field must be set before calling this method.
        /// This method can safely be called multiple times, and will do nothing if
        /// polling is already underway or waiting to be stopped.
        /// </remarks>
        public bool StartPollingAsync()
        {
            if (string.IsNullOrWhiteSpace(LocalPeerId))
            {
                throw new Exception("Cannot start polling with empty LocalId.");
            }

            if (Interlocked.CompareExchange(ref _isPolling, 1, 0) == 1)
            {
                // Already polling
                return false;
            }

            // Build the GET polling request
            string requestUri = $"{_httpServerAddress}data/{LocalPeerId}";

            long lastPollTimeTicks = DateTime.UtcNow.Ticks;
            long pollTimeTicks = TimeSpan.FromMilliseconds(PollTimeMs).Ticks;

            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;
            token.Register(() =>
            {
                Interlocked.Exchange(ref _isPolling, 0);
                // TODO - Potentially a race condition here if StartPollingAsync() called,
                //        but would be even worse with the exchange being after, as
                //        StopPollingAsync() would attempt to read from it while being disposed.
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
                OnPollingDone?.Invoke();
            });

            // Prepare the repeating poll task.
            // In order to poll at the specified frequency but also avoid overlapping requests,
            // use a repeating task which re-schedule itself on completion, either immediately
            // if the polling delay is elapsed, or at a later time otherwise.
            Action nextAction = null;
            nextAction = () =>
            {
                token.ThrowIfCancellationRequested();

                // Send GET request to DSS server.
                // In order to avoid exceptions in GetStreamAsync() when the server returns a non-success status code (e.g. 404),
                // first get the HTTP headers, check the status code, then if successful wait for content.
                lastPollTimeTicks = DateTime.UtcNow.Ticks;
                _httpClient.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead).ContinueWith((getTask) =>
                {
                    if (getTask.Exception != null)
                    {
                        OnFailure?.Invoke(getTask.Exception);
                        return;
                    }

                    token.ThrowIfCancellationRequested();

                    HttpResponseMessage response = getTask.Result;
                    if (response.IsSuccessStatusCode)
                    {
                        response.Content.ReadAsStringAsync().ContinueWith((readTask) =>
                        {
                            if (readTask.Exception != null)
                            {
                                OnFailure?.Invoke(readTask.Exception);
                                return;
                            }

                            token.ThrowIfCancellationRequested();

                            var jsonMsg = readTask.Result;
                            var msg = JsonConvert.DeserializeObject<Message>(jsonMsg);
                            if (msg != null)
                            {
                                OnMessage?.Invoke(msg);
                            }
                            else
                            {
                                OnFailure?.Invoke(new Exception($"Failed to deserialize SignalerMessage object from JSON."));
                            }
                        });
                    }

                    // Some time may have passed waiting for GET content; check token again for responsiveness
                    token.ThrowIfCancellationRequested();

                    // Repeat task to continue polling
                    long curTime = DateTime.UtcNow.Ticks;
                    long deltaTicks = curTime - lastPollTimeTicks;
                    if (deltaTicks >= pollTimeTicks)
                    {
                        // Previous GET task took more time than polling delay, execute ASAP
                        Task.Run(nextAction, token);
                    }
                    else
                    {
                        // Previous GET task took less time than polling delay, schedule next polling
                        long remainTicks = pollTimeTicks - deltaTicks;
                        int nextScheduleTimeMs = (int)(new TimeSpan(remainTicks).TotalMilliseconds);
                        Task.Delay(nextScheduleTimeMs, token).ContinueWith(_ => nextAction(), token);
                    }
                });
            };

            // Start the first poll task immediately
            Task.Run(nextAction, token);
            return true;
        }

        /// <summary>
        /// Asynchronously cancel the polling process. Once polling is actually stopped
        /// and can be restarted, the <see cref="OnPollingDone"/> event is fired.
        /// </summary>
        /// <returns>
        /// Returns <c>true</c> if polling cancellation was effectively initiated
        /// by this call.
        /// </returns>
        /// <remarks>
        /// This method can safely be called multiple times, and does nothing if not
        /// already polling or already waiting for polling to end.
        /// </remarks>
        public bool StopPollingAsync()
        {
            // Atomic read
            if (Interlocked.CompareExchange(ref _isPolling, 1, 1) == 1)
            {
                // Note: cannot dispose right away, need to wait for end of all tasks.
                _cancellationTokenSource?.Cancel();
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Extension methods for <see cref="SignalerMessage.WireMessageType"/>.
    /// </summary>
    public static class WireMessageTypeExtensions
    {
        /// <summary>
        /// Convert a message type from <see cref="SignalerMessage.WireMessageType"/> to <see xref="string"/>.
        /// </summary>
        /// <param name="type">The message type as <see cref="SignalerMessage.WireMessageType"/>.</param>
        /// <returns>The message type as a <see xref="string"/> object.</returns>
        public static string ToString(this NodeDssSignaler.Message.WireMessageType type)
        {
            return type.ToString().ToLowerInvariant();
        }
    }
}
