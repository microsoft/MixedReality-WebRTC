// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#pragma once

#if defined(WINUWP)
// Non-API helper. Returned object can be deleted at any time in theory.
// In practice because it's provided by a global object it's safe.
//< TODO - Remove that, clean-up API, this is bad (c).
rtc::Thread* UnsafeGetWorkerThread();
#endif

#include "export.h"

extern "C" {

//
// Errors
//

using mrsResult = std::uint32_t;

constexpr const mrsResult MRS_SUCCESS{0};

// Generic errors
constexpr const mrsResult MRS_E_UNKNOWN{0x80000000};
constexpr const mrsResult MRS_E_INVALID_PARAMETER{0x80000001};
constexpr const mrsResult MRS_E_INVALID_OPERATION{0x80000002};
constexpr const mrsResult MRS_E_WRONG_THREAD{0x80000003};

// Peer conection (0x1xx)
constexpr const mrsResult MRS_E_INVALID_PEER_HANDLE{0x80000101};
constexpr const mrsResult MRS_E_PEER_NOT_INITIALIZED{0x80000102};

// Data (0x3xx)
constexpr const mrsResult MRS_E_SCTP_NOT_NEGOTIATED{0x80000301};
constexpr const mrsResult MRS_E_INVALID_DATA_CHANNEL_ID{0x80000302};

//
// Generic utilities
//

/// Opaque enumerator type.
struct mrsEnumerator;

/// Handle to an enumerator.
/// This must be freed after use with |mrsCloseEnum|.
using mrsEnumHandle = mrsEnumerator*;

/// Close an enumerator previously obtained from one of the EnumXxx() calls.
MRS_API void MRS_CALL mrsCloseEnum(mrsEnumHandle* handleRef) noexcept;

//
// Interop
//

struct mrsDataChannelConfig;
struct mrsDataChannelCallbacks;

/// Opaque handle to the interop wrapper of a peer connection.
using mrsPeerConnectionInteropHandle = void*;

/// Opaque handle to the interop wrapper of a data channel.
using mrsDataChannelInteropHandle = void*;

/// Callback to create an interop wrapper for a data channel.
using mrsPeerConnectionDataChannelCreateObjectCallback =
    mrsDataChannelInteropHandle(MRS_CALL*)(
        mrsPeerConnectionInteropHandle parent,
        mrsDataChannelConfig config,
        mrsDataChannelCallbacks* callbacks);

//
// Video capture enumeration
//

/// Callback invoked for each enumerated video capture device.
using mrsVideoCaptureDeviceEnumCallback = void(MRS_CALL*)(const char* id,
                                                          const char* name,
                                                          void* user_data);

/// Callback invoked on video capture device enumeration completed.
using mrsVideoCaptureDeviceEnumCompletedCallback =
    void(MRS_CALL*)(void* user_data);

/// Enumerate the video capture devices asynchronously.
/// For each device found, invoke the mandatory |callback|.
/// At the end of the enumeration, invoke the optional |completedCallback| if it
/// was provided (non-null).
MRS_API void MRS_CALL mrsEnumVideoCaptureDevicesAsync(
    mrsVideoCaptureDeviceEnumCallback enumCallback,
    void* enumCallbackUserData,
    mrsVideoCaptureDeviceEnumCompletedCallback completedCallback,
    void* completedCallbackUserData) noexcept;

/// Callback invoked for each enumerated video capture format.
using mrsVideoCaptureFormatEnumCallback = void(MRS_CALL*)(uint32_t width,
                                                          uint32_t height,
                                                          double framerate,
                                                          uint32_t encoding,
                                                          void* user_data);

/// Callback invoked on video capture format enumeration completed.
using mrsVideoCaptureFormatEnumCompletedCallback =
    void(MRS_CALL*)(mrsResult result, void* user_data);

/// Enumerate the video capture formats asynchronously.
/// For each device found, invoke the mandatory |callback|.
/// At the end of the enumeration, invoke the optional |completedCallback| if it
/// was provided (non-null).
MRS_API mrsResult MRS_CALL mrsEnumVideoCaptureFormatsAsync(
    const char* device_id,
    mrsVideoCaptureFormatEnumCallback enumCallback,
    void* enumCallbackUserData,
    mrsVideoCaptureFormatEnumCompletedCallback completedCallback,
    void* completedCallbackUserData) noexcept;

//
// Peer connection
//

/// Opaque handle to a native PeerConnection C++ object.
using PeerConnectionHandle = void*;

/// Opaque handle to a native DataChannel C++ object.
using DataChannelHandle = void*;

/// Callback fired when the peer connection is connected, that is it finished
/// the JSEP offer/answer exchange successfully.
using PeerConnectionConnectedCallback = void(MRS_CALL*)(void* user_data);

/// Callback fired when a local SDP message has been prepared and is ready to be
/// sent by the user via the signaling service.
using PeerConnectionLocalSdpReadytoSendCallback =
    void(MRS_CALL*)(void* user_data, const char* type, const char* sdp_data);

/// Callback fired when an ICE candidate has been prepared and is ready to be
/// sent by the user via the signaling service.
using PeerConnectionIceCandidateReadytoSendCallback =
    void(MRS_CALL*)(void* user_data,
                    const char* candidate,
                    int sdpMlineindex,
                    const char* sdpMid);

/// State of the ICE connection.
/// See https://www.w3.org/TR/webrtc/#rtciceconnectionstate-enum.
/// Note that there is a mismatch currently due to the m71 implementation.
enum IceConnectionState : int32_t {
  kNew = 0,
  kChecking = 1,
  kConnected = 2,
  kCompleted = 3,
  kFailed = 4,
  kDisconnected = 5,
  kClosed = 6,
};

/// Callback fired when the state of the ICE connection changed.
using PeerConnectionIceStateChangedCallback =
    void(MRS_CALL*)(void* user_data, IceConnectionState new_state);

/// Callback fired when a renegotiation of the current session needs to occur to
/// account for new parameters (e.g. added or removed tracks).
using PeerConnectionRenegotiationNeededCallback =
    void(MRS_CALL*)(void* user_data);

/// Kind of media track. Equivalent to
/// webrtc::MediaStreamTrackInterface::kind().
enum class TrackKind : uint32_t {
  kUnknownTrack = 0,
  kAudioTrack = 1,
  kVideoTrack = 2,
  kDataTrack = 3,
};

/// Callback fired when a remote track is added to a connection.
using PeerConnectionTrackAddedCallback = void(MRS_CALL*)(void* user_data,
                                                         TrackKind track_kind);

/// Callback fired when a remote track is removed from a connection.
using PeerConnectionTrackRemovedCallback =
    void(MRS_CALL*)(void* user_data, TrackKind track_kind);

/// Callback fired when a data channel is added to the peer connection after
/// being negotiated with the remote peer.
using PeerConnectionDataChannelAddedCallback =
    void(MRS_CALL*)(void* user_data,
                    mrsDataChannelInteropHandle data_channel_wrapper,
                    DataChannelHandle data_channel);

/// Callback fired when a data channel is remoted from the peer connection.
using PeerConnectionDataChannelRemovedCallback =
    void(MRS_CALL*)(void* user_data,
                    mrsDataChannelInteropHandle data_channel_wrapper,
                    DataChannelHandle data_channel);

/// Callback fired when a local or remote (depending on use) video frame is
/// available to be consumed by the caller, usually for display.
/// The video frame is encoded in I420 triplanar format (NV12).
using PeerConnectionI420VideoFrameCallback =
    void(MRS_CALL*)(void* user_data,
                    const void* yptr,
                    const void* uptr,
                    const void* vptr,
                    const void* aptr,
                    const int ystride,
                    const int ustride,
                    const int vstride,
                    const int astride,
                    const int frame_width,  //< TODO : uint?
                    const int frame_height);

/// Callback fired when a local or remote (depending on use) video frame is
/// available to be consumed by the caller, usually for display.
/// The video frame is encoded in ARGB 32-bit per pixel.
using PeerConnectionARGBVideoFrameCallback =
    void(MRS_CALL*)(void* user_data,
                    const void* data,
                    const int stride,
                    const int frame_width,
                    const int frame_height);

/// Callback fired when a local or remote (depending on use) audio frame is
/// available to be consumed by the caller, usually for local output.
using PeerConnectionAudioFrameCallback =
    void(MRS_CALL*)(void* user_data,
                    const void* audio_data,
                    const uint32_t bits_per_sample,
                    const uint32_t sample_rate,
                    const uint32_t number_of_channels,
                    const uint32_t number_of_frames);

/// Callback fired when a message is received on a data channel.
using mrsDataChannelMessageCallback = void(MRS_CALL*)(void* user_data,
                                                      const void* data,
                                                      const uint64_t size);

/// Callback fired when a data channel buffering changes.
/// The |previous| and |current| values are the old and new sizes in byte of the
/// buffering buffer. The |limit| is the capacity of the buffer.
/// Note that when the buffer is full, any attempt to send data will result is
/// an abrupt closing of the data channel. So monitoring this state is critical.
using mrsDataChannelBufferingCallback = void(MRS_CALL*)(void* user_data,
                                                        const uint64_t previous,
                                                        const uint64_t current,
                                                        const uint64_t limit);

/// Callback fired when the state of a data channel changed.
using mrsDataChannelStateCallback = void(MRS_CALL*)(void* user_data,
                                                    int32_t state,
                                                    int32_t id);

/// ICE transport type. See webrtc::PeerConnectionInterface::IceTransportsType.
/// Currently values are aligned, but kept as a separate structure to allow
/// backward compatilibity in case of changes in WebRTC.
enum class IceTransportType : int32_t {
  kNone = 0,
  kRelay = 1,
  kNoHost = 2,
  kAll = 3
};

/// Bundle policy. See webrtc::PeerConnectionInterface::BundlePolicy.
/// Currently values are aligned, but kept as a separate structure to allow
/// backward compatilibity in case of changes in WebRTC.
enum class BundlePolicy : int32_t {
  kBalanced = 0,
  kMaxBundle = 1,
  kMaxCompat = 2
};

/// SDP semantic (protocol dialect) for (re)negotiating a peer connection.
/// This cannot be changed after the connection is established.
enum class SdpSemantic : int32_t {
  /// Unified Plan - default and recommended. Standardized in WebRTC 1.0.
  kUnifiedPlan = 0,
  /// Plan B - deprecated and soon to be removed. Do not use unless for
  /// compability with an older implementation. This is non-standard.
  kPlanB = 1
};

/// Configuration to intialize a peer connection object.
struct PeerConnectionConfiguration {
  /// ICE servers, encoded as a single string buffer.
  /// See |EncodeIceServers| and |DecodeIceServers|.
  const char* encoded_ice_servers = nullptr;

  /// ICE transport type for the connection.
  IceTransportType ice_transport_type = IceTransportType::kAll;

  /// Bundle policy for the connection.
  BundlePolicy bundle_policy = BundlePolicy::kBalanced;

  /// SDP semantic for connection negotiation.
  /// Do not use Plan B unless there is a problem with Unified Plan.
  SdpSemantic sdp_semantic = SdpSemantic::kUnifiedPlan;
};

/// Create a peer connection and return a handle to it.
/// On UWP this must be invoked from another thread than the main UI thread.
MRS_API mrsResult MRS_CALL
mrsPeerConnectionCreate(PeerConnectionConfiguration config,
                        mrsPeerConnectionInteropHandle interop_handle,
                        PeerConnectionHandle* peerHandleOut) noexcept;

struct mrsPeerConnectionInteropCallbacks {
  /// Construct an interop object for a DataChannel instance.
  mrsPeerConnectionDataChannelCreateObjectCallback data_channel_create_object;
};

MRS_API mrsResult MRS_CALL mrsPeerConnectionRegisterInteropCallbacks(
    PeerConnectionHandle peerHandle,
    mrsPeerConnectionInteropCallbacks* callbacks) noexcept;

/// Register a callback fired once connected to a remote peer.
/// To unregister, simply pass nullptr as the callback pointer.
MRS_API void MRS_CALL mrsPeerConnectionRegisterConnectedCallback(
    PeerConnectionHandle peerHandle,
    PeerConnectionConnectedCallback callback,
    void* user_data) noexcept;

/// Register a callback fired when a local message is ready to be sent via the
/// signaling service to a remote peer.
MRS_API void MRS_CALL mrsPeerConnectionRegisterLocalSdpReadytoSendCallback(
    PeerConnectionHandle peerHandle,
    PeerConnectionLocalSdpReadytoSendCallback callback,
    void* user_data) noexcept;

/// Register a callback fired when an ICE candidate message is ready to be sent
/// via the signaling service to a remote peer.
MRS_API void MRS_CALL mrsPeerConnectionRegisterIceCandidateReadytoSendCallback(
    PeerConnectionHandle peerHandle,
    PeerConnectionIceCandidateReadytoSendCallback callback,
    void* user_data) noexcept;

/// Register a callback fired when the ICE connection state changes.
MRS_API void MRS_CALL mrsPeerConnectionRegisterIceStateChangedCallback(
    PeerConnectionHandle peerHandle,
    PeerConnectionIceStateChangedCallback callback,
    void* user_data) noexcept;

/// Register a callback fired when a renegotiation of the current session needs
/// to occur to account for new parameters (e.g. added or removed tracks).
MRS_API void MRS_CALL mrsPeerConnectionRegisterRenegotiationNeededCallback(
    PeerConnectionHandle peerHandle,
    PeerConnectionRenegotiationNeededCallback callback,
    void* user_data) noexcept;

/// Register a callback fired when a remote media track is added to the current
/// peer connection.
MRS_API void MRS_CALL mrsPeerConnectionRegisterTrackAddedCallback(
    PeerConnectionHandle peerHandle,
    PeerConnectionTrackAddedCallback callback,
    void* user_data) noexcept;

/// Register a callback fired when a remote media track is removed from the
/// current peer connection.
MRS_API void MRS_CALL mrsPeerConnectionRegisterTrackRemovedCallback(
    PeerConnectionHandle peerHandle,
    PeerConnectionTrackRemovedCallback callback,
    void* user_data) noexcept;

/// Register a callback fired when a remote data channel is removed from the
/// current peer connection.
MRS_API void MRS_CALL mrsPeerConnectionRegisterDataChannelAddedCallback(
    PeerConnectionHandle peerHandle,
    PeerConnectionDataChannelAddedCallback callback,
    void* user_data) noexcept;

/// Register a callback fired when a remote data channel is removed from the
/// current peer connection.
MRS_API void MRS_CALL mrsPeerConnectionRegisterDataChannelRemovedCallback(
    PeerConnectionHandle peerHandle,
    PeerConnectionDataChannelRemovedCallback callback,
    void* user_data) noexcept;

/// Register a callback fired when a video frame is available from a local video
/// track, usually from a local video capture device (local webcam).
MRS_API void MRS_CALL mrsPeerConnectionRegisterI420LocalVideoFrameCallback(
    PeerConnectionHandle peerHandle,
    PeerConnectionI420VideoFrameCallback callback,
    void* user_data) noexcept;

/// Register a callback fired when a video frame is available from a local video
/// track, usually from a local video capture device (local webcam).
MRS_API void MRS_CALL mrsPeerConnectionRegisterARGBLocalVideoFrameCallback(
    PeerConnectionHandle peerHandle,
    PeerConnectionARGBVideoFrameCallback callback,
    void* user_data) noexcept;

/// Register a callback fired when a video frame from a video track was received
/// from the remote peer.
MRS_API void MRS_CALL mrsPeerConnectionRegisterI420RemoteVideoFrameCallback(
    PeerConnectionHandle peerHandle,
    PeerConnectionI420VideoFrameCallback callback,
    void* user_data) noexcept;

/// Register a callback fired when a video frame from a video track was received
/// from the remote peer.
MRS_API void MRS_CALL mrsPeerConnectionRegisterARGBRemoteVideoFrameCallback(
    PeerConnectionHandle peerHandle,
    PeerConnectionARGBVideoFrameCallback callback,
    void* user_data) noexcept;

/// Kind of video profile. Equivalent to org::webRtc::VideoProfileKind.
enum class VideoProfileKind : int32_t {
  kUnspecified,
  kVideoRecording,
  kHighQualityPhoto,
  kBalancedVideoAndPhoto,
  kVideoConferencing,
  kPhotoSequence,
  kHighFrameRate,
  kVariablePhotoSequence,
  kHdrWithWcgVideo,
  kHdrWithWcgPhoto,
  kVideoHdr8,
};

/// Register a callback fired when an audio frame is available from a local
/// audio track, usually from a local audio capture device (local microphone).
///
/// -- WARNING --
/// Currently this callback is never fired, because the internal audio capture
/// device implementation ignores any registration and only delivers its audio
/// data to the internal WebRTC engine for sending to the remote peer.
MRS_API void MRS_CALL mrsPeerConnectionRegisterLocalAudioFrameCallback(
    PeerConnectionHandle peerHandle,
    PeerConnectionAudioFrameCallback callback,
    void* user_data) noexcept;

/// Register a callback fired when an audio frame from an audio track was
/// received from the remote peer.
MRS_API void MRS_CALL mrsPeerConnectionRegisterRemoteAudioFrameCallback(
    PeerConnectionHandle peerHandle,
    PeerConnectionAudioFrameCallback callback,
    void* user_data) noexcept;

/// Configuration for opening a local video capture device.
struct VideoDeviceConfiguration {
  /// Unique identifier of the video capture device to select, as returned by
  /// |mrsEnumVideoCaptureDevicesAsync|, or a null or empty string to select the
  /// default device.
  const char* video_device_id = nullptr;

  /// Optional name of a video profile, if the platform supports it, or null to
  /// no use video profiles.
  const char* video_profile_id = nullptr;

  /// Optional kind of video profile to select, if the platform supports it.
  /// If a video profile ID is specified with |video_profile_id| it is
  /// recommended to leave this as kUnspecified to avoid over-constraining the
  /// video capture format selection.
  VideoProfileKind video_profile_kind = VideoProfileKind::kUnspecified;

  /// Optional preferred capture resolution width, in pixels, or zero for
  /// unconstrained.
  uint32_t width = 0;

  /// Optional preferred capture resolution height, in pixels, or zero for
  /// unconstrained.
  uint32_t height = 0;

  /// Optional preferred capture framerate, in frame per second (FPS), or zero
  /// for unconstrained.
  /// This framerate is compared exactly to the one reported by the video
  /// capture device (webcam), so should be queried rather than hard-coded to
  /// avoid mismatches with video formats reporting e.g. 29.99 instead of 30.0.
  double framerate = 0;

  /// On platforms supporting Mixed Reality Capture (MRC) like HoloLens, enable
  /// this feature. This produces a video track where the holograms rendering is
  /// overlaid over the webcam frame. This parameter is ignored on platforms not
  /// supporting MRC.
  /// Note that MRC is only available in exclusive-mode applications, or in
  /// shared apps with the restricted capability "rescap:screenDuplication". In
  /// any other case the capability will not be granted and MRC will silently
  /// fail, falling back to a simple webcam video feed without holograms.
  bool enable_mrc = true;
};

/// Add a local video track from a local video capture device (webcam) to
/// the collection of tracks to send to the remote peer.
/// |video_device_id| specifies the unique identifier of a video capture
/// device to open, as obtained by enumerating devices with
/// mrsEnumVideoCaptureDevicesAsync(), or null for any device.
/// |enable_mrc| allows enabling Mixed Reality Capture on HoloLens devices, and
/// is otherwise ignored for other video capture devices. On UWP this must be
/// invoked from another thread than the main UI thread.
MRS_API mrsResult MRS_CALL
mrsPeerConnectionAddLocalVideoTrack(PeerConnectionHandle peerHandle,
                                    VideoDeviceConfiguration config) noexcept;

/// Add a local audio track from a local audio capture device (microphone) to
/// the collection of tracks to send to the remote peer.
MRS_API mrsResult MRS_CALL
mrsPeerConnectionAddLocalAudioTrack(PeerConnectionHandle peerHandle) noexcept;

enum class mrsDataChannelConfigFlags : uint32_t {
  kOrdered = 0x1,
  kReliable = 0x2,
};

inline mrsDataChannelConfigFlags operator|(mrsDataChannelConfigFlags a,
                                           mrsDataChannelConfigFlags b) {
  return (mrsDataChannelConfigFlags)((uint32_t)a | (uint32_t)b);
}

inline uint32_t operator&(mrsDataChannelConfigFlags a,
                          mrsDataChannelConfigFlags b) {
  return ((uint32_t)a | (uint32_t)b);
}

struct mrsDataChannelConfig {
  int32_t id = -1;      // -1 for auto; >=0 for negotiated
  const char* label{};  // optional; can be null or empty string
  mrsDataChannelConfigFlags flags{};
};

struct mrsDataChannelCallbacks {
  mrsDataChannelMessageCallback message_callback{};
  void* message_user_data{};
  mrsDataChannelBufferingCallback buffering_callback{};
  void* buffering_user_data{};
  mrsDataChannelStateCallback state_callback{};
  void* state_user_data{};
};

/// Add a new data channel.
/// This function has two distinct uses:
/// - If id < 0, then it adds a new in-band data channel with an ID that will be
/// selected by the WebRTC implementation itself, and will be available later.
/// In that case the channel is announced to the remote peer for it to create a
/// channel with the same ID.
/// - If id >= 0, then it adds a new out-of-band negotiated channel with the
/// given ID, and it is the responsibility of the app to create a channel with
/// the same ID on the remote peer to be able to use the channel.
MRS_API mrsResult MRS_CALL mrsPeerConnectionAddDataChannel(
    PeerConnectionHandle peerHandle,
    mrsDataChannelInteropHandle dataChannelInteropHandle,
    mrsDataChannelConfig config,
    mrsDataChannelCallbacks callbacks,
    DataChannelHandle* dataChannelHandleOut) noexcept;

MRS_API void MRS_CALL mrsPeerConnectionRemoveLocalVideoTrack(
    PeerConnectionHandle peerHandle) noexcept;

MRS_API void MRS_CALL mrsPeerConnectionRemoveLocalAudioTrack(
    PeerConnectionHandle peerHandle) noexcept;

MRS_API mrsResult MRS_CALL mrsPeerConnectionRemoveDataChannel(
    PeerConnectionHandle peerHandle,
    DataChannelHandle dataChannelHandle) noexcept;

MRS_API mrsResult MRS_CALL
mrsPeerConnectionSetLocalVideoTrackEnabled(PeerConnectionHandle peerHandle,
                                           int32_t enabled) noexcept;

MRS_API int32_t MRS_CALL mrsPeerConnectionIsLocalVideoTrackEnabled(
    PeerConnectionHandle peerHandle) noexcept;

MRS_API mrsResult MRS_CALL
mrsPeerConnectionSetLocalAudioTrackEnabled(PeerConnectionHandle peerHandle,
                                           int32_t enabled) noexcept;

MRS_API int32_t MRS_CALL mrsPeerConnectionIsLocalAudioTrackEnabled(
    PeerConnectionHandle peerHandle) noexcept;

MRS_API mrsResult MRS_CALL
mrsDataChannelSendMessage(DataChannelHandle dataChannelHandle,
                          const void* data,
                          uint64_t size) noexcept;

/// Add a new ICE candidate received from a signaling service.
MRS_API mrsResult MRS_CALL
mrsPeerConnectionAddIceCandidate(PeerConnectionHandle peerHandle,
                                 const char* sdp_mid,
                                 const int sdp_mline_index,
                                 const char* candidate) noexcept;

/// Create a new JSEP offer to try to establish a connection with a remote peer.
/// This will generate a local offer message, then fire the
/// "LocalSdpReadytoSendCallback" callback, which should send this message via
/// the signaling service to a remote peer.
MRS_API mrsResult MRS_CALL
mrsPeerConnectionCreateOffer(PeerConnectionHandle peerHandle) noexcept;

/// Create a new JSEP answer to a received offer to try to establish a
/// connection with a remote peer. This will generate a local answer message,
/// then fire the "LocalSdpReadytoSendCallback" callback, which should send this
/// message via the signaling service to a remote peer.
MRS_API mrsResult MRS_CALL
mrsPeerConnectionCreateAnswer(PeerConnectionHandle peerHandle) noexcept;

/// Set a remote description received from a remote peer via the signaling
/// service.
MRS_API mrsResult MRS_CALL
mrsPeerConnectionSetRemoteDescription(PeerConnectionHandle peerHandle,
                                      const char* type,
                                      const char* sdp) noexcept;

/// Close a peer connection and free all resources associated with it.
MRS_API void MRS_CALL
mrsPeerConnectionClose(PeerConnectionHandle* peerHandle) noexcept;

//
// SDP utilities
//

/// Codec arguments for SDP filtering, to allow selecting a preferred codec and
/// overriding some of its parameters.
struct SdpFilter {
  /// SDP name of a preferred codec, which is to be retained alone if present in
  /// the SDP offer message, discarding all others.
  const char* codec_name = nullptr;

  /// Semicolon-separated list of "key=value" pairs of codec parameters to pass
  /// to the codec. Arguments are passed as is without validation of their name
  /// nor value.
  const char* params = nullptr;
};

/// Force audio and video codecs when advertizing capabilities in an SDP offer.#
///
/// This is a workaround for the lack of access to codec selection. Instead of
/// selecting codecs in code, this can be used to intercept a generated SDP
/// offer before it is sent to the remote peer, and modify it by removing the
/// codecs the user does not want.
///
/// Codec names are compared to the list of supported codecs in the input
/// message string, and if found then other codecs are pruned out. If the codec
/// name is not found, the codec is assumed to be unsupported, so codecs for
/// that type are not modified.
///
/// On return the SDP offer message string to be sent via the signaler is stored
/// into the output buffer pointed to by |buffer|.
///
/// Note that because this function always return a message shorter or equal to
/// the input message, one way to ensure this function doesn't fail is to pass
/// an output buffer as large as the input message.
///
/// |message| SDP message string to deserialize.
/// |audio_codec_name| Optional SDP name of the audio codec to
/// force if supported, or nullptr or empty string to leave unmodified.
/// |video_codec_name| Optional SDP name of the video codec to force if
/// supported, or nullptr or empty string to leave unmodified.
/// |buffer| Output buffer of capacity *|buffer_size|.
/// |buffer_size| Pointer to the buffer capacity on input, modified on output
/// with the actual size of the null-terminated string, including the null
/// terminator, so the size of the used part of the buffer, in bytes.
/// Returns true on success or false if the buffer is not large enough to
/// contain the new SDP message.
MRS_API mrsResult MRS_CALL mrsSdpForceCodecs(const char* message,
                                             SdpFilter audio_filter,
                                             SdpFilter video_filter,
                                             char* buffer,
                                             uint64_t* buffer_size);

//
// Generic utilities
//

/// Optimized helper to copy a contiguous block of memory.
/// This is equivalent to the standard malloc() function.
MRS_API void MRS_CALL mrsMemCpy(void* dst, const void* src, uint64_t size);

/// Optimized helper to copy a block of memory with source and destination
/// stride.
MRS_API void MRS_CALL mrsMemCpyStride(void* dst,
                                      int32_t dst_stride,
                                      const void* src,
                                      int32_t src_stride,
                                      int32_t elem_size,
                                      int32_t elem_count);
}
