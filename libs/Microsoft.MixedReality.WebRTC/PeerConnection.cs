// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.MixedReality.WebRTC.Interop;
using Microsoft.MixedReality.WebRTC.Tracing;

[assembly: InternalsVisibleTo("Microsoft.MixedReality.WebRTC.Tests")]

namespace Microsoft.MixedReality.WebRTC
{
    /// <summary>
    /// Type of ICE candidates offered to the remote peer.
    /// </summary>
    public enum IceTransportType : int
    {
        /// <summary>
        /// No ICE candidate offered.
        /// </summary>
        None = 0,

        /// <summary>
        /// Only advertize relay-type candidates, like TURN servers, to avoid leaking the IP address of the client.
        /// </summary>
        Relay = 1,

        /// ?
        NoHost = 2,

        /// <summary>
        /// Offer all types of ICE candidates.
        /// </summary>
        All = 3
    }

    /// <summary>
    /// Bundle policy.
    /// See https://www.w3.org/TR/webrtc/#rtcbundlepolicy-enum.
    /// </summary>
    public enum BundlePolicy : int
    {
        /// <summary>
        /// Gather ICE candidates for each media type in use (audio, video, and data). If the remote endpoint is
        /// not bundle-aware, negotiate only one audio and video track on separate transports.
        /// </summary>
        Balanced = 0,

        /// <summary>
        /// Gather ICE candidates for only one track. If the remote endpoint is not bundle-aware, negotiate only
        /// one media track.
        /// </summary>
        MaxBundle = 1,

        /// <summary>
        /// Gather ICE candidates for each track. If the remote endpoint is not bundle-aware, negotiate all media
        /// tracks on separate transports.
        /// </summary>
        MaxCompat = 2
    }

    /// <summary>
    /// SDP semantic used for (re)negotiating a peer connection.
    /// </summary>
    public enum SdpSemantic : int
    {
        /// <summary>
        /// Unified plan, as standardized in the WebRTC 1.0 standard.
        /// </summary>
        UnifiedPlan = 0,

        /// <summary>
        /// Legacy Plan B, deprecated and soon removed.
        /// Only available for compatiblity with older implementations if needed.
        /// Do not use unless there is a problem with the Unified Plan.
        /// </summary>
        PlanB = 1
    }

    /// <summary>
    /// ICE server configuration (STUN and/or TURN).
    /// </summary>
    public class IceServer
    {
        /// <summary>
        /// List of TURN and/or STUN server URLs to use for NAT bypass, in order of preference.
        ///
        /// The scheme is defined in the core WebRTC implementation, and is in short:
        /// stunURI     = stunScheme ":" stun-host [ ":" stun-port ]
        /// stunScheme  = "stun" / "stuns"
        /// turnURI     = turnScheme ":" turn-host [ ":" turn-port ] [ "?transport=" transport ]
        /// turnScheme  = "turn" / "turns"
        /// </summary>
        public List<string> Urls = new List<string>();

        /// <summary>
        /// Optional TURN server username.
        /// </summary>
        public string TurnUserName = string.Empty;

        /// <summary>
        /// Optional TURN server credentials.
        /// </summary>
        public string TurnPassword = string.Empty;

        /// <summary>
        /// Format the ICE server data according to the encoded marshalling of the C++ API.
        /// </summary>
        /// <returns>The encoded string of ICE servers.</returns>
        public override string ToString()
        {
            if (Urls == null)
            {
                return string.Empty;
            }
            string ret = string.Join("\n", Urls);
            if (!string.IsNullOrEmpty(TurnUserName))
            {
                ret += $"\nusername:{TurnUserName}";
                if (!string.IsNullOrEmpty(TurnPassword))
                {
                    ret += $"\npassword:{TurnPassword}";
                }
            }
            return ret;
        }
    }

    /// <summary>
    /// Configuration to initialize a <see cref="PeerConnection"/>.
    /// </summary>
    public class PeerConnectionConfiguration
    {
        /// <summary>
        /// List of TURN and/or STUN servers to use for NAT bypass, in order of preference.
        /// </summary>
        public List<IceServer> IceServers = new List<IceServer>();

        /// <summary>
        /// ICE transport policy for the connection.
        /// </summary>
        public IceTransportType IceTransportType = IceTransportType.All;

        /// <summary>
        /// Bundle policy for the connection.
        /// </summary>
        public BundlePolicy BundlePolicy = BundlePolicy.Balanced;

        /// <summary>
        /// SDP semantic for the connection.
        /// </summary>
        /// <remarks>Plan B is deprecated, do not use it.</remarks>
        public SdpSemantic SdpSemantic = SdpSemantic.UnifiedPlan;
    }

    /// <summary>
    /// State of an ICE connection.
    /// </summary>
    /// <remarks>
    /// Due to the underlying implementation, this is currently a mix of the
    /// <see href="https://www.w3.org/TR/webrtc/#rtcicegatheringstate-enum">RTPIceGatheringState</see>
    /// and the <see href="https://www.w3.org/TR/webrtc/#rtcpeerconnectionstate-enum">RTPPeerConnectionState</see>
    /// from the WebRTC 1.0 standard.
    /// </remarks>
    /// <seealso href="https://www.w3.org/TR/webrtc/#rtcicegatheringstate-enum"/>
    /// <seealso href="https://www.w3.org/TR/webrtc/#rtcpeerconnectionstate-enum"/>
    public enum IceConnectionState : int
    {
        /// <summary>
        /// Newly created ICE connection. This is the starting state.
        /// </summary>
        New = 0,

        /// <summary>
        /// ICE connection received an offer, but transports are not writable yet.
        /// </summary>
        Checking = 1,

        /// <summary>
        /// Transports are writable.
        /// </summary>
        Connected = 2,

        /// <summary>
        /// ICE connection finished establishing.
        /// </summary>
        Completed = 3,

        /// <summary>
        /// Failed establishing an ICE connection.
        /// </summary>
        Failed = 4,

        /// <summary>
        /// ICE connection is disconnected, there is no more writable transport.
        /// </summary>
        Disconnected = 5,

        /// <summary>
        /// The peer connection was closed entirely.
        /// </summary>
        Closed = 6,
    }

    /// <summary>
    /// State of an ICE gathering process.
    /// </summary>
    /// <remarks>
    /// See <see href="https://www.w3.org/TR/webrtc/#rtcicegatheringstate-enum">RTPIceGatheringState</see>
    /// from the WebRTC 1.0 standard.
    /// </remarks>
    /// <seealso href="https://www.w3.org/TR/webrtc/#rtcicegatheringstate-enum"/>
    public enum IceGatheringState : int
    {
        /// <summary>
        /// There is no ICE transport, or none of them started gathering ICE candidates.
        /// </summary>
        New = 0,

        /// <summary>
        /// The gathering process started. At least one ICE transport is active and gathering
        /// some ICE candidates.
        /// </summary>
        Gathering = 1,

        /// <summary>
        /// The gathering process is complete. At least one ICE transport was active, and
        /// all transports finished gathering ICE candidates.
        /// </summary>
        Complete = 2,
    }

    /// <summary>
    /// Identifier for a video capture device.
    /// </summary>
    [Serializable]
    public struct VideoCaptureDevice
    {
        /// <summary>
        /// Unique device identifier.
        /// </summary>
        public string id;

        /// <summary>
        /// Friendly device name.
        /// </summary>
        public string name;
    }

    /// <summary>
    /// Capture format for a video track.
    /// </summary>
    [Serializable]
    public struct VideoCaptureFormat
    {
        /// <summary>
        /// Frame width, in pixels.
        /// </summary>
        public uint width;

        /// <summary>
        /// Frame height, in pixels.
        /// </summary>
        public uint height;

        /// <summary>
        /// Capture framerate, in frames per second.
        /// </summary>
        public double framerate;

        /// <summary>
        /// FOURCC identifier of the video encoding.
        /// </summary>
        public uint fourcc;
    }

    /// <summary>
    /// Settings to create a new transceiver wrapper.
    /// </summary>
    public class TransceiverInitSettings
    {
        /// <summary>
        /// Transceiver name, for logging and debugging.
        /// </summary>
        public string Name;

        /// <summary>
        /// Initial value of <see cref="Transceiver.DesiredDirection"/>.
        /// </summary>
        public Transceiver.Direction InitialDesiredDirection = Transceiver.Direction.SendReceive;

        /// <summary>
        /// List of stream IDs to associate the transceiver with.
        /// </summary>
        public List<string> StreamIDs;
    }

    /// <summary>
    /// Wrapper for an event possibly delayed.
    /// </summary>
    internal class DelayedEvent
    {
        /// <summary>
        /// The event handler.
        /// </summary>
        public Action Event
        {
            get
            {
                lock (_lock)
                {
                    return _event;
                }
            }
            set
            {
                lock (_lock)
                {
                    _event = value;
                }
            }
        }

        /// <summary>
        /// Begin suspending <see cref="Event"/>. This must be matched with a call
        /// to <see cref="EndSuspend"/>. During this time, calling <see cref="Invoke"/>
        /// does not invoke the event but instead queue it for later invoking by
        /// <see cref="EndSuspend"/>.
        /// </summary>
        public void BeginSuspend()
        {
            lock (_lock)
            {
                ++_suspendCount;
            }
        }

        /// <summary>
        /// End suspending <see cref="Event"/> and invoke it if any call to <see cref="Invoke"/>
        /// was made since the first <see cref="BeginSuspend"/> call.
        /// </summary>
        public void EndSuspend()
        {
            Action cb;
            lock (_lock)
            {
                Debug.Assert(_suspendCount > 0);
                --_suspendCount;
                if (!_eventPending || (_suspendCount > 0))
                {
                    return;
                }
                _eventPending = false;
                cb = Event;
            }
            cb?.Invoke();
        }

        /// <summary>
        /// Try to invoke <see cref="Event"/>, either immediately if not suspended, or later
        /// when the last <see cref="EndSuspend"/> call stops suspending it.
        /// </summary>
        /// <param name="async">If the event is not suspended, and therefore is invoked immediately,
        /// then invoke it asynchronously from a worker thread.</param>
        public void Invoke(bool async = false)
        {
            Action cb;
            lock (_lock)
            {
                Debug.Assert(_suspendCount >= 0);
                if (_suspendCount > 0)
                {
                    _eventPending = true;
                    return;
                }
                cb = Event;
            }
            if (cb != null)
            {
                if (async)
                {
                    Task.Run(cb);
                }
                else
                {
                    cb.Invoke();
                }
            }
        }

        /// <summary>
        /// Try to invoke <see cref="Event"/> asynchronously.
        /// This is equivalent to <code>Invoke(async: true)</code>.
        /// </summary>
        public void InvokeAsync() => Invoke(async: true);

        /// <summary>
        /// Lock for internal variables:
        /// - <see cref="_suspendCount"/>
        /// - <see cref="_eventPending"/>
        /// - <see cref="_event"/>
        /// </summary>
        private readonly object _lock = new object();

        /// <summary>
        /// Number of concurrent calls currently suspending the event.
        /// When this value reaches zero, the thread which decremented it checks the value of
        /// <see cref="_eventPending"/> and if <c>true</c> then invoke the event.
        /// </summary>
        /// <remarks>This is protected by <see cref="_lock"/>.</remarks>
        private int _suspendCount = 0;

        /// <summary>
        /// Was any event internally raised while the public event was suspended (that is, <see cref="_suspendCount"/>
        /// was non-zero)? This is set by <see cref="Invoke"/> and cleared by any thread actually invoking the event after
        /// decrementing <see cref="_suspendCount"/> to zero.
        /// </summary>
        /// <remarks>This is protected by <see cref="_lock"/>.</remarks>
        private bool _eventPending = false;

        /// <summary>
        /// Backup field for <see cref="Event"/>, to be accessed only under <see cref="_lock"/>.
        /// </summary>
        private Action _event;
    }

    /// <summary>
    /// RAII helper to start/stop a delay block for a <see cref="DelayedEvent"/>.
    /// </summary>
    internal class ScopedDelayedEvent : IDisposable
    {
        /// <summary>
        /// Initialize a scoped delay for the specified <see cref="DelayedEvent"/>.
        /// This will call <see cref="DelayedEvent.BeginSuspend"/> immediately, and will call
        /// <see cref="DelayedEvent.EndSuspend"/> once disposed.
        /// </summary>
        /// <param name="ev"></param>
        public ScopedDelayedEvent(DelayedEvent ev)
        {
            _delayedEvent = ev;
            _delayedEvent.BeginSuspend();
        }

        /// <summary>
        /// Dispose of the helper and call <see cref="DelayedEvent.BeginSuspend"/> on
        /// <see cref="_delayedEvent"/>.
        /// </summary>
        public void Dispose()
        {
            _delayedEvent.EndSuspend();
        }

        private DelayedEvent _delayedEvent;
    }

    /// <summary>
    /// Type of SDP message.
    /// </summary>
    public enum SdpMessageType : int
    {
        /// <summary>
        /// Offer message used to initiate a new session.
        /// </summary>
        Offer = 1,

        /// <summary>
        /// Answer message used to accept a session offer.
        /// </summary>
        Answer = 2
    }

    /// <summary>
    /// SDP message passed between the local and remote peers via the user's signaling solution.
    /// </summary>
    public class SdpMessage
    {
        /// <summary>
        /// The message type.
        /// </summary>
        public SdpMessageType Type;

        /// <summary>
        /// The raw message content.
        /// </summary>
        public string Content;

        /// <summary>
        /// Convert an SDP message type to its internal string representation.
        /// </summary>
        /// <param name="type">The SDP message type to convert</param>
        /// <returns>The string representation of the SDP message type</returns>
        /// <exception xref="ArgumentException">The SDP message type was invalid.</exception>
        public static string TypeToString(SdpMessageType type)
        {
            switch (type)
            {
                case SdpMessageType.Offer: return "offer";
                case SdpMessageType.Answer: return "answer";
            }
            throw new ArgumentException($"Cannot convert invalid SdpMessageType value '{type}'.");
        }

        /// <summary>
        /// Convert an internal string representation of an SDP message type back to its enumerated value.
        /// </summary>
        /// <param name="type">The internal string representation of the SDP message</param>
        /// <returns>The SDP message type associated with the string representation</returns>
        /// <exception xref="ArgumentException">The string does not represent any SDP message type.</exception>
        public static SdpMessageType StringToType(string type)
        {
            if (string.Equals(type, "offer", StringComparison.OrdinalIgnoreCase))
            {
                return SdpMessageType.Offer;
            }
            else if (string.Equals(type, "answer", StringComparison.OrdinalIgnoreCase))
            {
                return SdpMessageType.Answer;
            }
            throw new ArgumentException($"Cannot convert invalid SdpMessageType string '{type}'.");
        }
    }

    /// <summary>
    /// ICE candidate to send to a remote peer or received from it.
    /// </summary>
    public class IceCandidate
    {
        /// <summary>
        /// Media ID (m=) of the candidate.
        /// </summary>
        public string SdpMid;

        /// <summary>
        /// Index of the media line associated with the candidate.
        /// </summary>
        public int SdpMlineIndex;

        /// <summary>
        /// Candidate raw content.
        /// </summary>
        public string Content;

    }

    /// <summary>
    /// The WebRTC peer connection object is the entry point to using WebRTC.
    /// </summary>
    public class PeerConnection : IDisposable
    {
        /// <summary>
        /// Delegate for <see cref="TransceiverAdded"/> event.
        /// </summary>
        /// <param name="transceiver">The newly added transceiver.</param>
        public delegate void TransceiverAddedDelegate(Transceiver transceiver);

        /// <summary>
        /// Delegate for <see cref="AudioTrackAdded"/> event.
        /// </summary>
        /// <param name="track">The newly added audio track.</param>
        public delegate void AudioTrackAddedDelegate(RemoteAudioTrack track);

        /// <summary>
        /// Delegate for <see cref="AudioTrackRemoved"/> event.
        /// </summary>
        /// <param name="transceiver">The audio transceiver the track was removed from.</param>
        /// <param name="track">The audio track just removed.</param>
        public delegate void AudioTrackRemovedDelegate(Transceiver transceiver, RemoteAudioTrack track);

        /// <summary>
        /// Delegate for <see cref="VideoTrackAdded"/> event.
        /// </summary>
        /// <param name="track">The newly added video track.</param>
        public delegate void VideoTrackAddedDelegate(RemoteVideoTrack track);

        /// <summary>
        /// Delegate for <see cref="VideoTrackRemoved"/> event.
        /// </summary>
        /// <param name="transceiver">The video transceiver the track was removed from.</param>
        /// <param name="track">The video track just removed.</param>
        public delegate void VideoTrackRemovedDelegate(Transceiver transceiver, RemoteVideoTrack track);

        /// <summary>
        /// Delegate for <see cref="DataChannelAdded"/> event.
        /// </summary>
        /// <param name="channel">The newly added data channel.</param>
        public delegate void DataChannelAddedDelegate(DataChannel channel);

        /// <summary>
        /// Delegate for <see cref="DataChannelRemoved"/> event.
        /// </summary>
        /// <param name="channel">The data channel just removed.</param>
        public delegate void DataChannelRemovedDelegate(DataChannel channel);

        /// <summary>
        /// Delegate for <see cref="LocalSdpReadytoSend"/> event.
        /// </summary>
        /// <param name="message">SDP message to send.</param>
        public delegate void LocalSdpReadyToSendDelegate(SdpMessage message);

        /// <summary>
        /// Delegate for the <see cref="IceCandidateReadytoSend"/> event.
        /// </summary>
        /// <param name="candidate">The ICE candidate to send.</param>
        public delegate void IceCandidateReadytoSendDelegate(IceCandidate candidate);

        /// <summary>
        /// Delegate for the <see cref="IceStateChanged"/> event.
        /// </summary>
        /// <param name="newState">The new ICE connection state.</param>
        public delegate void IceStateChangedDelegate(IceConnectionState newState);

        /// <summary>
        /// Delegate for the <see cref="IceGatheringStateChanged"/> event.
        /// </summary>
        /// <param name="newState">The new ICE gathering state.</param>
        public delegate void IceGatheringStateChangedDelegate(IceGatheringState newState);

        /// <summary>
        /// Kind of WebRTC track.
        /// </summary>
        public enum TrackKind : uint
        {
            /// <summary>
            /// Unknown track kind. Generally not initialized or error.
            /// </summary>
            Unknown = 0,

            /// <summary>
            /// Audio track.
            /// </summary>
            Audio = 1,

            /// <summary>
            /// Video track.
            /// </summary>
            Video = 2,

            /// <summary>
            /// Data track.
            /// </summary>
            Data = 3
        };


        #region Codec filtering

        /// <summary>
        /// Name of the preferred audio codec, or empty to let WebRTC decide.
        /// See https://en.wikipedia.org/wiki/RTP_audio_video_profile for the standard SDP names.
        /// </summary>
        public string PreferredAudioCodec = string.Empty;

        /// <summary>
        /// Advanced use only. List of additional codec-specific arguments requested to the
        /// remote endpoint.
        /// </summary>
        /// <remarks>
        /// This must be a semicolon-separated list of "key=value" pairs. Arguments are passed as is,
        /// and there is no check on the validity of the parameter names nor their value.
        /// Arguments are added to the audio codec section of SDP messages sent to the remote endpoint.
        ///
        /// This is ignored if <see cref="PreferredAudioCodec"/> is an empty string, or is not
        /// a valid codec name found in the SDP message offer.
        /// </remarks>
        public string PreferredAudioCodecExtraParamsRemote = string.Empty;

        /// <summary>
        /// Advanced use only. List of additional codec-specific arguments set on the local endpoint.
        /// </summary>
        /// <remarks>
        /// This must be a semicolon-separated list of "key=value" pairs. Arguments are passed as is,
        /// and there is no check on the validity of the parameter names nor their value.
        /// Arguments are set locally by adding them to the audio codec section of SDP messages
        /// received from the remote endpoint.
        ///
        /// This is ignored if <see cref="PreferredAudioCodec"/> is an empty string, or is not
        /// a valid codec name found in the SDP message offer.
        /// </remarks>
        public string PreferredAudioCodecExtraParamsLocal = string.Empty;

        /// <summary>
        /// Name of the preferred video codec, or empty to let WebRTC decide.
        /// See https://en.wikipedia.org/wiki/RTP_audio_video_profile for the standard SDP names.
        /// </summary>
        public string PreferredVideoCodec = string.Empty;

        /// <summary>
        /// Advanced use only. List of additional codec-specific arguments requested to the
        /// remote endpoint.
        /// </summary>
        /// <remarks>
        /// This must be a semicolon-separated list of "key=value" pairs. Arguments are passed as is,
        /// and there is no check on the validity of the parameter names nor their value.
        /// Arguments are added to the video codec section of SDP messages sent to the remote endpoint.
        ///
        /// This is ignored if <see cref="PreferredVideoCodec"/> is an empty string, or is not
        /// a valid codec name found in the SDP message offer.
        /// </remarks>
        public string PreferredVideoCodecExtraParamsRemote = string.Empty;

        /// <summary>
        /// Advanced use only. List of additional codec-specific arguments set on the local endpoint.
        /// </summary>
        /// <remarks>
        /// This must be a semicolon-separated list of "key=value" pairs. Arguments are passed as is,
        /// and there is no check on the validity of the parameter names nor their value.
        /// Arguments are set locally by adding them to the video codec section of SDP messages
        /// received from the remote endpoint.
        ///
        /// This is ignored if <see cref="PreferredVideoCodec"/> is an empty string, or is not
        /// a valid codec name found in the SDP message offer.
        /// </remarks>
        public string PreferredVideoCodecExtraParamsLocal = string.Empty;

        #endregion


        /// <summary>
        /// A name for the peer connection, used for logging and debugging.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Boolean property indicating whether the peer connection has been initialized.
        /// 
        /// <div class="WARNING alert alert-warning">
        /// <h5>Warning</h5>
        /// <p>
        /// This property will be deprecated and later removed in future versions.
        /// 
        /// The value of this property is undefined while the asynchronous task resulting from a call to
        /// <see cref="InitializeAsync(PeerConnectionConfiguration, CancellationToken)"/> is pending. This
        /// means its value is only relevant before the call (and then it is <c>false</c>) or after the
        /// asynchronous call completed (and then it is <c>true</c>), but not while the initialization is
        /// underway. For this reason, it is recommended NOT to use this property, and instead to rely on logic
        /// around <see cref="InitializeAsync(PeerConnectionConfiguration, CancellationToken)"/> and
        /// <see cref="Close"/> alone. Generally this means awaiting the initialize call (<c>await</c> operator)
        /// before using the peer connection object for anything else.</p>
        /// </div>
        /// </summary>
        [Obsolete("This property has confusing semantic and will be removed in a future version. Await the task returned by InitializeAsync() instead.", error: false)]
        public bool Initialized
        {
            get
            {
                lock (_openCloseLock)
                {
                    return (_initTask != null);
                }
            }
        }

        /// <summary>
        /// Indicates whether the peer connection is established and can exchange some
        /// track content (audio/video/data) with the remote peer.
        /// </summary>
        /// <remarks>
        /// This does not indicate whether the ICE exchange is done, as it
        /// may continue after the peer connection negotiated a first session.
        /// For ICE connection status, see the <see cref="IceStateChanged"/> event.
        /// </remarks>
        public bool IsConnected { get; private set; } = false;

        /// <summary>
        /// Collection of transceivers for the peer connection. Once a transceiver is added
        /// to the peer connection, it cannot be removed, but its tracks can be changed.
        /// Adding a transceiver or changing its direction require some new session negotiation.
        /// </summary>
        public List<Transceiver> Transceivers { get; } = new List<Transceiver>();

        /// <summary>
        /// Collection of transceivers which have already been associated with a media line.
        ///
        /// A transceiver is associated with a media line when a local or remote offer is applied
        /// to the peer connection, respectively during <see cref="CreateOffer"/> and
        /// <see cref="SetRemoteDescriptionAsync(SdpMessage)"/>.
        /// </summary>
        public IEnumerable<Transceiver> AssociatedTransceivers
        {
            get
            {
                foreach (var tr in Transceivers)
                {
                    if (tr.MlineIndex >= 0)
                    {
                        yield return tr;
                    }
                }
            }
        }

        /// <summary>
        /// Collection of local audio tracks attached to the peer connection.
        /// </summary>
        public IEnumerable<LocalAudioTrack> LocalAudioTracks
        {
            get
            {
                foreach (var tr in Transceivers)
                {
                    var audioTrack = tr.LocalAudioTrack;
                    if (audioTrack != null)
                    {
                        yield return audioTrack;
                    }
                }
            }
        }

        /// <summary>
        /// Collection of local video tracks attached to the peer connection.
        /// </summary>
        public IEnumerable<LocalVideoTrack> LocalVideoTracks
        {
            get
            {
                foreach (var tr in Transceivers)
                {
                    var videoTrack = tr.LocalVideoTrack;
                    if (videoTrack != null)
                    {
                        yield return videoTrack;
                    }
                }
            }
        }

        /// <summary>
        /// Collection of remote audio tracks attached to the peer connection.
        /// </summary>
        public IEnumerable<RemoteAudioTrack> RemoteAudioTracks
        {
            get
            {
                foreach (var tr in Transceivers)
                {
                    var audioTrack = tr.RemoteAudioTrack;
                    if (audioTrack != null)
                    {
                        yield return audioTrack;
                    }
                }
            }
        }

        /// <summary>
        /// Collection of remote video tracks attached to the peer connection.
        /// </summary>
        public IEnumerable<RemoteVideoTrack> RemoteVideoTracks
        {
            get
            {
                foreach (var tr in Transceivers)
                {
                    var videoTrack = tr.RemoteVideoTrack;
                    if (videoTrack != null)
                    {
                        yield return videoTrack;
                    }
                }
            }
        }

        /// <summary>
        /// Collection of data channels for the peer connection.
        ///
        /// Data channels are either manually added by calling
        /// <see cref="AddDataChannelAsync(string, bool, bool, CancellationToken)"/> or
        /// <see cref="AddDataChannelAsync(ushort, string, bool, bool, CancellationToken)"/>,
        /// or are created by the implementation while applying a remote offer when the remote
        /// peer created a new in-band data channel.
        /// </summary>
        public List<DataChannel> DataChannels { get; } = new List<DataChannel>();

        /// <summary>
        /// Event fired when a connection is established.
        /// </summary>
        public event Action Connected;

        /// <summary>
        /// Event fired when a data channel is added to the peer connection.
        /// This event is always fired, whether the data channel is created by the local peer
        /// or the remote peer, and is negotiated (out-of-band) or not (in-band).
        /// If an in-band data channel is created by the local peer, the <see cref="DataChannel.ID"/>
        /// field is not yet available when this event is fired, because the ID has not been
        /// agreed upon with the remote peer yet.
        /// </summary>
        public event DataChannelAddedDelegate DataChannelAdded;

        /// <summary>
        /// Event fired when a data channel is removed from the peer connection.
        /// This event is always fired, whatever its creation method (negotiated or not)
        /// and original creator (local or remote peer).
        /// </summary>
        public event DataChannelRemovedDelegate DataChannelRemoved;

        /// <summary>
        /// Event that occurs when a local SDP message is ready to be transmitted.
        /// </summary>
        public event LocalSdpReadyToSendDelegate LocalSdpReadytoSend;

        /// <summary>
        /// Event that occurs when a local ICE candidate is ready to be transmitted.
        /// </summary>
        public event IceCandidateReadytoSendDelegate IceCandidateReadytoSend;

        /// <summary>
        /// Event that occurs when the state of the ICE connection changed.
        /// </summary>
        public event IceStateChangedDelegate IceStateChanged;

        /// <summary>
        /// Event that occurs when the state of the ICE gathering changed.
        /// </summary>
        public event IceGatheringStateChangedDelegate IceGatheringStateChanged;

        /// <summary>
        /// Event that occurs when a renegotiation of the session is needed.
        /// This generally occurs as a result of adding or removing tracks,
        /// and the user should call <see cref="CreateOffer"/> to actually
        /// start a renegotiation.
        /// </summary>
        public event Action RenegotiationNeeded
        {
            add => _renegotiationNeededEvent.Event += value;
            remove => _renegotiationNeededEvent.Event -= value;
        }

        /// <summary>
        /// Event that occurs when a transceiver is added to the peer connection, either
        /// manually using <see cref="AddTransceiver(MediaKind, TransceiverInitSettings)"/>, or
        /// automatically as a result of a new session negotiation.
        /// </summary>
        /// <remarks>
        /// Transceivers cannot be removed from the peer connection, so there is no
        /// <c>TransceiverRemoved</c> event.
        /// </remarks>
        public event TransceiverAddedDelegate TransceiverAdded;

        /// <summary>
        /// Event that occurs when a remote audio track is added to the current connection.
        /// </summary>
        public event AudioTrackAddedDelegate AudioTrackAdded;

        /// <summary>
        /// Event that occurs when a remote audio track is removed from the current connection.
        /// </summary>
        public event AudioTrackRemovedDelegate AudioTrackRemoved;

        /// <summary>
        /// Event that occurs when a remote video track is added to the current connection.
        /// </summary>
        public event VideoTrackAddedDelegate VideoTrackAdded;

        /// <summary>
        /// Event that occurs when a remote video track is removed from the current connection.
        /// </summary>
        public event VideoTrackRemovedDelegate VideoTrackRemoved;

        /// <summary>
        /// GCHandle to self for the various native callbacks.
        /// This also acts as a marker of a connection created or in the process of being created.
        /// </summary>
        private GCHandle _selfHandle;

        /// <summary>
        /// Handle to the native PeerConnection object.
        /// </summary>
        /// <remarks>
        /// In native land this is a <code>Microsoft::MixedReality::WebRTC::PeerConnectionHandle</code>.
        /// </remarks>
        private PeerConnectionHandle _nativePeerhandle = new PeerConnectionHandle();

        /// <summary>
        /// Initialization task returned by <see cref="InitializeAsync"/>.
        /// </summary>
        private Task _initTask = null;

        /// <summary>
        /// Boolean to indicate if <see cref="Close"/> has been called and is waiting for a pending
        /// initializing task <see cref="_initTask"/> to complete or cancel.
        /// </summary>
        private bool _isClosing = false;

        /// <summary>
        /// Lock for asynchronous opening and closing of the connection, protecting
        /// changes to <see cref="_nativePeerhandle"/>, <see cref="_selfHandle"/>,
        /// <see cref="_initTask"/>, and <see cref="_isClosing"/>.
        /// </summary>
        private readonly object _openCloseLock = new object();

        private PeerConnectionInterop.PeerCallbackArgs _peerCallbackArgs;

        /// <summary>
        /// Lock for accessing the collections of tracks and transceivers:
        /// - <see cref="Transceivers"/>
        /// - <see cref="LocalAudioTracks"/>
        /// - <see cref="LocalVideoTracks"/>
        /// - <see cref="RemoteAudioTracks"/>
        /// - <see cref="RemoteVideoTracks"/>
        /// </summary>
        private readonly object _tracksLock = new object();

        /// <summary>
        /// Implementation of <see cref="RenegotiationNeeded"/> adding the capability to delay the event.
        /// This allows <see cref="AddTransceiver(MediaKind, TransceiverInitSettings)"/> to wait until the
        /// newly created transceiver wrapper is fully instantiated to dispatch the event.
        /// </summary>
        private DelayedEvent _renegotiationNeededEvent = new DelayedEvent();

        #region Initializing and shutdown

        /// <summary>
        /// Create a new peer connection object. The object is initially created empty, and cannot be used
        /// until <see cref="InitializeAsync(PeerConnectionConfiguration, CancellationToken)"/> has completed
        /// successfully.
        /// </summary>
        public PeerConnection()
        {
            MainEventSource.Log.Initialize();
        }

        /// <summary>
        /// Initialize the current peer connection object asynchronously.
        ///
        /// Most other methods will fail unless this call completes successfully, as it initializes the
        /// underlying native implementation object required to create and manipulate the peer connection.
        ///
        /// Once this call asynchronously completed, the <see cref="Initialized"/> property is <c>true</c>.
        /// </summary>
        /// <param name="config">Configuration for initializing the peer connection.</param>
        /// <param name="token">Optional cancellation token for the initialize task. This is only used if
        /// the singleton task was created by this call, and not a prior call.</param>
        /// <returns>The singleton task used to initialize the underlying native peer connection.</returns>
        /// <remarks>This method is multi-thread safe, and will always return the same task object
        /// from the first call to it until the peer connection object is deinitialized. This allows
        /// multiple callers to all execute some action following the initialization, without the need
        /// to force a single caller and to synchronize with it.</remarks>
        public Task InitializeAsync(PeerConnectionConfiguration config = null, CancellationToken token = default)
        {
            lock (_openCloseLock)
            {
                // If Close() is waiting for _initTask to finish, do not return it.
                if (_isClosing)
                {
                    throw new OperationCanceledException("A closing operation is pending.");
                }

                // If _initTask has already been created, return it.
                if (_initTask != null)
                {
                    return _initTask;
                }

                // Allocate a GC handle to self for static P/Invoke callbacks to be able to call
                // back into methods of this peer connection object. The handle is released when
                // the peer connection is closed, to allow this object to be destroyed.
                Debug.Assert(!_selfHandle.IsAllocated);
                _selfHandle = GCHandle.Alloc(this, GCHandleType.Normal);

                // Create and lock in memory delegates for all the static callback wrappers (see below).
                // This avoids delegates being garbage-collected, since the P/Invoke mechanism by itself
                // does not guarantee their lifetime.
                _peerCallbackArgs = new PeerConnectionInterop.PeerCallbackArgs()
                {
                    Peer = this,
                    DataChannelAddedCallback = PeerConnectionInterop.DataChannelAddedCallback,
                    DataChannelRemovedCallback = PeerConnectionInterop.DataChannelRemovedCallback,
                    ConnectedCallback = PeerConnectionInterop.ConnectedCallback,
                    LocalSdpReadytoSendCallback = PeerConnectionInterop.LocalSdpReadytoSendCallback,
                    IceCandidateReadytoSendCallback = PeerConnectionInterop.IceCandidateReadytoSendCallback,
                    IceStateChangedCallback = PeerConnectionInterop.IceStateChangedCallback,
                    IceGatheringStateChangedCallback = PeerConnectionInterop.IceGatheringStateChangedCallback,
                    RenegotiationNeededCallback = PeerConnectionInterop.RenegotiationNeededCallback,
                    TransceiverAddedCallback = PeerConnectionInterop.TransceiverAddedCallback,
                    AudioTrackAddedCallback = PeerConnectionInterop.AudioTrackAddedCallback,
                    AudioTrackRemovedCallback = PeerConnectionInterop.AudioTrackRemovedCallback,
                    VideoTrackAddedCallback = PeerConnectionInterop.VideoTrackAddedCallback,
                    VideoTrackRemovedCallback = PeerConnectionInterop.VideoTrackRemovedCallback,
                };

                // On UWP PeerConnectionCreate() fails on main UI thread, so always initialize the native peer
                // connection asynchronously from a background worker thread.
                _initTask = Task.Run(() =>
                {
                    token.ThrowIfCancellationRequested();

                    // Cache values in local variables before starting async task, to avoid any
                    // subsequent external change from affecting that task.
                    // Also set default values, as the native call doesn't handle NULL.
                    PeerConnectionInterop.PeerConnectionConfiguration nativeConfig;
                    if (config != null)
                    {
                        nativeConfig = new PeerConnectionInterop.PeerConnectionConfiguration
                        {
                            EncodedIceServers = string.Join("\n\n", config.IceServers),
                            IceTransportType = config.IceTransportType,
                            BundlePolicy = config.BundlePolicy,
                            SdpSemantic = config.SdpSemantic,
                        };
                    }
                    else
                    {
                        nativeConfig = new PeerConnectionInterop.PeerConnectionConfiguration();
                    }

                    uint res = PeerConnectionInterop.PeerConnection_Create(nativeConfig, out _nativePeerhandle);

                    lock (_openCloseLock)
                    {
                        // Handle errors
                        if ((res != Utils.MRS_SUCCESS) || _nativePeerhandle.IsInvalid)
                        {
                            if (_selfHandle.IsAllocated)
                            {
                                _peerCallbackArgs = null;
                                _selfHandle.Free();
                            }

                            Utils.ThrowOnErrorCode(res);
                            throw new Exception(); // if res == MRS_SUCCESS but handle is NULL
                        }

                        // The connection may have been aborted while being created, either via the
                        // cancellation token, or by calling Close() after the synchronous codepath
                        // above but before this task had a chance to run in the background.
                        if (token.IsCancellationRequested)
                        {
                            // Cancelled by token
                            _nativePeerhandle.Close();
                            throw new OperationCanceledException(token);
                        }
                        if (!_selfHandle.IsAllocated)
                        {
                            // Cancelled by calling Close()
                            _nativePeerhandle.Close();
                            throw new OperationCanceledException();
                        }

                        // Register all trampoline callbacks. Note that even passing a static managed method
                        // for the callback is not safe, because the compiler implicitly creates a delegate
                        // object (a static method is not a delegate itself; it can be wrapped inside one),
                        // and that delegate object will be garbage collected immediately at the end of this
                        // block. Instead, a delegate needs to be explicitly created and locked in memory.
                        // Since the current PeerConnection instance is already locked via _selfHandle,
                        // and it references all delegates via _peerCallbackArgs, those also can't be GC'd.
                        var self = GCHandle.ToIntPtr(_selfHandle);
                        PeerConnectionInterop.PeerConnection_RegisterConnectedCallback(
                            _nativePeerhandle, _peerCallbackArgs.ConnectedCallback, self);
                        PeerConnectionInterop.PeerConnection_RegisterLocalSdpReadytoSendCallback(
                            _nativePeerhandle, _peerCallbackArgs.LocalSdpReadytoSendCallback, self);
                        PeerConnectionInterop.PeerConnection_RegisterIceCandidateReadytoSendCallback(
                            _nativePeerhandle, _peerCallbackArgs.IceCandidateReadytoSendCallback, self);
                        PeerConnectionInterop.PeerConnection_RegisterIceStateChangedCallback(
                            _nativePeerhandle, _peerCallbackArgs.IceStateChangedCallback, self);
                        PeerConnectionInterop.PeerConnection_RegisterIceGatheringStateChangedCallback(
                            _nativePeerhandle, _peerCallbackArgs.IceGatheringStateChangedCallback, self);
                        PeerConnectionInterop.PeerConnection_RegisterRenegotiationNeededCallback(
                            _nativePeerhandle, _peerCallbackArgs.RenegotiationNeededCallback, self);
                        PeerConnectionInterop.PeerConnection_RegisterTransceiverAddedCallback(
                            _nativePeerhandle, _peerCallbackArgs.TransceiverAddedCallback, self);
                        PeerConnectionInterop.PeerConnection_RegisterAudioTrackAddedCallback(
                            _nativePeerhandle, _peerCallbackArgs.AudioTrackAddedCallback, self);
                        PeerConnectionInterop.PeerConnection_RegisterAudioTrackRemovedCallback(
                            _nativePeerhandle, _peerCallbackArgs.AudioTrackRemovedCallback, self);
                        PeerConnectionInterop.PeerConnection_RegisterVideoTrackAddedCallback(
                            _nativePeerhandle, _peerCallbackArgs.VideoTrackAddedCallback, self);
                        PeerConnectionInterop.PeerConnection_RegisterVideoTrackRemovedCallback(
                            _nativePeerhandle, _peerCallbackArgs.VideoTrackRemovedCallback, self);
                        PeerConnectionInterop.PeerConnection_RegisterDataChannelAddedCallback(
                            _nativePeerhandle, _peerCallbackArgs.DataChannelAddedCallback, self);
                        PeerConnectionInterop.PeerConnection_RegisterDataChannelRemovedCallback(
                            _nativePeerhandle, _peerCallbackArgs.DataChannelRemovedCallback, self);
                    }
                }, token);

                return _initTask;
            }
        }

        /// <summary>
        /// Close the peer connection and destroy the underlying native resources.
        /// </summary>
        /// <remarks>This is equivalent to <see cref="Dispose"/>.</remarks>
        /// <seealso cref="Dispose"/>
        public void Close()
        {
            // Begin shutdown sequence
            Task initTask = null;
            lock (_openCloseLock)
            {
                // If the connection is not initialized, return immediately.
                if (_initTask == null)
                {
                    return;
                }

                // Indicate to InitializeAsync() that it should stop returning _initTask
                // or create a new instance of it, even if it is NULL.
                _isClosing = true;

                // Ensure both Initialized and IsConnected return false
                initTask = _initTask;
                _initTask = null; // This marks the Initialized property as false
                IsConnected = false;

                // Unregister all callbacks that could add some new objects
                PeerConnectionInterop.PeerConnection_RegisterConnectedCallback(
                    _nativePeerhandle, null, IntPtr.Zero);
                PeerConnectionInterop.PeerConnection_RegisterLocalSdpReadytoSendCallback(
                    _nativePeerhandle, null, IntPtr.Zero);
                PeerConnectionInterop.PeerConnection_RegisterIceCandidateReadytoSendCallback(
                    _nativePeerhandle, null, IntPtr.Zero);
                PeerConnectionInterop.PeerConnection_RegisterIceStateChangedCallback(
                    _nativePeerhandle, null, IntPtr.Zero);
                PeerConnectionInterop.PeerConnection_RegisterIceGatheringStateChangedCallback(
                    _nativePeerhandle, null, IntPtr.Zero);
                PeerConnectionInterop.PeerConnection_RegisterRenegotiationNeededCallback(
                    _nativePeerhandle, null, IntPtr.Zero);
                PeerConnectionInterop.PeerConnection_RegisterTransceiverAddedCallback(
                    _nativePeerhandle, null, IntPtr.Zero);
                PeerConnectionInterop.PeerConnection_RegisterAudioTrackAddedCallback(
                    _nativePeerhandle, null, IntPtr.Zero);
                PeerConnectionInterop.PeerConnection_RegisterVideoTrackAddedCallback(
                    _nativePeerhandle, null, IntPtr.Zero);
                PeerConnectionInterop.PeerConnection_RegisterDataChannelAddedCallback(
                    _nativePeerhandle, null, IntPtr.Zero);
            }

            // Wait for any pending initializing to finish.
            // This must be outside of the lock because the initialization task will
            // eventually need to acquire the lock to complete.
            try
            {
                initTask.Wait();
            }
            catch (Exception)
            {
                // Discard; since we are closing, we don't care if initialization failed.
            }

            // Close the native peer connection, disconnecting from the remote peer if currently connected.
            // This will invalidate all handles to transceivers and remote tracks, as the corresponding
            // native objects will be destroyed.
            PeerConnectionInterop.PeerConnection_Close(_nativePeerhandle);

            // Destroy the native peer connection object. This may be delayed if a P/Invoke callback is underway,
            // but will be handled at some point anyway, even if the PeerConnection managed instance is gone.
            _nativePeerhandle.Close();

            // Notify owned objects to perform clean-up.
            lock (_tracksLock)
            {
                foreach (var transceiver in Transceivers)
                {
                    transceiver?.CleanUpAfterNativeDestroyed();
                }
                Transceivers.Clear();
            }

            // Complete shutdown sequence and re-enable InitializeAsync()
            lock (_openCloseLock)
            {
                // Free all delegates for callbacks previously registered with the native
                // peer connection, which is now destroyed.
                if (_selfHandle.IsAllocated)
                {
                    _peerCallbackArgs = null;
                    _selfHandle.Free();
                }

                _isClosing = false;
            }
        }

        /// <summary>
        /// Dispose of native resources by closing the peer connection.
        /// </summary>
        /// <remarks>This is equivalent to <see cref="Close"/>.</remarks>
        /// <seealso cref="Close"/>
        public void Dispose() => Close();

        #endregion


        #region Transceivers

        /// <summary>
        /// Add to the current connection a new media transceiver.
        ///
        /// A transceiver is a container for a pair of media tracks, one local sending to the remote
        /// peer, and one remote receiving from the remote peer. Both are optional, and the transceiver
        /// can be in receive-only mode (no local track), in send-only mode (no remote track), or
        /// inactive (neither local nor remote track).
        ///
        /// Once a transceiver is added to the peer connection, it cannot be removed, but its tracks can be
        /// changed (this requires some renegotiation).
        /// </summary>
        /// <param name="mediaKind">Kind of media the transeiver is transporting.</param>
        /// <param name="settings">Settings to initialize the new transceiver.</param>
        /// <returns>The newly created transceiver.</returns>
        public Transceiver AddTransceiver(MediaKind mediaKind, TransceiverInitSettings settings = null)
        {
            ThrowIfConnectionNotOpen();

            // Suspend RenegotiationNeeded event while creating the transceiver, to avoid firing an event
            // while the transceiver is in an intermediate state.
            using (var renegotiationNeeded = new ScopedDelayedEvent(_renegotiationNeededEvent))
            {
                // Create the transceiver implementation
                settings = settings ?? new TransceiverInitSettings();
                TransceiverInterop.InitConfig config = new TransceiverInterop.InitConfig(mediaKind, settings);
                uint res = PeerConnectionInterop.PeerConnection_AddTransceiver(_nativePeerhandle, config,
                    out IntPtr transceiverHandle);
                Utils.ThrowOnErrorCode(res);

                // The implementation fires the TransceiverAdded event, which creates the wrapper and
                // stores a reference in the UserData of the native object.
                IntPtr transceiver = ObjectInterop.Object_GetUserData(new TransceiverInterop.TransceiverHandle(transceiverHandle));
                Debug.Assert(transceiver != IntPtr.Zero);
                var wrapper = Utils.ToWrapper<Transceiver>(transceiver);
                return wrapper;
            }
        }

        #endregion


        #region Data channels

        /// <summary>
        /// Add a new out-of-band data channel with the given ID.
        ///
        /// A data channel is branded out-of-band when the peers agree on an identifier by any mean
        /// not known to WebRTC, and both open a data channel with that ID. The WebRTC will match the
        /// incoming and outgoing pipes by this ID to allow sending and receiving through that channel.
        ///
        /// This requires some external mechanism to agree on an available identifier not otherwise taken
        /// by another channel, and also requires to ensure that both peers explicitly open that channel.
        /// The advantage of in-band data channels is that no SDP session renegotiation is needed, except
        /// for the very first data channel added (in-band or out-of-band) which requires a negotiation
        /// for the SCTP handshake (see remarks).
        /// </summary>
        /// <param name="id">The unique data channel identifier to use.</param>
        /// <param name="label">The data channel name.</param>
        /// <param name="ordered">Indicates whether data channel messages are ordered (see
        /// <see cref="DataChannel.Ordered"/>).</param>
        /// <param name="reliable">Indicates whether data channel messages are reliably delivered
        /// (see <see cref="DataChannel.Reliable"/>).</param>
        /// <param name="cancellationToken">Cancellation token for the task returned.</param>
        /// <returns>Returns a task which completes once the data channel is created.</returns>
        /// <exception xref="InvalidOperationException">The peer connection is not initialized.</exception>
        /// <exception cref="SctpNotNegotiatedException">SCTP not negotiated. Call <see cref="CreateOffer()"/> first.</exception>
        /// <exception xref="ArgumentOutOfRangeException">Invalid data channel ID, must be in [0:65535].</exception>
        /// <remarks>
        /// Data channels use DTLS over SCTP, which ensure in particular that messages are encrypted. To that end,
        /// while establishing a connection with the remote peer, some specific SCTP handshake must occur. This
        /// handshake is only performed if at least one data channel was added to the peer connection when the
        /// connection starts its negotiation with <see cref="CreateOffer"/>. Therefore, if the user wants to use
        /// a data channel at any point during the lifetime of this peer connection, it is critical to add at least
        /// one data channel before <see cref="CreateOffer"/> is called. Otherwise all calls will fail with an
        /// <see cref="SctpNotNegotiatedException"/> exception.
        /// </remarks>
        public Task<DataChannel> AddDataChannelAsync(ushort id, string label, bool ordered, bool reliable,
            CancellationToken cancellationToken = default)
        {
            if (id < 0)
            {
                throw new ArgumentOutOfRangeException("id", id, "Data channel ID must be greater than or equal to zero.");
            }
            return AddDataChannelAsyncImpl(id, label, ordered, reliable, cancellationToken);
        }

        /// <summary>
        /// Add a new in-band data channel whose ID will be determined by the implementation.
        ///
        /// A data channel is branded in-band when one peer requests its creation to the WebRTC core,
        /// and the implementation negotiates with the remote peer an appropriate ID by sending some
        /// SDP offer message. In that case once accepted the other peer will automatically create the
        /// appropriate data channel on its side with that same ID, and the ID will be returned on
        /// both sides to the user for information.
        ///
        /// Compared to out-of-band messages, this requires exchanging some SDP messages, but avoids having
        /// to agree on a common unused ID and having to explicitly open the data channel on both sides.
        /// </summary>
        /// <param name="label">The data channel name.</param>
        /// <param name="ordered">Indicates whether data channel messages are ordered (see
        /// <see cref="DataChannel.Ordered"/>).</param>
        /// <param name="reliable">Indicates whether data channel messages are reliably delivered
        /// (see <see cref="DataChannel.Reliable"/>).</param>
        /// <param name="cancellationToken">Cancellation token for the task returned.</param>
        /// <returns>Returns a task which completes once the data channel is created.</returns>
        /// <exception xref="System.InvalidOperationException">The peer connection is not initialized.</exception>
        /// <exception cref="SctpNotNegotiatedException">SCTP not negotiated. Call <see cref="CreateOffer()"/> first.</exception>
        /// <exception xref="System.ArgumentOutOfRangeException">Invalid data channel ID, must be in [0:65535].</exception>
        /// <remarks>
        /// See the critical remark about SCTP handshake in <see cref="AddDataChannelAsync(ushort, string, bool, bool, CancellationToken)"/>.
        /// </remarks>
        public Task<DataChannel> AddDataChannelAsync(string label, bool ordered, bool reliable,
            CancellationToken cancellationToken = default)
        {
            return AddDataChannelAsyncImpl(-1, label, ordered, reliable, cancellationToken);
        }

        /// <summary>
        /// Add a new in-band or out-of-band data channel.
        /// </summary>
        /// <param name="id">Identifier in [0:65535] of the out-of-band data channel, or <c>-1</c> for in-band.</param>
        /// <param name="label">The data channel name.</param>
        /// <param name="ordered">Indicates whether data channel messages are ordered (see
        /// <see cref="DataChannel.Ordered"/>).</param>
        /// <param name="reliable">Indicates whether data channel messages are reliably delivered
        /// (see <see cref="DataChannel.Reliable"/>).</param>
        /// <param name="cancellationToken">Cancellation token for the task returned.</param>
        /// <returns>Returns a task which completes once the data channel is created.</returns>
        /// <exception xref="System.InvalidOperationException">The peer connection is not initialized.</exception>
        /// <exception xref="System.InvalidOperationException">SCTP not negotiated.</exception>
        /// <exception xref="System.ArgumentOutOfRangeException">Invalid data channel ID, must be in [0:65535].</exception>
        private Task<DataChannel> AddDataChannelAsyncImpl(int id, string label, bool ordered, bool reliable,
            CancellationToken cancellationToken)
        {
            ThrowIfConnectionNotOpen();

            // Create the native data channel
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Create the native object
                var config = new DataChannelInterop.CreateConfig
                {
                    id = id,
                    flags = (uint)((ordered ? DataChannelInterop.Flags.Ordered : DataChannelInterop.Flags.None)
                        | (reliable ? DataChannelInterop.Flags.Reliable : DataChannelInterop.Flags.None)),
                    label = label,
                };
                uint res = PeerConnectionInterop.PeerConnection_AddDataChannel(_nativePeerhandle, config, out IntPtr nativeHandle);
                Utils.ThrowOnErrorCode(res);

                // The wrapper is created by the "DataChannelAdded" event invoked by PeerConnection_AddDataChannel().
                // Find it via the UserData property. Note that it was already added to the DataChannels collection.
                var dataChannelRef = DataChannelInterop.DataChannel_GetUserData(nativeHandle);
                var dataChannelWrapper = Utils.ToWrapper<DataChannel>(dataChannelRef);
                Debug.Assert(dataChannelWrapper != null);
                return dataChannelWrapper;
            }, cancellationToken);
        }

        /// <summary>
        /// Remove an existing data channel from the peer connection and destroy its native implementation.
        /// </summary>
        /// <param name="dataChannel">The data channel to remove and destroy.</param>
        /// <exception xref="System.ArgumentException">The data channel is not owned by this peer connection.</exception>
        public void RemoveDataChannel(DataChannel dataChannel)
        {
            ThrowIfConnectionNotOpen();
            if (DataChannels.Remove(dataChannel))
            {
                // Notify the data channel is being closed.
                // DestroyNative() will further change the state to Closed() once done.
                dataChannel.State = DataChannel.ChannelState.Closing;

                var res = PeerConnectionInterop.PeerConnection_RemoveDataChannel(_nativePeerhandle, dataChannel._nativeHandle);
                Utils.ThrowOnErrorCode(res);
            }
            else
            {
                throw new ArgumentException($"Data channel {dataChannel.Label} is not owned by peer connection {Name}.", "dataChannel");
            }
        }

        #endregion


        #region Signaling

        /// <summary>
        /// Inform the WebRTC peer connection of a newly received ICE candidate.
        /// </summary>
        /// <param name="candidate">The ICE candidate received from the remote peer.</param>
        /// <exception xref="InvalidOperationException">The peer connection is not initialized.</exception>
        public void AddIceCandidate(IceCandidate candidate)
        {
            MainEventSource.Log.AddIceCandidate(candidate.SdpMid, candidate.SdpMlineIndex, candidate.Content);
            ThrowIfConnectionNotOpen();
            var marshalCandidate = new PeerConnectionInterop.IceCandidate
            {
                SdpMid = candidate.SdpMid,
                SdpMlineIndex = candidate.SdpMlineIndex,
                Content = candidate.Content
            };
            PeerConnectionInterop.PeerConnection_AddIceCandidate(_nativePeerhandle, in marshalCandidate);
        }

        /// <summary>
        /// Create an SDP offer message as an attempt to establish a connection.
        /// Once the message is ready to be sent, the <see cref="LocalSdpReadytoSend"/> event is fired
        /// to allow the user to send that message to the remote peer via its selected signaling solution.
        /// </summary>
        /// <returns><c>true</c> if the offer creation task was successfully submitted.</returns>
        /// <exception xref="InvalidOperationException">The peer connection is not initialized.</exception>
        /// <remarks>
        /// The SDP offer message is not successfully created until the <see cref="LocalSdpReadytoSend"/>
        /// event is triggered, and may still fail even if this method returns <c>true</c>, for example if
        /// the peer connection is not in a valid state to create an offer.
        /// </remarks>
        public bool CreateOffer()
        {
            MainEventSource.Log.CreateOffer();
            ThrowIfConnectionNotOpen();
            return (PeerConnectionInterop.PeerConnection_CreateOffer(_nativePeerhandle) == Utils.MRS_SUCCESS);
        }

        /// <summary>
        /// Create an SDP answer message to a previously-received offer, to accept a connection.
        /// Once the message is ready to be sent, the <see cref="LocalSdpReadytoSend"/> event is fired
        /// to allow the user to send that message to the remote peer via its selected signaling solution.
        /// Note that this cannot be called before <see cref="SetRemoteDescriptionAsync(SdpMessage)"/>
        /// successfully completed and applied the remote offer.
        /// </summary>
        /// <returns><c>true</c> if the answer creation task was successfully submitted.</returns>
        /// <exception xref="InvalidOperationException">The peer connection is not initialized.</exception>
        /// <remarks>
        /// The SDP answer message is not successfully created until the <see cref="LocalSdpReadytoSend"/>
        /// event is triggered, and may still fail even if this method returns <c>true</c>, for example if
        /// the peer connection is not in a valid state to create an answer.
        /// </remarks>
        public bool CreateAnswer()
        {
            MainEventSource.Log.CreateAnswer();
            ThrowIfConnectionNotOpen();
            return (PeerConnectionInterop.PeerConnection_CreateAnswer(_nativePeerhandle) == Utils.MRS_SUCCESS);
        }

        /// <summary>
        /// Set the bitrate allocated to all RTP streams sent by this connection.
        /// Other limitations might affect these limits and are respected (for example "b=AS" in SDP).
        /// </summary>
        /// <param name="minBitrateBps">Minimum bitrate in bits per second.</param>
        /// <param name="startBitrateBps">Start/current target bitrate in bits per second.</param>
        /// <param name="maxBitrateBps">Maximum bitrate in bits per second.</param>
        public void SetBitrate(uint? minBitrateBps = null, uint? startBitrateBps = null, uint? maxBitrateBps = null)
        {
            ThrowIfConnectionNotOpen();
            int signedMinBitrateBps = minBitrateBps.HasValue ? (int)minBitrateBps.Value : -1;
            int signedStartBitrateBps = startBitrateBps.HasValue ? (int)startBitrateBps.Value : -1;
            int signedMaxBitrateBps = maxBitrateBps.HasValue ? (int)maxBitrateBps.Value : -1;
            uint res = PeerConnectionInterop.PeerConnection_SetBitrate(_nativePeerhandle,
                signedMinBitrateBps, signedStartBitrateBps, signedMaxBitrateBps);
            Utils.ThrowOnErrorCode(res);
        }

        /// <summary>
        /// Pass the given SDP description received from the remote peer via signaling to the
        /// underlying WebRTC implementation, which will parse and use it.
        ///
        /// This must be called by the signaler when receiving a message. Once this operation
        /// has completed, it is safe to call <see cref="CreateAnswer"/>.
        /// </summary>
        /// <param name="message">The SDP message</param>
        /// <returns>Returns a task which completes once the remote description has been applied and transceivers
        /// have been updated.</returns>
        /// <exception xref="InvalidOperationException">The peer connection is not initialized, or the peer connection
        /// is not in an expected state to apply the given message.</exception>
        /// <exception xref="ArgumentException">At least one of the arguments is invalid, including a malformed SDP
        /// message that failed to be parsed.</exception>
        public Task SetRemoteDescriptionAsync(SdpMessage message)
        {
            ThrowIfConnectionNotOpen();

            // If the user specified a preferred audio or video codec, manipulate the SDP message
            // to exclude other codecs if the preferred one is supported.
            // We set the local codec params by forcing them here. There seems to be no direct way to set
            // local codec params so we "pretend" that the remote endpoint is asking for them.
            string newSdp = ForceSdpCodecs(sdp: message.Content,
                audio: PreferredAudioCodec,
                audioParams: PreferredAudioCodecExtraParamsLocal,
                video: PreferredVideoCodec,
                videoParams: PreferredVideoCodecExtraParamsLocal);

            return PeerConnectionInterop.SetRemoteDescriptionAsync(_nativePeerhandle, message.Type, newSdp);
        }

        #endregion

        /// <summary>
        /// Subset of RTCDataChannelStats. See <see href="https://www.w3.org/TR/webrtc-stats/#dcstats-dict*"/>
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct DataChannelStats
        {
            /// <summary>
            /// Unix timestamp (time since Epoch) of the statistics. For remote statistics, this is
            /// the time at which the information reached the local endpoint.
            /// </summary>
            public long TimestampUs;

            /// <summary>
            /// <see cref="DataChannel.ID"/> of the data channel associated with these statistics.
            /// </summary>
            public long DataChannelIdentifier;

            /// <summary>
            /// Total number of API message event sent.
            /// </summary>
            public uint MessagesSent;

            /// <summary>
            /// Total number of payload bytes sent, excluding headers and paddings.
            /// </summary>
            public ulong BytesSent;

            /// <summary>
            /// Total number of API message events received.
            /// </summary>
            public uint MessagesReceived;

            /// <summary>
            /// Total number of payload bytes received, excluding headers and paddings.
            /// </summary>
            public ulong BytesReceived;
        }

        /// <summary>
        /// Subset of RTCMediaStreamTrack (audio sender) and RTCOutboundRTPStreamStats.
        /// See <see href="https://www.w3.org/TR/webrtc-stats/#raststats-dict*"/>
        /// and <see href="https://www.w3.org/TR/webrtc-stats/#sentrtpstats-dict*"/>.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public unsafe struct AudioSenderStats
        {
            #region Track statistics

            /// <summary>
            /// Unix timestamp (time since Epoch) of the audio statistics. For remote statistics,
            /// this is the time at which the information reached the local endpoint.
            /// </summary>
            public long TrackStatsTimestampUs;

            /// <summary>
            /// Track identifier.
            /// </summary>
            [MarshalAs(UnmanagedType.LPStr)]
            public string TrackIdentifier;

            /// <summary>
            /// Linear audio level of the media source, in [0:1] range, averaged over a small interval.
            /// </summary>
            public double AudioLevel;

            /// <summary>
            /// Total audio energy of the media source. For multi-channel sources (stereo, etc.) this is
            /// the highest energy of any of the channels for each sample.
            /// </summary>
            public double TotalAudioEnergy;

            /// <summary>
            /// Total duration in seconds of all the samples produced by the media source for the lifetime
            /// of the underlying internal statistics object. Like <see cref="TotalAudioEnergy"/> this is not
            /// affected by the number of channels per sample.
            /// </summary>
            public double TotalSamplesDuration;

            #endregion


            #region RTP statistics

            /// <summary>
            /// Unix timestamp (time since Epoch) of the RTP statistics. For remote statistics,
            /// this is the time at which the information reached the local endpoint.
            /// </summary>
            public long RtpStatsTimestampUs;

            /// <summary>
            /// Total number of RTP packets sent for this SSRC.
            /// </summary>
            public uint PacketsSent;

            /// <summary>
            /// Total number of bytes sent for this SSRC.
            /// </summary>
            public ulong BytesSent;

            #endregion
        }

        /// <summary>
        /// Subset of RTCMediaStreamTrack (audio receiver) and RTCInboundRTPStreamStats.
        /// See <see href="https://www.w3.org/TR/webrtc-stats/#aststats-dict*"/>
        /// and <see href="https://www.w3.org/TR/webrtc-stats/#inboundrtpstats-dict*"/>.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct AudioReceiverStats
        {
            #region Track statistics

            /// <summary>
            /// Unix timestamp (time since Epoch) of the statistics. For remote statistics, this is
            /// the time at which the information reached the local endpoint.
            /// </summary>
            public long TrackStatsTimestampUs;

            /// <summary>
            /// Track identifier.
            /// </summary>
            public string TrackIdentifier;

            /// <summary>
            /// Linear audio level of the receiving track, in [0:1] range, averaged over a small interval.
            /// </summary>
            public double AudioLevel;

            /// <summary>
            /// Total audio energy of the received track. For multi-channel sources (stereo, etc.) this is
            /// the highest energy of any of the channels for each sample.
            /// </summary>
            public double TotalAudioEnergy;

            /// <summary>
            /// Total number of RTP samples received for this audio stream.
            /// Like <see cref="TotalAudioEnergy"/> this is not affected by the number of channels per sample.
            /// </summary>
            public double TotalSamplesReceived;

            /// <summary>
            /// Total duration in seconds of all the samples received (and thus counted by <see cref="TotalSamplesReceived"/>).
            /// Like <see cref="TotalAudioEnergy"/> this is not affected by the number of channels per sample.
            /// </summary>
            public double TotalSamplesDuration;

            #endregion


            #region RTP statistics

            /// <summary>
            /// Unix timestamp (time since Epoch) of the RTP statistics. For remote statistics,
            /// this is the time at which the information reached the local endpoint.
            /// </summary>
            public long RtpStatsTimestampUs;

            /// <summary>
            /// Total number of RTP packets received for this SSRC.
            /// </summary>
            public uint PacketsReceived;

            /// <summary>
            /// Total number of bytes received for this SSRC.
            /// </summary>
            public ulong BytesReceived;

            #endregion
        }

        /// <summary>
        /// Subset of RTCMediaStreamTrack (video sender) and RTCOutboundRTPStreamStats.
        /// See <see href="https://www.w3.org/TR/webrtc-stats/#vsstats-dict*"/>
        /// and <see href="https://www.w3.org/TR/webrtc-stats/#sentrtpstats-dict*"/>.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct VideoSenderStats
        {
            #region Track statistics

            /// <summary>
            /// Unix timestamp (time since Epoch) of the track statistics. For remote statistics,
            /// this is the time at which the information reached the local endpoint.
            /// </summary>
            public long TrackStatsTimestampUs;

            /// <summary>
            /// Track identifier.
            /// </summary>
            public string TrackIdentifier;

            /// <summary>
            /// Total number of frames sent on this RTP stream.
            /// </summary>
            public uint FramesSent;

            /// <summary>
            /// Total number of huge frames sent by this RTP stream. Huge frames are frames that have
            /// an encoded size at least 2.5 times the average size of the frames.
            /// </summary>
            public uint HugeFramesSent;

            #endregion


            #region RTP statistics

            /// <summary>
            /// Unix timestamp (time since Epoch) of the RTP statistics. For remote statistics,
            /// this is the time at which the information reached the local endpoint.
            /// </summary>
            public long RtpStatsTimestampUs;

            /// <summary>
            /// Total number of RTP packets sent for this SSRC.
            /// </summary>
            public uint PacketsSent;

            /// <summary>
            /// Total number of bytes sent for this SSRC.
            /// </summary>
            public ulong BytesSent;

            /// <summary>
            /// Total number of frames successfully encoded for this RTP media stream.
            /// </summary>
            public uint FramesEncoded;

            #endregion
        }

        /// <summary>
        /// Subset of RTCMediaStreamTrack (video receiver) + RTCInboundRTPStreamStats.
        /// See <see href="https://www.w3.org/TR/webrtc-stats/#rvststats-dict*"/>
        /// and <see href="https://www.w3.org/TR/webrtc-stats/#inboundrtpstats-dict*"/>
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct VideoReceiverStats
        {
            #region Track statistics

            /// <summary>
            /// Unix timestamp (time since Epoch) of the track statistics. For remote statistics,
            /// this is the time at which the information reached the local endpoint.
            /// </summary>
            public long TrackStatsTimestampUs;

            /// <summary>
            /// Track identifier.
            /// </summary>
            public string TrackIdentifier;

            /// <summary>
            /// Total number of complete frames received on this RTP stream.
            /// </summary>
            public uint FramesReceived;

            /// <summary>
            /// Total number since the receiver was created of frames dropped prior to decode or
            /// dropped because the frame missed its display deadline for this receiver's track.
            /// </summary>
            public uint FramesDropped;

            #endregion


            #region RTP statistics

            /// <summary>
            /// Unix timestamp (time since Epoch) of the RTP statistics. For remote statistics,
            /// this is the time at which the information reached the local endpoint.
            /// </summary>
            public long RtpStatsTimestampUs;

            /// <summary>
            /// Total number of RTP packets received for this SSRC.
            /// </summary>
            public uint PacketsReceived;

            /// <summary>
            /// Total number of bytes received for this SSRC.
            /// </summary>
            public ulong BytesReceived;

            /// <summary>
            /// Total number of frames correctly decoded for this RTP stream, that would be displayed
            /// if no frames are dropped.
            /// </summary>
            public uint FramesDecoded;

            #endregion
        }

        /// <summary>
        /// Subset of RTCTransportStats.
        /// See <see href="https://www.w3.org/TR/webrtc-stats/#transportstats-dict*"/>.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct TransportStats
        {
            /// <summary>
            /// Unix timestamp (time since Epoch) of the statistics. For remote statistics, this is
            /// the time at which the information reached the local endpoint.
            /// </summary>
            public long TimestampUs;

            /// <summary>
            /// Total number of payload bytes sent on this <see cref="PeerConnection"/>, excluding
            /// headers and paddings.
            /// </summary>
            public ulong BytesSent;

            /// <summary>
            /// Total number of payload bytes received on this <see cref="PeerConnection"/>, excluding
            /// headers and paddings.
            /// </summary>
            public ulong BytesReceived;
        }

        /// <summary>
        /// Snapshot of the statistics relative to a peer connection/track.
        /// The various stats objects can be read through <see cref="GetStats{T}"/>.
        /// </summary>
        public class StatsReport : IDisposable
        {
            internal class Handle : SafeHandle
            {
                internal Handle(IntPtr h) : base(IntPtr.Zero, true) { handle = h; }
                public override bool IsInvalid => handle == IntPtr.Zero;
                protected override bool ReleaseHandle()
                {
                    PeerConnectionInterop.StatsReport_RemoveRef(handle);
                    return true;
                }
            }

            private Handle _handle;

            internal StatsReport(IntPtr h) { _handle = new Handle(h); }

            /// <summary>
            /// Get all the instances of a specific stats type in the report.
            /// </summary>
            /// <typeparam name="T">
            /// Must be one of <see cref="DataChannelStats"/>, <see cref="AudioSenderStats"/>,
            /// <see cref="AudioReceiverStats"/>, <see cref="VideoSenderStats"/>, <see cref="VideoReceiverStats"/>,
            /// <see cref="TransportStats"/>.
            /// </typeparam>
            public IEnumerable<T> GetStats<T>()
            {
                return PeerConnectionInterop.GetStatsObject<T>(_handle);
            }

            /// <summary>
            /// Dispose of the report.
            /// </summary>
            public void Dispose()
            {
                ((IDisposable)_handle).Dispose();
            }
        }

        /// <summary>
        /// Get a snapshot of the statistics relative to the peer connection.
        /// </summary>
        /// <exception xref="InvalidOperationException">The peer connection is not initialized.</exception>
        public Task<StatsReport> GetSimpleStatsAsync()
        {
            ThrowIfConnectionNotOpen();
            return PeerConnectionInterop.GetSimpleStatsAsync(_nativePeerhandle);
        }

        /// <summary>
        /// Utility to throw an exception if a method is called before the underlying
        /// native peer connection has been initialized.
        /// </summary>
        /// <exception xref="InvalidOperationException">The peer connection is not initialized.</exception>
        private void ThrowIfConnectionNotOpen()
        {
            lock (_openCloseLock)
            {
                if (_nativePeerhandle.IsClosed)
                {
                    MainEventSource.Log.PeerConnectionNotOpenError();
                    throw new InvalidOperationException("Cannot invoke native method with invalid peer connection handle.");
                }
            }
        }

        /// <summary>
        /// Frame height round mode.
        /// </summary>
        /// <seealso cref="SetFrameHeightRoundMode(FrameHeightRoundMode)"/>
        public enum FrameHeightRoundMode
        {
            /// <summary>
            /// Leave frames unchanged.
            /// </summary>
            None = 0,

            /// <summary>
            /// Crop frame height to the nearest multiple of 16.
            /// ((height - nearestLowerMultipleOf16) / 2) rows are cropped from the top and
            /// (height - nearestLowerMultipleOf16 - croppedRowsTop) rows are cropped from the bottom.
            /// </summary>
            Crop = 1,

            /// <summary>
            /// Pad frame height to the nearest multiple of 16.
            /// ((nearestHigherMultipleOf16 - height) / 2) rows are added symmetrically at the top and
            /// (nearestHigherMultipleOf16 - height - addedRowsTop) rows are added symmetrically at the bottom.
            /// </summary>
            Pad = 2
        }

        /// <summary>
        /// [HoloLens 1 only]
        /// Use this function to select whether resolutions where height is not multiple of 16 pixels
        /// should be cropped, padded, or left unchanged.
        ///
        /// Default is <see cref="FrameHeightRoundMode.Crop"/> to avoid severe artifacts produced by
        /// the H.264 hardware encoder on HoloLens 1 due to a bug with the encoder. This is the
        /// recommended value, and should be used unless cropping discards valuable data in the top and
        /// bottom rows for a given usage, in which case <see cref="FrameHeightRoundMode.Pad"/> can
        /// be used as a replacement but may still produce some mild artifacts.
        ///
        /// This has no effect on other platforms.
        /// </summary>
        /// <param name="value">The rounding mode for video frames.</param>
        public static void SetFrameHeightRoundMode(FrameHeightRoundMode value)
        {
            Utils.SetFrameHeightRoundMode(value);
        }

        /// <summary>
        /// H.264 Encoding profile.
        /// </summary>
        public enum H264Profile
        {
            /// <summary>
            /// Constrained Baseline profile.
            /// </summary>
            ConstrainedBaseline,

            /// <summary>
            /// Baseline profile.
            /// </summary>
            Baseline,

            /// <summary>
            /// Main profile.
            /// </summary>
            Main,

            /// <summary>
            /// Constrained High profile.
            /// </summary>
            ConstrainedHigh,

            /// <summary>
            /// High profile.
            /// </summary>
            High
        };

        /// <summary>
        /// Rate control mode for the Media Foundation H.264.
        /// See https://docs.microsoft.com/en-us/windows/win32/medfound/h-264-video-encoder for details.
        /// </summary>
        public enum H264RcMode
        {
            /// <summary>
            /// Constant Bit Rate.
            /// </summary>
            CBR,

            /// <summary>
            /// Variable Bit Rate.
            /// </summary>
            VBR,

            /// <summary>
            /// Constant quality.
            /// </summary>
            Quality
        };

        /// <summary>
        /// Configuration for the Media Foundation H.264 encoder.
        /// </summary>
        public struct H264Config
        {
            /// <summary>
            /// H.264 profile.
            /// Note : by default we should use what's passed by WebRTC on codec
            /// initialization (which seems to be always ConstrainedBaseline), but we use
            /// Baseline to avoid changing behavior compared to earlier versions.
            /// </summary>
            public H264Profile Profile;

            /// <summary>
            /// Rate control mode.
            /// </summary>
            public H264RcMode? RcMode;

            /// <summary>
            /// If set to a value between 0 and 51, determines the max QP to use for
            /// encoding.
            /// </summary>
            public int? MaxQp;

            /// <summary>
            /// If set to a value between 0 and 100, determines the target quality value.
            /// The effect of this depends on the encoder and on the rate control mode
            /// chosen. In the Quality RC mode this will be the target for the whole
            /// stream, while in VBR it might be used as a target for individual frames
            /// while the average quality of the stream is determined by the target
            /// bitrate.
            /// </summary>
            public int? Quality;
        };

        /// <summary>
        /// Set the configuration used by the H.264 encoder.
        /// The passed value will apply to all tracks that start streaming, from any
        /// PeerConnection created by the application, after the call to this function.
        /// </summary>
        /// <param name="config"></param>
        public static void SetH264Config(H264Config config)
        {
            uint res = Utils.SetH264Config(new Utils.H264Config(config));
            Utils.ThrowOnErrorCode(res);
        }

        internal void OnConnected()
        {
            MainEventSource.Log.Connected();
            IsConnected = true;
            Connected?.Invoke();
        }

        internal void OnDataChannelAdded(DataChannel dataChannel)
        {
            MainEventSource.Log.DataChannelAdded(dataChannel.ID, dataChannel.Label);
            DataChannels.Add(dataChannel);
            DataChannelAdded?.Invoke(dataChannel);
        }

        internal void OnDataChannelRemoved(DataChannel dataChannel)
        {
            MainEventSource.Log.DataChannelRemoved(dataChannel.ID, dataChannel.Label);
            DataChannels.Remove(dataChannel);
            DataChannelRemoved?.Invoke(dataChannel);
        }

        private static string ForceSdpCodecs(string sdp, string audio, string audioParams, string video, string videoParams)
        {
            if ((audio.Length > 0) || (video.Length > 0))
            {
                // +1 for space/semicolon before params.
                var initialLength = sdp.Length +
                    audioParams.Length + 1 +
                    videoParams.Length + 1;
                var builder = new StringBuilder(initialLength);
                ulong lengthInOut = (ulong)builder.Capacity + 1; // includes null terminator
                var audioFilter = new Utils.SdpFilter
                {
                    CodecName = audio,
                    ExtraParams = audioParams
                };
                var videoFilter = new Utils.SdpFilter
                {
                    CodecName = video,
                    ExtraParams = videoParams
                };

                uint res = Utils.SdpForceCodecs(sdp, audioFilter, videoFilter, builder, ref lengthInOut);
                if (res == Utils.MRS_E_INVALID_PARAMETER && lengthInOut > (ulong)builder.Capacity + 1)
                {
                    // New string is longer than the estimate (there might be multiple tracks).
                    // Increase the capacity and retry.
                    builder.Capacity = (int)lengthInOut - 1;
                    res = Utils.SdpForceCodecs(sdp, audioFilter, videoFilter, builder, ref lengthInOut);
                }
                Utils.ThrowOnErrorCode(res);
                builder.Length = (int)lengthInOut - 1; // discard the null terminator
                return builder.ToString();
            }
            return sdp;
        }

        /// <summary>
        /// Callback invoked by the internal WebRTC implementation when it needs a SDP message
        /// to be dispatched to the remote peer.
        /// </summary>
        /// <param name="type">The SDP message type.</param>
        /// <param name="sdp">The SDP message content.</param>
        internal void OnLocalSdpReadytoSend(SdpMessageType type, string sdp)
        {
            MainEventSource.Log.LocalSdpReady(type, sdp);

            // If the user specified a preferred audio or video codec, manipulate the SDP message
            // to exclude other codecs if the preferred one is supported.
            // Outgoing answers are filtered for the only purpose of adding the extra params to them.
            // The codec itself will have already been selected when filtering the incoming offer that prompted
            // the answer. Note that filtering the codec in the answer without doing it in
            // the offer first will leave the connection in an inconsistent state.
            string newSdp = ForceSdpCodecs(sdp: sdp,
                audio: PreferredAudioCodec,
                audioParams: PreferredAudioCodecExtraParamsRemote,
                video: PreferredVideoCodec,
                videoParams: PreferredVideoCodecExtraParamsRemote);

            var message = new SdpMessage
            {
                Type = type,
                Content = newSdp
            };
            LocalSdpReadytoSend?.Invoke(message);
        }

        internal void OnIceCandidateReadytoSend(in PeerConnectionInterop.IceCandidate candidate)
        {
            MainEventSource.Log.IceCandidateReady(candidate.SdpMid, candidate.SdpMlineIndex, candidate.Content);
            var iceCandidate = new IceCandidate
            {
                SdpMid = candidate.SdpMid,
                SdpMlineIndex = candidate.SdpMlineIndex,
                Content = candidate.Content
            };
            IceCandidateReadytoSend?.Invoke(iceCandidate);
        }

        internal void OnIceStateChanged(IceConnectionState newState)
        {
            MainEventSource.Log.IceStateChanged(newState);
            IceStateChanged?.Invoke(newState);
        }

        internal void OnIceGatheringStateChanged(IceGatheringState newState)
        {
            MainEventSource.Log.IceGatheringStateChanged(newState);
            IceGatheringStateChanged?.Invoke(newState);
        }

        internal void OnRenegotiationNeeded()
        {
            MainEventSource.Log.RenegotiationNeeded();
            // Check if the RenegotiationNeeded event is temporarily suspended. This happens while
            // creating a new Transceiver, until the wrapper has finished syncing.
            // The current callback is invoked from the signaling thread by the WebRTC implementation.
            // It should free the thread ASAP and not re-enter it with another API call. Since
            // typically users will call CreateOffer() in response to this event, defer the event
            // handling to a .NET worker thread instead and return immediately to the WebRTC signaling one.
            _renegotiationNeededEvent.InvokeAsync();
        }

        /// <summary>
        /// Callback on transceiver created for the peer connection, irrelevant of whether
        /// it has tracks or not. This is called both when created from the managed side or
        /// from the native side.
        /// </summary>
        /// <param name="tr">The newly created transceiver which has this peer connection as owner</param>
        internal void OnTransceiverAdded(Transceiver tr)
        {
            lock (_tracksLock)
            {
                Transceivers.Add(tr);
            }
            TransceiverAdded?.Invoke(tr);
        }

        /// <summary>
        /// Callback invoked by the native implementation when a transceiver starts receiving,
        /// and a remote audio track is created as a result to receive its audio data.
        /// </summary>
        /// <param name="track">The newly created remote audio track.</param>
        /// <param name="transceiver">The audio transceiver now receiving from the remote peer.</param>
        internal void OnAudioTrackAdded(RemoteAudioTrack track, Transceiver transceiver)
        {
            MainEventSource.Log.AudioTrackAdded(track.Name);

            Debug.Assert(transceiver.MediaKind == MediaKind.Audio);
            Debug.Assert(track.Transceiver == null);
            Debug.Assert(transceiver.RemoteAudioTrack == null);
            // track.PeerConnection was set in its constructor
            track.Transceiver = transceiver;
            transceiver.RemoteAudioTrack = track;

            AudioTrackAdded?.Invoke(track);
        }

        /// <summary>
        /// Callback invoked by the native implementation when a transceiver stops receiving,
        /// and a remote audio track is removed from it as a result.
        /// </summary>
        /// <param name="track">The remote audio track removed from the audio transceiver.</param>
        internal void OnAudioTrackRemoved(RemoteAudioTrack track)
        {
            MainEventSource.Log.AudioTrackRemoved(track.Name);
            Transceiver transceiver = track.Transceiver; // cache before removed

            Debug.Assert(track.PeerConnection == this);
            Debug.Assert(transceiver.RemoteAudioTrack == track);
            Debug.Assert(track.Transceiver == transceiver);
            track.PeerConnection = null;
            transceiver.RemoteAudioTrack = null;
            track.Transceiver = null;

            AudioTrackRemoved?.Invoke(transceiver, track);

            // PeerConnection is owning the remote track, and all internal states have been
            // updated and events triggered, so notify the track to clean its internal state.
            track.DestroyNative();
        }

        /// <summary>
        /// Callback invoked by the native implementation when a transceiver starts receiving,
        /// and a remote video track is created as a result to receive its video data.
        /// </summary>
        /// <param name="track">The newly created remote video track.</param>
        /// <param name="transceiver">The video transceiver now receiving from the remote peer.</param>
        internal void OnVideoTrackAdded(RemoteVideoTrack track, Transceiver transceiver)
        {
            MainEventSource.Log.VideoTrackAdded(track.Name);

            Debug.Assert(transceiver.MediaKind == MediaKind.Video);
            Debug.Assert(track.Transceiver == null);
            Debug.Assert(transceiver.RemoteVideoTrack == null);
            // track.PeerConnection was set in its constructor
            track.Transceiver = transceiver;
            transceiver.RemoteVideoTrack = track;

            VideoTrackAdded?.Invoke(track);
        }

        /// <summary>
        /// Callback invoked by the native implementation when a transceiver stops receiving,
        /// and a remote video track is removed from it as a result.
        /// </summary>
        /// <param name="track">The remote video track removed from the video transceiver.</param>
        internal void OnVideoTrackRemoved(RemoteVideoTrack track)
        {
            MainEventSource.Log.VideoTrackRemoved(track.Name);
            Transceiver transceiver = track.Transceiver; // cache before removed

            Debug.Assert(track.PeerConnection == this);
            Debug.Assert(transceiver.RemoteVideoTrack == track);
            Debug.Assert(track.Transceiver == transceiver);
            track.PeerConnection = null;
            transceiver.RemoteVideoTrack = null;
            track.Transceiver = null;

            VideoTrackRemoved?.Invoke(transceiver, track);

            // PeerConnection is owning the remote track, and all internal states have been
            // updated and events triggered, so notify the track to clean its internal state.
            track.DestroyNative();
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"(PeerConnection)\"{Name}\"";
        }
    }
}
