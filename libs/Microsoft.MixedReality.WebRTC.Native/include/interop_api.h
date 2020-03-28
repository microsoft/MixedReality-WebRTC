// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include "audio_frame.h"
#include "export.h"
#include "result.h"
#include "video_frame.h"

extern "C" {

/// 32-bit boolean for interop API.
enum class mrsBool : int32_t { kTrue = -1, kFalse = 0 };

//
// Generic utilities
//

/// Report live objects to debug output, and return the number of live objects.
MRS_API uint32_t MRS_CALL mrsReportLiveObjects() noexcept;

/// Global MixedReality-WebRTC library shutdown options.
enum class mrsShutdownOptions : uint32_t {
  kNone = 0,

  /// Log some report about live objects when trying to shutdown, to help
  /// debugging. This flag is set by default.
  kLogLiveObjects = 0x1,

  /// When forcing shutdown, either because |mrsForceShutdown()| is called or
  /// because the program terminates, and some objects are still alive, attempt
  /// to break into the debugger. This is not available for all platforms.
  kDebugBreakOnForceShutdown = 0x2,

  /// Default flags value.
  kDefault = kLogLiveObjects
};

/// Get options for the automatic shutdown of the MixedReality-WebRTC library.
/// This enables controlling the behavior of the library when it is shut down as
/// a result of all tracked objects being released, or when the program
/// terminates.
MRS_API mrsShutdownOptions MRS_CALL mrsGetShutdownOptions() noexcept;

/// Set options for the automatic shutdown of the MixedReality-WebRTC library.
/// This enables controlling the behavior of the library when it is shut down as
/// a result of all tracked objects being released, or when the program
/// terminates.
MRS_API void MRS_CALL
mrsSetShutdownOptions(mrsShutdownOptions options) noexcept;

/// Forcefully shutdown the library and release all resources (as possible), and
/// terminate the WebRTC threads to allow the shared module to be unloaded. This
/// is a last-resort measure for exceptional situations like unit testing where
/// loss of data is acceptable.
MRS_API void MRS_CALL mrsForceShutdown() noexcept;

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

/// Opaque handle to the interop wrapper of a local video track.
using mrsLocalVideoTrackInteropHandle = void*;

/// Opaque handle to the interop wrapper of a data channel.
using mrsDataChannelInteropHandle = void*;

/// Callback to create an interop wrapper for a data channel.
///
/// The |config| parameter is passed by value to facilitate interop with C#, as
/// the struct contains a string which would otherwise not be marshaled
/// correctly.
///
/// The |callbacks| struct is filled by the callee with callbacks to register.
using mrsDataChannelCreateObjectCallback = mrsDataChannelInteropHandle(
    MRS_CALL*)(mrsPeerConnectionInteropHandle parent,
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
/// On UWP this must *not* be called from the main UI thread, otherwise a
/// |mrsResult::kWrongThread| error might be returned.
MRS_API mrsResult MRS_CALL mrsEnumVideoCaptureDevicesAsync(
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
/// On UWP this must *not* be called from the main UI thread, otherwise a
/// |mrsResult::kWrongThread| error might be returned.
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

/// Opaque handle to a native LocalVideoTrack C++ object.
using LocalVideoTrackHandle = void*;

/// Opaque handle to a native DataChannel C++ object.
using DataChannelHandle = void*;

/// Opaque handle to a native ExternalVideoTrackSource C++ object.
using ExternalVideoTrackSourceHandle = void*;

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
enum class IceConnectionState : int32_t {
  kNew = 0,
  kChecking = 1,
  kConnected = 2,
  kCompleted = 3,
  kFailed = 4,
  kDisconnected = 5,
  kClosed = 6,
};

/// State of the ICE gathering process.
/// See https://www.w3.org/TR/webrtc/#rtcicegatheringstate-enum
enum class IceGatheringState : int32_t {
  kNew = 0,
  kGathering = 1,
  kComplete = 2,
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

using mrsI420AVideoFrame = Microsoft::MixedReality::WebRTC::I420AVideoFrame;

/// Callback fired when a local or remote (depending on use) video frame is
/// available to be consumed by the caller, usually for display.
/// The video frame is encoded in I420 triplanar format (NV12).
using mrsI420AVideoFrameCallback =
    void(MRS_CALL*)(void* user_data, const mrsI420AVideoFrame& frame);

using mrsArgb32VideoFrame = Microsoft::MixedReality::WebRTC::Argb32VideoFrame;

/// Callback fired when a local or remote (depending on use) video frame is
/// available to be consumed by the caller, usually for display.
/// The video frame is encoded in ARGB 32-bit per pixel.
using mrsArgb32VideoFrameCallback =
    void(MRS_CALL*)(void* user_data, const mrsArgb32VideoFrame& frame);

using mrsAudioFrame = Microsoft::MixedReality::WebRTC::AudioFrame;

/// Callback fired when a local or remote (depending on use) audio frame is
/// available to be consumed by the caller, usually for local output.
using PeerConnectionAudioFrameCallback =
    void(MRS_CALL*)(void* user_data, const mrsAudioFrame& frame);

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
/// The newly-created peer connection native resource is reference-counted, and
/// has a single reference when this function returns. Additional references may
/// be added with |mrsPeerConnectionAddRef| and removed with
/// |mrsPeerConnectionRemoveRef|. When the last reference is removed, the native
/// object is destroyed.
MRS_API mrsResult MRS_CALL
mrsPeerConnectionCreate(PeerConnectionConfiguration config,
                        mrsPeerConnectionInteropHandle interop_handle,
                        PeerConnectionHandle* peerHandleOut) noexcept;

struct mrsPeerConnectionInteropCallbacks {
  /// Construct an interop object for a DataChannel instance.
  mrsDataChannelCreateObjectCallback data_channel_create_object{};
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

/// Register a callback fired when a video frame from a video track was received
/// from the remote peer.
MRS_API void MRS_CALL mrsPeerConnectionRegisterI420ARemoteVideoFrameCallback(
    PeerConnectionHandle peerHandle,
    mrsI420AVideoFrameCallback callback,
    void* user_data) noexcept;

/// Register a callback fired when a video frame from a video track was received
/// from the remote peer.
MRS_API void MRS_CALL mrsPeerConnectionRegisterArgb32RemoteVideoFrameCallback(
    PeerConnectionHandle peerHandle,
    mrsArgb32VideoFrameCallback callback,
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

/// Configuration for opening a local video capture device and creating a local
/// video track.
struct LocalVideoTrackInitConfig {
  /// Handle of the local video track interop wrapper, if any, which will be
  /// associated with the native local video track object.
  mrsLocalVideoTrackInteropHandle track_interop_handle{};

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
  mrsBool enable_mrc = mrsBool::kTrue;

  /// When Mixed Reality Capture is enabled, enable or disable the recording
  /// indicator shown on screen.
  mrsBool enable_mrc_recording_indicator = mrsBool::kTrue;
};

/// Add a local video track from a local video capture device (webcam) to
/// the collection of tracks to send to the remote peer.
/// |video_device_id| specifies the unique identifier of a video capture
/// device to open, as obtained by enumerating devices with
/// mrsEnumVideoCaptureDevicesAsync(), or null for any device.
/// |enable_mrc| allows enabling Mixed Reality Capture on HoloLens devices, and
/// is otherwise ignored for other video capture devices. On UWP this must be
/// invoked from another thread than the main UI thread.
MRS_API mrsResult MRS_CALL mrsPeerConnectionAddLocalVideoTrack(
    PeerConnectionHandle peerHandle,
    const char* track_name,
    const LocalVideoTrackInitConfig* config,
    LocalVideoTrackHandle* trackHandle) noexcept;

using mrsRequestExternalI420AVideoFrameCallback =
    mrsResult(MRS_CALL*)(void* user_data,
                         ExternalVideoTrackSourceHandle source_handle,
                         uint32_t request_id,
                         int64_t timestamp_ms);

using mrsRequestExternalArgb32VideoFrameCallback =
    mrsResult(MRS_CALL*)(void* user_data,
                         ExternalVideoTrackSourceHandle source_handle,
                         uint32_t request_id,
                         int64_t timestamp_ms);

/// Configuration for creating a local video track from an external source.
struct LocalVideoTrackFromExternalSourceInitConfig {
  /// Handle of the local video track interop wrapper, if any, which will be
  /// associated with the native local video track object.
  mrsLocalVideoTrackInteropHandle track_interop_handle{};
};

/// Add a local video track from a custom video source external to the
/// implementation. This allows feeding into WebRTC frames from any source,
/// including generated or synthetic frames, for example for testing.
/// The track source initially starts as capuring. Capture can be stopped with
/// |mrsExternalVideoTrackSourceShutdown|.
/// This returns a handle to a newly allocated object, which must be released
/// once not used anymore with |mrsLocalVideoTrackRemoveRef()|.
MRS_API mrsResult MRS_CALL
mrsPeerConnectionAddLocalVideoTrackFromExternalSource(
    PeerConnectionHandle peerHandle,
    const char* track_name,
    ExternalVideoTrackSourceHandle source_handle,
    const LocalVideoTrackFromExternalSourceInitConfig* config,
    LocalVideoTrackHandle* track_handle) noexcept;

/// Remove a local video track from the given peer connection and destroy it.
/// After this call returned, the video track handle is invalid.
MRS_API mrsResult MRS_CALL mrsPeerConnectionRemoveLocalVideoTrack(
    PeerConnectionHandle peer_handle,
    LocalVideoTrackHandle track_handle) noexcept;

/// Remove all local video tracks backed by the given video track source from
/// the given peer connection and destroy the video track source.
/// After this call returned, the video track source handle is invalid.
MRS_API mrsResult MRS_CALL mrsPeerConnectionRemoveLocalVideoTracksFromSource(
    PeerConnectionHandle peer_handle,
    ExternalVideoTrackSourceHandle source_handle) noexcept;

/// Add a local audio track from a local audio capture device (microphone) to
/// the collection of tracks to send to the remote peer.
MRS_API mrsResult MRS_CALL
mrsPeerConnectionAddLocalAudioTrack(PeerConnectionHandle peerHandle) noexcept;

enum class mrsDataChannelConfigFlags : uint32_t {
  kOrdered = 0x1,
  kReliable = 0x2,
};

inline mrsDataChannelConfigFlags operator|(
    mrsDataChannelConfigFlags a,
    mrsDataChannelConfigFlags b) noexcept {
  return (mrsDataChannelConfigFlags)((uint32_t)a | (uint32_t)b);
}

inline uint32_t operator&(mrsDataChannelConfigFlags a,
                          mrsDataChannelConfigFlags b) noexcept {
  return ((uint32_t)a | (uint32_t)b);
}

struct mrsDataChannelConfig {
  int32_t id = -1;  // -1 for auto; >=0 for negotiated
  mrsDataChannelConfigFlags flags{};
  const char* label{};  // optional; can be null or empty string
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

MRS_API mrsResult MRS_CALL mrsPeerConnectionRemoveLocalVideoTrack(
    PeerConnectionHandle peerHandle,
    LocalVideoTrackHandle trackHandle) noexcept;

MRS_API void MRS_CALL mrsPeerConnectionRemoveLocalAudioTrack(
    PeerConnectionHandle peerHandle) noexcept;

MRS_API mrsResult MRS_CALL mrsPeerConnectionRemoveDataChannel(
    PeerConnectionHandle peerHandle,
    DataChannelHandle dataChannelHandle) noexcept;

MRS_API mrsResult MRS_CALL
mrsPeerConnectionSetLocalAudioTrackEnabled(PeerConnectionHandle peerHandle,
                                           mrsBool enabled) noexcept;

MRS_API mrsBool MRS_CALL mrsPeerConnectionIsLocalAudioTrackEnabled(
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

/// Set the bitrate allocated to all RTP streams sent by this connection.
/// Other limitations might affect these limits and are respected (for example
/// "b=AS" in SDP).
///
/// Setting |start_bitrate_bps| will reset the current bitrate estimate to the
/// provided value.
///
/// The values are in bits per second.
/// If any of the arguments has a negative value, it will be ignored.
MRS_API mrsResult MRS_CALL
mrsPeerConnectionSetBitrate(PeerConnectionHandle peer_handle,
                            int min_bitrate_bps,
                            int start_bitrate_bps,
                            int max_bitrate_bps) noexcept;

/// Parameter-less callback.
using ActionCallback = void(MRS_CALL*)(void* user_data);

/// Set a remote description received from a remote peer via the signaling
/// service. Once the remote description is applied, the action callback is
/// invoked to signal the caller it is safe to continue the negotiation, and in
/// particular it is safe to call |CreateAnswer()|.
MRS_API mrsResult MRS_CALL
mrsPeerConnectionSetRemoteDescriptionAsync(PeerConnectionHandle peerHandle,
                                           const char* type,
                                           const char* sdp,
                                           ActionCallback callback,
                                           void* user_data) noexcept;

/// Close a peer connection, removing all tracks and disconnecting from the
/// remote peer currently connected. This does not invalidate the handle nor
/// destroy the native peer connection object, but leaves it in a state where it
/// can only be destroyed.
MRS_API mrsResult MRS_CALL
mrsPeerConnectionClose(PeerConnectionHandle peerHandle) noexcept;

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
                                             uint64_t* buffer_size) noexcept;

/// Must be the same as PeerConnection::FrameHeightRoundMode.
enum class FrameHeightRoundMode : int32_t { kNone = 0, kCrop = 1, kPad = 2 };

/// Check if the given SDP token is valid according to the RFC 4566 standard.
/// See https://tools.ietf.org/html/rfc4566#page-43 for details.
MRS_API mrsBool MRS_CALL mrsSdpIsValidToken(const char* token) noexcept;

/// See PeerConnection::SetFrameHeightRoundMode.
MRS_API void MRS_CALL mrsSetFrameHeightRoundMode(FrameHeightRoundMode value);

//
// Generic utilities
//

/// Optimized helper to copy a contiguous block of memory.
/// This is equivalent to the standard malloc() function.
MRS_API void MRS_CALL mrsMemCpy(void* dst,
                                const void* src,
                                uint64_t size) noexcept;

/// Optimized helper to copy a block of memory with source and destination
/// stride.
MRS_API void MRS_CALL mrsMemCpyStride(void* dst,
                                      int32_t dst_stride,
                                      const void* src,
                                      int32_t src_stride,
                                      int32_t elem_size,
                                      int32_t elem_count) noexcept;
//
// Stats extraction.
//

/// Subset of RTCDataChannelStats. See
/// https://www.w3.org/TR/webrtc-stats/#dcstats-dict*
struct mrsDataChannelStats {
  int64_t timestamp_us;
  int64_t data_channel_identifier;
  uint32_t messages_sent;
  uint64_t bytes_sent;
  uint32_t messages_received;
  uint64_t bytes_received;
};

/// Subset of RTCMediaStreamTrack (audio sender) and RTCOutboundRTPStreamStats.
/// See https://www.w3.org/TR/webrtc-stats/#raststats-dict* and
/// https://www.w3.org/TR/webrtc-stats/#sentrtpstats-dict*
struct mrsAudioSenderStats {
  int64_t track_stats_timestamp_us;
  const char* track_identifier;
  double audio_level;
  double total_audio_energy;
  double total_samples_duration;

  int64_t rtp_stats_timestamp_us;
  uint32_t packets_sent;
  uint64_t bytes_sent;
};

/// Subset of RTCMediaStreamTrack (audio receiver) and RTCInboundRTPStreamStats.
/// See https://www.w3.org/TR/webrtc-stats/#aststats-dict* and
/// https://www.w3.org/TR/webrtc-stats/#inboundrtpstats-dict*
struct mrsAudioReceiverStats {
  int64_t track_stats_timestamp_us;
  const char* track_identifier;
  double audio_level;
  double total_audio_energy;
  uint64_t total_samples_received;
  double total_samples_duration;

  int64_t rtp_stats_timestamp_us;
  uint32_t packets_received;
  uint64_t bytes_received;
};

/// Subset of RTCMediaStreamTrack (video sender) and RTCOutboundRTPStreamStats.
/// See https://www.w3.org/TR/webrtc-stats/#vsstats-dict* and
/// https://www.w3.org/TR/webrtc-stats/#sentrtpstats-dict*
struct mrsVideoSenderStats {
  int64_t track_stats_timestamp_us;
  const char* track_identifier;
  uint32_t frames_sent;
  uint32_t huge_frames_sent;

  int64_t rtp_stats_timestamp_us;
  uint32_t packets_sent;
  uint64_t bytes_sent;
  uint32_t frames_encoded;
};

/// Subset of RTCMediaStreamTrack (video receiver) + RTCInboundRTPStreamStats.
/// See https://www.w3.org/TR/webrtc-stats/#rvststats-dict* and
/// https://www.w3.org/TR/webrtc-stats/#inboundrtpstats-dict*
struct mrsVideoReceiverStats {
  int64_t track_stats_timestamp_us;
  const char* track_identifier;
  uint32_t frames_received;
  uint32_t frames_dropped;

  int64_t rtp_stats_timestamp_us;
  uint32_t packets_received;
  uint64_t bytes_received;
  uint32_t frames_decoded;
};

/// Subset of RTCTransportStats. See
/// https://www.w3.org/TR/webrtc-stats/#transportstats-dict*
struct mrsTransportStats {
  int64_t timestamp_us;
  uint64_t bytes_sent;
  uint64_t bytes_received;
};

/// Handle to a WebRTC stats report.
using mrsStatsReportHandle = const void*;

/// Called by mrsPeerConnectionGetSimpleStats when a stats report is ready.
using PeerConnectionGetSimpleStatsCallback =
    void(MRS_CALL*)(void* user_data, mrsStatsReportHandle stats_report);

/// Called by mrsStatsReportGetObjects for every instance of the requested stats
/// type.
using mrsStatsReportGetObjectCallback =
    void(MRS_CALL*)(void* user_data, const void* stats_object);

/// Get a stats report for the connection.
/// The report passed to the callback must be released when finished through
/// mrsStatsReportRemoveRef.
MRS_API mrsResult MRS_CALL
mrsPeerConnectionGetSimpleStats(PeerConnectionHandle peer_handle,
                                PeerConnectionGetSimpleStatsCallback callback,
                                void* user_data);

/// Get all the instances of the requested stats type.
/// The type must be one of "DataChannelStats", "AudioSenderStats",
/// "AudioReceiverStats", "VideoSenderStats", "VideoReceiverStats",
/// "TransportStats".
MRS_API mrsResult MRS_CALL
mrsStatsReportGetObjects(mrsStatsReportHandle report_handle,
                         const char* stats_type,
                         mrsStatsReportGetObjectCallback callback,
                         void* user_data);

/// Release a stats report.
MRS_API mrsResult MRS_CALL
mrsStatsReportRemoveRef(mrsStatsReportHandle stats_report);
}  // extern "C"
