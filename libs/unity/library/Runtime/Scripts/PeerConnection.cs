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
using System.Runtime.CompilerServices;

#if UNITY_WSA && !UNITY_EDITOR
using global::Windows.UI.Core;
using global::Windows.Foundation;
using global::Windows.Media.Core;
using global::Windows.Media.Capture;
using global::Windows.ApplicationModel.Core;
#endif

[assembly: InternalsVisibleTo("Microsoft.MixedReality.WebRTC.Unity.Tests.Runtime")]

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
    /// ICE server as a serializable data structure for the Unity inspector.
    /// </summary>
    [Serializable]
    public struct ConfigurableIceServer
    {
        /// <summary>
        /// The type of ICE server.
        /// </summary>
        [Tooltip("Type of ICE server")]
        public IceType Type;

        /// <summary>
        /// The unqualified URI of the server.
        /// </summary>
        /// <remarks>
        /// The URI must not have any <c>stun:</c> or <c>turn:</c> prefix.
        /// </remarks>
        [Tooltip("ICE server URI, without any stun: or turn: prefix.")]
        public string Uri;

        /// <summary>
        /// Convert the server to the representation the underlying implementation use.
        /// </summary>
        /// <returns>The stringified server information.</returns>
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
    /// Exception thrown when an invalid transceiver media kind was detected, generally when trying to pair a
    /// transceiver of one media kind with a media line of a different media kind.
    /// </summary>
    public class InvalidTransceiverMediaKindException : Exception
    {
        /// <inheritdoc/>
        public InvalidTransceiverMediaKindException()
            : base("Invalid transceiver kind.")
        {
        }

        /// <inheritdoc/>
        public InvalidTransceiverMediaKindException(string message)
            : base(message)
        {
        }

        /// <inheritdoc/>
        public InvalidTransceiverMediaKindException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }

    /// <summary>
    /// High-level wrapper for Unity WebRTC functionalities.
    /// This is the API entry point for establishing a connection with a remote peer.
    /// </summary>
    /// <remarks>
    /// The component initializes the underlying <see cref="WebRTC.PeerConnection"/> asynchronously
    /// when enabled, and closes it when disabled. The <see cref="OnInitialized"/> event is called
    /// when the connection object is ready to be used. Call <see cref="StartConnection"/>
    /// to create an offer for a remote peer.
    /// </remarks>
    [AddComponentMenu("MixedReality-WebRTC/Peer Connection")]
    public class PeerConnection : WorkQueue, ISerializationCallbackReceiver
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
        [Tooltip("Automatically create a new offer when receiving a renegotiation needed event.")]
        [Editor.ToggleLeft]
        public bool AutoCreateOfferOnRenegotiationNeeded = true;

        /// <summary>
        /// Flag to log all errors to the Unity console automatically.
        /// </summary>
        [Tooltip("Automatically log all errors to the Unity console.")]
        [Editor.ToggleLeft]
        public bool AutoLogErrorsToUnityConsole = true;

        #endregion


        #region Interactive Connectivity Establishment (ICE)

        /// <summary>
        /// Set of ICE servers the WebRTC library will use to try to establish a connection.
        /// </summary>
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
        /// Underlying native peer connection wrapper.
        /// </summary>
        /// <remarks>
        /// Unlike the public <see cref="Peer"/> property, this is never <c>NULL</c>,
        /// but can be an uninitialized peer.
        /// </remarks>
        private WebRTC.PeerConnection _nativePeer = null;

        /// <summary>
        /// List of transceiver media lines and their associated media sender/receiver components.
        /// </summary>
        [SerializeField]
        private List<MediaLine> _mediaLines = new List<MediaLine>();

        // Indicates if Awake has been called. Used by media lines to figure out whether to
        // invoke callbacks or not.
        internal bool IsAwake { get; private set; }

        #endregion


        #region Public methods

        /// <summary>
        /// Enumerate the video capture devices available as a WebRTC local video feed source.
        /// </summary>
        /// <returns>The list of local video capture devices available to WebRTC.</returns>
        public static Task<IReadOnlyList<VideoCaptureDevice>> GetVideoCaptureDevicesAsync()
        {
            return DeviceVideoTrackSource.GetCaptureDevicesAsync();
        }

        /// <summary>
        /// Initialize the underlying WebRTC peer connection.
        /// </summary>
        /// <remarks>
        /// This method must be called once before using the peer connection. If <see cref="AutoInitializeOnStart"/>
        /// is <c>true</c> then it is automatically called during <a href="https://docs.unity3d.com/ScriptReference/MonoBehaviour.Start.html">MonoBehaviour.Start()</a>.
        ///
        /// This method is asynchronous and completes its task when the initializing completed.
        /// On successful completion, it also trigger the <see cref="OnInitialized"/> event.
        /// Note however that this completion is free-threaded and complete immediately when the
        /// underlying peer connection is initialized, whereas any <see cref="OnInitialized"/>
        /// event handler is invoked when control returns to the main Unity app thread. The former
        /// is faster, but does not allow accessing the underlying peer connection because it
        /// returns before <see cref="OnPostInitialize"/> executed. Therefore it is generally
        /// recommended to listen to the <see cref="OnInitialized"/> event, and ignore the returned
        /// <see xref="System.Threading.Tasks.Task"/> object.
        ///
        /// If the peer connection is already initialized, this method returns immediately with
        /// a <see xref="System.Threading.Tasks.Task.CompletedTask"/> object. The caller can check
        /// that the <see cref="Peer"/> property is non-<c>null</c> to confirm that the connection
        /// is in fact initialized.
        /// </remarks>
        private Task InitializeAsync(CancellationToken token = default(CancellationToken))
        {
            CreateNativePeerConnection();

            // Ensure Android binding is initialized before accessing the native implementation
            Android.Initialize();

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
        /// Add a new media line of the given kind.
        ///
        /// This method creates a media line, which expresses an intent from the user to get a transceiver.
        /// The actual <see xref="WebRTC.Transceiver"/> object creation is delayed until a session
        /// negotiation is completed.
        ///
        /// Once the media line is created, the user can then assign its <see cref="MediaLine.Source"/> and
        /// <see cref="MediaLine.Receiver"/> properties to express their intent to send and/or receive some media
        /// through the transceiver that will be associated with that media line once a session is negotiated.
        /// This information is used in subsequent negotiations to derive a
        /// <see xref="Microsoft.MixedReality.WebRTC.Transceiver.Direction"/> to negotiate. Therefore users
        /// should avoid modifying the <see cref="Transceiver.DesiredDirection"/> property manually when using
        /// the Unity library, and instead modify the <see cref="MediaLine.Source"/> and
        /// <see cref="MediaLine.Receiver"/> properties.
        /// </summary>
        /// <param name="kind">The kind of media (audio or video) for the transceiver.</param>
        /// <returns>A newly created media line, which will be associated with a transceiver once the next session
        /// is negotiated.</returns>
        public MediaLine AddMediaLine(MediaKind kind)
        {
            var ml = new MediaLine(this, kind);
            _mediaLines.Add(ml);
            return ml;
        }

        /// <summary>
        /// Create a new connection offer, either for a first connection to the remote peer, or for
        /// renegotiating some new or removed transceivers.
        ///
        /// This method submits an internal task to create an SDP offer message. Once the message is
        /// created, the implementation raises the <see xref="Microsoft.MixedReality.WebRTC.PeerConnection.LocalSdpReadytoSend"/>
        /// event to allow the user to send the message via the chosen signaling solution to the remote
        /// peer.
        ///
        /// <div class="IMPORTANT alert alert-important">
        /// <h5>IMPORTANT</h5>
        /// <p>
        /// This method is very similar to the <c>CreateOffer()</c> method available in the underlying C# library,
        /// and actually calls it. However it also performs additional work in order to pair the transceivers of
        /// the local and remote peer. Therefore Unity applications must call this method instead of the C# library
        /// one to ensure transceiver pairing works as intended.
        /// </p>
        /// </div>
        /// </summary>
        /// <returns>
        /// <c>true</c> if the offer creation task was submitted successfully, and <c>false</c> otherwise.
        /// The offer SDP message is always created asynchronously.
        /// </returns>
        /// <remarks>
        /// This method can only be called from the main Unity application thread, where Unity objects can
        /// be safely accessed.
        /// </remarks>
        public bool StartConnection()
        {
            // MediaLine manipulates some MonoBehaviour objects when managing senders and receivers
            EnsureIsMainAppThread();

            if (Peer == null)
            {
                throw new InvalidOperationException("Cannot create an offer with an uninitialized peer.");
            }

            // Batch all changes into a single offer
            AutoCreateOfferOnRenegotiationNeeded = false;

            // Add all new transceivers for local tracks. Since transceivers are only paired by negotiated mid,
            // we need to know which peer sends the offer before adding the transceivers on the offering side only,
            // and then pair them on the receiving side. Otherwise they are duplicated, as the transceiver mid from
            // locally-created transceivers is not negotiated yet, so ApplyRemoteDescriptionAsync() won't be able
            // to find them and will re-create a new set of transceivers, leading to duplicates.
            // So we wait until we know this peer is the offering side, and add transceivers to it right before
            // creating an offer. The remote peer will then match the transceivers by index after it applied the offer,
            // then add any missing one.

            // Update all transceivers, whether previously existing or just created above
            var transceivers = _nativePeer.Transceivers;
            int index = 0;
            foreach (var mediaLine in _mediaLines)
            {
                // Ensure each media line has a transceiver
                Transceiver tr = mediaLine.Transceiver;
                if (tr != null)
                {
                    // Media line already had a transceiver from a previous session negotiation
                    Debug.Assert(tr.MlineIndex >= 0); // associated
                }
                else
                {
                    // Create new transceivers for a media line added since last session negotiation.

                    // Compute the transceiver desired direction based on what the local peer expects, both in terms
                    // of sending and in terms of receiving. Note that this means the remote peer will not be able to
                    // send any data if the local peer did not add a remote source first.
                    // Tracks are not tested explicitly since the local track can be swapped on-the-fly without renegotiation,
                    // and the remote track is generally not added yet at the beginning of the negotiation, but only when
                    // the remote description is applied (so for the offering side, at the end of the exchange when the
                    // answer is received).
                    bool wantsSend = (mediaLine.Source != null);
                    bool wantsRecv = (mediaLine.Receiver != null);
                    var wantsDir = Transceiver.DirectionFromSendRecv(wantsSend, wantsRecv);
                    var settings = new TransceiverInitSettings
                    {
                        Name = $"mrsw#{index}",
                        InitialDesiredDirection = wantsDir
                    };
                    tr = _nativePeer.AddTransceiver(mediaLine.MediaKind, settings);
                    try
                    {
                        mediaLine.PairTransceiver(tr);
                    }
                    catch (Exception ex)
                    {
                        LogErrorOnMediaLineException(ex, mediaLine, tr);
                    }
                }
                Debug.Assert(tr != null);
                Debug.Assert(transceivers[index] == tr);
                ++index;
            }

            // Create the offer
            AutoCreateOfferOnRenegotiationNeeded = true;
            return _nativePeer.CreateOffer();
        }

        /// <summary>
        /// Call <see cref="StartConnection"/> and discard the result. Can be wired to a <see cref="UnityEvent"/>.
        /// </summary>
        public void StartConnectionIgnoreError()
        {
            _ = StartConnection();
        }

        /// <summary>
        /// Pass the given SDP description received from the remote peer via signaling to the
        /// underlying WebRTC implementation, which will parse and use it.
        ///
        /// This must be called by the signaler when receiving a message. Once this operation
        /// has completed, it is safe to call <see xref="WebRTC.PeerConnection.CreateAnswer"/>.
        ///
        /// <div class="IMPORTANT alert alert-important">
        /// <h5>IMPORTANT</h5>
        /// <p>
        /// This method is very similar to the <c>SetRemoteDescriptionAsync()</c> method available in the
        /// underlying C# library, and actually calls it. However it also performs additional work in order
        /// to pair the transceivers of the local and remote peer. Therefore Unity applications must call
        /// this method instead of the C# library one to ensure transceiver pairing works as intended.
        /// </p>
        /// </div>
        /// </summary>
        /// <param name="message">The SDP message to handle.</param>
        /// <returns>A task which completes once the remote description has been applied and transceivers
        /// have been updated.</returns>
        /// <exception xref="InvalidOperationException">The peer connection is not intialized.</exception>
        /// <remarks>
        /// This method can only be called from the main Unity application thread, where Unity objects can
        /// be safely accessed.
        /// </remarks>
        public async Task HandleConnectionMessageAsync(SdpMessage message)
        {
            // MediaLine manipulates some MonoBehaviour objects when managing senders and receivers
            EnsureIsMainAppThread();

            if (!isActiveAndEnabled)
            {
                Debug.LogWarning("Message received by disabled PeerConnection");
                return;
            }

            // First apply the remote description
            try
            {
                await Peer.SetRemoteDescriptionAsync(message);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Cannot apply remote description: {ex.Message}");
            }

            // Sort associated transceiver by media line index. The media line index is not the index of
            // the transceiver, but they are both monotonically increasing, so sorting by one or the other
            // yields the same ordered collection, which allows pairing transceivers and media lines.
            // TODO - Ensure PeerConnection.Transceivers is already sorted
            var transceivers = new List<Transceiver>(_nativePeer.AssociatedTransceivers);
            transceivers.Sort((tr1, tr2) => (tr1.MlineIndex - tr2.MlineIndex));
            int numAssociatedTransceivers = transceivers.Count;
            int numMatching = Math.Min(numAssociatedTransceivers, _mediaLines.Count);

            // Once applied, try to pair transceivers and remote tracks with the Unity receiver components
            if (message.Type == SdpMessageType.Offer)
            {
                // Match transceivers with media line, in order
                for (int i = 0; i < numMatching; ++i)
                {
                    var tr = transceivers[i];
                    var mediaLine = _mediaLines[i];
                    if (mediaLine.Transceiver == null)
                    {
                        mediaLine.PairTransceiver(tr);
                    }
                    else
                    {
                        Debug.Assert(tr == mediaLine.Transceiver);
                    }

                    // Associate the transceiver with the media line, if not already done, and associate
                    // the track components of the media line to the tracks of the transceiver.
                    try
                    {
                        mediaLine.UpdateAfterSdpReceived();
                    }
                    catch (Exception ex)
                    {
                        LogErrorOnMediaLineException(ex, mediaLine, tr);
                    }

                    // Check if the remote peer was planning to send something to this peer, but cannot.
                    bool wantsRecv = (mediaLine.Receiver != null);
                    if (!wantsRecv)
                    {
                        var desDir = tr.DesiredDirection;
                        if (Transceiver.HasRecv(desDir))
                        {
                            string peerName = name;
                            int idx = i;
                            InvokeOnAppThread(() => LogWarningOnMissingReceiver(peerName, idx));
                        }
                    }
                }

                // Ignore extra transceivers without a registered component to attach
                if (numMatching < numAssociatedTransceivers)
                {
                    string peerName = name;
                    InvokeOnAppThread(() =>
                    {
                        for (int i = numMatching; i < numAssociatedTransceivers; ++i)
                        {
                            LogWarningOnIgnoredTransceiver(peerName, i);
                        }
                    });
                }
            }
            else if (message.Type == SdpMessageType.Answer)
            {
                // Associate registered media senders/receivers with existing transceivers
                for (int i = 0; i < numMatching; ++i)
                {
                    Transceiver tr = transceivers[i];
                    var mediaLine = _mediaLines[i];
                    Debug.Assert(mediaLine.Transceiver == transceivers[i]);
                    mediaLine.UpdateAfterSdpReceived();
                }

                // Ignore extra transceivers without a registered component to attach
                if (numMatching < numAssociatedTransceivers)
                {
                    string peerName = name;
                    InvokeOnAppThread(() =>
                    {
                        for (int i = numMatching; i < numAssociatedTransceivers; ++i)
                        {
                            LogWarningOnIgnoredTransceiver(peerName, i);
                        }
                    });
                }
            }
        }

        /// <summary>
        /// Uninitialize the underlying WebRTC library, effectively cleaning up the allocated peer connection.
        /// </summary>
        /// <remarks>
        /// <see cref="Peer"/> will be <c>null</c> afterward.
        /// </remarks>
        private void Uninitialize()
        {
            Debug.Assert(_nativePeer.Initialized);
            // Fire signals before doing anything else to allow listeners to clean-up,
            // including un-registering any callback from the connection.
            OnShutdown.Invoke();

            // Prevent publicly accessing the native peer after it has been deinitialized.
            // This does not prevent systems caching a reference from accessing it, but it
            // is their responsibility to check that the peer is initialized.
            Peer = null;

            // Detach all transceivers. This prevents senders/receivers from trying to access
            // them during their clean-up sequence, as transceivers are about to be destroyed
            // by the native implementation.
            foreach (var mediaLine in _mediaLines)
            {
                mediaLine.UnpairTransceiver();
            }

            // Close the connection and release native resources.
            _nativePeer.Dispose();
            _nativePeer = null;
        }

        #endregion


        #region Unity MonoBehaviour methods

        protected override void Awake()
        {
            base.Awake();
            IsAwake = true;
            foreach (var ml in _mediaLines)
            {
                ml.Awake();
            }
        }

        /// <summary>
        /// Unity Engine OnEnable() hook
        /// </summary>
        /// <remarks>
        /// See <see href="https://docs.unity3d.com/ScriptReference/MonoBehaviour.OnEnable.html"/>
        /// </remarks>
        private void OnEnable()
        {
            if (AutoLogErrorsToUnityConsole)
            {
                OnError.AddListener(OnError_Listener);
            }
            InitializeAsync();
        }

        /// <summary>
        /// Unity Engine OnDisable() hook
        /// </summary>
        /// <remarks>
        /// https://docs.unity3d.com/ScriptReference/MonoBehaviour.OnDisable.html
        /// </remarks>
        private void OnDisable()
        {
            Uninitialize();
            OnError.RemoveListener(OnError_Listener);
        }

        private void OnDestroy()
        {
            foreach (var ml in _mediaLines)
            {
                ml.OnDestroy();
            }
        }

        #endregion


        #region Private implementation

        public void OnBeforeSerialize() { }

        public void OnAfterDeserialize()
        {
            foreach (var ml in _mediaLines)
            {
                ml.Peer = this;
            }
        }

        /// <summary>
        /// Create a new native peer connection and register event handlers to it.
        /// This does not initialize the peer connection yet.
        /// </summary>
        private void CreateNativePeerConnection()
        {
            // Create the peer connection managed wrapper and its native implementation
            _nativePeer = new WebRTC.PeerConnection();

            _nativePeer.AudioTrackAdded +=
                (RemoteAudioTrack track) =>
                {
                    // Tracks will be output by AudioReceivers, so avoid outputting them twice.
                    track.OutputToDevice(false);
                };
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
            // FIXME - Use ADM2 instead, this /maybe/ avoids this.
            // On UWP the app must have the "microphone" capability, and the user must allow microphone
            // access. This is due to the audio module (ADM1) being initialized at startup, even if no audio
            // track is used. Preventing access to audio crashes the ADM1 at startup and the entire application.
            var mediaAccessRequester = new MediaCapture();
            var mediaSettings = new MediaCaptureInitializationSettings();
            mediaSettings.AudioDeviceId = "";
            mediaSettings.VideoDeviceId = "";
            mediaSettings.StreamingCaptureMode = StreamingCaptureMode.Audio;
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
                    var ex = prevTask.Exception;
                    InvokeOnAppThread(() => OnError.Invoke($"Audio access failure: {ex.Message}."));
                }
            }, token);
#else
            return InitializePluginAsync(token);
#endif
        }

        /// <summary>
        /// Internal handler to actually initialize the plugin.
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

                Exception ex = initTask.Exception;
                if (ex != null)
                {
                    InvokeOnAppThread(() =>
                    {
                        var errorMessage = new StringBuilder();
                        errorMessage.Append("WebRTC plugin initializing failed. See full log for exception details.\n");
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

                InvokeOnAppThread(OnPostInitialize);
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
            // This prevent scripts from accessing it before it is initialized.
            Peer = _nativePeer;

            OnInitialized.Invoke();
        }

        private void Peer_RenegotiationNeeded()
        {
            // If already connected, update the connection on the fly.
            // If not, wait for user action and don't automatically connect.
            if (AutoCreateOfferOnRenegotiationNeeded && _nativePeer.IsConnected)
            {
                // Defer to the main app thread, because this implementation likely will
                // again trigger the renegotiation needed event, which is not re-entrant.
                // This also allows accessing Unity objects, and makes it safer in general
                // for other objects.
                InvokeOnAppThread(() => StartConnection());
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

        /// <summary>
        /// Log an error when receiving an exception related to a media line and transceiver pairing.
        /// </summary>
        /// <param name="ex">The exception to log.</param>
        /// <param name="mediaLine">The media line associated with the exception.</param>
        /// <param name="transceiver">The transceiver associated with the exception.</param>
        private void LogErrorOnMediaLineException(Exception ex, MediaLine mediaLine, Transceiver transceiver)
        {
            // Dispatch to main thread to access Unity objects to get their names
            InvokeOnAppThread(() =>
            {
                string msg;
                if (ex is InvalidTransceiverMediaKindException)
                {
                    msg = $"Peer connection \"{name}\" received {transceiver.MediaKind} transceiver #{transceiver.MlineIndex} \"{transceiver.Name}\", but local peer expected some {mediaLine.MediaKind} transceiver instead.";
                    if (mediaLine.Source != null)
                    {
                        msg += $" Sender \"{(mediaLine.Source as MonoBehaviour).name}\" will be ignored.";
                    }
                    if (mediaLine.Receiver != null)
                    {
                        msg += $" Receiver \"{(mediaLine.Receiver as MonoBehaviour).name}\" will be ignored.";
                    }
                }
                else
                {
                    // Generic exception, log its message
                    msg = ex.Message;
                }
                Debug.LogError(msg);
            });
        }

        private void LogWarningOnMissingReceiver(string peerName, int trIndex)
        {
            Debug.LogWarning($"The remote peer connected to the local peer connection '{peerName}' offered to send some media"
                + $" through transceiver #{trIndex}, but the local peer connection '{peerName}' has no receiver component to"
                + " process this media. The remote peer's media will be ignored. To be able to receive that media, ensure that"
                + $" the local peer connection '{peerName}' has a receiver component associated with its transceiver #{trIndex}.");
        }

        private void LogWarningOnIgnoredTransceiver(string peerName, int trIndex)
        {
            Debug.LogWarning($"The remote peer connected to the local peer connection '{peerName}' has transceiver #{trIndex},"
                + " but the local peer connection doesn't have a local transceiver to pair with it. The remote peer's media for"
                + " this transceiver will be ignored. To be able to receive that media, ensure that the local peer connection"
                + $" '{peerName}' has transceiver #{trIndex} and a receiver component associated with it.");
        }

        #endregion
    }
}
