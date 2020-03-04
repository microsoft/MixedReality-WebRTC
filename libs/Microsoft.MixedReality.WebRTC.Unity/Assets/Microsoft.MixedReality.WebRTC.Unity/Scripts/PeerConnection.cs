// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using System.Collections.Concurrent;
using System.Text;

#if UNITY_WSA && !UNITY_EDITOR
using global::Windows.UI.Core;
using global::Windows.Foundation;
using global::Windows.Media.Core;
using global::Windows.Media.Capture;
using global::Windows.ApplicationModel.Core;
#endif

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// Enumeration of the different types of ICE servers.
    /// </summary>
    public enum IceType
    {
        /// <summary>
        /// Indicates there is no ICE information
        /// </summary>
        /// <remarks>
        /// Under normal use, this should not be used
        /// </remarks>
        None = 0,

        /// <summary>
        /// Indicates ICE information is of type STUN
        /// </summary>
        /// <remarks>
        /// https://en.wikipedia.org/wiki/STUN
        /// </remarks>
        Stun,

        /// <summary>
        /// Indicates ICE information is of type TURN
        /// </summary>
        /// <remarks>
        /// https://en.wikipedia.org/wiki/Traversal_Using_Relays_around_NAT
        /// </remarks>
        Turn
    }

    /// <summary>
    /// Represents an Ice server in a simple way that allows configuration from the unity inspector
    /// </summary>
    [Serializable]
    public struct ConfigurableIceServer
    {
        /// <summary>
        /// The type of the server
        /// </summary>
        [Tooltip("Type of ICE server")]
        public IceType Type;

        /// <summary>
        /// The unqualified uri of the server
        /// </summary>
        /// <remarks>
        /// You should not prefix this with "stun:" or "turn:"
        /// </remarks>
        [Tooltip("ICE server URI, without any stun: or turn: prefix.")]
        public string Uri;

        /// <summary>
        /// Convert the server to the representation the underlying libraries use
        /// </summary>
        /// <returns>stringified server information</returns>
        public override string ToString()
        {
            return string.Format("{0}:{1}", Type.ToString().ToLowerInvariant(), Uri);
        }
    }

    /// <summary>
    /// A <a href="https://docs.unity3d.com/ScriptReference/Events.UnityEvent.html">UnityEvent</a> that represents a WebRTC error event.
    /// </summary>
    [Serializable]
    public class WebRTCErrorEvent : UnityEvent<string>
    {
    }

    /// <summary>
    /// High-level wrapper for Unity WebRTC functionalities.
    /// This is the API entry point for establishing a connection with a remote peer.
    /// </summary>
    [AddComponentMenu("MixedReality-WebRTC/Peer Connection")]
    public class PeerConnection : MonoBehaviour
    {
        /// <summary>
        /// Retrieves the underlying peer connection object once initialized.
        /// </summary>
        /// <remarks>
        /// If <see cref="OnInitialized"/> has not fired, this will be <c>null</c>.
        /// </remarks>
        public WebRTC.PeerConnection Peer { get; private set; } = null;


        #region Behavior settings

        /// <summary>
        /// Initialize the peer connection on <a href="https://docs.unity3d.com/ScriptReference/MonoBehaviour.Start.html">MonoBehaviour.Start()</a>.
        /// If this field is <c>false</c> then the user needs to call <see cref="InitializeAsync(CancellationToken)"/>
        /// to manually initialize the peer connection before it can be used for any purpose.
        /// </summary>
        [Header("Behavior")]
        [Tooltip("Automatically initialize the peer connection on Start()")]
        public bool AutoInitializeOnStart = true;

        /// <summary>
        /// Automatically create a new offer whenever a renegotiation needed event is received.
        /// </summary>
        /// <remarks>
        /// Note that the renegotiation needed event may be dispatched asynchronously, so it is
        /// discourages to toggle this field ON and OFF. Instead, the user should choose an
        /// approach (manual or automatic) and stick to it.
        /// 
        /// In particular, temporarily setting this to <c>false</c> during a batch of changes and
        /// setting it back to <c>true</c> right after the last change may or may not produce an
        /// automatic offer, depending on whether the negotiated event was dispatched while the
        /// property was still <c>false</c> or not.
        /// </remarks>
        [Tooltip("Automatically create a new offer when receiving a renegotiation needed event")]
        public bool AutoCreateOfferOnRenegotiationNeeded = true;

        /// <summary>
        /// Flag to log all errors to the Unity console automatically.
        /// </summary>
        [Tooltip("Automatically log all errors to the Unity console")]
        public bool AutoLogErrorsToUnityConsole = true;

        #endregion


        #region Signaling

        /// <summary>
        /// Signaler to use to establish the connection.
        /// </summary>
        [Header("Signaling")]
        [Tooltip("Signaler to use to establish the connection")]
        public Signaler Signaler;

        #endregion


        #region Interactive Connectivity Establishment (ICE)

        /// <summary>
        /// Set of ICE servers the WebRTC library will use to try to establish a connection.
        /// </summary>
        [Header("ICE servers")]
        [Tooltip("Optional set of ICE servers (STUN and/or TURN)")]
        public List<ConfigurableIceServer> IceServers = new List<ConfigurableIceServer>()
        {
            new ConfigurableIceServer()
            {
                Type = IceType.Stun,
                Uri = "stun.l.google.com:19302"
            }
        };

        /// <summary>
        /// Optional username for the ICE servers.
        /// </summary>
        [Tooltip("Optional username for the ICE servers")]
        public string IceUsername;

        /// <summary>
        /// Optional credential for the ICE servers.
        /// </summary>
        [Tooltip("Optional credential for the ICE servers")]
        public string IceCredential;

        #endregion


        #region Events

        /// <summary>
        /// Event fired after the peer connection is initialized and ready for use.
        /// </summary>
        [Header("Peer connection events")]
        [Tooltip("Event fired after the peer connection is initialized and ready for use")]
        public UnityEvent OnInitialized = new UnityEvent();

        /// <summary>
        /// Event fired after the peer connection is shut down and cannot be used anymore.
        /// </summary>
        [Tooltip("Event fired after the peer connection is shut down and cannot be used anymore")]
        public UnityEvent OnShutdown = new UnityEvent();

        /// <summary>
        /// Event that occurs when a WebRTC error occurs
        /// </summary>
        [Tooltip("Event that occurs when a WebRTC error occurs")]
        public WebRTCErrorEvent OnError = new WebRTCErrorEvent();

        #endregion


        #region Private variables

        /// <summary>
        /// Internal queue used to marshal work back to the main Unity thread.
        /// </summary>
        private ConcurrentQueue<Action> _mainThreadWorkQueue = new ConcurrentQueue<Action>();

        /// <summary>
        /// Underlying native peer connection wrapper.
        /// </summary>
        /// <seealso cref="CreateNativePeerConnection"/>
        private WebRTC.PeerConnection _nativePeer = null;

        #endregion


        #region Public methods

        /// <summary>
        /// Enumerate the video capture devices available as a WebRTC local video feed source.
        /// </summary>
        /// <returns>The list of local video capture devices available to WebRTC.</returns>
        public static Task<List<VideoCaptureDevice>> GetVideoCaptureDevicesAsync()
        {
            return WebRTC.PeerConnection.GetVideoCaptureDevicesAsync();
        }

        /// <summary>
        /// Initialize the underlying WebRTC libraries
        /// </summary>
        /// <remarks>
        /// This function is asynchronous, to monitor it's status bind a handler to OnInitialized and OnError
        /// </remarks>
        public Task InitializeAsync(CancellationToken token = default(CancellationToken))
        {
            // Normally would be initialized by Awake(), but in case the component is disabled
            if (_nativePeer == null)
            {
                CreateNativePeerConnection();
            }

            // if the peer is already set, we refuse to initialize again.
            // Note: for multi-peer scenarios, use multiple WebRTC components.
            if (_nativePeer.Initialized)
            {
                return Task.CompletedTask;
            }

#if UNITY_ANDROID
            AndroidJavaClass systemClass = new AndroidJavaClass("java.lang.System");
            string libname = "jingle_peerconnection_so";
            systemClass.CallStatic("loadLibrary", new object[1] { libname });
            Debug.Log("loadLibrary loaded : " + libname);

            /*
                * Below is equivalent of this java code:
                * PeerConnectionFactory.InitializationOptions.Builder builder = 
                *   PeerConnectionFactory.InitializationOptions.builder(UnityPlayer.currentActivity);
                * PeerConnectionFactory.InitializationOptions options = 
                *   builder.createInitializationOptions();
                * PeerConnectionFactory.initialize(options);
                */

            AndroidJavaClass playerClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject activity = playerClass.GetStatic<AndroidJavaObject>("currentActivity");
            AndroidJavaClass webrtcClass = new AndroidJavaClass("org.webrtc.PeerConnectionFactory");
            AndroidJavaClass initOptionsClass = new AndroidJavaClass("org.webrtc.PeerConnectionFactory$InitializationOptions");
            AndroidJavaObject builder = initOptionsClass.CallStatic<AndroidJavaObject>("builder", new object[1] { activity });
            AndroidJavaObject options = builder.Call<AndroidJavaObject>("createInitializationOptions");

            if (webrtcClass != null)
            {
                webrtcClass.CallStatic("initialize", new object[1] { options });
            }
#endif

#if UNITY_WSA && !UNITY_EDITOR
            if (UnityEngine.WSA.Application.RunningOnUIThread())
#endif
            {
                return RequestAccessAndInitAsync(token);
            }
#if UNITY_WSA && !UNITY_EDITOR
            else
            {
                UnityEngine.WSA.Application.InvokeOnUIThread(() => RequestAccessAndInitAsync(token), waitUntilDone: true);
                return Task.CompletedTask;
            }
#endif
        }

        /// <summary>
        /// Uninitialize the underlying WebRTC library, effectively cleaning up the allocated peer connection.
        /// </summary>
        /// <remarks>
        /// <see cref="Peer"/> will be <c>null</c> afterward.
        /// </remarks>
        public void Uninitialize()
        {
            if ((_nativePeer != null) && _nativePeer.Initialized)
            {
                // Fire signals before doing anything else to allow listeners to clean-up,
                // including un-registering any callback and remove any track from the connection.
                OnShutdown.Invoke();

                if (Signaler != null)
                {
                    Signaler.OnMessage -= Signaler_OnMessage;
                    Signaler.OnPeerUninitializing(this);
                }

                // Prevent publicly accessing the native peer after it has been deinitialized.
                // This does not prevent systems caching a reference from accessing it, but it
                // is their responsibility to check that the peer is initialized.
                Peer = null;

                // Close the connection and release native resources.
                _nativePeer.Dispose();
                _nativePeer = null;
            }
        }

        #endregion


        #region Unity MonoBehaviour methods

        private void Awake()
        {
            if (_nativePeer == null)
            {
                CreateNativePeerConnection();
            }
        }

        /// <summary>
        /// Unity Engine Start() hook
        /// </summary>
        /// <remarks>
        /// See <see href="https://docs.unity3d.com/ScriptReference/MonoBehaviour.Start.html"/>
        /// </remarks>
        private void Start()
        {
            if (AutoLogErrorsToUnityConsole)
            {
                OnError.AddListener(OnError_Listener);
            }

            // List video capture devices to Unity console
            GetVideoCaptureDevicesAsync().ContinueWith((prevTask) =>
            {
                var devices = prevTask.Result;
                _mainThreadWorkQueue.Enqueue(() =>
                {
                    foreach (var device in devices)
                    {
                        Debug.Log($"Found video capture device '{device.name}' (id:{device.id}).");
                    }
                });
            });

            if (AutoInitializeOnStart)
            {
                InitializeAsync();
            }
        }

        /// <summary>
        /// Unity Engine Update() hook
        /// </summary>
        /// <remarks>
        /// https://docs.unity3d.com/ScriptReference/MonoBehaviour.Update.html
        /// </remarks>
        private void Update()
        {
            // Execute any pending work enqueued by background tasks
            while (_mainThreadWorkQueue.TryDequeue(out Action workload))
            {
                workload();
            }
        }

        /// <summary>
        /// Unity Engine OnDestroy() hook
        /// </summary>
        /// <remarks>
        /// https://docs.unity3d.com/ScriptReference/MonoBehaviour.OnDestroy.html
        /// </remarks>
        private void OnDestroy()
        {
            if (_nativePeer != null)
            {
                _nativePeer.IceCandidateReadytoSend -= Signaler_IceCandidateReadytoSend;
                _nativePeer.LocalSdpReadytoSend -= Signaler_LocalSdpReadyToSend;
                Uninitialize();
            }
            OnError.RemoveListener(OnError_Listener);
        }

        #endregion


        #region Private implementation

        /// <summary>
        /// Create the underlying native peer connection and its C# wrapper, and register
        /// event handlers for signaling.
        /// </summary>
        private void CreateNativePeerConnection()
        {
            Debug.Assert((_nativePeer == null) || !_nativePeer.Initialized);
            _nativePeer = new WebRTC.PeerConnection();
            _nativePeer.LocalSdpReadytoSend += Signaler_LocalSdpReadyToSend;
            _nativePeer.IceCandidateReadytoSend += Signaler_IceCandidateReadytoSend;
        }

        /// <summary>
        /// Internal helper to ensure device access and continue initialization.
        /// </summary>
        /// <remarks>
        /// On UWP this must be called from the main UI thread.
        /// </remarks>
        private Task RequestAccessAndInitAsync(CancellationToken token)
        {
#if UNITY_WSA && !UNITY_EDITOR
            // On UWP the app must have the "webcam" capability, and the user must allow webcam
            // access. So check that access before trying to initialize the WebRTC library, as this
            // may result in a popup window being displayed the first time, which needs to be accepted
            // before the camera can be accessed by WebRTC.
            var mediaAccessRequester = new MediaCapture();
            var mediaSettings = new MediaCaptureInitializationSettings();
            mediaSettings.AudioDeviceId = "";
            mediaSettings.VideoDeviceId = "";
            mediaSettings.StreamingCaptureMode = StreamingCaptureMode.AudioAndVideo;
            mediaSettings.PhotoCaptureSource = PhotoCaptureSource.VideoPreview;
            mediaSettings.SharingMode = MediaCaptureSharingMode.SharedReadOnly; // for MRC and lower res camera
            var accessTask = mediaAccessRequester.InitializeAsync(mediaSettings).AsTask(token);
            return accessTask.ContinueWith(prevTask =>
            {
                token.ThrowIfCancellationRequested();

                if (prevTask.Exception == null)
                {
                    InitializePluginAsync(token);
                }
                else
                {
                    _mainThreadWorkQueue.Enqueue(() =>
                    {
                        OnError.Invoke($"Audio/Video access failure: {prevTask.Exception.Message}.");
                    });
                }
            }, token);
#else
            return InitializePluginAsync(token);
#endif
        }

        /// <summary>
        /// Internal handler to actually initialize the 
        /// </summary>
        private Task InitializePluginAsync(CancellationToken token)
        {
            Debug.Log("Initializing WebRTC plugin...");
            var config = new PeerConnectionConfiguration();
            foreach (var server in IceServers)
            {
                config.IceServers.Add(new IceServer
                {
                    Urls = { server.ToString() },
                    TurnUserName = IceUsername,
                    TurnPassword = IceCredential
                });
            }
            return _nativePeer.InitializeAsync(config, token).ContinueWith((initTask) =>
            {
                token.ThrowIfCancellationRequested();

                if (initTask.Exception != null)
                {
                    _mainThreadWorkQueue.Enqueue(() =>
                    {
                        var errorMessage = new StringBuilder();
                        errorMessage.Append("WebRTC plugin initializing failed. See full log for exception details.\n");
                        Exception ex = initTask.Exception;
                        while (ex is AggregateException ae)
                        {
                            errorMessage.Append($"AggregationException: {ae.Message}\n");
                            ex = ae.InnerException;
                        }
                        errorMessage.Append($"Exception: {ex.Message}");
                        OnError.Invoke(errorMessage.ToString());
                    });
                    throw initTask.Exception;
                }

                _mainThreadWorkQueue.Enqueue(OnPostInitialize);
            }, token);
        }

        /// <summary>
        /// Callback fired on the main Unity app thread once the WebRTC plugin was initialized successfully.
        /// </summary>
        private void OnPostInitialize()
        {
            Debug.Log("WebRTC plugin initialized successfully.");

            if (AutoCreateOfferOnRenegotiationNeeded)
            {
                _nativePeer.RenegotiationNeeded += Peer_RenegotiationNeeded;
            }

            // Once the peer is initialized, it becomes publicly accessible.
            // This prevent scripts from accessing it before it is initialized,
            // or worse before it is constructed in Awake(). This happens because
            // some scripts try to access Peer in OnEnabled(), which won't work
            // if Unity decided to initialize that script before the current one.
            // However subsequent calls will (and should) work as expected.
            Peer = _nativePeer;

            if (Signaler != null)
            {
                Signaler.OnPeerInitialized(this);
                Signaler.OnMessage += Signaler_OnMessage;
            }
            OnInitialized.Invoke();
        }

        private void Signaler_LocalSdpReadyToSend(string type, string sdp)
        {
            var message = new Signaler.Message
            {
                MessageType = Signaler.Message.WireMessageTypeFromString(type),
                Data = sdp
            };
            Signaler.SendMessageAsync(message);
        }

        private void Signaler_IceCandidateReadytoSend(string candidate, int sdpMlineindex, string sdpMid)
        {
            var message = new Signaler.Message
            {
                MessageType = Signaler.Message.WireMessageType.Ice,
                Data = $"{candidate}|{sdpMlineindex}|{sdpMid}",
                IceDataSeparator = "|"
            };
            Signaler.SendMessageAsync(message);
        }

        private async void Signaler_OnMessage(Signaler.Message message)
        {
            switch (message.MessageType)
            {
                case Signaler.Message.WireMessageType.Offer:
                    await _nativePeer.SetRemoteDescriptionAsync("offer", message.Data);
                    // If we get an offer, we immediately send an answer back
                    _nativePeer.CreateAnswer();
                    break;

                case Signaler.Message.WireMessageType.Answer:
                    _ = _nativePeer.SetRemoteDescriptionAsync("answer", message.Data);
                    break;

                case Signaler.Message.WireMessageType.Ice:
                    // TODO - This is NodeDSS-specific
                    // this "parts" protocol is defined above, in OnIceCandiateReadyToSend listener
                    var parts = message.Data.Split(new string[] { message.IceDataSeparator }, StringSplitOptions.RemoveEmptyEntries);
                    // Note the inverted arguments; candidate is last here, but first in OnIceCandiateReadyToSend
                    _nativePeer.AddIceCandidate(parts[2], int.Parse(parts[1]), parts[0]);
                    break;

                default:
                    throw new InvalidOperationException($"Unhandled signaler message type '{message.MessageType}'");
            }
        }

        private void Peer_RenegotiationNeeded()
        {
            // If already connected, update the connection on the fly.
            // If not, wait for user action and don't automatically connect.
            if (AutoCreateOfferOnRenegotiationNeeded && _nativePeer.IsConnected)
            {
                _nativePeer.CreateOffer();
            }
        }

        /// <summary>
        /// Internal handler for on-error, if <see cref="AutoLogErrorsToUnityConsole"/> is <c>true</c>
        /// </summary>
        /// <param name="error">The error message</param>
        private void OnError_Listener(string error)
        {
            Debug.LogError(error);
        }

        #endregion
    }
}
