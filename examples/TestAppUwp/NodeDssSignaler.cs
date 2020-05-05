// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Newtonsoft.Json;
using System;
using System.Collections;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.MixedReality.WebRTC;
using System.ComponentModel;
using Newtonsoft.Json.Serialization;

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
        : NotifierBase // FIXME - Use ViewModel instead
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

            public static WireMessageType TypeFromSdpMessageType(SdpMessageType type)
            {
                switch (type)
                {
                case SdpMessageType.Offer: return WireMessageType.Offer;
                case SdpMessageType.Answer: return WireMessageType.Answer;
                }
                throw new ArgumentException($"Invalid SDP message type '{type}'.");
            }

            public static Message FromSdpMessage(SdpMessage message)
            {
                return new Message
                {
                    MessageType = TypeFromSdpMessageType(message.Type),
                    Data = message.Content,
                    IceDataSeparator = "|"
                };
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
        public int PollTimeMs = 500;

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
                lock (_pollingLock)
                {
                    return _isPolling;
                }
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
        public event Action OnDisconnect;

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
        /// Indicates whether polling is underway, including being cancelled.
        /// </summary>
        private bool _isPolling = false;

        private readonly object _pollingLock = new object();

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
                    OnDisconnect?.Invoke();
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
        public bool StartPollingAsync(CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(LocalPeerId))
            {
                throw new InvalidOperationException("Cannot start polling with empty LocalId.");
            }

            lock (_pollingLock)
            {
                if (_isPolling)
                {
                    return false;
                }
                _isPolling = true;
                _cancellationTokenSource = new CancellationTokenSource();
            }
            RaisePropertyChanged("IsPolling");

            // Build the GET polling request
            string requestUri = $"{_httpServerAddress}data/{LocalPeerId}";

            long lastPollTimeTicks = DateTime.UtcNow.Ticks;
            long pollTimeTicks = TimeSpan.FromMilliseconds(PollTimeMs).Ticks;

            var masterTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.Token, cancellationToken);
            var masterToken = masterTokenSource.Token;
            masterToken.Register(() =>
            {
                lock (_pollingLock)
                {
                    _isPolling = false;
                    _cancellationTokenSource.Dispose();
                    _cancellationTokenSource = null;
                }
                RaisePropertyChanged("IsPolling");
                Interlocked.Exchange(ref _connectedEventFired, 0);
                OnDisconnect?.Invoke();
                OnPollingDone?.Invoke();
                masterTokenSource.Dispose();
            });

            // Prepare the repeating poll task.
            // In order to poll at the specified frequency but also avoid overlapping requests,
            // use a repeating task which re-schedule itself on completion, either immediately
            // if the polling delay is elapsed, or at a later time otherwise.
            async void PollServer()
            {
                try
                {
                    // Polling loop
                    while (true)
                    {
                        masterToken.ThrowIfCancellationRequested();

                        // Send GET request to DSS server.
                        lastPollTimeTicks = DateTime.UtcNow.Ticks;
                        HttpResponseMessage response = await _httpClient.GetAsync(requestUri,
                            HttpCompletionOption.ResponseHeadersRead, masterToken);

                        // On first successful HTTP request, raise the connected event
                        if (0 == Interlocked.Exchange(ref _connectedEventFired, 1))
                        {
                            OnConnect?.Invoke();
                        }

                        masterToken.ThrowIfCancellationRequested();

                        // In order to avoid exceptions in GetStreamAsync() when the server returns a non-success status code (e.g. 404),
                        // first get the HTTP headers, check the status code, then if successful wait for content.
                        if (response.IsSuccessStatusCode)
                        {
                            string jsonMsg = await response.Content.ReadAsStringAsync();

                            masterToken.ThrowIfCancellationRequested();

                            var jsonSettings = new JsonSerializerSettings
                            {
                                Error = (object s, ErrorEventArgs e) => throw new Exception("JSON error: " + e.ErrorContext.Error.Message)
                            };
                            Message msg = JsonConvert.DeserializeObject<Message>(jsonMsg, jsonSettings);
                            if (msg != null)
                            {
                                OnMessage?.Invoke(msg);
                            }
                            else
                            {
                                throw new Exception("Failed to deserialize signaler message from JSON.");
                            }
                        }

                        masterToken.ThrowIfCancellationRequested();

                        // Delay next loop iteration if current polling was faster than target poll duration
                        long curTime = DateTime.UtcNow.Ticks;
                        long deltaTicks = curTime - lastPollTimeTicks;
                        long remainTicks = pollTimeTicks - deltaTicks;
                        if (remainTicks > 0)
                        {
                            int waitTimeMs = new TimeSpan(remainTicks).Milliseconds;
                            await Task.Delay(waitTimeMs);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Manual cancellation via UI, do not report error
                }
                catch (Exception ex)
                {
                    OnFailure?.Invoke(ex);
                }
            }

            // Start the poll task immediately
            Task.Run(PollServer, masterToken);
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
            lock (_pollingLock)
            {
                if (_isPolling)
                {
                    // Note: cannot dispose right away, need to wait for end of all tasks.
                    _cancellationTokenSource?.Cancel();
                    return true;
                }
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
