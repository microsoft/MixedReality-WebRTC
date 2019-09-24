using System;
using System.Runtime.InteropServices;

namespace Microsoft.MixedReality.WebRTC.Interop
{
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
            public PeerConnectionI420VideoFrameCallback I420LocalVideoFrameCallback;
            public PeerConnectionI420VideoFrameCallback I420RemoteVideoFrameCallback;
            public PeerConnectionARGBVideoFrameCallback ARGBLocalVideoFrameCallback;
            public PeerConnectionARGBVideoFrameCallback ARGBRemoteVideoFrameCallback;
            public PeerConnectionAudioFrameCallback LocalAudioFrameCallback;
            public PeerConnectionAudioFrameCallback RemoteAudioFrameCallback;
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

        [MonoPInvokeCallback(typeof(I420VideoFrameDelegate))]
        public static void I420LocalVideoFrameCallback(IntPtr userData,
            IntPtr dataY, IntPtr dataU, IntPtr dataV, IntPtr dataA,
            int strideY, int strideU, int strideV, int strideA,
            int width, int height)
        {
            var peer = Utils.ToWrapper<PeerConnection>(userData);
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
            peer.OnI420LocalVideoFrameReady(frame);
        }

        [MonoPInvokeCallback(typeof(I420VideoFrameDelegate))]
        public static void I420RemoteVideoFrameCallback(IntPtr userData,
            IntPtr dataY, IntPtr dataU, IntPtr dataV, IntPtr dataA,
            int strideY, int strideU, int strideV, int strideA,
            int width, int height)
        {
            var peer = Utils.ToWrapper<PeerConnection>(userData);
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
            peer.OnI420RemoteVideoFrameReady(frame);
        }

        [MonoPInvokeCallback(typeof(ARGBVideoFrameDelegate))]
        public static void ARGBLocalVideoFrameCallback(IntPtr userData,
            IntPtr data, int stride, int width, int height)
        {
            var peer = Utils.ToWrapper<PeerConnection>(userData);
            var frame = new ARGBVideoFrame()
            {
                width = (uint)width,
                height = (uint)height,
                data = data,
                stride = stride
            };
            peer.OnARGBLocalVideoFrameReady(frame);
        }

        [MonoPInvokeCallback(typeof(ARGBVideoFrameDelegate))]
        public static void ARGBRemoteVideoFrameCallback(IntPtr userData,
            IntPtr data, int stride, int width, int height)
        {
            var peer = Utils.ToWrapper<PeerConnection>(userData);
            var frame = new ARGBVideoFrame()
            {
                width = (uint)width,
                height = (uint)height,
                data = data,
                stride = stride
            };
            peer.OnARGBRemoteVideoFrameReady(frame);
        }

        [MonoPInvokeCallback(typeof(AudioFrameDelegate))]
        public static void LocalAudioFrameCallback(IntPtr userData, IntPtr audioData, uint bitsPerSample,
            uint sampleRate, uint channelCount, uint frameCount)
        {
            var peer = Utils.ToWrapper<PeerConnection>(userData);
            var frame = new AudioFrame()
            {
                bitsPerSample = bitsPerSample,
                sampleRate = sampleRate,
                channelCount = channelCount,
                frameCount = frameCount,
                audioData = audioData
            };
            peer.OnLocalAudioFrameReady(frame);
        }

        [MonoPInvokeCallback(typeof(AudioFrameDelegate))]
        public static void RemoteAudioFrameCallback(IntPtr userData, IntPtr audioData, uint bitsPerSample,
            uint sampleRate, uint channelCount, uint frameCount)
        {
            var peer = Utils.ToWrapper<PeerConnection>(userData);
            var frame = new AudioFrame()
            {
                bitsPerSample = bitsPerSample,
                sampleRate = sampleRate,
                channelCount = channelCount,
                frameCount = frameCount,
                audioData = audioData
            };
            peer.OnRemoteAudioFrameReady(frame);
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

            public string VideoProfileId;
            public PeerConnection.VideoProfileKind VideoProfileKind;
            public uint Width;
            public uint Height;
            public double Framerate;

            /// <summary>
            /// Enable Mixed Reality Capture (MRC). This flag is ignored if the platform doesn't support MRC.
            /// </summary>
            public bool EnableMixedRealityCapture;
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
            EntryPoint = "mrsPeerConnectionCreate")]
        public static extern uint PeerConnection_Create(PeerConnectionConfiguration config, IntPtr peer, out IntPtr peerHandleOut);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionRegisterInteropCallbacks")]
        public static extern void PeerConnection_RegisterInteropCallbacks(IntPtr peerHandle,
            ref MarshaledInteropCallbacks callback);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionRegisterConnectedCallback")]
        public static extern void PeerConnection_RegisterConnectedCallback(IntPtr peerHandle,
            PeerConnectionConnectedCallback callback, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionRegisterLocalSdpReadytoSendCallback")]
        public static extern void PeerConnection_RegisterLocalSdpReadytoSendCallback(IntPtr peerHandle,
            PeerConnectionLocalSdpReadytoSendCallback callback, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionRegisterIceCandidateReadytoSendCallback")]
        public static extern void PeerConnection_RegisterIceCandidateReadytoSendCallback(IntPtr peerHandle,
            PeerConnectionIceCandidateReadytoSendCallback callback, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionRegisterIceStateChangedCallback")]
        public static extern void PeerConnection_RegisterIceStateChangedCallback(IntPtr peerHandle,
            PeerConnectionIceStateChangedCallback callback, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionRegisterRenegotiationNeededCallback")]
        public static extern void PeerConnection_RegisterRenegotiationNeededCallback(IntPtr peerHandle,
            PeerConnectionRenegotiationNeededCallback callback, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionRegisterTrackAddedCallback")]
        public static extern void PeerConnection_RegisterTrackAddedCallback(IntPtr peerHandle,
            PeerConnectionTrackAddedCallback callback, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionRegisterTrackRemovedCallback")]
        public static extern void PeerConnection_RegisterTrackRemovedCallback(IntPtr peerHandle,
            PeerConnectionTrackRemovedCallback callback, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionRegisterDataChannelAddedCallback")]
        public static extern void PeerConnection_RegisterDataChannelAddedCallback(IntPtr peerHandle,
            PeerConnectionDataChannelAddedCallback callback, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionRegisterDataChannelRemovedCallback")]
        public static extern void PeerConnection_RegisterDataChannelRemovedCallback(IntPtr peerHandle,
            PeerConnectionDataChannelRemovedCallback callback, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionRegisterI420LocalVideoFrameCallback")]
        public static extern void PeerConnection_RegisterI420LocalVideoFrameCallback(IntPtr peerHandle,
            PeerConnectionI420VideoFrameCallback callback, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionRegisterI420RemoteVideoFrameCallback")]
        public static extern void PeerConnection_RegisterI420RemoteVideoFrameCallback(IntPtr peerHandle,
            PeerConnectionI420VideoFrameCallback callback, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionRegisterARGBLocalVideoFrameCallback")]
        public static extern void PeerConnection_RegisterARGBLocalVideoFrameCallback(IntPtr peerHandle,
            PeerConnectionARGBVideoFrameCallback callback, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionRegisterARGBRemoteVideoFrameCallback")]
        public static extern void PeerConnection_RegisterARGBRemoteVideoFrameCallback(IntPtr peerHandle,
            PeerConnectionARGBVideoFrameCallback callback, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionRegisterLocalAudioFrameCallback")]
        public static extern void PeerConnection_RegisterLocalAudioFrameCallback(IntPtr peerHandle,
            PeerConnectionAudioFrameCallback callback, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionRegisterRemoteAudioFrameCallback")]
        public static extern void PeerConnection_RegisterRemoteAudioFrameCallback(IntPtr peerHandle,
            PeerConnectionAudioFrameCallback callback, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionAddLocalVideoTrack")]
        public static extern uint PeerConnection_AddLocalVideoTrack(IntPtr peerHandle, VideoDeviceConfiguration config);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionAddLocalAudioTrack")]
        public static extern uint PeerConnection_AddLocalAudioTrack(IntPtr peerHandle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionAddDataChannel")]
        public static extern uint PeerConnection_AddDataChannel(IntPtr peerHandle, IntPtr dataChannel,
            DataChannelInterop.CreateConfig config, DataChannelInterop.Callbacks callbacks,
            ref IntPtr dataChannelHandle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionRemoveLocalVideoTrack")]
        public static extern void PeerConnection_RemoveLocalVideoTrack(IntPtr peerHandle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionRemoveLocalAudioTrack")]
        public static extern void PeerConnection_RemoveLocalAudioTrack(IntPtr peerHandle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionRemoveDataChannel")]
        public static extern uint PeerConnection_RemoveDataChannel(IntPtr peerHandle, IntPtr dataChannelHandle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionSetLocalVideoTrackEnabled")]
        public static extern uint PeerConnection_SetLocalVideoTrackEnabled(IntPtr peerHandle, int enabled);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionIsLocalVideoTrackEnabled")]
        public static extern int PeerConnection_IsLocalVideoTrackEnabled(IntPtr peerHandle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionSetLocalAudioTrackEnabled")]
        public static extern uint PeerConnection_SetLocalAudioTrackEnabled(IntPtr peerHandle, int enabled);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionIsLocalAudioTrackEnabled")]
        public static extern int PeerConnection_IsLocalAudioTrackEnabled(IntPtr peerHandle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionAddIceCandidate")]
        public static extern void PeerConnection_AddIceCandidate(IntPtr peerHandle, string sdpMid,
            int sdpMlineindex, string candidate);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionCreateOffer")]
        public static extern uint PeerConnection_CreateOffer(IntPtr peerHandle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionCreateAnswer")]
        public static extern uint PeerConnection_CreateAnswer(IntPtr peerHandle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionSetRemoteDescription")]
        public static extern uint PeerConnection_SetRemoteDescription(IntPtr peerHandle, string type, string sdp);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionClose")]
        public static extern void PeerConnection_Close(ref IntPtr peerHandle);

        #endregion
    }
}
