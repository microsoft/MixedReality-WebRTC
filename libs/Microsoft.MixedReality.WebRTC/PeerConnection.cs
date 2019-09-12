// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("Microsoft.MixedReality.WebRTC.Tests")]

namespace Microsoft.MixedReality.WebRTC
{
    /// <summary>
    /// Attribute to decorate managed delegates used as native callbacks (reverse P/Invoke).
    /// Required by Mono in Ahead-Of-Time (AOT) compiling, and Unity with the IL2CPP backend.
    /// </summary>
    /// 
    /// This attribute is required by Mono AOT and Unity IL2CPP, but not by .NET Core or Framework.
    /// The implementation was copied from the Mono source code (https://github.com/mono/mono).
    /// The type argument does not seem to be used anywhere in the code, and a stub implementation
    /// like this seems to be enough for IL2CPP to be able to marshal the delegate (untested on Mono).
    [AttributeUsage(AttributeTargets.Method)]
    sealed class MonoPInvokeCallbackAttribute : Attribute
    {
        public MonoPInvokeCallbackAttribute(Type t) { }
    }

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
            string ret = string.Join("\n", Urls);
            if (TurnUserName.Length > 0)
            {
                ret += $"\nusername:{TurnUserName}";
                if (TurnPassword.Length > 0)
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
    }

    /// <summary>
    /// The WebRTC peer connection object is the entry point to using WebRTC.
    /// </summary>
    public class PeerConnection : IDisposable
    {
        //public delegate void DataChannelMessageDelegate(byte[] data);
        //public delegate void DataChannelBufferingDelegate(ulong previous, ulong current, ulong limit);
        //public delegate void DataChannelStateDelegate(WebRTCDataChannel.State state);

        public delegate void LocalSdpReadyToSendDelegate(string type, string sdp);
        public delegate void IceCandidateReadytoSendDelegate(string candidate, int sdpMlineindex, string sdpMid);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void VideoCaptureDeviceEnumCallback(string id, string name);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void VideoCaptureDeviceEnumCompletedCallback();

        /// <summary>
        /// Kind of WebRTC track.
        /// </summary>
        public enum TrackKind : uint
        {
            Unknown = 0,
            Audio = 1,
            Video = 2,
            Data = 3
        };

        public enum VideoProfileKind : int
        {
            Unspecified,
            VideoRecording,
            HighQualityPhoto,
            BalancedVideoAndPhoto,
            VideoConferencing,
            PhotoSequence,
            HighFrameRate,
            VariablePhotoSequence,
            HdrWithWcgVideo,
            HdrWithWcgPhoto,
            VideoHdr8,
        };


        /// <summary>
        /// Signaler implementation used by this peer connection, as specified in the constructor.
        /// </summary>
        public ISignaler Signaler { get; }


        #region Codec filtering

        /// <summary>
        /// Name of the preferred audio codec, or empty to let WebRTC decide.
        /// See https://en.wikipedia.org/wiki/RTP_audio_video_profile for the standard SDP names.
        /// </summary>
        public string PreferredAudioCodec = string.Empty;

        /// <summary>
        /// Advanced use only. A semicolon-separated list of "key=value" pairs of arguments
        /// passed as extra parameters to the preferred audio codec during SDP filtering.
        /// This enables configuring codec-specific parameters. Arguments are passed as is,
        /// and there is no check on the validity of the parameter names nor their value.
        /// This is ignored if <see cref="PreferredAudioCodec"/> is an empty string, or is not
        /// a valid codec name found in the SDP message offer.
        /// </summary>
        public string PreferredAudioCodecExtraParams = string.Empty;

        /// <summary>
        /// Name of the preferred video codec, or empty to let WebRTC decide.
        /// See https://en.wikipedia.org/wiki/RTP_audio_video_profile for the standard SDP names.
        /// </summary>
        public string PreferredVideoCodec = string.Empty;

        /// <summary>
        /// Advanced use only. A semicolon-separated list of "key=value" pairs of arguments
        /// passed as extra parameters to the preferred video codec during SDP filtering.
        /// This enables configuring codec-specific parameters. Arguments are passed as is,
        /// and there is no check on the validity of the parameter names nor their value.
        /// This is ignored if <see cref="PreferredVideoCodec"/> is an empty string, or is not
        /// a valid codec name found in the SDP message offer.
        /// </summary>
        public string PreferredVideoCodecExtraParams = string.Empty;

        #endregion


        /// <summary>
        /// Boolean property indicating whether the peer connection has been initialized.
        /// </summary>
        public bool Initialized
        {
            get
            {
                lock (_openCloseLock)
                {
                    return (_nativePeerhandle != IntPtr.Zero);
                }
            }
        }

        public bool IsConnected { get; private set; } = false;

        /// <summary>
        /// Event fired when a connection is established.
        /// </summary>
        public event Action Connected;

        /// <summary>
        /// Event that occurs when a local SDP message is ready to be transmitted.
        /// </summary>
        public event LocalSdpReadyToSendDelegate LocalSdpReadytoSend;

        /// <summary>
        /// Event that occurs when a local ICE candidate is ready to be transmitted.
        /// </summary>
        public event IceCandidateReadytoSendDelegate IceCandidateReadytoSend;

        /// <summary>
        /// Event that occurs when a renegotiation of the session is needed.
        /// This generally occurs as a result of adding or removing tracks,
        /// and the user should call <see cref="CreateOffer"/> to actually
        /// start a renegotiation.
        /// </summary>
        public event Action RenegotiationNeeded;

        /// <summary>
        /// Event that occurs when a remote track is added to the current connection.
        /// </summary>
        public event Action<TrackKind> TrackAdded;

        /// <summary>
        /// Event that occurs when a remote track is removed from the current connection.
        /// </summary>
        public event Action<TrackKind> TrackRemoved;

        /// <summary>
        /// Event that occurs when a video frame from a local track has been
        /// produced locally and is available for render.
        /// </summary>
        public event I420VideoFrameDelegate I420LocalVideoFrameReady;

        /// <summary>
        /// Event that occurs when a video frame from a remote peer has been
        /// received and is available for render.
        /// </summary>
        public event I420VideoFrameDelegate I420RemoteVideoFrameReady;

        /// <summary>
        /// Event that occurs when a video frame from a local track has been
        /// produced locally and is available for render.
        /// </summary>
        public event ARGBVideoFrameDelegate ARGBLocalVideoFrameReady;

        /// <summary>
        /// Event that occurs when a video frame from a remote peer has been
        /// received and is available for render.
        /// </summary>
        public event ARGBVideoFrameDelegate ARGBRemoteVideoFrameReady;

        /// <summary>
        /// Event that occurs when an audio frame from a local track has been
        /// produced locally and is available for render.
        /// </summary>
        /// <remarks>
        /// WARNING -- This is currently not implemented in the underlying WebRTC
        /// implementation, so THIS EVENT IS NEVER FIRED.
        /// </remarks>
        public event AudioFrameDelegate LocalAudioFrameReady;

        /// <summary>
        /// Event that occurs when an audio frame from a remote peer has been
        /// received and is available for render.
        /// </summary>
        public event AudioFrameDelegate RemoteAudioFrameReady;

        /// <summary>
        /// Static wrappers for native callbacks to invoke instance methods on objects.
        /// </summary>
        private static class CallbacksWrappers
        {
            private delegate void VideoCaptureDeviceEnumDelegate(string id, string name, IntPtr handle);
            private delegate void VideoCaptureDeviceEnumCompletedDelegate(IntPtr handle);
            private delegate void ConnectedDelegate(IntPtr peer);
            private delegate void LocalSdpReadytoSendDelegate(IntPtr peer, string type, string sdp);
            private delegate void IceCandidateReadytoSendDelegate(IntPtr peer, string candidate, int sdpMlineindex, string sdpMid);
            private delegate void RenegotiationNeededDelegate(IntPtr peer);
            private delegate void TrackAddedDelegate(IntPtr peer, TrackKind trackKind);
            private delegate void TrackRemovedDelegate(IntPtr peer, TrackKind trackKind);
            private delegate void DataChannelMessageDelegate(IntPtr peer, IntPtr data, ulong size);
            private delegate void DataChannelBufferingDelegate(IntPtr peer, ulong previous, ulong current, ulong limit);
            private delegate void DataChannelStateDelegate(IntPtr peer, int state, int id);

            public class EnumVideoCaptureWrapper
            {
                public VideoCaptureDeviceEnumCallback callback;
                public VideoCaptureDeviceEnumCompletedCallback completedCallback;
            }

            [MonoPInvokeCallback(typeof(VideoCaptureDeviceEnumDelegate))]
            public static void VideoCaptureDeviceEnumCallback(string id, string name, IntPtr userData)
            {
                var handle = GCHandle.FromIntPtr(userData);
                var wrapper = (handle.Target as EnumVideoCaptureWrapper);
                wrapper.callback(id, name); // this is mandatory, never null
            }

            [MonoPInvokeCallback(typeof(VideoCaptureDeviceEnumCompletedDelegate))]
            public static void VideoCaptureDeviceEnumCompletedCallback(IntPtr userData)
            {
                var handle = GCHandle.FromIntPtr(userData);
                var wrapper = (handle.Target as EnumVideoCaptureWrapper);
                wrapper.completedCallback?.Invoke(); // this is optional, allows to be null
            }

            /// <summary>
            /// Utility to lock all delegates registered with the native plugin and prevent their garbage collection.
            /// </summary>
            /// <remarks>
            /// The delegate don't need to be pinned, just referenced to prevent garbage collection.
            /// So referencing them from this class is enough to keep them alive and usable.
            /// </remarks>
            public class PeerCallbackArgs
            {
                public PeerConnection Peer;
                public NativeMethods.PeerConnectionConnectedCallback ConnectedCallback;
                public NativeMethods.PeerConnectionLocalSdpReadytoSendCallback LocalSdpReadytoSendCallback;
                public NativeMethods.PeerConnectionIceCandidateReadytoSendCallback IceCandidateReadytoSendCallback;
                public NativeMethods.PeerConnectionRenegotiationNeededCallback RenegotiationNeededCallback;
                public NativeMethods.PeerConnectionTrackAddedCallback TrackAddedCallback;
                public NativeMethods.PeerConnectionTrackRemovedCallback TrackRemovedCallback;
                public NativeMethods.PeerConnectionI420VideoFrameCallback I420LocalVideoFrameCallback;
                public NativeMethods.PeerConnectionI420VideoFrameCallback I420RemoteVideoFrameCallback;
                public NativeMethods.PeerConnectionARGBVideoFrameCallback ARGBLocalVideoFrameCallback;
                public NativeMethods.PeerConnectionARGBVideoFrameCallback ARGBRemoteVideoFrameCallback;
                public NativeMethods.PeerConnectionAudioFrameCallback LocalAudioFrameCallback;
                public NativeMethods.PeerConnectionAudioFrameCallback RemoteAudioFrameCallback;
            }

            public class DataChannelCallbackArgs
            {
                public PeerConnection Peer;
                public DataChannel DataChannel;
                public NativeMethods.PeerConnectionDataChannelMessageCallback MessageCallback;
                public NativeMethods.PeerConnectionDataChannelBufferingCallback BufferingCallback;
                public NativeMethods.PeerConnectionDataChannelStateCallback StateCallback;

                public static DataChannelCallbackArgs FromIntPtr(IntPtr userData)
                {
                    var handle = GCHandle.FromIntPtr(userData);
                    return (handle.Target as DataChannelCallbackArgs);
                }
            }

            [MonoPInvokeCallback(typeof(ConnectedDelegate))]
            public static void ConnectedCallback(IntPtr userData)
            {
                var peer = FromIntPtr(userData);
                peer.IsConnected = true;
                peer.Connected?.Invoke();
            }

            [MonoPInvokeCallback(typeof(LocalSdpReadytoSendDelegate))]
            public static void LocalSdpReadytoSendCallback(IntPtr userData, string type, string sdp)
            {
                var peer = FromIntPtr(userData);
                peer.OnLocalSdpReadytoSend(type, sdp);
            }

            [MonoPInvokeCallback(typeof(IceCandidateReadytoSendDelegate))]
            public static void IceCandidateReadytoSendCallback(IntPtr userData, string candidate, int sdpMlineindex, string sdpMid)
            {
                var peer = FromIntPtr(userData);
                peer.OnIceCandidateReadytoSend(candidate, sdpMlineindex, sdpMid);
            }

            [MonoPInvokeCallback(typeof(RenegotiationNeededDelegate))]
            public static void RenegotiationNeededCallback(IntPtr userData)
            {
                var peer = FromIntPtr(userData);
                peer.RenegotiationNeeded?.Invoke();
            }

            [MonoPInvokeCallback(typeof(TrackAddedDelegate))]
            public static void TrackAddedCallback(IntPtr userData, TrackKind trackKind)
            {
                var peer = FromIntPtr(userData);
                peer.TrackAdded?.Invoke(trackKind);
            }

            [MonoPInvokeCallback(typeof(TrackRemovedDelegate))]
            public static void TrackRemovedCallback(IntPtr userData, TrackKind trackKind)
            {
                var peer = FromIntPtr(userData);
                peer.TrackRemoved?.Invoke(trackKind);
            }

            [MonoPInvokeCallback(typeof(I420VideoFrameDelegate))]
            public static void I420LocalVideoFrameCallback(IntPtr userData,
                IntPtr dataY, IntPtr dataU, IntPtr dataV, IntPtr dataA,
                int strideY, int strideU, int strideV, int strideA,
                int width, int height)
            {
                var peer = FromIntPtr(userData);
                var frame = new I420AVideoFrame()
                {
                    width = (uint)width,
                    height = (uint)height,
                    dataY = dataY,
                    dataU = dataU,
                    dataV = dataV,
                    dataA = dataA,
                    strideY = strideY,
                    strideU = strideU,
                    strideV = strideV,
                    strideA = strideA
                };
                peer.I420LocalVideoFrameReady?.Invoke(frame);
            }

            [MonoPInvokeCallback(typeof(I420VideoFrameDelegate))]
            public static void I420RemoteVideoFrameCallback(IntPtr userData,
                IntPtr dataY, IntPtr dataU, IntPtr dataV, IntPtr dataA,
                int strideY, int strideU, int strideV, int strideA,
                int width, int height)
            {
                var peer = FromIntPtr(userData);
                var frame = new I420AVideoFrame()
                {
                    width = (uint)width,
                    height = (uint)height,
                    dataY = dataY,
                    dataU = dataU,
                    dataV = dataV,
                    dataA = dataA,
                    strideY = strideY,
                    strideU = strideU,
                    strideV = strideV,
                    strideA = strideA
                };
                peer.I420RemoteVideoFrameReady?.Invoke(frame);
            }

            [MonoPInvokeCallback(typeof(ARGBVideoFrameDelegate))]
            public static void ARGBLocalVideoFrameCallback(IntPtr userData,
                IntPtr data, int stride, int width, int height)
            {
                var peer = FromIntPtr(userData);
                var frame = new ARGBVideoFrame()
                {
                    width = (uint)width,
                    height = (uint)height,
                    data = data,
                    stride = stride
                };
                peer.ARGBLocalVideoFrameReady?.Invoke(frame);
            }

            [MonoPInvokeCallback(typeof(ARGBVideoFrameDelegate))]
            public static void ARGBRemoteVideoFrameCallback(IntPtr userData,
                IntPtr data, int stride, int width, int height)
            {
                var peer = FromIntPtr(userData);
                var frame = new ARGBVideoFrame()
                {
                    width = (uint)width,
                    height = (uint)height,
                    data = data,
                    stride = stride
                };
                peer.ARGBRemoteVideoFrameReady?.Invoke(frame);
            }

            [MonoPInvokeCallback(typeof(AudioFrameDelegate))]
            public static void LocalAudioFrameCallback(IntPtr userData, IntPtr audioData, uint bitsPerSample,
                uint sampleRate, uint channelCount, uint frameCount)
            {
                var peer = FromIntPtr(userData);
                var frame = new AudioFrame()
                {
                    bitsPerSample = bitsPerSample,
                    sampleRate = sampleRate,
                    channelCount = channelCount,
                    frameCount = frameCount,
                    audioData = audioData
                };
                peer.LocalAudioFrameReady?.Invoke(frame);
            }

            [MonoPInvokeCallback(typeof(AudioFrameDelegate))]
            public static void RemoteAudioFrameCallback(IntPtr userData, IntPtr audioData, uint bitsPerSample,
                uint sampleRate, uint channelCount, uint frameCount)
            {
                var peer = FromIntPtr(userData);
                var frame = new AudioFrame()
                {
                    bitsPerSample = bitsPerSample,
                    sampleRate = sampleRate,
                    channelCount = channelCount,
                    frameCount = frameCount,
                    audioData = audioData
                };
                peer.RemoteAudioFrameReady?.Invoke(frame);
            }

            [MonoPInvokeCallback(typeof(DataChannelMessageDelegate))]
            public static void DataChannelMessageCallback(IntPtr userData, IntPtr data, ulong size)
            {
                var args = DataChannelCallbackArgs.FromIntPtr(userData);
                args.DataChannel.OnMessageReceived(data, size);
            }

            [MonoPInvokeCallback(typeof(DataChannelBufferingDelegate))]
            public static void DataChannelBufferingCallback(IntPtr userData, ulong previous, ulong current, ulong limit)
            {
                var args = DataChannelCallbackArgs.FromIntPtr(userData);
                args.DataChannel.OnBufferingChanged(previous, current, limit);
            }

            [MonoPInvokeCallback(typeof(DataChannelStateDelegate))]
            public static void DataChannelStateCallback(IntPtr userData, int state, int id)
            {
                var args = DataChannelCallbackArgs.FromIntPtr(userData);
                args.DataChannel.OnStateChanged(state, id);
            }
        }

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
        private IntPtr _nativePeerhandle = IntPtr.Zero;

        /// <summary>
        /// Initialization task returned by <see cref="InitializeAsync"/>.
        /// </summary>
        private Task _initTask = null;

        private CancellationTokenSource _initCTS = new CancellationTokenSource();

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
        private object _openCloseLock = new object();

        private CallbacksWrappers.PeerCallbackArgs _peerCallbackArgs;


        #region Initializing and shutdown

        /// <summary>
        /// Construct an uninitialized peer connection object which will delegate to the given
        /// <see cref="ISignaler"/> implementation for its WebRTC signaling needs.
        /// </summary>
        /// <param name="signaler">The signaling implementation to use.</param>
        public PeerConnection(ISignaler signaler)
        {
            Signaler = signaler;
            Signaler.OnMessage += Signaler_OnMessage;
        }

        private void Signaler_OnMessage(SignalerMessage message)
        {
            switch (message.MessageType)
            {
                case SignalerMessage.WireMessageType.Offer:
                    SetRemoteDescription("offer", message.Data);
                    // If we get an offer, we immediately send an answer back
                    CreateAnswer();
                    break;

                case SignalerMessage.WireMessageType.Answer:
                    SetRemoteDescription("answer", message.Data);
                    break;

                case SignalerMessage.WireMessageType.Ice:
                    // TODO - This is NodeDSS-specific
                    // this "parts" protocol is defined above, in OnIceCandiateReadyToSend listener
                    var parts = message.Data.Split(new string[] { message.IceDataSeparator }, StringSplitOptions.RemoveEmptyEntries);
                    // Note the inverted arguments; candidate is last here, but first in OnIceCandiateReadyToSend
                    AddIceCandidate(parts[2], int.Parse(parts[1]), parts[0]);
                    break;

                default:
                    throw new InvalidOperationException($"Unhandled signaler message type '{message.MessageType}'");
            }
        }

        /// <summary>
        /// Initialize the current peer connection object asynchronously.
        /// </summary>
        /// <param name="token">Optional cancellation token for the initialize task. This is only used if
        /// the singleton task was created by this call, and not a prior call.</param>
        /// <returns>The singleton task used to initialize the underlying native peer connection.</returns>
        /// <remarks>This method is multi-thread safe, and will always return the same task object
        /// from the first call to it until the peer connection object is deinitialized. This allows
        /// multiple callers to all execute some action following the initialization, without the need
        /// to force a single caller and to synchronize with it.</remarks>
        public Task InitializeAsync(PeerConnectionConfiguration config = default, CancellationToken token = default)
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
                _peerCallbackArgs = new CallbacksWrappers.PeerCallbackArgs()
                {
                    Peer = this,
                    ConnectedCallback = CallbacksWrappers.ConnectedCallback,
                    LocalSdpReadytoSendCallback = CallbacksWrappers.LocalSdpReadytoSendCallback,
                    IceCandidateReadytoSendCallback = CallbacksWrappers.IceCandidateReadytoSendCallback,
                    RenegotiationNeededCallback = CallbacksWrappers.RenegotiationNeededCallback,
                    TrackAddedCallback = CallbacksWrappers.TrackAddedCallback,
                    TrackRemovedCallback = CallbacksWrappers.TrackRemovedCallback,
                    I420LocalVideoFrameCallback = CallbacksWrappers.I420LocalVideoFrameCallback,
                    I420RemoteVideoFrameCallback = CallbacksWrappers.I420RemoteVideoFrameCallback,
                    ARGBLocalVideoFrameCallback = CallbacksWrappers.ARGBLocalVideoFrameCallback,
                    ARGBRemoteVideoFrameCallback = CallbacksWrappers.ARGBRemoteVideoFrameCallback,
                    LocalAudioFrameCallback = CallbacksWrappers.LocalAudioFrameCallback,
                    RemoteAudioFrameCallback = CallbacksWrappers.RemoteAudioFrameCallback
                };

                // Cache values in local variables before starting async task, to avoid any
                // subsequent external change from affecting that task.
                // Also set default values, as the native call doesn't handle NULL.
                var nativeConfig = new NativeMethods.PeerConnectionConfiguration
                {
                    EncodedIceServers = string.Join("\n\n", config.IceServers),
                    IceTransportType = config.IceTransportType,
                    BundlePolicy = config.BundlePolicy,
                };

                // On UWP PeerConnectionCreate() fails on main UI thread, so always initialize the native peer
                // connection asynchronously from a background worker thread.
                //using (var cancelOrCloseToken = CancellationTokenSource.CreateLinkedTokenSource(_initCTS.Token, token))
                //{
                _initTask = Task.Run(() =>
                {
                    token.ThrowIfCancellationRequested();

                    IntPtr nativeHandle = IntPtr.Zero;
                    uint res = NativeMethods.PeerConnectionCreate(nativeConfig, out nativeHandle);

                    lock (_openCloseLock)
                    {
                        // Handle errors
                        if ((res != NativeMethods.MRS_SUCCESS) || (nativeHandle == IntPtr.Zero))
                        {
                            if (_selfHandle.IsAllocated)
                            {
                                _peerCallbackArgs = null;
                                _selfHandle.Free();
                            }

                            if (res == NativeMethods.MRS_E_INVALID_PARAMETER)
                            {
                                throw new ArgumentNullException();
                            }
                            throw new Exception();
                        }

                        // The connection may have been aborted while being created, either via the
                        // cancellation token, or by calling Close() after the synchronous codepath
                        // above but before this task had a chance to run in the background.
                        if (token.IsCancellationRequested)
                        {
                            // Cancelled by token
                            NativeMethods.PeerConnectionClose(ref nativeHandle);
                            throw new OperationCanceledException(token);
                        }
                        if (!_selfHandle.IsAllocated)
                        {
                            // Cancelled by calling Close()
                            NativeMethods.PeerConnectionClose(ref nativeHandle);
                            throw new OperationCanceledException();
                        }

                        _nativePeerhandle = nativeHandle;

                        // Register all trampoline callbacks. Note that even passing a static managed method
                        // for the callback is not safe, because the compiler implicitly creates a delegate
                        // object (a static method is not a delegate itself; it can be wrapped inside one),
                        // and that delegate object will be garbage collected immediately at the end of this
                        // block. Instead, a delegate needs to be explicitly created and locked in memory.
                        // Since the current PeerConnection instance is already locked via _selfHandle,
                        // and it references all delegates via _peerCallbackArgs, those also can't be GC'd.
                        var self = GCHandle.ToIntPtr(_selfHandle);
                        NativeMethods.PeerConnectionRegisterConnectedCallback(
                            _nativePeerhandle, _peerCallbackArgs.ConnectedCallback, self);
                        NativeMethods.PeerConnectionRegisterLocalSdpReadytoSendCallback(
                            _nativePeerhandle, _peerCallbackArgs.LocalSdpReadytoSendCallback, self);
                        NativeMethods.PeerConnectionRegisterIceCandidateReadytoSendCallback(
                            _nativePeerhandle, _peerCallbackArgs.IceCandidateReadytoSendCallback, self);
                        NativeMethods.PeerConnectionRegisterRenegotiationNeededCallback(
                            _nativePeerhandle, _peerCallbackArgs.RenegotiationNeededCallback, self);
                        NativeMethods.PeerConnectionRegisterTrackAddedCallback(
                            _nativePeerhandle, _peerCallbackArgs.TrackAddedCallback, self);
                        NativeMethods.PeerConnectionRegisterTrackRemovedCallback(
                            _nativePeerhandle, _peerCallbackArgs.TrackRemovedCallback, self);
                        NativeMethods.PeerConnectionRegisterI420LocalVideoFrameCallback(
                            _nativePeerhandle, _peerCallbackArgs.I420LocalVideoFrameCallback, self);
                        NativeMethods.PeerConnectionRegisterI420RemoteVideoFrameCallback(
                            _nativePeerhandle, _peerCallbackArgs.I420RemoteVideoFrameCallback, self);
                        //NativeMethods.PeerConnectionRegisterARGBLocalVideoFrameCallback(
                        //    _nativePeerhandle, _peerCallbackArgs.ARGBLocalVideoFrameCallback, self);
                        //NativeMethods.PeerConnectionRegisterARGBRemoteVideoFrameCallback(
                        //    _nativePeerhandle, _peerCallbackArgs.ARGBRemoteVideoFrameCallback, self);
                        NativeMethods.PeerConnectionRegisterLocalAudioFrameCallback(
                            _nativePeerhandle, _peerCallbackArgs.LocalAudioFrameCallback, self);
                        NativeMethods.PeerConnectionRegisterRemoteAudioFrameCallback(
                            _nativePeerhandle, _peerCallbackArgs.RemoteAudioFrameCallback, self);
                    }
                }, token);

                return _initTask;
            }
        }

        /// <summary>
        /// Close the peer connection and destroy the underlying native resources.
        /// </summary>
        public void Close()
        {
            lock (_openCloseLock)
            {
                // If the connection is not initialized, return immediately.
                if (_initTask == null)
                {
                    return;
                }

                // Indicate to InitializeAsync() that it should stop returning _initTask,
                // as it is about to become invalid.
                _isClosing = true;
            }

            // Wait for any pending initializing to finish.
            // This must be outside of the lock because the initialization task will
            // eventually need to acquire the lock to complete.
            _initTask.Wait();

            lock (_openCloseLock)
            {
                _initTask = null;

                // This happens on connected connection only
                if (_nativePeerhandle != IntPtr.Zero)
                {
                    // Un-register all static trampoline callbacks
                    NativeMethods.PeerConnectionRegisterConnectedCallback(_nativePeerhandle, null, IntPtr.Zero);
                    NativeMethods.PeerConnectionRegisterLocalSdpReadytoSendCallback(_nativePeerhandle, null, IntPtr.Zero);
                    NativeMethods.PeerConnectionRegisterIceCandidateReadytoSendCallback(_nativePeerhandle, null, IntPtr.Zero);
                    NativeMethods.PeerConnectionRegisterRenegotiationNeededCallback(_nativePeerhandle, null, IntPtr.Zero);
                    NativeMethods.PeerConnectionRegisterTrackAddedCallback(_nativePeerhandle, null, IntPtr.Zero);
                    NativeMethods.PeerConnectionRegisterTrackRemovedCallback(_nativePeerhandle, null, IntPtr.Zero);
                    NativeMethods.PeerConnectionRegisterI420LocalVideoFrameCallback(_nativePeerhandle, null, IntPtr.Zero);
                    NativeMethods.PeerConnectionRegisterI420RemoteVideoFrameCallback(_nativePeerhandle, null, IntPtr.Zero);
                    NativeMethods.PeerConnectionRegisterARGBLocalVideoFrameCallback(_nativePeerhandle, null, IntPtr.Zero);
                    NativeMethods.PeerConnectionRegisterARGBRemoteVideoFrameCallback(_nativePeerhandle, null, IntPtr.Zero);
                    NativeMethods.PeerConnectionRegisterLocalAudioFrameCallback(_nativePeerhandle, null, IntPtr.Zero);
                    NativeMethods.PeerConnectionRegisterRemoteAudioFrameCallback(_nativePeerhandle, null, IntPtr.Zero);

                    // Close the native WebRTC peer connection and destroy the object
                    NativeMethods.PeerConnectionClose(ref _nativePeerhandle);
                    _nativePeerhandle = IntPtr.Zero;
                }

                // This happens on connected or connecting connection
                if (_selfHandle.IsAllocated)
                {
                    _peerCallbackArgs = null;
                    _selfHandle.Free();
                }

                _isClosing = false;
                IsConnected = false;
            }
        }

        /// <summary>
        /// Dispose of native resources by closing the peer connection.
        /// </summary>
        /// <remarks>This is equivalent to <see cref="Close"/>.</remarks>
        public void Dispose()
        {
            Close();
        }

        #endregion


        #region Local audio and video tracks

        /// <summary>
        /// Add to the current connection a video track from a local video capture device (webcam).
        /// </summary>
        /// <param name="device">Optional device to use, defaults to first available one.</param>
        /// <returns>Asynchronous task completed once the device is capturing and the track is added.</returns>
        /// <remarks>
        /// On UWP this requires the "webcam" capability.
        /// See <see href="https://docs.microsoft.com/en-us/windows/uwp/packaging/app-capability-declarations"/>
        /// for more details.
        /// </remarks>
        /// <exception xref="InvalidOperationException">The peer connection is not intialized.</exception>
        public Task AddLocalVideoTrackAsync(VideoCaptureDevice device = default(VideoCaptureDevice),
            string videoProfileId = default, VideoProfileKind videoProfileKind = VideoProfileKind.Unspecified,
            int width = 0, int height = 0, double framerate = 0.0, bool enableMrc = false)
        {
            ThrowIfConnectionNotOpen();
            return Task.Run(() =>
            {
                // On UWP this cannot be called from the main UI thread, so always call it from
                // a background worker thread.
                var config = new NativeMethods.VideoDeviceConfiguration
                {
                    VideoDeviceId = device.id,
                    VideoProfileId = videoProfileId,
                    VideoProfileKind = videoProfileKind,
                    Width = (uint)width,
                    Height = (uint)height,
                    Framerate = framerate,
                    EnableMixedRealityCapture = enableMrc
                };
                if (NativeMethods.PeerConnectionAddLocalVideoTrack(_nativePeerhandle, config) != NativeMethods.MRS_SUCCESS)
                {
                    throw new Exception("Failed to add a local video track to the peer connection.");
                }
            });
        }

        /// <summary>
        /// Remove from the current connection the local video track added with <see cref="AddLocalAudioTrackAsync"/>.
        /// </summary>
        /// <exception xref="InvalidOperationException">The peer connection is not intialized.</exception>
        public void RemoveLocalVideoTrack()
        {
            ThrowIfConnectionNotOpen();
            NativeMethods.PeerConnectionRemoveLocalVideoTrack(_nativePeerhandle);
        }

        /// <summary>
        /// Add to the current connection an audio track from a local audio capture device (microphone).
        /// </summary>
        /// <returns>Asynchronous task completed once the device is capturing and the track is added.</returns>
        /// <remarks>
        /// On UWP this requires the "microphone" capability.
        /// See <see href="https://docs.microsoft.com/en-us/windows/uwp/packaging/app-capability-declarations"/>
        /// for more details.
        /// </remarks>
        /// <exception xref="InvalidOperationException">The peer connection is not intialized.</exception>
        public Task AddLocalAudioTrackAsync()
        {
            ThrowIfConnectionNotOpen();
            return Task.Run(() =>
            {
                // On UWP this cannot be called from the main UI thread, so always call it from
                // a background worker thread.
                if (NativeMethods.PeerConnectionAddLocalAudioTrack(_nativePeerhandle) != NativeMethods.MRS_SUCCESS)
                {
                    throw new Exception();
                }
            });
        }

        /// <summary>
        /// Remove from the current connection the local audio track added with <see cref="AddLocalAudioTrackAsync"/>.
        /// </summary>
        /// <exception xref="InvalidOperationException">The peer connection is not intialized.</exception>
        public void RemoveLocalAudioTrack()
        {
            ThrowIfConnectionNotOpen();
            NativeMethods.PeerConnectionRemoveLocalAudioTrack(_nativePeerhandle);
        }

        #endregion


        #region Data tracks

        /// <summary>
        /// Add a new out-of-band data channel with the given ID.
        /// 
        /// A data channel is negotiated out-of-band when the peers agree on an identifier by any mean
        /// not known to WebRTC, and both open a data channel with that ID. The WebRTC will match the
        /// incoming and outgoing pipes by this ID to allow sending and receiving through that channel.
        /// 
        /// This requires some external mechanism to agree on an available identifier not otherwise taken
        /// by another channel, and also requires to ensure that both peers explicitly open that channel.
        /// </summary>
        /// <param name="id">The unique data channel identifier to use.</param>
        /// <param name="label">The data channel name.</param>
        /// <param name="ordered">Indicates whether data channel messages are ordered (see
        /// <see cref="DataChannel.Ordered"/>).</param>
        /// <param name="reliable">Indicates whether data channel messages are reliably delivered
        /// (see <see cref="DataChannel.Reliable"/>).</param>
        /// <returns>Returns a task which completes once the data channel is created.</returns>
        /// <exception xref="InvalidOperationException">The peer connection is not intialized.</exception>
        /// <exception xref="InvalidOperationException">SCTP not negotiated.</exception>
        /// <exception xref="ArgumentOutOfRangeException">Invalid data channel ID, must be in [0:65535].</exception>
        public async Task<DataChannel> AddDataChannelAsync(ushort id, string label, bool ordered, bool reliable)
        {
            if (id < 0)
            {
                throw new ArgumentOutOfRangeException("id", id, "Data channel ID must be greater than or equal to zero.");
            }
            return await AddDataChannelAsyncImpl(id, label, ordered, reliable);
        }

        /// <summary>
        /// Add a new in-band data channel whose ID will be determined by the implementation.
        /// 
        /// A data channel is negotiated in-band when one peer requests its creation to the WebRTC core,
        /// and the implementation negotiates with the remote peer an appropriate ID by sending some
        /// SDP offer message. In that case once accepted the other peer will automatically create the
        /// appropriate data channel on its side with that negotiated ID, and the ID will be returned on
        /// both sides to the user for information.
        /// 
        /// Compares to out-of-band messages, this requires exchanging some SDP messages, but avoids having
        /// to determine a common unused ID and having to explicitly open the data channel on both sides.
        /// </summary>
        /// <param name="label">The data channel name.</param>
        /// <param name="ordered">Indicates whether data channel messages are ordered (see
        /// <see cref="DataChannel.Ordered"/>).</param>
        /// <param name="reliable">Indicates whether data channel messages are reliably delivered
        /// (see <see cref="DataChannel.Reliable"/>).</param>
        /// <returns>Returns a task which completes once the data channel is created.</returns>
        /// <exception xref="InvalidOperationException">The peer connection is not intialized.</exception>
        /// <exception xref="InvalidOperationException">SCTP not negotiated.</exception>
        /// <exception xref="ArgumentOutOfRangeException">Invalid data channel ID, must be in [0:65535].</exception>
        public async Task<DataChannel> AddDataChannelAsync(string label, bool ordered, bool reliable)
        {
            return await AddDataChannelAsyncImpl(-1, label, ordered, reliable);
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
        /// <returns>Returns a task which completes once the data channel is created.</returns>
        /// <exception xref="InvalidOperationException">The peer connection is not intialized.</exception>
        /// <exception xref="InvalidOperationException">SCTP not negotiated.</exception>
        /// <exception xref="ArgumentOutOfRangeException">Invalid data channel ID, must be in [0:65535].</exception>
        private async Task<DataChannel> AddDataChannelAsyncImpl(int id, string label, bool ordered, bool reliable)
        {
            // Preconditions
            ThrowIfConnectionNotOpen();

            // Create a new data channel object and its callback args
            var dataChannel = new DataChannel(this, id, label, ordered, reliable);
            var args = new CallbacksWrappers.DataChannelCallbackArgs()
            {
                Peer = this,
                DataChannel = dataChannel,
                MessageCallback = CallbacksWrappers.DataChannelMessageCallback,
                BufferingCallback = CallbacksWrappers.DataChannelBufferingCallback,
                StateCallback = CallbacksWrappers.DataChannelStateCallback
            };

            // Pin the args to pin the delegates
            var handle = GCHandle.Alloc(args, GCHandleType.Normal);
            IntPtr userData = GCHandle.ToIntPtr(handle);

            // Create the native channel
            return await Task.Run(() =>
            {
                uint res = NativeMethods.PeerConnectionAddDataChannel(_nativePeerhandle, id, label, ordered, reliable,
                args.MessageCallback, userData, args.BufferingCallback, userData, args.StateCallback, userData);
                if (res == 0)
                {
                    return dataChannel;
                }
                handle.Free();
                if (res == 0x80000301) // MRS_E_SCTP_NOT_NEGOTIATED
                {
                    throw new InvalidOperationException("Cannot add a first data channel after the connection handshake started. Call AddDataChannelAsync() before calling CreateOffer().");
                }
                if (res == 0x80000302) // MRS_E_INVALID_DATA_CHANNEL_ID
                {
                    throw new ArgumentOutOfRangeException("id", id, "Invalid ID passed to AddDataChannelAsync().");
                }
                throw new Exception("AddDataChannelAsync() failed.");
            });
        }

        internal bool RemoveDataChannel(DataChannel dataChannel)
        {
            ThrowIfConnectionNotOpen();
            return (NativeMethods.PeerConnectionRemoveDataChannel(_nativePeerhandle, dataChannel.ID) == NativeMethods.MRS_SUCCESS);
        }

        internal void SendDataChannelMessage(int id, byte[] message)
        {
            ThrowIfConnectionNotOpen();
            NativeMethods.PeerConnectionSendDataChannelMessage(_nativePeerhandle, id, message, (ulong)message.LongLength);
        }

        #endregion


        #region Signaling

        /// <summary>
        /// Inform the WebRTC peer connection of a newly received ICE candidate.
        /// </summary>
        /// <param name="sdpMid"></param>
        /// <param name="sdpMlineindex"></param>
        /// <param name="candidate"></param>
        /// <exception xref="InvalidOperationException">The peer connection is not intialized.</exception>
        public void AddIceCandidate(string sdpMid, int sdpMlineindex, string candidate)
        {
            ThrowIfConnectionNotOpen();
            NativeMethods.PeerConnectionAddIceCandidate(_nativePeerhandle, sdpMid, sdpMlineindex, candidate);
        }

        /// <summary>
        /// Create an SDP offer message as an attempt to establish a connection.
        /// </summary>
        /// <returns><c>true</c> if the offer was created successfully.</returns>
        /// <exception xref="InvalidOperationException">The peer connection is not intialized.</exception>
        public bool CreateOffer()
        {
            ThrowIfConnectionNotOpen();
            return (NativeMethods.PeerConnectionCreateOffer(_nativePeerhandle) == NativeMethods.MRS_SUCCESS);
        }

        /// <summary>
        /// Create an SDP answer message to a previously-received offer, to accept a connection.
        /// </summary>
        /// <returns><c>true</c> if the offer was created successfully.</returns>
        /// <exception xref="InvalidOperationException">The peer connection is not intialized.</exception>
        public bool CreateAnswer()
        {
            ThrowIfConnectionNotOpen();
            return (NativeMethods.PeerConnectionCreateAnswer(_nativePeerhandle) == NativeMethods.MRS_SUCCESS);
        }

        /// <summary>
        /// Pass the given SDP description received from the remote peer via signaling to the
        /// underlying WebRTC implementation, which will parse and use it.
        /// 
        /// This must be called by the signaler when receiving a message.
        /// </summary>
        /// <param name="type">The type of SDP message ("offer", "answer", "ice")</param>
        /// <param name="sdp">The content of the SDP message</param>
        /// <exception xref="InvalidOperationException">The peer connection is not intialized.</exception>
        public void SetRemoteDescription(string type, string sdp)
        {
            ThrowIfConnectionNotOpen();
            NativeMethods.PeerConnectionSetRemoteDescription(_nativePeerhandle, type, sdp);
        }

        #endregion


        /// <summary>
        /// Utility to throw an exception if a method is called before the underlying
        /// native peer connection has been initialized.
        /// </summary>
        /// <exception xref="InvalidOperationException">The peer connection is not intialized.</exception>
        private void ThrowIfConnectionNotOpen()
        {
            lock (_openCloseLock)
            {
                if (_nativePeerhandle == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Cannot invoke native method with invalid peer connection handle.");
                }
            }
        }

        /// <summary>
        /// Helper to convert between an <see cref="IntPtr"/> holding a <see cref="GCHandle"/> to
        /// a <see cref="PeerConnection"/> object and the object itself.
        /// </summary>
        /// <param name="userData">The <see cref="GCHandle"/> to the <see cref="PeerConnection"/> object,
        /// wrapped inside an <see cref="IntPtr"/>.</param>
        /// <returns>The corresponding <see cref="PeerConnection"/> object.</returns>
        private static PeerConnection FromIntPtr(IntPtr userData)
        {
            var peerHandle = GCHandle.FromIntPtr(userData);
            var peer = (peerHandle.Target as PeerConnection);
            return peer;
        }

        /// <summary>
        /// Collection of native P/Invoke static functions.
        /// </summary>
        private static class NativeMethods
        {
#if MR_SHARING_WIN
            internal const string dllPath = "Microsoft.MixedReality.WebRTC.Native.dll";
#elif MR_SHARING_ANDROID
            internal const string dllPath = "Microsoft.MixedReality.WebRTC.Native.so";
#endif

            internal const uint MRS_SUCCESS = 0u;
            internal const uint MRS_E_UNKNOWN = 0x80000000;
            internal const uint MRS_E_INVALID_PARAMETER = 0x80000001u;
            internal const uint MRS_E_INVALID_OPERATION = 0x80000002u;
            internal const uint MRS_E_INVALID_PEER_HANDLE = 0x80000101u;
            internal const uint MRS_E_PEER_NOT_INITIALIZED = 0x80000102u;
            internal const uint MRS_E_SCTP_NOT_NEGOTIATED = 0x80000301u;
            internal const uint MRS_E_INVALID_DATA_CHANNEL_ID = 0x80000302u;

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
            internal struct PeerConnectionConfiguration
            {
                public string EncodedIceServers;
                public IceTransportType IceTransportType;
                public BundlePolicy BundlePolicy;
            }

            /// <summary>
            /// Helper structure to pass video capture device configuration to the underlying C++ library.
            /// </summary>
            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
            internal struct VideoDeviceConfiguration
            {
                /// <summary>
                /// Video capture device unique identifier, as returned by <see cref="GetVideoCaptureDevicesAsync"/>.
                /// </summary>
                public string VideoDeviceId;

                public string VideoProfileId;
                public VideoProfileKind VideoProfileKind;
                public uint Width;
                public uint Height;
                public double Framerate;

                /// <summary>
                /// Enable Mixed Reality Capture (MRC). This flag is ignored if the platform doesn't support MRC.
                /// </summary>
                public bool EnableMixedRealityCapture;
            }

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
            internal struct SdpFilter
            {
                public string CodecName;
                public string ExtraParams;
            }


            #region Unmanaged delegates

            [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
            public delegate void VideoCaptureDeviceEnumCallback(string id, string name, IntPtr userData);

            [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
            public delegate void VideoCaptureDeviceEnumCompletedCallback(IntPtr userData);

            [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
            public delegate void PeerConnectionConnectedCallback(IntPtr userData);

            [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
            public delegate void PeerConnectionLocalSdpReadytoSendCallback(IntPtr userData,
                string type, string sdp);

            [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
            public delegate void PeerConnectionIceCandidateReadytoSendCallback(IntPtr userData,
                string candidate, int sdpMlineindex, string sdpMid);

            [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
            public delegate void PeerConnectionRenegotiationNeededCallback(IntPtr userData);

            [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
            public delegate void PeerConnectionTrackAddedCallback(IntPtr userData, TrackKind trackKind);

            [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
            public delegate void PeerConnectionTrackRemovedCallback(IntPtr userData, TrackKind trackKind);

            [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
            public delegate void PeerConnectionI420VideoFrameCallback(IntPtr userData,
                IntPtr ydata, IntPtr udata, IntPtr vdata, IntPtr adata,
                int ystride, int ustride, int vstride, int astride,
                int frameWidth, int frameHeight);

            [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
            public delegate void PeerConnectionARGBVideoFrameCallback(IntPtr userData,
                IntPtr data, int stride, int frameWidth, int frameHeight);

            [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
            public delegate void PeerConnectionAudioFrameCallback(IntPtr userData,
                IntPtr data, uint bitsPerSample, uint sampleRate, uint channelCount, uint frameCount);

            [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
            public delegate void PeerConnectionDataChannelMessageCallback(IntPtr userData, IntPtr data, ulong size);

            [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
            public delegate void PeerConnectionDataChannelBufferingCallback(IntPtr userData,
                ulong previous, ulong current, ulong limit);

            [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
            public delegate void PeerConnectionDataChannelStateCallback(IntPtr userData, int state, int id);

            #endregion


            #region P/Invoke static functions

            [DllImport(dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
                EntryPoint = "mrsEnumVideoCaptureDevicesAsync")]
            public static extern void EnumVideoCaptureDevicesAsync(VideoCaptureDeviceEnumCallback callback, IntPtr userData,
                VideoCaptureDeviceEnumCompletedCallback completedCallback, IntPtr completedUserData);

            [DllImport(dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
                EntryPoint = "mrsPeerConnectionCreate")]
            public static extern uint PeerConnectionCreate(PeerConnectionConfiguration config, out IntPtr peerHandle);

            [DllImport(dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
                EntryPoint = "mrsPeerConnectionRegisterConnectedCallback")]
            public static extern void PeerConnectionRegisterConnectedCallback(IntPtr peerHandle,
                PeerConnectionConnectedCallback callback, IntPtr userData);

            [DllImport(dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
                EntryPoint = "mrsPeerConnectionRegisterLocalSdpReadytoSendCallback")]
            public static extern void PeerConnectionRegisterLocalSdpReadytoSendCallback(IntPtr peerHandle,
                PeerConnectionLocalSdpReadytoSendCallback callback, IntPtr userData);

            [DllImport(dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
                EntryPoint = "mrsPeerConnectionRegisterIceCandidateReadytoSendCallback")]
            public static extern void PeerConnectionRegisterIceCandidateReadytoSendCallback(IntPtr peerHandle,
                PeerConnectionIceCandidateReadytoSendCallback callback, IntPtr userData);

            [DllImport(dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
                EntryPoint = "mrsPeerConnectionRegisterRenegotiationNeededCallback")]
            public static extern void PeerConnectionRegisterRenegotiationNeededCallback(IntPtr peerHandle,
                PeerConnectionRenegotiationNeededCallback callback, IntPtr userData);

            [DllImport(dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
                EntryPoint = "mrsPeerConnectionRegisterTrackAddedCallback")]
            public static extern void PeerConnectionRegisterTrackAddedCallback(IntPtr peerHandle,
                PeerConnectionTrackAddedCallback callback, IntPtr userData);

            [DllImport(dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
                EntryPoint = "mrsPeerConnectionRegisterTrackRemovedCallback")]
            public static extern void PeerConnectionRegisterTrackRemovedCallback(IntPtr peerHandle,
                PeerConnectionTrackRemovedCallback callback, IntPtr userData);

            [DllImport(dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
                EntryPoint = "mrsPeerConnectionRegisterI420LocalVideoFrameCallback")]
            public static extern void PeerConnectionRegisterI420LocalVideoFrameCallback(IntPtr peerHandle,
                PeerConnectionI420VideoFrameCallback callback, IntPtr userData);

            [DllImport(dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
                EntryPoint = "mrsPeerConnectionRegisterI420RemoteVideoFrameCallback")]
            public static extern void PeerConnectionRegisterI420RemoteVideoFrameCallback(IntPtr peerHandle,
                PeerConnectionI420VideoFrameCallback callback, IntPtr userData);

            [DllImport(dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
                EntryPoint = "mrsPeerConnectionRegisterARGBLocalVideoFrameCallback")]
            public static extern void PeerConnectionRegisterARGBLocalVideoFrameCallback(IntPtr peerHandle,
                PeerConnectionARGBVideoFrameCallback callback, IntPtr userData);

            [DllImport(dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
                EntryPoint = "mrsPeerConnectionRegisterARGBRemoteVideoFrameCallback")]
            public static extern void PeerConnectionRegisterARGBRemoteVideoFrameCallback(IntPtr peerHandle,
                PeerConnectionARGBVideoFrameCallback callback, IntPtr userData);

            [DllImport(dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
                EntryPoint = "mrsPeerConnectionRegisterLocalAudioFrameCallback")]
            public static extern void PeerConnectionRegisterLocalAudioFrameCallback(IntPtr peerHandle,
                PeerConnectionAudioFrameCallback callback, IntPtr userData);

            [DllImport(dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
                EntryPoint = "mrsPeerConnectionRegisterRemoteAudioFrameCallback")]
            public static extern void PeerConnectionRegisterRemoteAudioFrameCallback(IntPtr peerHandle,
                PeerConnectionAudioFrameCallback callback, IntPtr userData);

            [DllImport(dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
                EntryPoint = "mrsPeerConnectionAddLocalVideoTrack")]
            public static extern uint PeerConnectionAddLocalVideoTrack(IntPtr peerHandle, VideoDeviceConfiguration config);

            [DllImport(dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
                EntryPoint = "mrsPeerConnectionAddLocalAudioTrack")]
            public static extern uint PeerConnectionAddLocalAudioTrack(IntPtr peerHandle);

            [DllImport(dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
                EntryPoint = "mrsPeerConnectionAddDataChannel")]
            public static extern uint PeerConnectionAddDataChannel(IntPtr peerHandle, int id, string label,
                bool ordered, bool reliable, PeerConnectionDataChannelMessageCallback messageCallback,
                IntPtr messageUserData, PeerConnectionDataChannelBufferingCallback bufferingCallback,
                IntPtr bufferingUserData, PeerConnectionDataChannelStateCallback stateCallback,
                IntPtr stateUserData);

            [DllImport(dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
                EntryPoint = "mrsPeerConnectionRemoveLocalVideoTrack")]
            public static extern void PeerConnectionRemoveLocalVideoTrack(IntPtr peerHandle);

            [DllImport(dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
                EntryPoint = "mrsPeerConnectionRemoveLocalAudioTrack")]
            public static extern void PeerConnectionRemoveLocalAudioTrack(IntPtr peerHandle);

            [DllImport(dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
                EntryPoint = "mrsPeerConnectionRemoveDataChannelById")]
            public static extern uint PeerConnectionRemoveDataChannel(IntPtr peerHandle, int id);

            [DllImport(dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
                EntryPoint = "mrsPeerConnectionRemoveDataChannelByLabel")]
            public static extern uint PeerConnectionRemoveDataChannel(IntPtr peerHandle, string label);

            [DllImport(dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
                EntryPoint = "mrsPeerConnectionSendDataChannelMessage")]
            public static extern uint PeerConnectionSendDataChannelMessage(IntPtr peerHandle, int id,
                byte[] data, ulong size);

            [DllImport(dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
                EntryPoint = "mrsPeerConnectionAddIceCandidate")]
            public static extern void PeerConnectionAddIceCandidate(IntPtr peerHandle, string sdpMid,
                int sdpMlineindex, string candidate);

            [DllImport(dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
                EntryPoint = "mrsPeerConnectionCreateOffer")]
            public static extern uint PeerConnectionCreateOffer(IntPtr peerHandle);

            [DllImport(dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
                EntryPoint = "mrsPeerConnectionCreateAnswer")]
            public static extern uint PeerConnectionCreateAnswer(IntPtr peerHandle);

            [DllImport(dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
                EntryPoint = "mrsPeerConnectionSetRemoteDescription")]
            public static extern uint PeerConnectionSetRemoteDescription(IntPtr peerHandle, string type, string sdp);

            [DllImport(dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
                EntryPoint = "mrsPeerConnectionClose")]
            public static extern void PeerConnectionClose(ref IntPtr peerHandle);

            [DllImport(dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
                EntryPoint = "mrsSdpForceCodecs")]
            public static unsafe extern uint SdpForceCodecs(string message, SdpFilter audioFilter, SdpFilter videoFilter, StringBuilder messageOut, ref ulong messageOutLength);

            [DllImport(dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
                EntryPoint = "mrsMemCpyStride")]
            public static unsafe extern void MemCpyStride(void* dst, int dst_stride, void* src, int src_stride, int elem_size, int elem_count);

            #endregion
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
        /// Callback invoked by the internal WebRTC implementation when it needs a SDP message
        /// to be dispatched to the remote peer.
        /// </summary>
        /// <param name="type">The SDP message type.</param>
        /// <param name="sdp">The SDP message content.</param>
        private void OnLocalSdpReadytoSend(string type, string sdp)
        {
            SignalerMessage.WireMessageType messageType = SignalerMessage.WireMessageTypeFromString(type);

            // If the user specified a preferred audio or video codec, manipulate the SDP message
            // to exclude other codecs if the preferred one is supported.
            if ((PreferredAudioCodec.Length > 0) || (PreferredVideoCodec.Length > 0))
            {
                // Only filter offers, so that both peers think it's each other's fault
                // for only supporting a single codec.
                // Filtering an answer will not work because the internal implementation
                // already decided what codec to use before this callback is called, so
                // that will only confuse the other peer.
                if (messageType == SignalerMessage.WireMessageType.Offer)
                {
                    var builder = new StringBuilder(sdp.Length);
                    ulong lengthInOut = (ulong)builder.Capacity;
                    var audioFilter = new NativeMethods.SdpFilter
                    {
                        CodecName = PreferredAudioCodec,
                        ExtraParams = PreferredAudioCodecExtraParams
                    };
                    var videoFilter = new NativeMethods.SdpFilter
                    {
                        CodecName = PreferredVideoCodec,
                        ExtraParams = PreferredVideoCodecExtraParams
                    };
                    if (NativeMethods.SdpForceCodecs(sdp, audioFilter, videoFilter, builder, ref lengthInOut) == NativeMethods.MRS_SUCCESS)
                    {
                        builder.Length = (int)lengthInOut;
                        sdp = builder.ToString();
                    }
                }
            }

            var msg = new SignalerMessage()
            {
                MessageType = messageType,
                Data = sdp,
                IceDataSeparator = "|"
            };
            Signaler?.SendMessageAsync(msg);

            LocalSdpReadytoSend?.Invoke(type, sdp);
        }

        private void OnIceCandidateReadytoSend(string candidate, int sdpMlineindex, string sdpMid)
        {
            var msg = new SignalerMessage()
            {
                MessageType = SignalerMessage.WireMessageType.Ice,
                Data = $"{candidate}|{sdpMlineindex}|{sdpMid}",
                IceDataSeparator = "|"
            };
            Signaler?.SendMessageAsync(msg);

            IceCandidateReadytoSend?.Invoke(candidate, sdpMlineindex, sdpMid);
        }

        /// <summary>
        /// Get the list of available video capture devices.
        /// </summary>
        /// <returns>The list of available video capture devices.</returns>
        public static Task<List<VideoCaptureDevice>> GetVideoCaptureDevicesAsync()
        {
            var devices = new List<VideoCaptureDevice>();
            var eventWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            var wrapper = new CallbacksWrappers.EnumVideoCaptureWrapper()
            {
                callback = (id, name) =>
                {
                    devices.Add(new VideoCaptureDevice() { id = id, name = name });
                },
                completedCallback = () =>
                {
                    // On enumeration end, signal the caller thread
                    eventWaitHandle.Set();
                }
            };

            // Prevent garbage collection of the wrapper delegates until the enumeration is completed.
            IntPtr handle = GCHandle.ToIntPtr(GCHandle.Alloc(wrapper, GCHandleType.Normal));

            return Task.Run(() =>
            {
                // Execute the native async callback
                NativeMethods.EnumVideoCaptureDevicesAsync(CallbacksWrappers.VideoCaptureDeviceEnumCallback, handle,
                    CallbacksWrappers.VideoCaptureDeviceEnumCompletedCallback, handle);

                // Wait for end of enumerating
                eventWaitHandle.WaitOne();

                // Clean-up and release the wrapper delegates
                GCHandle.FromIntPtr(handle).Free();

                return devices;
            });
        }

        /// <summary>
        /// Unsafe utility to copy a contiguous block of memory.
        /// This is equivalent to the C function <c>memcpy()</c>, and is provided for optimization purpose only.
        /// </summary>
        /// <param name="dst">Pointer to the beginning of the destination buffer data is copied to.</param>
        /// <param name="src">Pointer to the beginning of the source buffer data is copied from.</param>
        /// <param name="size">Size of the memory block, in bytes.</param>
        [DllImport(NativeMethods.dllPath, CallingConvention = CallingConvention.StdCall, EntryPoint = "mrsMemCpy")]
        public static unsafe extern void MemCpy(void* dst, void* src, ulong size);

        /// <summary>
        /// Unsafe utility to copy a memory block with stride.
        /// 
        /// This utility loops over the rows of the input memory block, and copy them to the output
        /// memory block, then increment the read and write pointers by the source and destination
        /// strides, respectively. For each row, exactly <paramref name="elem_size"/> bytes are copied,
        /// even if the row stride is higher. The extra bytes in the destination buffer past the row
        /// size until the row stride are left untouched.
        /// 
        /// This is equivalent to the following pseudo-code:
        /// <code>
        /// for (int row = 0; row &lt; elem_count; ++row) {
        ///   memcpy(dst, src, elem_size);
        ///   dst += dst_stride;
        ///   src += src_stride;
        /// }
        /// </code>
        /// </summary>
        /// <param name="dst">Pointer to the beginning of the destination buffer data is copied to.</param>
        /// <param name="dst_stride">Stride in bytes of the destination rows. This must be greater than
        /// or equal to the row size <paramref name="elem_size"/>.</param>
        /// <param name="src">Pointer to the beginning of the source buffer data is copied from.</param>
        /// <param name="src_stride">Stride in bytes of the source rows. This must be greater than
        /// or equal to the row size <paramref name="elem_size"/>.</param>
        /// <param name="elem_size">Size of each row, in bytes.</param>
        /// <param name="elem_count">Total number of rows to copy.</param>
        [DllImport(NativeMethods.dllPath, CallingConvention = CallingConvention.StdCall, EntryPoint = "mrsMemCpyStride")]
        public static unsafe extern void MemCpyStride(void* dst, int dst_stride, void* src, int src_stride, int elem_size, int elem_count);
    }
}
