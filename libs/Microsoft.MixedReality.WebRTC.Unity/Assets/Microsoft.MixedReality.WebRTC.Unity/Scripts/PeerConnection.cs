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
    /// Media line abstraction for a peer connection.
    /// 
    /// This container binds together a sender component (<see cref="MediaSender"/>) and/or a receiver component
    /// (<see cref="MediaReceiver"/>) to a media line, and therefore once a negotiation was completed to the
    /// transceiver attached to that media line.
    /// 
    /// Assigning Unity components to the <see cref="Sender"/> and <see cref="Receiver"/> fields serves
    /// as an indication of the user intent to send and/or receive media through the transceiver, and is
    /// used during negotiation to derive the <see xref="WebRTC.Transceiver.Direction"/> to negotiate.
    /// After the negotiation is completed, the <see cref="Transceiver"/> property refers to the transceiver
    /// associated with this media line, and which the sender and receiver will use.
    /// </summary>
    [Serializable]
    public class MediaLine
    {
        /// <summary>
        /// Backing field to serialize the <see cref="Kind"/> property.
        /// </summary>
        /// <seealso cref="Kind"/>
        [SerializeField]
        private MediaKind _kind;

        /// <summary>
        /// Kind of media of the media line and its attached transceiver.
        ///
        /// This is assiged when the media line is created with <see cref="PeerConnection.AddTransceiver(MediaKind)"/>
        /// and is immutable for the lifetime of the peer connection.
        /// </summary>
        public MediaKind Kind => _kind;

        /// <summary>
        /// Backing field to serialize the <see cref="Sender"/> property.
        /// </summary>
        /// <seealso cref="Sender"/>
        [SerializeField]
        private MediaSender _sender;

        /// <summary>
        /// Media sender producing the media to send through the transceiver attached to this media line.
        /// This must be an instance of a class derived from <see cref="AudioSender"/> or <see cref="VideoSender"/>
        /// depending on <see cref="Kind"/>.
        ///
        /// If this is non-<c>null</c> then the peer connection will negotiate sending some media, otherwise
        /// it will signal the remote peer that it does not wish to send (receive-only or inactive).
        ///
        /// If <see cref="Transceiver"/> is valid, that is a first session negotiation has already been completed,
        /// then changing this value raises a <see cref="WebRTC.PeerConnection.RenegotiationNeeded"/> event on the
        /// peer connection of <see cref="Transceiver"/>.
        /// </summary>
        public MediaSender Sender
        {
            get { return _sender; }
            set
            {
                if (_sender != value)
                {
                    _sender = value;
                    UpdateTransceiverDesiredDirection();
                }
            }
        }

        /// <summary>
        /// Backing field to serialize the <see cref="Receiver"/> property.
        /// </summary>
        /// <seealso cref="Receiver"/>
        [SerializeField]
        private MediaReceiver _receiver;

        /// <summary>
        /// Media receiver consuming the media received through the transceiver attached to this media line.
        /// This must be an instance of a class derived from <see cref="AudioReceiver"/> or <see cref="VideoReceiver"/>
        /// depending on <see cref="Kind"/>.
        ///
        /// If this is non-<c>null</c> then the peer connection will negotiate receiving some media, otherwise
        /// it will signal the remote peer that it does not wish to receive (send-only or inactive).
        ///
        /// If <see cref="Transceiver"/> is valid, that is a first session negotiation has already been conducted,
        /// then changing this value raises a <see cref="WebRTC.PeerConnection.RenegotiationNeeded"/> event on the
        /// peer connection of <see cref="Transceiver"/>.
        /// </summary>
        public MediaReceiver Receiver
        {
            get { return _receiver; }
            set
            {
                if (_receiver != value)
                {
                    _receiver = value;
                    UpdateTransceiverDesiredDirection();
                }
            }
        }

        /// <summary>
        /// Transceiver attached with this media line.
        /// 
        /// On the offering peer this changes during <see cref="PeerConnection.CreateOffer"/>, while this is updated by
        /// <see cref="PeerConnection.SetRemoteDescriptionAsync(string, string)"/> when receiving an offer on the answering peer.
        /// </summary>
        public Transceiver Transceiver { get; private set; }

        /// <summary>
        /// Sender actually attached during <see cref="PeerConnection.SetRemoteDescriptionAsync(string, string)"/>.
        /// This is different from <see cref="Sender"/> until a negotiation is achieved.
        /// </summary>
        [NonSerialized]
        private MediaSender _attachedSender;

        /// <summary>
        /// Receiver actually paired during <see cref="PeerConnection.SetRemoteDescriptionAsync(string, string)"/>.
        /// This is different from <see cref="Receiver"/> until a negotiation is achieved.
        /// </summary>
        [NonSerialized]
        private MediaReceiver _pairedReceiver;

        public MediaLine(MediaKind kind)
        {
            _kind = kind;
        }

        protected void UpdateTransceiverDesiredDirection()
        {
            if (Transceiver != null)
            {
                bool wantsSend = (_sender != null);
                bool wantsRecv = (_receiver != null);
                Transceiver.DesiredDirection = Transceiver.DirectionFromSendRecv(wantsSend, wantsRecv);
            }
        }

        internal async Task AttachTrackAsync()
        {
            Debug.Assert(Transceiver != null);
            Debug.Assert(Sender != null);
            Debug.Assert(_attachedSender == null);
            await Sender.AttachTrackAsync();
            _attachedSender = Sender;
        }

        internal void DetachTrack()
        {
            Debug.Assert(Transceiver != null);
            Debug.Assert(Sender == null);
            Debug.Assert(_attachedSender != null);
            _attachedSender.DetachTrack();
            _attachedSender = null;
        }

        internal void UpdateReceiver()
        {
            var transceiver = Transceiver;
            Debug.Assert(transceiver != null);
            bool wantsRecv = (Receiver != null);
            bool wasReceiving = (_pairedReceiver != null);
            bool isReceiving = (transceiver.RemoteTrack != null);
            // Note the extra "isReceiving" check, which ensures that when the remote track was
            // just removed by OnUnpaired(RemoteTrack) from the TrackRemoved event then it is not
            // immediately re-added by mistake. 
            if (wantsRecv && isReceiving && !wasReceiving)
            {
                // Transceiver started receiving, and user actually wants to receive
                Receiver.OnPaired(transceiver.RemoteTrack);
                _pairedReceiver = Receiver;
            }
            else if (!isReceiving && wasReceiving)
            {
                // Transceiver stopped receiving (user intent does not matter here)
                Receiver.OnUnpaired(transceiver.RemoteTrack);
                _pairedReceiver = null;
            }
        }

        internal void OnUnpaired(RemoteAudioTrack track)
        {
            Debug.Assert(track != null);
            Debug.Assert(Kind == MediaKind.Audio);
            Debug.Assert(Transceiver != null);
            Debug.Assert(Transceiver.RemoteAudioTrack == null); // already removed
            // This is called by the TrackRemoved event, which can be fired sometimes even
            // though we did not have any opportunity yet to pair. So only unpair if we did.
            // In details, the case is the answering peer being in sendonly mode, yet created
            // automatically by the implementation during SetRemoteDescription() in recvonly
            // mode (per the WebRTC spec). So the SetDirection(sendonly) triggers the TrackRemoved
            // event, but the pairing was never done because SetDirection() is called before
            // the received is updated.
            if (_pairedReceiver != null)
            {
                var audioReceiver = (AudioReceiver)_pairedReceiver;
                audioReceiver.OnUnpaired(track);
                _pairedReceiver = null;
            }
        }

        internal void OnUnpaired(RemoteVideoTrack track)
        {
            Debug.Assert(track != null);
            Debug.Assert(Kind == MediaKind.Video);
            Debug.Assert(Transceiver != null);
            Debug.Assert(Transceiver.RemoteTrack == null); // already removed
            // This is called by the TrackRemoved event, which can be fired sometimes even
            // though we did not have any opportunity yet to pair. So only unpair if we did.
            // In details, the case is the answering peer being in sendonly mode, yet created
            // automatically by the implementation during SetRemoteDescription() in recvonly
            // mode (per the WebRTC spec). So the SetDirection(sendonly) triggers the TrackRemoved
            // event, but the pairing was never done because SetDirection() is called before
            // the received is updated.
            if (_pairedReceiver != null)
            {
                var videoReceiver = (VideoReceiver)_pairedReceiver;
                videoReceiver.OnUnpaired(track);
                _pairedReceiver = null;
            }
        }

        internal void PairTransceiverOnFirstOffer(Transceiver tr)
        {
            Debug.Assert(tr != null);
            Debug.Assert((Transceiver == null) || (Transceiver == tr));
            Transceiver = tr;
            UpdateTransceiverDesiredDirection();

            // Always do this even after first offer, because the sender and receiver
            // components can be assigned to the media line later, and therefore will
            // need to be updated on the next offer even if that is not the first one.
            bool wantsSend = (Sender != null);
            bool wantsRecv = (Receiver != null);
            if (Kind == MediaKind.Audio)
            {
                var audioTransceiver = Transceiver;
                if (wantsSend)
                {
                    var audioSender = (AudioSender)Sender;
                    audioSender.AttachToTransceiver(audioTransceiver);
                }
                if (wantsRecv)
                {
                    var audioReceiver = (AudioReceiver)Receiver;
                    audioReceiver.AttachToTransceiver(audioTransceiver);
                }
            }
            else if (Kind == MediaKind.Video)
            {
                var videoTransceiver = Transceiver;
                if (wantsSend)
                {
                    var videoSender = (VideoSender)Sender;
                    videoSender.AttachToTransceiver(videoTransceiver);
                }
                if (wantsRecv)
                {
                    var videoReceiver = (VideoReceiver)Receiver;
                    videoReceiver.AttachToTransceiver(videoTransceiver);
                }
            }
        }

        internal async Task UpdateForCreateOfferAsync(Transceiver tr)
        {
            Debug.Assert(tr != null);

            PairTransceiverOnFirstOffer(tr);

            // Create the local sender track and attach it to the transceiver
            bool wantsSend = (Sender != null);
            bool wasSending = (_attachedSender != null);
            if (wantsSend && !wasSending)
            {
                await AttachTrackAsync();
            }
            else if (!wantsSend && wasSending)
            {
                DetachTrack();
            }

            // The remote track is only created when applying a description, so only attach
            // the transceiver for now (above) but do not try to pair remote tracks.
        }

        internal async Task UpdateOnReceiveOfferAsync(Transceiver tr)
        {
            Debug.Assert(tr != null);

            PairTransceiverOnFirstOffer(tr);

            bool wantsSend = (Sender != null);
            bool wasSending = (_attachedSender != null);
            if (wantsSend && !wasSending)
            {
                // If the offer doesn't allow to send then this will generate a renegotiation needed event,
                // which will be temporarily delayed since we are in the middle of a negotiation already.
                await AttachTrackAsync();
            }
            else if (!wantsSend && wasSending)
            {
                DetachTrack();
            }

            UpdateReceiver();
        }

        internal void UpdateOnReceiveAnswer()
        {
            UpdateReceiver();
        }
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
        [Tooltip("Automatically initialize the peer connection on Start().")]
        [Editor.ToggleLeft]
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


        #region Signaling

        /// <summary>
        /// Signaler to use to establish the connection.
        /// </summary>
        [Header("Signaling")]
        [Tooltip("Signaler to use to establish the connection.")]
        public Signaler Signaler;

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
        /// Internal queue used to marshal work back to the main Unity app thread where access
        /// to Unity objects is allowed. This is generally used to defer events/callbacks, which
        /// are free-threaded in the low-level implementation.
        /// </summary>
        private ConcurrentQueue<Action> _mainThreadWorkQueue = new ConcurrentQueue<Action>();

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
        public Task InitializeAsync(CancellationToken token = default(CancellationToken))
        {
            // Check in case Awake() was called first
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
        /// Add a new transceiver of the given kind to the peer connection.
        /// 
        /// This creates a media line, which expresses an intent from the user to get a transceiver.
        /// The actual <see xref="WebRTC.Transceiver"/> object creation is delayed
        /// until a session negotiation is completed.
        /// 
        /// The user can then assign the <see cref="MediaLine.Sender"/> and <see cref="MediaLine.Receiver"/>
        /// fields to express their intent to send and/or receive some media through the transceiver.
        /// This information is used in subsequent negotiations to derive a <see xref="WebRTC.Transceiver.Direction"/>
        /// to negotiate.
        /// </summary>
        /// <param name="kind">The kind of media (audio or video) for the transceiver.</param>
        /// <returns>A newly created media line for the future transceiver.</returns>
        public MediaLine AddTransceiver(MediaKind kind)
        {
            var ml = new MediaLine(kind);
            _mediaLines.Add(ml);
            return ml;
        }

        /// <summary>
        /// Create a new connection offer, either for a first connection to the remote peer, or for
        /// renegotiating some new or removed tracks.
        /// </summary>
        public bool CreateOffer()
        {
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
            // creating an offer. The remote peer will then match the transceivers by mlineIndex, then add any missing
            // ones, after it applied the offer.
            {
                // In case this is not the first offer, some transceivers might already exist
                var transceivers = _nativePeer.Transceivers;
                int numTransceivers = transceivers.Count;

                for (int mlineIndex = 0; mlineIndex < _mediaLines.Count; ++mlineIndex)
                {
                    var mediaLine = _mediaLines[mlineIndex];

                    // Compute the transceiver desired direction based on what the local peer expects, both in terms
                    // of sending and in terms of receiving. Note that this means the remote peer will not be able to
                    // send any data if the local peer did not add a remote source first.
                    // Tracks are not tested explicitly since the local track can be swapped on-the-fly without renegotiation,
                    // and the remote track is generally not added yet at the beginning of the negotiation, but only when
                    // the remote description is applied (so for the offering side, at the end of the exchange when the
                    // answer is received).
                    bool wantsSend = (mediaLine.Sender != null);
                    bool wantsRecv = (mediaLine.Receiver != null);
                    var wantsDir = (wantsSend ? (wantsRecv ? Transceiver.Direction.SendReceive : Transceiver.Direction.SendOnly)
                        : (wantsRecv ? Transceiver.Direction.ReceiveOnly : Transceiver.Direction.Inactive));

                    // Ensure the media line has a transceiver, and update its desired direction
                    Transceiver tr = null;
                    if (mlineIndex < transceivers.Count)
                    {
                        tr = transceivers[mlineIndex];

                        // If kind mismatch, likely the transceiver order was changed, which is not allowed.
                        // Transceivers are immutably mapped to a unique mid when negotiated for the first time.
                        // The only possible way to change this is stopping a tranceiver and recycling the m= line,
                        // which is not supported by the C# library, so we ignore this case.
                        Debug.Assert(mediaLine.Kind == tr.MediaKind);

                        // Use an existing transceiver created during a previous negotiation
                        tr.DesiredDirection = wantsDir;
                    }
                    else
                    {
                        // Create a new transceiver if none exists
                        var settings = new TransceiverInitSettings
                        {
                            Name = $"mrsw#{mlineIndex}",
                            InitialDesiredDirection = wantsDir
                        };
                        tr = _nativePeer.AddTransceiver(mediaLine.Kind, settings);
                    }
                    Debug.Assert(tr != null);
                    Debug.Assert(tr.MlineIndex == mlineIndex);

                    // Update tracks
                    //< FIXME - CreateOfferAsync() to use await instead of Wait()
                    mediaLine.UpdateForCreateOfferAsync(tr).Wait();
                }

                // Ignore extra transceivers without a registered component to attach
                int numComponents = _mediaLines.Count;
                if (numComponents < numTransceivers)
                {
                    string peerName = name;
                    _mainThreadWorkQueue.Enqueue(() =>
                    {
                        for (int i = numComponents; i < numTransceivers; ++i)
                        {
                            Debug.LogWarning($"Peer connection {peerName} has transceiver #{i} but no sender/receiver component to process it. The transceiver will be ignored.");
                        }
                    });
                }
            }

            // Create the offer
            AutoCreateOfferOnRenegotiationNeeded = true;
            return _nativePeer.CreateOffer();
        }

        /// <summary>
        /// Pass the given SDP description received from the remote peer via signaling to the
        /// underlying WebRTC implementation, which will parse and use it.
        ///
        /// This must be called by the signaler when receiving a message. Once this operation
        /// has completed, it is safe to call <see xref="WebRTC.PeerConnection.CreateAnswer"/>.
        /// 
        /// This implementation does some extra work compared to the lower level implementation
        /// <see xref="WebRTC.PeerConnection.SetRemoteDescriptionAsync(string, string)"/> found
        /// in the C# library, and must be called instead of it when using the Unity integration.
        /// This ensures media receiver components are correctly paired with the remote tracks.
        /// </summary>
        /// <param name="type">The type of SDP message ("offer" or "answer")</param>
        /// <param name="sdp">The content of the SDP message</param>
        /// <returns>Returns a task which completes once the remote description has been applied and transceivers
        /// have been updated.</returns>
        /// <exception xref="InvalidOperationException">The peer connection is not intialized.</exception>
        public Task SetRemoteDescriptionAsync(string type, string sdp)
        {
            // First apply the remote description
            var task = Peer.SetRemoteDescriptionAsync(type, sdp);

            // Once applied, try to pair transceivers and remote tracks with the Unity receiver components
            return task.ContinueWith(async _ =>
            {
                // If receiving an offer, this is the first opportunity for the answering side to add its local tracks
                // (media senders) and any extra transceiver it wants. The offering peer already did that in CreateOffer().
                if (type == "offer")
                {
                    // Remote description has been applied to the peer connection receiving the offer (above). That
                    // created any transceiver that the remote peer wants added, either to send or to receive (or both).
                    // Now match that list with the list of local transceivers that this peer wants added, and add the
                    // ones missing, while updating the transceiver info for existing ones, so that the answer that this
                    // peer will then send matches the reality of the existing Unity components.
                    var transceivers = _nativePeer.Transceivers;
                    int numTransceivers = transceivers.Count;
                    int numExisting = Math.Min(numTransceivers, _mediaLines.Count);

                    // Associate registered media senders/receivers with existing transceivers
                    for (int mlineIndex = 0; mlineIndex < numExisting; ++mlineIndex)
                    {
                        var mediaLine = _mediaLines[mlineIndex];
                        Transceiver tr = transceivers[mlineIndex];

                        // If sending, fix up transceiver direction. Because the remote description was already applied before
                        // any sending transceiver was added (remember, the answering peer didn't add its media senders yet),
                        // all transceivers automatically added are in ReceiveOnly or Inactive state, since the implementation
                        // couldn't associate them with existing senders (none were added yet). This ensures that a TrackAdded
                        // event is fired on the remote peer when it receives the answer.
                        bool wantsSend = (mediaLine.Sender != null);
                        bool wantsRecv = (mediaLine.Receiver != null);
                        {
                            var wantsDir = (wantsSend ? (wantsRecv ? Transceiver.Direction.SendReceive : Transceiver.Direction.SendOnly)
                                : (wantsRecv ? Transceiver.Direction.ReceiveOnly : Transceiver.Direction.Inactive));
                            tr.DesiredDirection = wantsDir;
                        }

                        await mediaLine.UpdateOnReceiveOfferAsync(tr);

                        // Check if the remote peer was planning to send something to this peer, but cannot.
                        if (!wantsRecv)
                        {
                            var desDir = tr.DesiredDirection;
                            if ((desDir == Transceiver.Direction.ReceiveOnly) || (desDir == Transceiver.Direction.SendReceive))
                            {
                                string peerName = name;
                                int idx = mlineIndex;
                                _mainThreadWorkQueue.Enqueue(() =>
                                {
                                    Debug.LogWarning($"The remote peer of peer connection {peerName} offered to send through transceiver #{idx},"
                                        + $" but peer connection {peerName} has no receiver component to process this media. The remote peer's media will not be negotiated."
                                        + $" Ensure that peer connection {peerName} has a receiver component associated with its transceiver #{idx}.");
                                });
                            }
                        }
                    }

                    // Ignore extra transceivers without a registered component to attach
                    if (numExisting < numTransceivers)
                    {
                        string peerName = name;
                        _mainThreadWorkQueue.Enqueue(() =>
                        {
                            for (int mlineIndex = numExisting; mlineIndex < numTransceivers; ++mlineIndex)
                            {
                                Debug.LogWarning($"The remote peer of peer connection {peerName} has transceiver #{mlineIndex}, but the peer connection"
                                    + " doesn't have a local transceiver to pair with it. The remote peer's media for this transceiver will not be negotiated."
                                    + $" Ensure that peer connection {peerName} has transceiver #{mlineIndex} and a receiver component associated with it.");
                            }
                        });
                    }
                }
                else if (type == "answer")
                {
                    // Much simpler, just pair the newly created remote tracks
                    var transceivers = _nativePeer.Transceivers;
                    int numNativeTransceivers = transceivers.Count;
                    int numExisting = Math.Min(numNativeTransceivers, _mediaLines.Count);

                    // Associate registered media senders/receivers with existing transceivers
                    for (int i = 0; i < numExisting; ++i)
                    {
                        var mediaLine = _mediaLines[i];
                        Debug.Assert(mediaLine.Transceiver == transceivers[i]);
                        mediaLine.UpdateOnReceiveAnswer();
                    }

                    // Ignore extra transceivers without a registered component to attach
                    if (numExisting < numNativeTransceivers)
                    {
                        string peerName = name;
                        _mainThreadWorkQueue.Enqueue(() =>
                        {
                            for (int mlineIndex = numExisting; mlineIndex < numNativeTransceivers; ++mlineIndex)
                            {
                                Debug.LogWarning($"The remote peer of peer connection {peerName} has transceiver #{mlineIndex}, but the peer connection"
                                    + " doesn't have a local transceiver to pair with it. The remote peer's media for this transceiver will not be negotiated."
                                    + $" Ensure that peer connection {peerName} has transceiver #{mlineIndex} and a receiver component associated with it.");
                            }
                        });
                    }
                }
            });
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
            // Check in case InitializeAsync() was called first.
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
            // Check that _nativePeer was not already shut down
            if ((Signaler != null) && (_nativePeer != null))
            {
                _nativePeer.IceCandidateReadytoSend -= Signaler_IceCandidateReadytoSend;
                _nativePeer.LocalSdpReadytoSend -= Signaler_LocalSdpReadyToSend;
            }
            Uninitialize();
            OnError.RemoveListener(OnError_Listener);
        }

        #endregion


        #region Private implementation

        /// <summary>
        /// Create a new native peer connection and register event handlers to it.
        /// This does not initialize the peer connection yet.
        /// </summary>
        private void CreateNativePeerConnection()
        {
            // Create the peer connection managed wrapper and its native implementation
            _nativePeer = new WebRTC.PeerConnection();

            // Register event handlers for remote tracks removed (media receivers).
            // There is no point registering events for tracks added because when they
            // are invoked the transceivers have not been paired yet, so there's not much
            // we can do with those events.
            _nativePeer.AudioTrackRemoved += Peer_AudioTrackRemoved;
            _nativePeer.VideoTrackRemoved += Peer_VideoTrackRemoved;

            // Register event handlers for signaling
            if (Signaler != null)
            {
                _nativePeer.LocalSdpReadytoSend += Signaler_LocalSdpReadyToSend;
                _nativePeer.IceCandidateReadytoSend += Signaler_IceCandidateReadytoSend;
            }
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
                    var ex = prevTask.Exception;
                    _mainThreadWorkQueue.Enqueue(() =>
                    {
                        OnError.Invoke($"Audio/Video access failure: {ex.Message}.");
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

                Exception ex = initTask.Exception;
                if (ex != null)
                {
                    _mainThreadWorkQueue.Enqueue(() =>
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

        private void Peer_AudioTrackRemoved(Transceiver transceiver, RemoteAudioTrack track)
        {
            Debug.Assert(track.Transceiver == null); // already removed
            // Note: if the transceiver was created by setting a remote description internally, but
            // the user did not add any MediaLine in the component, then the transceiver is ignored.
            // SetRemoteDescriptionAsync() already triggered a warning in this case, so be silent here.
            MediaLine mediaLine = _mediaLines.Find((MediaLine ml) => ml.Transceiver == transceiver);
            if (mediaLine != null)
            {
                Debug.Assert(mediaLine != null);
                Debug.Assert(mediaLine.Kind == MediaKind.Audio);
                mediaLine.OnUnpaired(track);
            }
        }

        private void Peer_VideoTrackRemoved(Transceiver transceiver, RemoteVideoTrack track)
        {
            Debug.Assert(track.Transceiver == null); // already removed
            // Note: if the transceiver was created by SetRemoteDescription() internally, but the user
            // did not add any MediaLine in the component, then the transceiver is ignored.
            // SetRemoteDescriptionAsync() already triggered a warning in this case, so be silent here.
            MediaLine mediaLine = _mediaLines.Find((MediaLine ml) => ml.Transceiver == transceiver);
            if (mediaLine != null)
            {
                Debug.Assert(mediaLine != null);
                Debug.Assert(mediaLine.Kind == MediaKind.Video);
                mediaLine.OnUnpaired(track);
            }
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
                await SetRemoteDescriptionAsync("offer", message.Data);
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
                // Defer to the main app thread, because this implementation likely will
                // again trigger the renegotiation needed event, which is not re-entrant.
                // This also allows accessing Unity objects, and makes it safer in general
                // for other objects.
                _mainThreadWorkQueue.Enqueue(() => CreateOffer());
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
