// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.MixedReality.WebRTC.Interop
{
    /// <summary>
    /// Handle to a native peer connection object.
    /// </summary>
    public sealed class PeerConnectionHandle : SafeHandle
    {
        /// <summary>
        /// Check if the current handle is invalid, which means it is not referencing
        /// an actual native object. Note that a valid handle only means that the internal
        /// handle references a native object, but does not guarantee that the native
        /// object is still accessible. It is only safe to access the native object if
        /// the handle is not closed, which implies it being valid.
        /// </summary>
        public override bool IsInvalid
        {
            get
            {
                return (handle == IntPtr.Zero);
            }
        }

        /// <summary>
        /// Default constructor for an invalid handle.
        /// </summary>
        public PeerConnectionHandle() : base(IntPtr.Zero, ownsHandle: true)
        {
        }

        /// <summary>
        /// Constructor for a valid handle referencing the given native object.
        /// </summary>
        /// <param name="handle">The valid internal handle to the native object.</param>
        public PeerConnectionHandle(IntPtr handle) : base(IntPtr.Zero, ownsHandle: true)
        {
            SetHandle(handle);
        }

        /// <summary>
        /// Release the native object while the handle is being closed.
        /// </summary>
        /// <returns>Return <c>true</c> if the native object was successfully released.</returns>
        protected override bool ReleaseHandle()
        {
            PeerConnectionInterop.PeerConnection_RemoveRef(handle);
            return true;
        }
    }

    internal class PeerConnectionInterop
    {
        // Types of trampolines for MonoPInvokeCallback
        private delegate void VideoCaptureDeviceEnumDelegate(string id, string name, IntPtr handle);
        private delegate void VideoCaptureDeviceEnumCompletedDelegate(IntPtr handle);
        private delegate void VideoCaptureFormatEnumDelegate(uint width, uint height, double framerate, string encoding, IntPtr handle);
        private delegate void VideoCaptureFormatEnumCompletedDelegate(IntPtr handle);
        private delegate void ConnectedDelegate(IntPtr peer);
        private delegate void DataChannelCreateObjectDelegate(IntPtr peer, DataChannelInterop.CreateConfig config,
            out DataChannelInterop.Callbacks callbacks);
        private delegate void DataChannelAddedDelegate(IntPtr peer, IntPtr dataChannel, IntPtr dataChannelHandle);
        private delegate void DataChannelRemovedDelegate(IntPtr peer, IntPtr dataChannel, IntPtr dataChannelHandle);
        private delegate void LocalSdpReadytoSendDelegate(IntPtr peer, string type, string sdp);
        private delegate void IceCandidateReadytoSendDelegate(IntPtr peer, string candidate, int sdpMlineindex, string sdpMid);
        private delegate void IceStateChangedDelegate(IntPtr peer, IceConnectionState newState);
        private delegate void RenegotiationNeededDelegate(IntPtr peer);
        private delegate void TrackAddedDelegate(IntPtr peer, PeerConnection.TrackKind trackKind);
        private delegate void TrackRemovedDelegate(IntPtr peer, PeerConnection.TrackKind trackKind);
        private delegate void DataChannelMessageDelegate(IntPtr peer, IntPtr data, ulong size);
        private delegate void DataChannelBufferingDelegate(IntPtr peer, ulong previous, ulong current, ulong limit);
        private delegate void DataChannelStateDelegate(IntPtr peer, int state, int id);

        // Callbacks for internal enumeration implementation only
        public delegate void VideoCaptureDeviceEnumCallbackImpl(string id, string name);
        public delegate void VideoCaptureDeviceEnumCompletedCallbackImpl();
        public delegate void VideoCaptureFormatEnumCallbackImpl(uint width, uint height, double framerate, uint fourcc);
        public delegate void VideoCaptureFormatEnumCompletedCallbackImpl(Exception e);

        public class EnumVideoCaptureDeviceWrapper
        {
            public VideoCaptureDeviceEnumCallbackImpl enumCallback;
            public VideoCaptureDeviceEnumCompletedCallbackImpl completedCallback;
        }

        [MonoPInvokeCallback(typeof(VideoCaptureDeviceEnumDelegate))]
        public static void VideoCaptureDevice_EnumCallback(string id, string name, IntPtr userData)
        {
            var wrapper = Utils.ToWrapper<EnumVideoCaptureDeviceWrapper>(userData);
            wrapper.enumCallback(id, name); // this is mandatory, never null
        }

        [MonoPInvokeCallback(typeof(VideoCaptureDeviceEnumCompletedDelegate))]
        public static void VideoCaptureDevice_EnumCompletedCallback(IntPtr userData)
        {
            var wrapper = Utils.ToWrapper<EnumVideoCaptureDeviceWrapper>(userData);
            wrapper.completedCallback(); // this is optional, allows to be null
        }

        public class EnumVideoCaptureFormatsWrapper
        {
            public VideoCaptureFormatEnumCallbackImpl enumCallback;
            public VideoCaptureFormatEnumCompletedCallbackImpl completedCallback;
        }

        [MonoPInvokeCallback(typeof(VideoCaptureFormatEnumDelegate))]
        public static void VideoCaptureFormat_EnumCallback(uint width, uint height, double framerate, uint fourcc, IntPtr userData)
        {
            var wrapper = Utils.ToWrapper<EnumVideoCaptureFormatsWrapper>(userData);
            wrapper.enumCallback(width, height, framerate, fourcc); // this is mandatory, never null
        }

        [MonoPInvokeCallback(typeof(VideoCaptureFormatEnumCompletedDelegate))]
        public static void VideoCaptureFormat_EnumCompletedCallback(uint resultCode, IntPtr userData)
        {
            var exception = (resultCode == /*MRS_SUCCESS*/0 ? null : new Exception());
            var wrapper = Utils.ToWrapper<EnumVideoCaptureFormatsWrapper>(userData);
            wrapper.completedCallback(exception); // this is optional, allows to be null
        }

        /// <summary>
        /// Utility to lock all low-level interop delegates registered with the native plugin for the duration
        /// of the peer connection wrapper lifetime, and prevent their garbage collection.
        /// </summary>
        /// <remarks>
        /// The delegate don't need to be pinned, just referenced to prevent garbage collection.
        /// So referencing them from this class is enough to keep them alive and usable.
        /// </remarks>
        public class InteropCallbacks
        {
            public PeerConnection Peer;
            public DataChannelInterop.CreateObjectCallback DataChannelCreateObjectCallback;
        }

        /// <summary>
        /// Utility to lock all optional delegates registered with the native plugin for the duration
        /// of the peer connection wrapper lifetime, and prevent their garbage collection.
        /// </summary>
        /// <remarks>
        /// The delegate don't need to be pinned, just referenced to prevent garbage collection.
        /// So referencing them from this class is enough to keep them alive and usable.
        /// </remarks>
        public class PeerCallbackArgs
        {
            public PeerConnection Peer;
            public PeerConnectionDataChannelAddedCallback DataChannelAddedCallback;
            public PeerConnectionDataChannelRemovedCallback DataChannelRemovedCallback;
            public PeerConnectionConnectedCallback ConnectedCallback;
            public PeerConnectionLocalSdpReadytoSendCallback LocalSdpReadytoSendCallback;
            public PeerConnectionIceCandidateReadytoSendCallback IceCandidateReadytoSendCallback;
            public PeerConnectionIceStateChangedCallback IceStateChangedCallback;
            public PeerConnectionRenegotiationNeededCallback RenegotiationNeededCallback;
            public PeerConnectionTrackAddedCallback TrackAddedCallback;
            public PeerConnectionTrackRemovedCallback TrackRemovedCallback;
            public PeerConnectionI420AVideoFrameCallback I420ALocalVideoFrameCallback;
            public PeerConnectionI420AVideoFrameCallback I420ARemoteVideoFrameCallback;
            public PeerConnectionArgb32VideoFrameCallback Argb32LocalVideoFrameCallback;
            public PeerConnectionArgb32VideoFrameCallback Argb32RemoteVideoFrameCallback;
            public PeerConnectionAudioFrameCallback LocalAudioFrameCallback;
            public PeerConnectionAudioFrameCallback RemoteAudioFrameCallback;
        }

        /// <summary>
        /// Callback arguments for <see cref="AddLocalVideoTrackFromExternalI420ASource"/> trampoline.
        /// </summary>
        public class ExternalI420AVideoFrameRequestCallbackArgs
        {
            public PeerConnection Peer;
            public ExternalVideoTrackSource Source;
            public I420AVideoFrameRequestDelegate FrameRequestCallback;
        }

        /// <summary>
        /// Callback arguments for <see cref="AddLocalVideoTrackFromExternalArgb32Source"/> trampoline.
        /// </summary>
        public class ExternalArgb32VideoFrameRequestCallbackArgs
        {
            public PeerConnection Peer;
            public ExternalVideoTrackSource Source;
            public Argb32VideoFrameRequestDelegate FrameRequestCallback;
        }

        [MonoPInvokeCallback(typeof(ConnectedDelegate))]
        public static void ConnectedCallback(IntPtr userData)
        {
            var peer = Utils.ToWrapper<PeerConnection>(userData);
            peer.OnConnected();
        }

        [MonoPInvokeCallback(typeof(DataChannelAddedDelegate))]
        public static void DataChannelAddedCallback(IntPtr peer, IntPtr dataChannel, IntPtr dataChannelHandle)
        {
            var peerWrapper = Utils.ToWrapper<PeerConnection>(peer);
            var dataChannelWrapper = Utils.ToWrapper<DataChannel>(dataChannel);
            // Ensure that the DataChannel wrapper knows about its native object.
            // This is not always the case, if created via the interop constructor,
            // as the wrapper is created before the native object exists.
            DataChannelInterop.SetHandle(dataChannelWrapper, dataChannelHandle);
            peerWrapper.OnDataChannelAdded(dataChannelWrapper);
        }

        [MonoPInvokeCallback(typeof(DataChannelRemovedDelegate))]
        public static void DataChannelRemovedCallback(IntPtr peer, IntPtr dataChannel, IntPtr dataChannelHandle)
        {
            var peerWrapper = Utils.ToWrapper<PeerConnection>(peer);
            var dataChannelWrapper = Utils.ToWrapper<DataChannel>(dataChannel);
            peerWrapper.OnDataChannelRemoved(dataChannelWrapper);
        }

        [MonoPInvokeCallback(typeof(LocalSdpReadytoSendDelegate))]
        public static void LocalSdpReadytoSendCallback(IntPtr userData, string type, string sdp)
        {
            var peer = Utils.ToWrapper<PeerConnection>(userData);
            peer.OnLocalSdpReadytoSend(type, sdp);
        }

        [MonoPInvokeCallback(typeof(IceCandidateReadytoSendDelegate))]
        public static void IceCandidateReadytoSendCallback(IntPtr userData, string candidate, int sdpMlineindex, string sdpMid)
        {
            var peer = Utils.ToWrapper<PeerConnection>(userData);
            peer.OnIceCandidateReadytoSend(candidate, sdpMlineindex, sdpMid);
        }

        [MonoPInvokeCallback(typeof(IceStateChangedDelegate))]
        public static void IceStateChangedCallback(IntPtr userData, IceConnectionState newState)
        {
            var peer = Utils.ToWrapper<PeerConnection>(userData);
            peer.OnIceStateChanged(newState);
        }

        [MonoPInvokeCallback(typeof(RenegotiationNeededDelegate))]
        public static void RenegotiationNeededCallback(IntPtr userData)
        {
            var peer = Utils.ToWrapper<PeerConnection>(userData);
            peer.OnRenegotiationNeeded();
        }

        [MonoPInvokeCallback(typeof(TrackAddedDelegate))]
        public static void TrackAddedCallback(IntPtr userData, PeerConnection.TrackKind trackKind)
        {
            var peer = Utils.ToWrapper<PeerConnection>(userData);
            peer.OnTrackAdded(trackKind);
        }

        [MonoPInvokeCallback(typeof(TrackRemovedDelegate))]
        public static void TrackRemovedCallback(IntPtr userData, PeerConnection.TrackKind trackKind)
        {
            var peer = Utils.ToWrapper<PeerConnection>(userData);
            peer.OnTrackRemoved(trackKind);
        }

        [MonoPInvokeCallback(typeof(I420AVideoFrameDelegate))]
        public static void I420ARemoteVideoFrameCallback(IntPtr userData, I420AVideoFrame frame)
        {
            var peer = Utils.ToWrapper<PeerConnection>(userData);
            peer.OnI420ARemoteVideoFrameReady(frame);
        }

        [MonoPInvokeCallback(typeof(Argb32VideoFrameDelegate))]
        public static void Argb32RemoteVideoFrameCallback(IntPtr userData, Argb32VideoFrame frame)
        {
            var peer = Utils.ToWrapper<PeerConnection>(userData);
            peer.OnArgb32RemoteVideoFrameReady(frame);
        }

        [MonoPInvokeCallback(typeof(AudioFrameDelegate))]
        public static void LocalAudioFrameCallback(IntPtr userData, AudioFrame frame)
        {
            var peer = Utils.ToWrapper<PeerConnection>(userData);
            peer.OnLocalAudioFrameReady(frame);
        }

        [MonoPInvokeCallback(typeof(AudioFrameDelegate))]
        public static void RemoteAudioFrameCallback(IntPtr userData, AudioFrame frame)
        {
            var peer = Utils.ToWrapper<PeerConnection>(userData);
            peer.OnRemoteAudioFrameReady(frame);
        }

        [MonoPInvokeCallback(typeof(PeerConnectionRequestExternalI420AVideoFrameCallback))]
        public static void RequestI420AVideoFrameFromExternalSourceCallback(IntPtr userData,
            ExternalVideoTrackSourceHandle sourceHandle, uint requestId, long timestampMs)
        {
            var args = Utils.ToWrapper<ExternalI420AVideoFrameRequestCallbackArgs>(userData);
            var request = new FrameRequest
            {
                Source = args.Source,
                RequestId = requestId,
                TimestampMs = timestampMs
            };
            args.FrameRequestCallback.Invoke(in request);
        }

        [MonoPInvokeCallback(typeof(PeerConnectionRequestExternalArgb32VideoFrameCallback))]
        public static void RequestArgb32VideoFrameFromExternalSourceCallback(IntPtr userData,
            ExternalVideoTrackSourceHandle sourceHandle, uint requestId, long timestampMs)
        {
            var args = Utils.ToWrapper<ExternalArgb32VideoFrameRequestCallbackArgs>(userData);
            var request = new FrameRequest
            {
                Source = args.Source,
                RequestId = requestId,
                TimestampMs = timestampMs
            };
            args.FrameRequestCallback.Invoke(in request);
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        internal struct MarshaledInteropCallbacks
        {
            public DataChannelInterop.CreateObjectCallback DataChannelCreateObjectCallback;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        internal struct PeerConnectionConfiguration
        {
            public string EncodedIceServers;
            public IceTransportType IceTransportType;
            public BundlePolicy BundlePolicy;
            public SdpSemantic SdpSemantic;
        }

        /// <summary>
        /// Helper structure to pass video capture device configuration to the underlying C++ library.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        internal struct VideoDeviceConfiguration
        {
            /// <summary>
            /// Video capture device unique identifier, as returned by <see cref="PeerConnection.GetVideoCaptureDevicesAsync"/>.
            /// </summary>
            public string VideoDeviceId;

            /// <summary>
            /// Optional video profile unique identifier to use.
            /// Ignored if the video capture device specified by <see cref="VideoDeviceId"/> does not
            /// support video profiles.
            /// </summary>
            /// <remarks>
            /// This is generally preferred over <see cref="VideoProfileKind"/> to get full
            /// control over the video profile selection. Specifying both this and <see cref="VideoProfileKind"/>
            /// is discouraged, as it over-constraints the selection algorithm.
            /// </remarks>
            /// <seealso xref="MediaCapture.IsVideoProfileSupported(string)"/>
            public string VideoProfileId;

            /// <summary>
            /// Optional video profile kind to select a video profile from.
            /// Ignored if the video capture device specified by <see cref="VideoDeviceId"/> does not
            /// support video profiles.
            /// </summary>
            /// <remarks>
            /// This is generally preferred over <see cref="VideoProfileId"/> to find a matching
            /// capture format (resolution and/or framerate) when one does not care about which video
            /// profile provides this capture format. Specifying both this and <see cref="VideoProfileId"/>
            /// is discouraged, as it over-constraints the selection algorithm.
            /// </remarks>
            /// <seealso xref="MediaCapture.IsVideoProfileSupported(string)"/>
            public PeerConnection.VideoProfileKind VideoProfileKind;

            /// <summary>
            /// Optional capture resolution width, in pixels, or zero for no constraint.
            /// </summary>
            public uint Width;

            /// <summary>
            /// Optional capture resolution height, in pixels, or zero for no constraint.
            /// </summary>
            public uint Height;

            /// <summary>
            /// Optional capture framerate, in frames per second (FPS), or zero for no constraint.
            /// </summary>
            public double Framerate;

            /// <summary>
            /// Enable Mixed Reality Capture (MRC). This flag is ignored if the platform doesn't support MRC.
            /// </summary>
            public mrsBool EnableMixedRealityCapture;

            /// <summary>
            /// When MRC is enabled, enable the on-screen recording indicator.
            /// </summary>
            public mrsBool EnableMRCRecordingIndicator;
        }


        #region Unmanaged delegates

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void VideoCaptureDeviceEnumCallback(string id, string name, IntPtr userData);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void VideoCaptureDeviceEnumCompletedCallback(IntPtr userData);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void VideoCaptureFormatEnumCallback(uint width, uint height, double framerate, uint fourcc, IntPtr userData);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void VideoCaptureFormatEnumCompletedCallback(uint resultCode, IntPtr userData);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void PeerConnectionDataChannelAddedCallback(IntPtr peer, IntPtr dataChannel, IntPtr dataChannelHandle);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void PeerConnectionDataChannelRemovedCallback(IntPtr peer, IntPtr dataChannel, IntPtr dataChannelHandle);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void PeerConnectionInteropCallbacks(IntPtr userData);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void PeerConnectionConnectedCallback(IntPtr userData);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void PeerConnectionLocalSdpReadytoSendCallback(IntPtr userData,
            string type, string sdp);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void PeerConnectionIceCandidateReadytoSendCallback(IntPtr userData,
            string candidate, int sdpMlineindex, string sdpMid);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void PeerConnectionIceStateChangedCallback(IntPtr userData,
            IceConnectionState newState);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void PeerConnectionRenegotiationNeededCallback(IntPtr userData);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void PeerConnectionTrackAddedCallback(IntPtr userData, PeerConnection.TrackKind trackKind);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void PeerConnectionTrackRemovedCallback(IntPtr userData, PeerConnection.TrackKind trackKind);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void PeerConnectionI420AVideoFrameCallback(IntPtr userData, I420AVideoFrame frame);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void PeerConnectionArgb32VideoFrameCallback(IntPtr userData, Argb32VideoFrame frame);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void PeerConnectionAudioFrameCallback(IntPtr userData, AudioFrame frame);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public unsafe delegate void PeerConnectionRequestExternalI420AVideoFrameCallback(IntPtr userData,
            ExternalVideoTrackSourceHandle sourceHandle, uint requestId, long timestampMs);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public unsafe delegate void PeerConnectionRequestExternalArgb32VideoFrameCallback(IntPtr userData,
            ExternalVideoTrackSourceHandle sourceHandle, uint requestId, long timestampMs);

        #endregion


        #region P/Invoke static functions

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsEnumVideoCaptureDevicesAsync")]
        public static extern void EnumVideoCaptureDevicesAsync(VideoCaptureDeviceEnumCallback enumCallback, IntPtr userData,
            VideoCaptureDeviceEnumCompletedCallback completedCallback, IntPtr completedUserData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsEnumVideoCaptureFormatsAsync")]
        public static extern uint EnumVideoCaptureFormatsAsync(string deviceId, VideoCaptureFormatEnumCallback enumCallback,
            IntPtr userData, VideoCaptureFormatEnumCompletedCallback completedCallback, IntPtr completedUserData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionAddRef")]
        public static unsafe extern void PeerConnection_AddRef(PeerConnectionHandle handle);

        // Note - This is used during SafeHandle.ReleaseHandle(), so cannot use PeerConnectionHandle
        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionRemoveRef")]
        public static unsafe extern void PeerConnection_RemoveRef(IntPtr handle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionCreate")]
        public static extern uint PeerConnection_Create(PeerConnectionConfiguration config, IntPtr peer,
            out PeerConnectionHandle peerHandleOut);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionRegisterInteropCallbacks")]
        public static extern void PeerConnection_RegisterInteropCallbacks(PeerConnectionHandle peerHandle,
            in MarshaledInteropCallbacks callback);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionRegisterConnectedCallback")]
        public static extern void PeerConnection_RegisterConnectedCallback(PeerConnectionHandle peerHandle,
            PeerConnectionConnectedCallback callback, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionRegisterLocalSdpReadytoSendCallback")]
        public static extern void PeerConnection_RegisterLocalSdpReadytoSendCallback(PeerConnectionHandle peerHandle,
            PeerConnectionLocalSdpReadytoSendCallback callback, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionRegisterIceCandidateReadytoSendCallback")]
        public static extern void PeerConnection_RegisterIceCandidateReadytoSendCallback(PeerConnectionHandle peerHandle,
            PeerConnectionIceCandidateReadytoSendCallback callback, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionRegisterIceStateChangedCallback")]
        public static extern void PeerConnection_RegisterIceStateChangedCallback(PeerConnectionHandle peerHandle,
            PeerConnectionIceStateChangedCallback callback, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionRegisterRenegotiationNeededCallback")]
        public static extern void PeerConnection_RegisterRenegotiationNeededCallback(PeerConnectionHandle peerHandle,
            PeerConnectionRenegotiationNeededCallback callback, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionRegisterTrackAddedCallback")]
        public static extern void PeerConnection_RegisterTrackAddedCallback(PeerConnectionHandle peerHandle,
            PeerConnectionTrackAddedCallback callback, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionRegisterTrackRemovedCallback")]
        public static extern void PeerConnection_RegisterTrackRemovedCallback(PeerConnectionHandle peerHandle,
            PeerConnectionTrackRemovedCallback callback, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionRegisterDataChannelAddedCallback")]
        public static extern void PeerConnection_RegisterDataChannelAddedCallback(PeerConnectionHandle peerHandle,
            PeerConnectionDataChannelAddedCallback callback, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionRegisterDataChannelRemovedCallback")]
        public static extern void PeerConnection_RegisterDataChannelRemovedCallback(PeerConnectionHandle peerHandle,
            PeerConnectionDataChannelRemovedCallback callback, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionRegisterI420ARemoteVideoFrameCallback")]
        public static extern void PeerConnection_RegisterI420ARemoteVideoFrameCallback(PeerConnectionHandle peerHandle,
            PeerConnectionI420AVideoFrameCallback callback, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionRegisterArgb32RemoteVideoFrameCallback")]
        public static extern void PeerConnection_RegisterArgb32RemoteVideoFrameCallback(PeerConnectionHandle peerHandle,
            PeerConnectionArgb32VideoFrameCallback callback, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionRegisterLocalAudioFrameCallback")]
        public static extern void PeerConnection_RegisterLocalAudioFrameCallback(PeerConnectionHandle peerHandle,
            PeerConnectionAudioFrameCallback callback, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionRegisterRemoteAudioFrameCallback")]
        public static extern void PeerConnection_RegisterRemoteAudioFrameCallback(PeerConnectionHandle peerHandle,
            PeerConnectionAudioFrameCallback callback, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionAddLocalVideoTrack")]
        public static extern uint PeerConnection_AddLocalVideoTrack(PeerConnectionHandle peerHandle,
            string trackName, VideoDeviceConfiguration config, out LocalVideoTrackHandle trackHandle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionAddLocalVideoTrackFromExternalI420ASource")]
        public static extern uint PeerConnection_AddLocalVideoTrackFromExternalI420ASource(
            PeerConnectionHandle peerHandle, string trackName, PeerConnectionRequestExternalI420AVideoFrameCallback callback,
            IntPtr userData, out ExternalVideoTrackSourceHandle sourceHandle, out LocalVideoTrackHandle trackHandle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionAddLocalVideoTrackFromExternalArgb32Source")]
        public static extern uint PeerConnection_AddLocalVideoTrackFromExternalArgb32Source(
            PeerConnectionHandle peerHandle, string trackName, PeerConnectionRequestExternalArgb32VideoFrameCallback callback,
            IntPtr userData, out ExternalVideoTrackSourceHandle sourceHandle, out LocalVideoTrackHandle trackHandle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionAddLocalAudioTrack")]
        public static extern uint PeerConnection_AddLocalAudioTrack(PeerConnectionHandle peerHandle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionAddDataChannel")]
        public static extern uint PeerConnection_AddDataChannel(PeerConnectionHandle peerHandle, IntPtr dataChannel,
            DataChannelInterop.CreateConfig config, DataChannelInterop.Callbacks callbacks,
            ref IntPtr dataChannelHandle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionRemoveLocalAudioTrack")]
        public static extern void PeerConnection_RemoveLocalAudioTrack(PeerConnectionHandle peerHandle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionRemoveLocalVideoTrack")]
        public static extern uint PeerConnection_RemoveLocalVideoTrack(PeerConnectionHandle peerHandle,
            LocalVideoTrackHandle trackHandle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionRemoveLocalVideoTracksFromSource")]
        public static extern uint PeerConnection_RemoveLocalVideoTracksFromSource(PeerConnectionHandle peerHandle,
            ExternalVideoTrackSourceHandle sourceHandle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionRemoveDataChannel")]
        public static extern uint PeerConnection_RemoveDataChannel(PeerConnectionHandle peerHandle, IntPtr dataChannelHandle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionSetLocalAudioTrackEnabled")]
        public static extern uint PeerConnection_SetLocalAudioTrackEnabled(PeerConnectionHandle peerHandle, int enabled);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionIsLocalAudioTrackEnabled")]
        public static extern int PeerConnection_IsLocalAudioTrackEnabled(PeerConnectionHandle peerHandle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionAddIceCandidate")]
        public static extern void PeerConnection_AddIceCandidate(PeerConnectionHandle peerHandle, string sdpMid,
            int sdpMlineindex, string candidate);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionCreateOffer")]
        public static extern uint PeerConnection_CreateOffer(PeerConnectionHandle peerHandle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionCreateAnswer")]
        public static extern uint PeerConnection_CreateAnswer(PeerConnectionHandle peerHandle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionSetBitrate")]
        public static extern uint PeerConnection_SetBitrate(PeerConnectionHandle peerHandle, int minBitrate, int startBitrate, int maxBitrate);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionSetRemoteDescription")]
        public static extern uint PeerConnection_SetRemoteDescription(PeerConnectionHandle peerHandle, string type, string sdp);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionClose")]
        public static extern uint PeerConnection_Close(PeerConnectionHandle peerHandle);

        #endregion


        #region Helpers

        public static LocalVideoTrack AddLocalVideoTrackFromExternalI420ASource(PeerConnection peer,
            PeerConnectionHandle peerHandle, string trackName, I420AVideoFrameRequestDelegate frameRequestCallback)
        {
            // Create some static callback args which keep the sourceDelegate alive
            var args = new ExternalI420AVideoFrameRequestCallbackArgs
            {
                Peer = peer,
                FrameRequestCallback = frameRequestCallback
            };
            var argsRef = Utils.MakeWrapperRef(args);

            // Add the local track based on the static interop trampoline callback
            uint res = PeerConnection_AddLocalVideoTrackFromExternalI420ASource(peerHandle, trackName,
                RequestI420AVideoFrameFromExternalSourceCallback, argsRef,
                out ExternalVideoTrackSourceHandle sourceHandle, out LocalVideoTrackHandle trackHandle);
            if (res != Utils.MRS_SUCCESS)
            {
                Utils.ReleaseWrapperRef(argsRef);
                Utils.ThrowOnErrorCode(res);
            }
            unsafe
            {
                var source = new ExternalVideoTrackSource(sourceHandle, peer, argsRef);
                var track = new LocalVideoTrack(trackHandle, peer, trackName, source);
                args.Source = source;
                return track;
            }
        }

        public static LocalVideoTrack AddLocalVideoTrackFromExternalArgb32Source(PeerConnection peer,
            PeerConnectionHandle peerHandle, string trackName, Argb32VideoFrameRequestDelegate frameRequestCallback)
        {
            // Create some static callback args which keep the sourceDelegate alive
            var args = new ExternalArgb32VideoFrameRequestCallbackArgs
            {
                Peer = peer,
                FrameRequestCallback = frameRequestCallback
            };
            var argsRef = Utils.MakeWrapperRef(args);

            // Add the local track based on the static interop trampoline callback
            uint res = PeerConnection_AddLocalVideoTrackFromExternalArgb32Source(peerHandle, trackName,
                RequestArgb32VideoFrameFromExternalSourceCallback, argsRef,
                out ExternalVideoTrackSourceHandle sourceHandle, out LocalVideoTrackHandle trackHandle);
            if (res != Utils.MRS_SUCCESS)
            {
                Utils.ReleaseWrapperRef(argsRef);
                Utils.ThrowOnErrorCode(res);
            }
            unsafe
            {
                var source = new ExternalVideoTrackSource(sourceHandle, peer, argsRef);
                var track = new LocalVideoTrack(trackHandle, peer, trackName, source);
                args.Source = source;
                return track;
            }
        }

        #endregion
    }
}
