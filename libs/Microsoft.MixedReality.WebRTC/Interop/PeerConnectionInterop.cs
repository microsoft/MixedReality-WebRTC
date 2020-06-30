// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.WebRTC.Interop
{
    /// <summary>
    /// Handle to a native peer connection object.
    /// </summary>
    internal sealed class PeerConnectionHandle : RefCountedObjectHandle { }

    internal class PeerConnectionInterop
    {
        // Types of trampolines for MonoPInvokeCallback
        private delegate void ConnectedDelegate(IntPtr peer);
        private delegate void LocalSdpReadytoSendDelegate(IntPtr peer, string type, string sdp);
        private delegate void IceCandidateReadytoSendDelegate(IntPtr peer, in IceCandidate candidate);
        private delegate void IceStateChangedDelegate(IntPtr peer, IceConnectionState newState);
        private delegate void IceGatheringStateChangedDelegate(IntPtr peer, IceGatheringState newState);
        private delegate void RenegotiationNeededDelegate(IntPtr peer);

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
            public PeerConnectionTransceiverAddedCallback TransceiverAddedCallback;
            public PeerConnectionAudioTrackAddedCallback AudioTrackAddedCallback;
            public PeerConnectionAudioTrackRemovedCallback AudioTrackRemovedCallback;
            public PeerConnectionVideoTrackAddedCallback VideoTrackAddedCallback;
            public PeerConnectionVideoTrackRemovedCallback VideoTrackRemovedCallback;
        }

        [MonoPInvokeCallback(typeof(ConnectedDelegate))]
        public static void ConnectedCallback(IntPtr userData)
        {
            var peer = Utils.ToWrapper<PeerConnection>(userData);
            peer.OnConnected();
        }

        [MonoPInvokeCallback(typeof(PeerConnectionDataChannelAddedCallback))]
        public static void DataChannelAddedCallback(IntPtr userData, in DataChannelAddedInfo info)
        {
            var peerWrapper = Utils.ToWrapper<PeerConnection>(userData);
            var dataChannelWrapper = DataChannelInterop.CreateWrapper(peerWrapper, in info);
            peerWrapper.OnDataChannelAdded(dataChannelWrapper);
        }

        [MonoPInvokeCallback(typeof(PeerConnectionDataChannelRemovedCallback))]
        public static void DataChannelRemovedCallback(IntPtr userData, IntPtr dataChannelHandle)
        {
            var peerWrapper = Utils.ToWrapper<PeerConnection>(userData);
            IntPtr dataChannelRef = DataChannelInterop.DataChannel_GetUserData(dataChannelHandle);
            DataChannelInterop.DataChannel_SetUserData(dataChannelHandle, IntPtr.Zero);
            var dataChannelWrapper = Utils.ToWrapper<DataChannel>(dataChannelRef);
            peerWrapper.OnDataChannelRemoved(dataChannelWrapper);
            dataChannelWrapper.DestroyNative();
            Utils.ReleaseWrapperRef(dataChannelRef);
        }

        [MonoPInvokeCallback(typeof(LocalSdpReadytoSendDelegate))]
        public static void LocalSdpReadytoSendCallback(IntPtr userData, SdpMessageType type, string sdp)
        {
            var peer = Utils.ToWrapper<PeerConnection>(userData);
            peer.OnLocalSdpReadytoSend(type, sdp);
        }

        [MonoPInvokeCallback(typeof(IceCandidateReadytoSendDelegate))]
        public static void IceCandidateReadytoSendCallback(IntPtr userData, in IceCandidate candidate)
        {
            var peer = Utils.ToWrapper<PeerConnection>(userData);
            peer.OnIceCandidateReadytoSend(candidate);
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

        [MonoPInvokeCallback(typeof(PeerConnectionTransceiverAddedCallback))]
        public static void TransceiverAddedCallback(IntPtr peer, in TransceiverAddedInfo info)
        {
            var peerWrapper = Utils.ToWrapper<PeerConnection>(peer);
            var transceiverWrapper = TransceiverInterop.CreateWrapper(peerWrapper, in info);
            peerWrapper.OnTransceiverAdded(transceiverWrapper);
        }

        [MonoPInvokeCallback(typeof(PeerConnectionAudioTrackAddedCallback))]
        public static void AudioTrackAddedCallback(IntPtr peer, in RemoteAudioTrackAddedInfo info)
        {
            var peerWrapper = Utils.ToWrapper<PeerConnection>(peer);
            IntPtr transceiver = ObjectInterop.Object_GetUserData(new TransceiverInterop.TransceiverHandle(info.audioTransceiverHandle));
            Debug.Assert(transceiver != IntPtr.Zero); // must have been set by the TransceiverAdded event
            var transceiverWrapper = Utils.ToWrapper<Transceiver>(transceiver);
            var remoteAudioTrackWrapper = RemoteAudioTrackInterop.CreateWrapper(peerWrapper, in info);
            peerWrapper.OnAudioTrackAdded(remoteAudioTrackWrapper, transceiverWrapper);
        }

        [MonoPInvokeCallback(typeof(PeerConnectionAudioTrackRemovedCallback))]
        public static void AudioTrackRemovedCallback(IntPtr userData, IntPtr audioTrackHandle, IntPtr audioTransceiverHandle)
        {
            var peerWrapper = Utils.ToWrapper<PeerConnection>(userData);
            IntPtr audioTrackRef = ObjectInterop.Object_GetUserData(new RemoteAudioTrackInterop.RemoteAudioTrackHandle(audioTrackHandle));
            var audioTrackWrapper = Utils.ToWrapper<RemoteAudioTrack>(audioTrackRef);
            peerWrapper.OnAudioTrackRemoved(audioTrackWrapper);
        }

        [MonoPInvokeCallback(typeof(PeerConnectionVideoTrackAddedCallback))]
        public static void VideoTrackAddedCallback(IntPtr peer, in RemoteVideoTrackAddedInfo info)
        {
            var peerWrapper = Utils.ToWrapper<PeerConnection>(peer);
            IntPtr transceiver = ObjectInterop.Object_GetUserData(new TransceiverInterop.TransceiverHandle(info.videoTransceiverHandle));
            Debug.Assert(transceiver != IntPtr.Zero); // must have been set by the TransceiverAdded event
            var transceiverWrapper = Utils.ToWrapper<Transceiver>(transceiver);
            var remoteVideoTrackWrapper = RemoteVideoTrackInterop.CreateWrapper(peerWrapper, in info);
            peerWrapper.OnVideoTrackAdded(remoteVideoTrackWrapper, transceiverWrapper);
        }

        [MonoPInvokeCallback(typeof(PeerConnectionVideoTrackRemovedCallback))]
        public static void VideoTrackRemovedCallback(IntPtr userData, IntPtr videoTrackHandle, IntPtr videoTransceiverHandle)
        {
            var peerWrapper = Utils.ToWrapper<PeerConnection>(userData);
            IntPtr videoTrackRef = ObjectInterop.Object_GetUserData(new RemoteVideoTrackInterop.RemoteVideoTrackHandle(videoTrackHandle));
            var videoTrackWrapper = Utils.ToWrapper<RemoteVideoTrack>(videoTrackRef);
            peerWrapper.OnVideoTrackRemoved(videoTrackWrapper);
        }

        [MonoPInvokeCallback(typeof(PeerConnectionRemoteDescriptionAppliedDelegate))]
        public static void RemoteDescriptionApplied(IntPtr argsRef, uint result, string errorMessage)
        {
            var remoteDescArgs = Utils.ToWrapper<RemoteDescArgs>(argsRef);
            if (result != Utils.MRS_SUCCESS)
            {
                remoteDescArgs.tcs.SetException(Utils.GetExceptionForErrorCode(result));
            }
            else
            {
                remoteDescArgs.tcs.SetResult(true);
            }
            Utils.ReleaseWrapperRef(argsRef);
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

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        internal ref struct PeerConnectionConfiguration
        {
            [MarshalAs(UnmanagedType.LPStr)]
            public string EncodedIceServers;
            public IceTransportType IceTransportType;
            public BundlePolicy BundlePolicy;
            public SdpSemantic SdpSemantic;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        internal ref struct IceCandidate
        {
            [MarshalAs(UnmanagedType.LPStr)]
            public string SdpMid;
            [MarshalAs(UnmanagedType.LPStr)]
            public string Content;
            public int SdpMlineIndex;
        }

        /// <summary>
        /// Marshalling structure to receive information about a newly created data channel
        /// just added to the peer connection after a remote description was applied.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        internal struct DataChannelAddedInfo
        {
            /// <summary>
            /// Handle of the newly created data channel.
            /// </summary>
            public IntPtr dataChannelHandle;

            public int id;
            public uint flags;
            public string label;
        }

        /// <summary>
        /// Marshalling structure to receive information about a newly created transceiver
        /// just added to the peer connection.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        internal struct TransceiverAddedInfo
        {
            /// <summary>
            /// Handle to the newly-created native transceiver object.
            /// </summary>
            public IntPtr transceiverHandle;

            /// <summary>
            /// Transceiver name.
            /// </summary>
            [MarshalAs(UnmanagedType.LPStr)]
            public string name;

            /// <summary>
            /// Kind of media the transceiver transports.
            /// </summary>
            public MediaKind mediaKind;

            /// <summary>
            /// Media line index of the transceiver.
            /// </summary>
            public int mlineIndex;

            /// <summary>
            /// Encoded string of semi-colon separated list of stream IDs.
            /// Example for stream IDs ("id1", "id2", "id3"):
            ///   encodedStreamIDs = "id1;id2;id3";
            /// </summary>
            [MarshalAs(UnmanagedType.LPStr)]
            public string encodedStreamIDs;

            /// <summary>
            /// Initial desired direction of the transceiver on creation.
            /// </summary>
            public Transceiver.Direction desiredDirection;
        }

        /// <summary>
        /// Marshalling structure to receive information about a newly created remote audio track
        /// just added to a transceiver of the peer connection.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        internal struct RemoteAudioTrackAddedInfo
        {
            /// <summary>
            /// Handle of the newly created remote audio track.
            /// </summary>
            public IntPtr trackHandle;

            /// <summary>
            /// Handle of the audio transceiver the track was added to.
            /// </summary>
            public IntPtr audioTransceiverHandle;

            /// <summary>
            /// Name of the remote audio track.
            /// </summary>
            [MarshalAs(UnmanagedType.LPStr)]
            public string trackName;
        }

        /// <summary>
        /// Marshalling structure to receive information about a newly created remote video track
        /// just added to a transceiver of the peer connection.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        internal struct RemoteVideoTrackAddedInfo
        {
            /// <summary>
            /// Handle of the newly created remote video track.
            /// </summary>
            public IntPtr trackHandle;

            /// <summary>
            /// Handle of the video transceiver the track was added to.
            /// </summary>
            public IntPtr videoTransceiverHandle;

            /// <summary>
            /// Name of the remote video track.
            /// </summary>
            [MarshalAs(UnmanagedType.LPStr)]
            public string trackName;
        }


        #region Reverse P/Invoke delegates

        // Note - none of those method arguments can be SafeHandle; use IntPtr instead.

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void PeerConnectionDataChannelAddedCallback(IntPtr userData, in DataChannelAddedInfo info);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void PeerConnectionDataChannelRemovedCallback(IntPtr userData, IntPtr dataChannelHandle);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void PeerConnectionRemoteDescriptionAppliedDelegate(IntPtr userData, uint result, string errorMessage);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void PeerConnectionInteropCallbacks(IntPtr userData);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void PeerConnectionConnectedCallback(IntPtr userData);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void PeerConnectionLocalSdpReadytoSendCallback(IntPtr userData,
            SdpMessageType type, string sdp);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void PeerConnectionIceCandidateReadytoSendCallback(IntPtr userData, in IceCandidate candidate);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void PeerConnectionIceStateChangedCallback(IntPtr userData,
            IceConnectionState newState);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void PeerConnectionIceGatheringStateChangedCallback(IntPtr userData,
            IceGatheringState newState);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void PeerConnectionRenegotiationNeededCallback(IntPtr userData);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void PeerConnectionTransceiverAddedCallback(IntPtr peerHandle, in TransceiverAddedInfo info);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void PeerConnectionAudioTrackAddedCallback(IntPtr peerHandle, in RemoteAudioTrackAddedInfo info);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void PeerConnectionAudioTrackRemovedCallback(IntPtr peerHandle, IntPtr audioTrackHandle, IntPtr audioTransceiverHandle);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void PeerConnectionVideoTrackAddedCallback(IntPtr peerHandle, in RemoteVideoTrackAddedInfo info);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void PeerConnectionVideoTrackRemovedCallback(IntPtr peerHandle, IntPtr videoTrackHandle, IntPtr videoTransceiverHandle);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void AudioFrameUnmanagedCallback(IntPtr userData, in AudioFrame frame);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void PeerConnectionSimpleStatsCallback(IntPtr userData, IntPtr statsReport);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void PeerConnectionSimpleStatsObjectCallback(IntPtr userData, IntPtr statsObject);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void ActionDelegate(IntPtr peer);


        #endregion


        #region P/Invoke static functions

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionCreate")]
        public static extern uint PeerConnection_Create(in PeerConnectionConfiguration config, out PeerConnectionHandle peerHandleOut);

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
            EntryPoint = "mrsPeerConnectionRegisterTransceiverAddedCallback")]
        public static extern void PeerConnection_RegisterTransceiverAddedCallback(PeerConnectionHandle peerHandle,
            PeerConnectionTransceiverAddedCallback callback, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionRegisterAudioTrackAddedCallback")]
        public static extern void PeerConnection_RegisterAudioTrackAddedCallback(PeerConnectionHandle peerHandle,
            PeerConnectionAudioTrackAddedCallback callback, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionRegisterAudioTrackRemovedCallback")]
        public static extern void PeerConnection_RegisterAudioTrackRemovedCallback(PeerConnectionHandle peerHandle,
            PeerConnectionAudioTrackRemovedCallback callback, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionRegisterVideoTrackAddedCallback")]
        public static extern void PeerConnection_RegisterVideoTrackAddedCallback(PeerConnectionHandle peerHandle,
            PeerConnectionVideoTrackAddedCallback callback, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionRegisterVideoTrackRemovedCallback")]
        public static extern void PeerConnection_RegisterVideoTrackRemovedCallback(PeerConnectionHandle peerHandle,
            PeerConnectionVideoTrackRemovedCallback callback, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionRegisterDataChannelAddedCallback")]
        public static extern void PeerConnection_RegisterDataChannelAddedCallback(PeerConnectionHandle peerHandle,
            PeerConnectionDataChannelAddedCallback callback, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionRegisterDataChannelRemovedCallback")]
        public static extern void PeerConnection_RegisterDataChannelRemovedCallback(PeerConnectionHandle peerHandle,
            PeerConnectionDataChannelRemovedCallback callback, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionAddTransceiver")]
        public static extern uint PeerConnection_AddTransceiver(PeerConnectionHandle peerHandle,
            in TransceiverInterop.InitConfig config, out IntPtr transceiverHandle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionAddDataChannel")]
        public static extern uint PeerConnection_AddDataChannel(PeerConnectionHandle peerHandle,
            in DataChannelInterop.CreateConfig config, out IntPtr dataChannelHandle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionRemoveDataChannel")]
        public static extern uint PeerConnection_RemoveDataChannel(PeerConnectionHandle peerHandle, IntPtr dataChannelHandle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsPeerConnectionAddIceCandidate")]
        public static extern void PeerConnection_AddIceCandidate(PeerConnectionHandle peerHandle, in IceCandidate candidate);

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
            SdpMessageType type, string sdp, PeerConnectionRemoteDescriptionAppliedDelegate callback, IntPtr callbackArgs);

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

        #region Utilities

        class RemoteDescArgs
        {
            public PeerConnectionRemoteDescriptionAppliedDelegate callback;
            public TaskCompletionSource<bool> tcs;
        }

        public static Task SetRemoteDescriptionAsync(PeerConnectionHandle peerHandle, SdpMessageType type, string sdp)
        {
            var args = new RemoteDescArgs
            {
                // Keep the delegate alive during the async call
                callback = RemoteDescriptionApplied,
                // Use dummy <bool> due to lack of parameterless variant
                tcs = new TaskCompletionSource<bool>()
            };
            IntPtr argsRef = Utils.MakeWrapperRef(args);
            uint res = PeerConnection_SetRemoteDescriptionAsync(peerHandle, type, sdp, args.callback, argsRef);
            if (res != Utils.MRS_SUCCESS)
            {
                // On error, the SRD task was not enqueued, so the callback will never get called
                Utils.ReleaseWrapperRef(argsRef);
                Utils.ThrowOnErrorCode(res);
                return Task.CompletedTask;
            }
            return args.tcs.Task;
        }

        #endregion
    }
}
