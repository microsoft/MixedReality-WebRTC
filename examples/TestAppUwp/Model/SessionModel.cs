// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.MixedReality.WebRTC;
using Windows.ApplicationModel;
using Windows.Media.Capture;
using Windows.UI.Xaml.Controls;

namespace TestAppUwp
{
    /// <summary>
    /// Negotiation state similar to the one described in the WebRTC 1.0 standard.
    ///
    /// Differences are:
    /// - Starts in <see cref="Closed"/> state instead of <see cref="Stable"/> state to
    ///   wait for the peer connection to be initialized.
    /// - Adds <see cref="Starting"/> state in-between the <see cref="Stable"/> state and
    ///   the <see cref="HaveLocalOffer"/> state to prevent creating multiple offers locally.
    /// </summary>
    /// <seealso href="https://www.w3.org/TR/webrtc/#rtcsignalingstate-enum"/>
    public enum NegotiationState
    {
        /// <summary>
        /// Peer connection not initialized yet, or closed. Session cannot be established
        /// in this state.
        /// </summary>
        Closed,

        /// <summary>
        /// Ready to negotiate a new session. This happens once the peer connection is
        /// initialized, and once again after an answer is either created and applied locally
        /// (see <see cref="NotifyLocalAnswerApplied"/>) or received from the remote peer
        /// (see <see cref="ApplyRemoteAnswerAsync(string)"/>.
        /// </summary>
        Stable,

        /// <summary>
        /// New negotiation has been started locally, waiting for offer message to be created.
        /// This happens when calling <see cref="StartNegotiation"/> locally.
        /// </summary>
        Starting,

        /// <summary>
        /// Negotiation started by the local peer, waiting for answer from the remote peer.
        /// This happens after the offer message has been created and applied locally (see
        /// <see cref="NotifyLocalOfferApplied"/>).
        /// </summary>
        HaveLocalOffer,

        /// <summary>
        /// Negotiation started by the remote peer, need to send back an answer.
        /// This happens when a remote offer has been received and applied locally (see
        /// <see cref="ApplyRemoteOfferAsync(string)"/>).
        /// </summary>
        HaveRemoteOffer,
    }

    /// <summary>
    /// Model of the WebRTC session, including the peer connection used to implement it.
    /// </summary>
    public class SessionModel : NotifierBase
    {
        /// <summary>
        /// Predetermined chat data channel ID, negotiated out of band.
        /// </summary>
        private const int ChatChannelID = 42;

        /// <summary>
        /// Singleton instance of the current session model.
        /// </summary>
        public static SessionModel Current
        {
            get
            {
                lock (_singletonLock)
                {
                    if (_current == null)
                    {
                        _current = new SessionModel();
                    }
                    return _current;
                }
            }
        }

        /// <summary>
        /// Is the peer connection successfully initialized and ready to be used?
        /// </summary>
        public bool IsPeerInitialized { get; private set; }

        /// <summary>
        /// SDP semantic to use for establishing a session. Changes only have effect before
        /// the peer connection is initialized with <see cref="InitializePeerConnectionAsync"/>.
        /// </summary>
        public SdpSemantic SdpSemantic
        {
            get { return _sdpSemantic; }
            set { SetProperty(ref _sdpSemantic, value); }
        }

        /// <summary>
        /// ICE server used for NAT punching to establish a session. Changes only have effect before
        /// the peer connection is initialized with <see cref="InitializePeerConnectionAsync"/>.
        /// </summary>
        public IceServer IceServer
        {
            get { return _iceServer; }
            set { SetProperty(ref _iceServer, value); }
        }

        /// <summary>
        /// Signaler used to negotiate sessions.
        /// </summary>
        public NodeDssSignaler NodeDssSignaler { get; } = new NodeDssSignaler();

        /// <summary>
        /// Collection of transceivers of the peer connection associated with this session.
        /// </summary>
        public CollectionViewModel<TransceiverViewModel> Transceivers { get; }
            = new CollectionViewModel<TransceiverViewModel>();

        /// <summary>
        /// Collection of local tracks created and owned by the user, and which can be associated with a transceiver.
        /// </summary>
        /// <remarks>
        /// Updated on main UI thread.
        /// The collection also contains some UI placeholders for the "Add" buttons (<see cref="AddNewTrackViewModel"/>).
        /// </remarks>
        /// <seealso cref="TrackViewModel"/>
        /// <seealso cref="AddNewTrackViewModel"/>
        public ObservableCollection<TrackViewModelBase> LocalTracks = new ObservableCollection<TrackViewModelBase>();

        /// <summary>
        /// Collection of audio tracks known to the session:
        /// - local tracks owned by the session and optionally attached to a transceiver;
        /// - remote tracks attached to their transceiver.
        /// </summary>
        /// <remarks>Updated on main UI thread.</remarks>
        public CollectionViewModel<AudioTrackViewModel> AudioTracks { get; private set; }
            = new CollectionViewModel<AudioTrackViewModel>();

        /// <summary>
        /// Collection of video tracks known to the session:
        /// - local tracks owned by the session and optionally attached to a transceiver;
        /// - remote tracks attached to their transceiver.
        /// </summary>
        /// <remarks>Updated on main UI thread.</remarks>
        public CollectionViewModel<VideoTrackViewModel> VideoTracks { get; private set; }
            = new CollectionViewModel<VideoTrackViewModel>();

        /// <summary>
        /// Collection of chat channels of the current peer connection.
        /// </summary>
        public CollectionViewModel<ChatChannelModel> ChatChannels { get; }
            = new CollectionViewModel<ChatChannelModel>();

        /// <summary>
        /// Enable automated renegotiation offers. This can be disabled when adding multiple tracks,
        /// to bundle all changes together and avoid multiple round trips to the signaler and remote peer.
        /// Alternatively when making batch changes a <see cref="SessionNegotiationDeferral"/> can be
        /// used to temporarily delay the automated renegotiation.
        /// </summary>
        public bool AutomatedNegotiation
        {
            get { return _automatedNegotiation; }
            set { SetProperty(ref _automatedNegotiation, value); }
        }

        /// <summary>
        /// Current session negotiation state.
        /// </summary>
        public NegotiationState NegotiationState
        {
            get
            {
                lock (_stateLock)
                {
                    return _negotiationState;
                }
            }
        }

        /// <summary>
        /// Check whether a new negotiation can be initiated with <see cref="StartNegotiation"/>.
        /// This is mostly informative, as the internal state can change again between this call and the
        /// call to <see cref="StartNegotiation"/>, and make the latter fail.
        /// </summary>
        public bool CanNegotiate
        {
            get
            {
                if (!_signalerReady)
                {
                    return false;
                }
                lock (_stateLock)
                {
                    return (_negotiationState == NegotiationState.Stable);
                }
            }
        }

        /// <summary>
        /// Is the signaler ready for negotiating a new session?
        /// </summary>
        public bool SignalerReady
        {
            get { return _signalerReady; }
        }

        /// <summary>
        /// Current peer connection state.
        /// </summary>
        public IceConnectionState IceConnectionState
        {
            get { return _iceConnectionState; }
            private set { SetProperty(ref _iceConnectionState, value); }
        }

        /// <summary>
        /// Current ICE candidate gathering state.
        /// </summary>
        public IceGatheringState IceGatheringState
        {
            get { return _iceGatheringState; }
            private set { SetProperty(ref _iceGatheringState, value); }
        }

        public string PreferredAudioCodec
        {
            get { return _preferredAudioCodec; }
            set { SetProperty(ref _preferredAudioCodec, value); }
        }

        public string PreferredVideoCodec
        {
            get { return _preferredVideoCodec; }
            set { SetProperty(ref _preferredVideoCodec, value); }
        }

        public string PreferredAudioCodecExtraParamsLocal
        {
            get { return _preferredAudioCodecExtraParamsLocal; }
            set { SetProperty(ref _preferredAudioCodecExtraParamsLocal, value); }
        }

        public string PreferredAudioCodecExtraParamsRemote
        {
            get { return _preferredAudioCodecExtraParamsRemote; }
            set { SetProperty(ref _preferredAudioCodecExtraParamsRemote, value); }
        }

        public string PreferredVideoCodecExtraParamsLocal
        {
            get { return _preferredVideoCodecExtraParamsLocal; }
            set { SetProperty(ref _preferredVideoCodecExtraParamsLocal, value); }
        }

        public string PreferredVideoCodecExtraParamsRemote
        {
            get { return _preferredVideoCodecExtraParamsRemote; }
            set { SetProperty(ref _preferredVideoCodecExtraParamsRemote, value); }
        }

        public PeerConnection.H264Config H264Config = new PeerConnection.H264Config();

        /// <summary>
        /// Helper class to temporarily defer an automated negotiation until this object
        /// is disposed.
        /// </summary>
        /// <seealso cref="GetNegotiationDeferral"/>
        public class SessionNegotiationDeferral : IDisposable
        {
            private SessionModel _session;

            public SessionNegotiationDeferral(SessionModel session)
            {
                _session = session;
            }

            public void Dispose()
            {
                _session.EndDeferral();
            }
        }

        /// <summary>
        /// WebRTC peer connection this session abstracts and uses.
        /// </summary>
        private readonly PeerConnection _peerConnection = new PeerConnection();

        /// <summary>
        /// Is automated negotiation active? If <c>true</c> then a new SDP offer is created
        /// immediately when the peer connection raises a renegotiation needed event.
        /// </summary>
        private bool _automatedNegotiation = false;

        /// <summary>
        /// Is the signaler ready to send session messages? This prevents allowing to start
        /// a negotiation if the signaler is not ready to handle the SDP exchange.
        /// </summary>
        private bool _signalerReady = false;

        private static readonly object _singletonLock = new object();
        private static SessionModel _current = null;

        private SdpSemantic _sdpSemantic = SdpSemantic.UnifiedPlan;
        private IceServer _iceServer;

        private readonly object _stateLock = new object();
        private bool _needsNegotiation = false;
        private NegotiationState _negotiationState = NegotiationState.Closed;
        private int _numNegotiationDeferrals = 0;

        private IceConnectionState _iceConnectionState = IceConnectionState.Closed;
        private IceGatheringState _iceGatheringState = IceGatheringState.New;
        private string _preferredAudioCodec;
        private string _preferredVideoCodec;
        private string _preferredAudioCodecExtraParamsLocal;
        private string _preferredAudioCodecExtraParamsRemote;
        private string _preferredVideoCodecExtraParamsLocal;
        private string _preferredVideoCodecExtraParamsRemote;

        private SessionModel()
        {
            _peerConnection.Connected += OnPeerConnected;
            _peerConnection.LocalSdpReadytoSend += OnLocalSdpReadyToSend;
            _peerConnection.IceCandidateReadytoSend += OnIceCandidateReadyToSend;
            _peerConnection.IceStateChanged += OnIceStateChanged;
            _peerConnection.IceGatheringStateChanged += OnIceGatheringStateChanged;
            _peerConnection.RenegotiationNeeded += OnRenegotiationNeeded;
            _peerConnection.TransceiverAdded += OnTransceiverAdded;
            _peerConnection.AudioTrackAdded += OnRemoteAudioTrackAdded;
            _peerConnection.AudioTrackRemoved += OnRemoteAudioTrackRemoved;
            _peerConnection.VideoTrackAdded += OnRemoteVideoTrackAdded;
            _peerConnection.VideoTrackRemoved += OnRemoteVideoTrackRemoved;
            _peerConnection.DataChannelAdded += OnDataChannelAdded;
            _peerConnection.DataChannelRemoved += OnDataChannelRemoved;

            NodeDssSignaler.OnConnect += DssSignaler_OnConnected;
            NodeDssSignaler.OnDisconnect += DssSignaler_OnDisconnected;
            NodeDssSignaler.OnMessage += DssSignaler_OnMessage;
            NodeDssSignaler.OnFailure += DssSignaler_OnFailure;
            NodeDssSignaler.OnPollingDone += DssSignaler_OnPollingDone;

            LocalTracks.Add(new AddNewTrackViewModel() { DisplayName = "Add audio track", PageType = typeof(AddAudioTrackPage) });
            LocalTracks.Add(new AddNewTrackViewModel() { DisplayName = "Add video track", PageType = typeof(AddVideoTrackPage) });
        }

        /// <summary>
        /// Update the negotiation state from <paramref name="oldState"/> to <paramref name="newState"/>,
        /// while raising the appropriate property changed events for all related properties.
        /// </summary>
        /// <param name="oldState">The expected previous state. Code will assert on this state being current.</param>
        /// <param name="newState">The new state to set <see cref="_negotiationState"/> to.</param>
        private void ExchangeNegotiationState(NegotiationState oldState, NegotiationState newState)
        {
            lock (_stateLock)
            {
                Debug.Assert(_negotiationState == oldState);
                _negotiationState = newState;
            }
            RaisePropertyChanged("NegotiationState");
            if ((oldState == NegotiationState.Stable) || (newState == NegotiationState.Stable))
            {
                RaisePropertyChanged("CanNegotiate");
            }
        }

        /// <summary>
        /// Initialize the peer connection.
        /// </summary>
        /// <returns>A task that completes once the peer connection is ready to be used.</returns>
        public async Task InitializePeerConnectionAsync()
        {
            Logger.Log("Initializing the peer connection...");

            // Cannot run in UI thread on UWP because this will initialize the global factory
            // (first library call) which needs to be done on a background thread.
            await ThreadHelper.RunOnWorkerThread(() => Library.ShutdownOptions = Library.ShutdownOptionsFlags.LogLiveObjects);

            // Initialize the native peer connection object
            try
            {
                var config = new PeerConnectionConfiguration
                {
                    SdpSemantic = _sdpSemantic,
                    IceServers = new List<IceServer> { _iceServer }
                };
                await _peerConnection.InitializeAsync(config);
                IsPeerInitialized = true;
                RaisePropertyChanged("IsPeerInitialized");
            }
            catch (Exception ex)
            {
                Logger.Log($"WebRTC native plugin init failed: {ex.Message}");
                throw ex;
            }
            Logger.Log("Peer connection initialized.");
            OnPeerInitialized();

            // It is CRUCIAL to add any data channel BEFORE the SDP offer is sent, if data channels are
            // to be used at all. Otherwise the SCTP will not be negotiated, and then all channels will
            // stay forever in the kConnecting state.
            // https://stackoverflow.com/questions/43788872/how-are-data-channels-negotiated-between-two-peers-with-webrtc
            await _peerConnection.AddDataChannelAsync(ChatChannelID, "chat", true, true);

            //_videoPlayer.CurrentStateChanged += OnMediaStateChanged;
            //_videoPlayer.MediaOpened += OnMediaOpened;
            //_videoPlayer.MediaFailed += OnMediaFailed;
            //_videoPlayer.MediaEnded += OnMediaEnded;
            //_videoPlayer.RealTimePlayback = true;
            //_videoPlayer.AutoPlay = false;

            // Bind the XAML UI control (videoPlayerElement) to the MediaFoundation rendering pipeline (_videoPlayer)
            // so that the former can render in the UI the video frames produced in the background by the latter.
            //videoPlayerElement.SetMediaPlayer(_videoPlayer);

            //// Uncomment to initialize local transceivers and tracks.
            //if (Utils.IsFirstInstance())
            //{
            //    // Add transceivers
            //    var transceiverA = AddTransceiver(MediaKind.Audio,
            //        new TransceiverInitSettings { Name = "audio_transceiver" });

            //    var transceiverV = AddTransceiver(MediaKind.Video,
            //        new TransceiverInitSettings { Name = "video_transceiver", });

            //    // Add audio track
            //    var sourceA = await DeviceAudioTrackSource.CreateAsync(new LocalAudioDeviceInitConfig());
            //    var trackA = LocalAudioTrack.CreateFromSource(sourceA,
            //        new LocalAudioTrackInitConfig { trackName = "local_audio" });
            //    AddAudioTrack(trackA, "Audio Device");

            //    // Add the track to the transceiver.
            //    {
            //        var transceiverVM = Transceivers.First(t => t.Transceiver == transceiverA);
            //        var trackVM = transceiverVM.AvailableSenders.Last();
            //        transceiverVM.Sender = trackVM;
            //    }

            //    // Add video track
            //    var sourceV = await DeviceVideoTrackSource.CreateAsync(
            //        new LocalVideoDeviceInitConfig
            //        {
            //            videoDevice = new VideoCaptureDevice
            //            {
            //                id = @"<insert_device_id>"
            //            },
            //            videoProfileId = string.Empty,
            //            width = 640,
            //            height = 480,
            //            framerate = 30
            //        });
            //    // Crate the track
            //    var trackV = LocalVideoTrack.CreateFromSource(sourceV,
            //        new LocalVideoTrackInitConfig { trackName = "local_video" });
            //    AddVideoTrack(trackV, "Video Device");

            //    // Add the track to the transceiver.
            //    {
            //        var transceiverVM = Transceivers.First(t => t.Transceiver == transceiverV);
            //        var trackVM = transceiverVM.AvailableSenders.Last();
            //        transceiverVM.Sender = trackVM;
            //    }
            //}
        }

        public Transceiver AddTransceiver(MediaKind mediaKind, TransceiverInitSettings settings)
        {
            // This will raise the TransceiverAdded event, which adds a new view model
            // for the newly added transceiver automatically.
            return _peerConnection.AddTransceiver(mediaKind, settings);
        }

        /// <summary>
        /// Start a new session negotiation.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The session is not in an internal state where a new negotiation can be started, either because
        /// the signaler is not ready (see <see cref="SignalerReady"/>), or because the internal negotiation
        /// state is not <see cref="NegotiationState.Stable"/> meaning that another session negotiation is
        /// already in process.
        /// </exception>
        public void StartNegotiation()
        {
            if (!_signalerReady)
            {
                throw new InvalidOperationException($"Cannot start a new session negotiation before the signaler is ready.");
            }
            lock (_stateLock)
            {
                if (_negotiationState != NegotiationState.Stable)
                {
                    throw new InvalidOperationException($"Cannot start a new session negotiation in {_negotiationState} state.");
                }
                _negotiationState = NegotiationState.Starting;
                _needsNegotiation = false;
                // FIXME - Race condition here; if receiving a RenegotiationNeeded event between this point
                // and the moment the peer connection actually starts creating the offer message. In that case
                // _needsNegotiation will become true, but might conceptually be false since the change that
                // raised the event will be taken into account in the call to CreateOffer() below. This is because
                // we are crafting a state machine in C# instead of taping into the native implementation one.
            }
            RaisePropertyChanged("NegotiationState");
            RaisePropertyChanged("CanNegotiate");

            // TODO - Use per-transceiver properties instead of per-connection ones
            // FIXME - null string crashes, need to pass empty string
            _peerConnection.PreferredAudioCodec = PreferredAudioCodec ?? "";
            _peerConnection.PreferredAudioCodecExtraParamsLocal = PreferredAudioCodecExtraParamsLocal ?? "";
            _peerConnection.PreferredAudioCodecExtraParamsRemote = PreferredAudioCodecExtraParamsRemote ?? "";
            _peerConnection.PreferredVideoCodec = PreferredVideoCodec ?? "";
            _peerConnection.PreferredVideoCodecExtraParamsLocal = PreferredVideoCodecExtraParamsLocal ?? "";
            _peerConnection.PreferredVideoCodecExtraParamsRemote = PreferredVideoCodecExtraParamsRemote ?? "";

            PeerConnection.SetH264Config(H264Config);

            // This cannot be inside the lock, otherwise the UI thread has the lock and block on the WebRTC
            // signaling thread, which itself might invoked a callback into the UI thread which might require
            // the lock, in which case we'd have a deadlock.
            _peerConnection.CreateOffer();
        }

        /// <summary>
        /// Notify the view model that the peer connection was initialized and is ready.
        /// </summary>
        public void OnPeerInitialized()
        {
            ExchangeNegotiationState(NegotiationState.Closed, NegotiationState.Stable);
        }

        /// <summary>
        /// Callback invoked when an answer is available. This does not necesarilly indicate
        /// the remote peer has received this answer, if it was generated locally. But the
        /// local peer at least is in a state where the SDP session exchange is completed on
        /// its end. WebRTC does not have any built-in way to ensure the remote peer has also
        /// receveived the answer and finished the session exchange.
        /// </summary>
        private void OnPeerConnected()
        {
        }

        private void OnLocalSdpReadyToSend(SdpMessage message)
        {
            Logger.Log($"Local {message.Type} ready to be sent to remote peer.");
            if (message.Type == SdpMessageType.Offer)
            {
                NotifyLocalOfferApplied();
            }
            if (message.Type == SdpMessageType.Answer)
            {
                NotifyLocalAnswerApplied();
            }
            var dssMessage = NodeDssSignaler.Message.FromSdpMessage(message);
            NodeDssSignaler.SendMessageAsync(dssMessage);
            //RunOnMainThread(() => negotiationStatusText.Text = (type == "offer" ? "Sending local offer" : "Idle (answer sent)"));
        }

        private void OnIceCandidateReadyToSend(IceCandidate candidate)
        {
            var message = NodeDssSignaler.Message.FromIceCandidate(candidate);
            NodeDssSignaler.SendMessageAsync(message);
        }

        private void OnIceStateChanged(IceConnectionState newState)
        {
            Logger.Log($"Connection state changed to {newState}.");
            IceConnectionState = newState;

            // ICE was disconnected, which generally indicates that the remote peer was closed.
            // Shut down the remote media player and clear the remote video statistics.
            if (newState == IceConnectionState.Disconnected)
            {
                //OnPeerDisconnected();
            }
        }

        private void OnIceGatheringStateChanged(IceGatheringState newState)
        {
            Logger.Log($"ICE gathering changed to {newState}.");
            IceGatheringState = newState;
        }

        private void OnPeerDisconnected()
        {
            Logger.Log($"Peer disconnected.");

            //// Update the video source if needed
            //var selectedVideoTrack = LocalVideoTracks.SelectedItem;
            //if (selectedVideoTrack.IsRemote)
            //{

            //    _remoteVideoPlayer.Pause();
            //    _remoteVideoPlayer.Source = null;
            //    remoteVideo.SetMediaPlayer(null);
            //    _remoteVideoSource?.NotifyError(MediaStreamSourceErrorStatus.ConnectionToServerLost);
            //    _remoteMediaSource?.Dispose();
            //}

            ThreadHelper.RunOnMainThread(() =>
            {
                // Remove all remote tracks
                for (int i = AudioTracks.Count - 1; i >= 0; --i)
                {
                    if (AudioTracks[i].IsRemote)
                    {
                        AudioTracks.RemoveAt(i);
                    }
                }
                for (int i = VideoTracks.Count - 1; i >= 0; --i)
                {
                    if (VideoTracks[i].IsRemote)
                    {
                        VideoTracks.RemoveAt(i);
                    }
                }
            });
        }

        /// <summary>
        /// Notify the peer connection that a local SDP offer message was applied.
        /// </summary>
        public void NotifyLocalOfferApplied()
        {
            ExchangeNegotiationState(NegotiationState.Starting, NegotiationState.HaveLocalOffer);
        }

        /// <summary>
        /// Notify the peer connection that a local SDP answer message was applied.
        /// </summary>
        public void NotifyLocalAnswerApplied()
        {
            ExchangeNegotiationState(NegotiationState.HaveRemoteOffer, NegotiationState.Stable);
        }

        /// <summary>
        /// Apply a remote SDP offer message to the peer connection.
        /// </summary>
        /// <param name="content">The SDP offer message content.</param>
        public async Task ApplyRemoteOfferAsync(string content)
        {
            // Change state first to prevent user from initiating a new negotiation
            // while the remote peer is known to have already initiated one.
            // This slightly deviates from the WebRTC standard which says that the
            // HaveRemoteOffer state is after the remote offer was applied, not before.
            ExchangeNegotiationState(NegotiationState.Stable, NegotiationState.HaveRemoteOffer);
            var message = new SdpMessage { Type = SdpMessageType.Offer, Content = content };
            await _peerConnection.SetRemoteDescriptionAsync(message);
        }

        /// <summary>
        /// Apply a remote SDP answer message to the peer connection.
        /// </summary>
        /// <param name="content">The SDP answer message content.</param>
        public async Task ApplyRemoteAnswerAsync(string content)
        {
            var message = new SdpMessage { Type = SdpMessageType.Answer, Content = content };
            await _peerConnection.SetRemoteDescriptionAsync(message);

            ExchangeNegotiationState(NegotiationState.HaveLocalOffer, NegotiationState.Stable);
        }

        /// <summary>
        /// Get a deferral to temporarily suspend automated session negotiation until
        /// a batch of changes has been conducted. This is typically used when modifying
        /// multiple transceivers at once while using automated negotiation, to avoid the
        /// very first modification starting a negotiation before all changes have been
        /// applied locally.
        /// </summary>
        /// <returns>A deferral temporarily blocking any automated negotiation until disposed.</returns>
        /// <remarks>
        /// This is typically used in a <c>using</c> block:
        /// <code>
        /// using (session.GetNegotiationDeferral())
        /// {
        ///     // Add multiple transceivers...
        /// }
        /// </code>
        /// </remarks>
        public SessionNegotiationDeferral GetNegotiationDeferral()
        {
            var deferral = new SessionNegotiationDeferral(this);
            lock (_stateLock)
            {
                ++_numNegotiationDeferrals;
            }
            return deferral;
        }

        private void EndDeferral()
        {
            lock (_stateLock)
            {
                --_numNegotiationDeferrals;
                if ((_numNegotiationDeferrals == 0) && (_negotiationState == NegotiationState.Stable)
                    && _needsNegotiation && _automatedNegotiation)
                {
                    StartNegotiation();
                }
            }
        }

        private void OnRenegotiationNeeded()
        {
            lock (_stateLock)
            {
                _needsNegotiation = true;

                if ((_numNegotiationDeferrals == 0) && (_negotiationState == NegotiationState.Stable)
                    && _automatedNegotiation && _signalerReady)
                {
                    StartNegotiation();
                }
            }
        }

        /// <summary>
        /// Callback invoked when a transceiver is added to the peer connection, either from
        /// applying a remote offer/answer, or from manually adding it. In the latter case, the
        /// transceiver has already a view model in the pending collection. In the former case,
        /// create a view model for it and add it to the negotiated collection.
        /// </summary>
        /// <param name="transceiver">The newly added transceiver.</param>
        private void OnTransceiverAdded(Transceiver transceiver)
        {
            Logger.Log($"Added transceiver '{transceiver.Name}' (mid=#{transceiver.MlineIndex}).");
            ThreadHelper.RunOnMainThread(() =>
            {
                var trvm = new TransceiverViewModel(transceiver);
                Transceivers.Add(trvm);
            });
        }


        /// <summary>
        /// Callback on remote audio track added.
        ///
        /// For simplicity this grabs the first remote audio track found. However currently the user has no
        /// control over audio output, so this is only used for audio statistics.
        /// </summary>
        /// <param name="track">The audio track added.</param>
        /// <seealso cref="RemoteAudioTrack_FrameReady"/>
        private void OnRemoteAudioTrackAdded(RemoteAudioTrack track)
        {
            Logger.Log($"Added remote audio track {track.Name} of transceiver {track.Transceiver.Name}.");

            ThreadHelper.RunOnMainThread(() =>
            {
                var trvm = Transceivers.SingleOrDefault(tr => tr.Transceiver == track.Transceiver);
                Debug.Assert(trvm.Transceiver.RemoteAudioTrack == track);
                trvm.NotifyReceiverChanged(); // this is thread-aware
                // This raises property changed events in current thread, needs to be main one
                AudioTracks.Add(new AudioTrackViewModel(track));
            });
        }

        /// <summary>
        /// Callback on remote videa track added.
        ///
        /// For simplicity this grabs the first remote video track found and uses it to render its
        /// content on the right pane of the Tracks window.
        /// </summary>
        /// <param name="track">The video track added.</param>
        /// <seealso cref="RemoteVideoTrack_I420AFrameReady"/>
        private void OnRemoteVideoTrackAdded(RemoteVideoTrack track)
        {
            Logger.Log($"Added remote video track {track.Name} of transceiver {track.Transceiver.Name}.");

            ThreadHelper.RunOnMainThread(() =>
            {
                var trvm = Transceivers.SingleOrDefault(tr => tr.Transceiver == track.Transceiver);
                Debug.Assert(trvm.Transceiver.RemoteVideoTrack == track);
                trvm.NotifyReceiverChanged(); // this is thread-aware
                // This raises property changed events in current thread, needs to be main one
                VideoTracks.Add(new VideoTrackViewModel(track));
            });
        }

        /// <summary>
        /// Callback on remote audio track removed.
        /// </summary>
        /// <param name="track">The audio track removed.</param>
        private void OnRemoteAudioTrackRemoved(Transceiver transceiver, RemoteAudioTrack track)
        {
            Logger.Log($"Removed remote audio track {track.Name} from transceiver {transceiver.Name}.");

            ThreadHelper.RunOnMainThread(() =>
            {
                var atvm = AudioTracks.Single(vm => vm.TrackImpl == track);
                AudioTracks.Remove(atvm);
            });

            //IAudioTrack newPlaybackAudioTrack = null;
            //if (LocalAudioTracks.Count > 0)
            //{
            //    newPlaybackAudioTrack = LocalAudioTracks[0].Track;
            //}
            //SwitchMediaPlayerSource(newPlaybackAudioTrack, _playbackVideoTrack);
        }

        /// <summary>
        /// Callback on remote video track removed.
        /// </summary>
        /// <param name="track">The video track removed.</param>
        private void OnRemoteVideoTrackRemoved(Transceiver transceiver, RemoteVideoTrack track)
        {
            Logger.Log($"Removed remote video track {track.Name} from transceiver {transceiver.Name}.");

            ThreadHelper.RunOnMainThread(() =>
            {
                var vtvm = VideoTracks.Single(vm => vm.TrackImpl == track);
                VideoTracks.Remove(vtvm);
            });

            //IVideoTrack newPlaybackVideoTrack = null;
            //if (LocalVideoTracks.Count > 0)
            //{
            //    newPlaybackVideoTrack = LocalVideoTracks[0].Track;
            //}
            //else
            //{
            //    videoTrackComboBox.IsEnabled = false;
            //    _videoStatsTimer.Stop();
            //}
            //SwitchMediaPlayerSource(_playbackAudioTrack, newPlaybackVideoTrack);
        }

        private void OnDataChannelAdded(DataChannel channel)
        {
            Logger.Log($"Added data channel '{channel.Label}' (#{channel.ID}).");
            ThreadHelper.RunOnMainThread(() =>
            {
                var chat = new ChatChannelModel(channel);
                ChatChannels.Add(chat);
            });
        }

        private void OnDataChannelRemoved(DataChannel channel)
        {
            Logger.Log($"Removed data channel '{channel.Label}' (#{channel.ID}).");
            // FIXME - Delegating to another thread means possible race condition if user initiates
            // some action on the data channel before the delegated remove executes.
            ThreadHelper.RunOnMainThread(() =>
            {
                var chat = ChatChannels.Where(c => c.DataChannel == channel).First();
                ChatChannels.Remove(chat);
            });
        }

        private void DssSignaler_OnConnected()
        {
            if (SetProperty(ref _signalerReady, true, "SignalerReady"))
            {
                RaisePropertyChanged("CanNegotiate");
            }
        }

        private void DssSignaler_OnDisconnected()
        {
            if (SetProperty(ref _signalerReady, false, "SignalerReady"))
            {
                RaisePropertyChanged("CanNegotiate");
            }
        }

        private async void DssSignaler_OnMessage(NodeDssSignaler.Message message)
        {
            Logger.Log($"DSS received message: {message.MessageType}");

            // Ensure that the filtering values are up to date before passing the message on.
            //UpdateCodecFilters();
            //_peerConnection.PreferredAudioCodec = PreferredAudioCodec;
            //_peerConnection.PreferredAudioCodecExtraParamsRemote = PreferredAudioCodecExtraParamsRemoteTextBox.Text;
            //_peerConnection.PreferredAudioCodecExtraParamsLocal = PreferredAudioCodecExtraParamsLocalTextBox.Text;
            //_peerConnection.PreferredVideoCodec = PreferredVideoCodec;
            //_peerConnection.PreferredVideoCodecExtraParamsRemote = PreferredVideoCodecExtraParamsRemoteTextBox.Text;
            //_peerConnection.PreferredVideoCodecExtraParamsLocal = PreferredVideoCodecExtraParamsLocalTextBox.Text;

            switch (message.MessageType)
            {
                case NodeDssSignaler.Message.WireMessageType.Offer:
                    await ApplyRemoteOfferAsync(message.Data);
                    // If we get an offer, we immediately send an answer back once the offer is applied
                    _peerConnection.CreateAnswer();
                    break;

                case NodeDssSignaler.Message.WireMessageType.Answer:
                    await ApplyRemoteAnswerAsync(message.Data);
                    break;

                case NodeDssSignaler.Message.WireMessageType.Ice:
                    // TODO - This is NodeDSS-specific
                    _peerConnection.AddIceCandidate(message.ToIceCandidate());
                    break;

                default:
                    throw new InvalidOperationException($"Unhandled signaler message type '{message.MessageType}'");
            }
        }

        private void DssSignaler_OnFailure(Exception e)
        {
            Logger.Log($"DSS polling failed: {e.Message}");

            //// TODO - differentiate between StartPollingAsync() failure and SendMessageAsync() ones!
            //if (_dssSignaler.IsPolling)
            //{
            //    Logger.Log($"Cancelling polling DSS server...");
            //    //pollDssButton.IsEnabled = false; // will be re-enabled when cancellation is completed, see DssSignaler_OnPollingDone
            //    //pollDssButton.Content = "Start polling";
            //    _dssSignaler.StopPollingAsync();
            //}
        }

        private void DssSignaler_OnPollingDone()
        {
            Logger.Log($"DSS polling done.");

            //_isDssPolling = false;
            ////pollDssButton.IsEnabled = true;
            //Logger.Log($"Polling DSS server stopped.");
        }

        public void AddAudioTrack(LocalAudioTrack track, string deviceName)
        {
            ThreadHelper.EnsureIsMainThread();
            AudioTracks.Add(new AudioTrackViewModel(track, deviceName));
            LocalTracks.Add(new LocalTrackViewModel(Symbol.Volume) { DisplayName = deviceName });
        }

        public void AddVideoTrack(LocalVideoTrack track, string deviceName)
        {
            ThreadHelper.EnsureIsMainThread();
            VideoTracks.Add(new VideoTrackViewModel(track, deviceName));
            LocalTracks.Add(new LocalTrackViewModel(Symbol.Video) { DisplayName = deviceName });
        }
    }

    internal static class Utils
    {

        /// <summary>
        /// Check if this application instance is the first one launched on the host device.
        /// </summary>
        /// <returns><c>true</c> if the current application instance is the first and therefore only instance.</returns>
        internal static bool IsFirstInstance()
        {
            var firstInstance = AppInstance.FindOrRegisterInstanceForKey("{44CD414E-B604-482E-8CFD-A9E09076CABD}");
            return firstInstance.IsCurrentInstance;
        }

        internal static async Task RequestMediaAccessAsync(StreamingCaptureMode mode)
        {
            // Ensure that the UWP app was authorized to capture audio (cap:microphone)
            // or video (cap:webcam), otherwise the native plugin will fail.
            try
            {
                MediaCapture mediaAccessRequester = new MediaCapture();
                var mediaSettings = new MediaCaptureInitializationSettings
                {
                    AudioDeviceId = "",
                    VideoDeviceId = "",
                    StreamingCaptureMode = mode,
                    PhotoCaptureSource = PhotoCaptureSource.VideoPreview
                };
                await mediaAccessRequester.InitializeAsync(mediaSettings);
            }
            catch (UnauthorizedAccessException uae)
            {
                Logger.Log("Access to A/V denied, check app permissions: " + uae.Message);
                throw uae;
            }
            catch (Exception ex)
            {
                Logger.Log("Failed to initialize A/V with unknown exception: " + ex.Message);
                throw ex;
            }
        }
    }
}
