// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.WebRTC.Interop
{
    /// <summary>
    /// Handle to a native peer connection object.
    /// </summary>
    internal sealed class PeerConnectionHandle : SafeHandle
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
        private delegate void ConnectedDelegate(IntPtr peer);
        private delegate void DataChannelAddedDelegate(IntPtr peer, IntPtr dataChannel, IntPtr dataChannelHandle);
        private delegate void DataChannelRemovedDelegate(IntPtr peer, IntPtr dataChannel, IntPtr dataChannelHandle);
        private delegate void LocalSdpReadytoSendDelegate(IntPtr peer, string type, string sdp);
        private delegate void IceCandidateReadytoSendDelegate(IntPtr peer, string candidate, int sdpMlineindex, string sdpMid);
        private delegate void IceStateChangedDelegate(IntPtr peer, IceConnectionState newState);
        private delegate void IceGatheringStateChangedDelegate(IntPtr peer, IceGatheringState newState);
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
            // Keep delegates alive!
            public VideoCaptureDeviceEnumCallback EnumTrampoline;
            public VideoCaptureDeviceEnumCompletedCallback CompletedTrampoline;
        }

        [MonoPInvokeCallback(typeof(VideoCaptureDeviceEnumCallback))]
        public static void VideoCaptureDevice_EnumCallback(string id, string name, IntPtr userData)
        {
            var wrapper = Utils.ToWrapper<EnumVideoCaptureDeviceWrapper>(userData);
            wrapper.enumCallback(id, name); // this is mandatory, never null
        }

        [MonoPInvokeCallback(typeof(VideoCaptureDeviceEnumCompletedCallback))]
        public static void VideoCaptureDevice_EnumCompletedCallback(IntPtr userData)
        {
            var wrapper = Utils.ToWrapper<EnumVideoCaptureDeviceWrapper>(userData);
            wrapper.completedCallback(); // this is optional, allows to be null
        }

        public class EnumVideoCaptureFormatsWrapper
        {
            public VideoCaptureFormatEnumCallbackImpl enumCallback;
            public VideoCaptureFormatEnumCompletedCallbackImpl completedCallback;
            // Keep delegates alive!
            public VideoCaptureFormatEnumCallback EnumTrampoline;
            public VideoCaptureFormatEnumCompletedCallback CompletedTrampoline;
        }

        [MonoPInvokeCallback(typeof(VideoCaptureFormatEnumCallback))]
        public static void VideoCaptureFormat_EnumCallback(uint width, uint height, double framerate, uint fourcc, IntPtr userData)
        {
            var wrapper = Utils.ToWrapper<EnumVideoCaptureFormatsWrapper>(userData);
            wrapper.enumCallback(width, height, framerate, fourcc); // this is mandatory, never null
        }

        [MonoPInvokeCallback(typeof(VideoCaptureFormatEnumCompletedCallback))]
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
            public DataChannelInterop.CreateObjectDelegate DataChannelCreateObjectCallback;
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
            public PeerConnectionIceGatheringStateChangedCallback IceGatheringStateChangedCallback;
            public PeerConnectionRenegotiationNeededCallback RenegotiationNeededCallback;
            public PeerConnectionTrackAddedCallback TrackAddedCallback;
            public PeerConnectionTrackRemovedCallback TrackRemovedCallback;
            public LocalVideoTrackInterop.I420AVideoFrameUnmanagedCallback I420ALocalVideoFrameCallback;
            public LocalVideoTrackInterop.I420AVideoFrameUnmanagedCallback I420ARemoteVideoFrameCallback;
            public LocalVideoTrackInterop.Argb32VideoFrameUnmanagedCallback Argb32LocalVideoFrameCallback;
            public LocalVideoTrackInterop.Argb32VideoFrameUnmanagedCallback Argb32RemoteVideoFrameCallback;
            public AudioFrameUnmanagedCallback LocalAudioFrameCallback;
            public AudioFrameUnmanagedCallback RemoteAudioFrameCallback;
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

        [MonoPInvokeCallback(typeof(IceGatheringStateChangedDelegate))]
        public static void IceGatheringStateChangedCallback(IntPtr userData, IceGatheringState newState)
        {
            var peer = Utils.ToWrapper<PeerConnection>(userData);
            peer.OnIceGatheringStateChanged(newState);
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

        [MonoPInvokeCallback(typeof(LocalVideoTrackInterop.I420AVideoFrameUnmanagedCallback))]
        public static void I420ARemoteVideoFrameCallback(IntPtr userData, ref I420AVideoFrame frame)
        {
            var peer = Utils.ToWrapper<PeerConnection>(userData);
            peer.OnI420ARemoteVideoFrameReady(frame);
        }

        [MonoPInvokeCallback(typeof(LocalVideoTrackInterop.Argb32VideoFrameUnmanagedCallback))]
        public static void Argb32RemoteVideoFrameCallback(IntPtr userData, ref Argb32VideoFrame frame)
        {
            var peer = Utils.ToWrapper<PeerConnection>(userData);
            peer.OnArgb32RemoteVideoFrameReady(frame);
        }

        [MonoPInvokeCallback(typeof(AudioFrameUnmanagedCallback))]
        public static void LocalAudioFrameCallback(IntPtr userData, ref AudioFrame frame)
        {
            var peer = Utils.ToWrapper<PeerConnection>(userData);
            peer.OnLocalAudioFrameReady(frame);
        }

        [MonoPInvokeCallback(typeof(AudioFrameUnmanagedCallback))]
        public static void RemoteAudioFrameCallback(IntPtr userData, ref AudioFrame frame)
        {
            var peer = Utils.ToWrapper<PeerConnection>(userData);
            peer.OnRemoteAudioFrameReady(frame);
        }

        public static readonly PeerConnectionSimpleStatsCallback SimpleStatsReportDelegate = SimpleStatsReportCallback;

        [MonoPInvokeCallback(typeof(PeerConnectionSimpleStatsCallback))]
        public unsafe static void SimpleStatsReportCallback(IntPtr userData, IntPtr report)
        {
            var tcsHandle = GCHandle.FromIntPtr(userData);
            var tcs = tcsHandle.Target as TaskCompletionSource<PeerConnection.StatsReport>;
            tcs.SetResult(new PeerConnection.StatsReport(report));
            tcsHandle.Free();
        }

        public static Task<PeerConnection.StatsReport> GetSimpleStatsAsync(PeerConnectionHandle peerHandle)
        {
            var tcs = new TaskCompletionSource<PeerConnection.StatsReport>();
            var resPtr = Utils.MakeWrapperRef(tcs);
            PeerConnection_GetSimpleStats(peerHandle, SimpleStatsReportDelegate, resPtr);

            // The Task result will be set by the callback when the report is ready.
            return tcs.Task;
        }

        [MonoPInvokeCallback(typeof(PeerConnectionSimpleStatsObjectCallback))]
        public unsafe static void GetStatsObjectCallback(IntPtr userData, IntPtr statsObject)
        {
            var list = Utils.ToWrapper<object>(userData);
            if (list is List<PeerConnection.DataChannelStats> dataStatsList)
            {
                dataStatsList.Add(*(PeerConnection.DataChannelStats*)statsObject);
            }
            else if (list is List<PeerConnection.AudioSenderStats> audioSenderStatsList)
            {
                audioSenderStatsList.Add(Marshal.PtrToStructure<PeerConnection.AudioSenderStats>(statsObject));
            }
            else if (list is List<PeerConnection.AudioReceiverStats> audioReceiverStatsList)
            {
                audioReceiverStatsList.Add(Marshal.PtrToStructure<PeerConnection.AudioReceiverStats>(statsObject));
            }
            else if (list is List<PeerConnection.VideoSenderStats> videoSenderStatsList)
            {
                videoSenderStatsList.Add(Marshal.PtrToStructure<PeerConnection.VideoSenderStats>(statsObject));
            }
            else if (list is List<PeerConnection.VideoReceiverStats> videoReceiverStatsList)
            {
                videoReceiverStatsList.Add(Marshal.PtrToStructure<PeerConnection.VideoReceiverStats>(statsObject));
            }
            else if (list is List<PeerConnection.TransportStats> transportStatsList)
            {
                transportStatsList.Add(*(PeerConnection.TransportStats*)statsObject);
            }
        }

        public static IEnumerable<T> GetStatsObject<T>(PeerConnection.StatsReport.Handle reportHandle)
        {
            var res = new List<T>();
            var resHandle = GCHandle.Alloc(res, GCHandleType.Normal);
            StatsReport_GetObjects(reportHandle, typeof(T).Name, GetStatsObjectCallback, GCHandle.ToIntPtr(resHandle));
            resHandle.Free();
            return res;
        }

        [MonoPInvokeCallback(typeof(ActionDelegate))]
        public static void RemoteDescriptionApplied(IntPtr args)
        {
            var remoteDesc = Utils.ToWrapper<RemoteDescArgs>(args);
            remoteDesc.completedEvent.Set();
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        internal struct MarshaledInteropCallbacks
        {
            public DataChannelInterop.CreateObjectDelegate DataChannelCreateObjectCallback;
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
        /// Helper structure to pass parameters to the native implementation when creating a local video track
        /// by opening a local video capture device.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        internal ref struct LocalVideoTrackInteropInitConfig
        {
            /// <summary>
            /// Handle to the local video track wrapper.
            /// </summary>
            public IntPtr trackHandle;

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
            public VideoProfileKind VideoProfileKind;

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

            /// <summary>
            /// Constructor for creating a local video track from a wrapper and some user settings.
            /// </summary>
            /// <param name="track">The newly created track wrapper.</param>
            /// <param name="settings">The settings to initialize the newly created native track.</param>
            /// <seealso cref="PeerConnection.AddLocalVideoTrackAsync(LocalVideoTrackSettings)"/>
            public LocalVideoTrackInteropInitConfig(LocalVideoTrack track, LocalVideoTrackSettings settings)
            {
                trackHandle = Utils.MakeWrapperRef(track);

                if (settings != null)
                {
                    VideoDeviceId = settings.videoDevice.id;
                    VideoProfileId = settings.videoProfileId;
                    VideoProfileKind = settings.videoProfileKind;
                    Width = settings.width.GetValueOrDefault(0);
                    Height = settings.height.GetValueOrDefault(0);
                    Framerate = settings.framerate.GetValueOrDefault(0.0);
                    EnableMixedRealityCapture = (mrsBool)settings.enableMrc;
                    EnableMRCRecordingIndicator = (mrsBool)settings.enableMrcRecordingIndicator;
                }
                else
                {
                    VideoDeviceId = string.Empty;
                    VideoProfileId = string.Empty;
                    VideoProfileKind = VideoProfileKind.Unspecified;
                    Width = 0;
                    Height = 0;
                    Framerate = 0.0;
                    EnableMixedRealityCapture = mrsBool.True;
                    EnableMRCRecordingIndicator = mrsBool.True;
                }
            }
        }

        /// <summary>
        /// Helper structure to pass parameters to the native implementation when creating a local video track
        /// from an existing external video track source.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        internal ref struct LocalVideoTrackFromExternalSourceInteropInitConfig
        {
            /// <summary>
            /// Handle to the <see cref="LocalVideoTrack"/> wrapper for the native local video track that will
            /// be created.
            /// </summary>
            public IntPtr LocalVideoTrackWrapperHandle;

            /// <summary>
            /// Constructor for creating a local video track from a wrapper and an existing external source.
            /// </summary>
            /// <param name="track">The newly created track wrapper.</param>
            /// <param name="source">The external source to use with the newly created native track.</param>
            /// <seealso cref="PeerConnection.AddCustomLocalVideoTrack(string, ExternalVideoTrackSource)"/>
            public LocalVideoTrackFromExternalSourceInteropInitConfig(LocalVideoTrack track, ExternalVideoTrackSource source)
            {
                LocalVideoTrackWrapperHandle = Utils.MakeWrapperRef(track);
            }
        }


        #region Reverse P/Invoke delegates

        // Note - none of those method arguments can be SafeHandle; use IntPtr instead.

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
        public delegate void PeerConnectionIceGatheringStateChangedCallback(IntPtr userData,
            IceGatheringState newState);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void PeerConnectionRenegotiationNeededCallback(IntPtr userData);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void PeerConnectionTrackAddedCallback(IntPtr userData, PeerConnection.TrackKind trackKind);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void PeerConnectionTrackRemovedCallback(IntPtr userData, PeerConnection.TrackKind trackKind);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void AudioFrameUnmanagedCallback(IntPtr userData, ref AudioFrame frame);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void PeerConnectionSimpleStatsCallback(IntPtr userData, IntPtr statsReport);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void PeerConnectionSimpleStatsObjectCallback(IntPtr userData, IntPtr statsObject);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void ActionDelegate(IntPtr peer);

        #endregion


        #region P/Invoke static functions

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsEnumVideoCaptureDevicesAsync")]
        public static extern uint EnumVideoCaptureDevicesAsync(VideoCaptureDeviceEnumCallback enumCallback, IntPtr userData,
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
            EntryPoint = "mrsPeerConnectionRegisterIceGatheringStateChangedCallback")]
        public static extern void PeerConnection_RegisterIceGatheringStateChangedCallback(PeerConnectionHandle peerHandle,
            PeerConnectionIceGatheringStateChangedCallback callback, IntPtr userData);

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
            LocalVideoTrackInterop.I420AVideoFrameUnmanagedCallback callback, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionRegisterArgb32RemoteVideoFrameCallback")]
        public static extern void PeerConnection_RegisterArgb32RemoteVideoFrameCallback(PeerConnectionHandle peerHandle,
            LocalVideoTrackInterop.Argb32VideoFrameUnmanagedCallback callback, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionRegisterLocalAudioFrameCallback")]
        public static extern void PeerConnection_RegisterLocalAudioFrameCallback(PeerConnectionHandle peerHandle,
            AudioFrameUnmanagedCallback callback, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionRegisterRemoteAudioFrameCallback")]
        public static extern void PeerConnection_RegisterRemoteAudioFrameCallback(PeerConnectionHandle peerHandle,
            AudioFrameUnmanagedCallback callback, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionAddLocalVideoTrack")]
        public static extern uint PeerConnection_AddLocalVideoTrack(PeerConnectionHandle peerHandle,
            string trackName, in LocalVideoTrackInteropInitConfig config, out LocalVideoTrackHandle trackHandle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionAddLocalVideoTrackFromExternalSource")]
        public static extern uint PeerConnection_AddLocalVideoTrackFromExternalSource(
            PeerConnectionHandle peerHandle, string trackName, ExternalVideoTrackSourceHandle sourceHandle,
            in LocalVideoTrackFromExternalSourceInteropInitConfig config, out LocalVideoTrackHandle trackHandle);

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
            EntryPoint = "mrsPeerConnectionSetRemoteDescriptionAsync")]
        public static extern uint PeerConnection_SetRemoteDescriptionAsync(PeerConnectionHandle peerHandle,
            string type, string sdp, ActionDelegate callback, IntPtr callbackArgs);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionClose")]
        public static extern uint PeerConnection_Close(PeerConnectionHandle peerHandle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionGetSimpleStats")]
        public static extern void PeerConnection_GetSimpleStats(PeerConnectionHandle peerHandle, PeerConnectionSimpleStatsCallback callback, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsStatsReportGetObjects")]
        public static extern void StatsReport_GetObjects(PeerConnection.StatsReport.Handle reportHandle, string stats_type, PeerConnectionSimpleStatsObjectCallback callback, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsStatsReportRemoveRef")]
        public static extern void StatsReport_RemoveRef(IntPtr reportHandle);

        #endregion


        #region Helpers

        class RemoteDescArgs
        {
            public ActionDelegate callback;
            public ManualResetEventSlim completedEvent;
        }

        public static Task SetRemoteDescriptionAsync(PeerConnectionHandle peerHandle, string type, string sdp)
        {
            return Task.Run(() =>
            {
                var args = new RemoteDescArgs
                {
                    callback = RemoteDescriptionApplied,
                    completedEvent = new ManualResetEventSlim(initialState: false)
                };
                IntPtr argsRef = Utils.MakeWrapperRef(args);
                uint res = PeerConnection_SetRemoteDescriptionAsync(peerHandle, type, sdp, args.callback, argsRef);
                if (res != Utils.MRS_SUCCESS)
                {
                    Utils.ReleaseWrapperRef(argsRef);
                    Utils.ThrowOnErrorCode(res);
                }
                args.completedEvent.Wait();
                Utils.ReleaseWrapperRef(argsRef);
            });
        }

        #endregion
    }
}
