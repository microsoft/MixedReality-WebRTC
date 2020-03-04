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

struct mrsTransceiverWrapperInitConfig;
struct mrsRemoteAudioTrackConfig;
struct mrsRemoteVideoTrackConfig;
struct mrsDataChannelConfig;
struct mrsDataChannelCallbacks;

// Handles to interop objects (internal implementation)

/// Opaque handle to a native PeerConnection interop object.
using mrsPeerConnectionHandle = void*;

/// Opaque handle to a native MediaTrack interop object.
using mrsMediaTrackHandle = void*;

/// Opaque handle to a native Transceiver interop object.
using mrsTransceiverHandle = void*;

/// Opaque handle to a native LocalAudioTrack interop object.
using mrsLocalAudioTrackHandle = void*;

/// Opaque handle to a native LocalVideoTrack interop object.
using mrsLocalVideoTrackHandle = void*;

/// Opaque handle to a native RemoteAudioTrack interop object.
using mrsRemoteAudioTrackHandle = void*;

/// Opaque handle to a native RemoteVideoTrack interop object.
using mrsRemoteVideoTrackHandle = void*;

/// Opaque handle to a native DataChannel interop object.
using mrsDataChannelHandle = void*;

/// Opaque handle to a native ExternalVideoTrackSource interop object.
using mrsExternalVideoTrackSourceHandle = void*;

// Handles to wrapper objects

/// Opaque handle to the interop wrapper of a peer connection.
using mrsPeerConnectionInteropHandle = void*;

/// Opaque handle to the interop wrapper of a transceiver.
using mrsTransceiverInteropHandle = void*;

/// Opaque handle to the interop wrapper of a local audio track.
using mrsLocalAudioTrackInteropHandle = void*;

/// Opaque handle to the interop wrapper of a local video track.
using mrsLocalVideoTrackInteropHandle = void*;

/// Opaque handle to the interop wrapper of a remote audio track.
using mrsRemoteAudioTrackInteropHandle = void*;

/// Opaque handle to the interop wrapper of a remote video track.
using mrsRemoteVideoTrackInteropHandle = void*;

/// Opaque handle to the interop wrapper of a data channel.
using mrsDataChannelInteropHandle = void*;

/// Callback to create an interop wrapper for a transceiver.
/// The callback must return the handle of the created interop wrapper.
using mrsTransceiverCreateObjectCallback = mrsTransceiverInteropHandle(
    MRS_CALL*)(mrsPeerConnectionInteropHandle parent,
               const mrsTransceiverWrapperInitConfig& config) noexcept;

/// Callback to finish the creation of the interop wrapper by assigning to it
/// the handle of the Transceiver native object it wraps.
/// This is called shortly after |mrsTransceiverCreateObjectCallback|, with
/// the same |mrsTransceiverInteropHandle| returned by that callback.
using mrsTransceiverFinishCreateCallback =
    void(MRS_CALL*)(mrsTransceiverInteropHandle, mrsTransceiverHandle);

/// Callback to create an interop wrapper for a remote audio track.
using mrsRemoteAudioTrackCreateObjectCallback =
    mrsRemoteAudioTrackInteropHandle(MRS_CALL*)(
        mrsPeerConnectionInteropHandle parent,
        const mrsRemoteAudioTrackConfig& config) noexcept;

/// Callback to create an interop wrapper for a remote video track.
using mrsRemoteVideoTrackCreateObjectCallback =
    mrsRemoteVideoTrackInteropHandle(MRS_CALL*)(
        mrsPeerConnectionInteropHandle parent,
        const mrsRemoteVideoTrackConfig& config) noexcept;

/// Callback to create an interop wrapper for a data channel.
using mrsDataChannelCreateObjectCallback = mrsDataChannelInteropHandle(
    MRS_CALL*)(mrsPeerConnectionInteropHandle parent,
               const mrsDataChannelConfig& config,
               mrsDataChannelCallbacks* callbacks) noexcept;

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

/// Callback fired when the peer connection is connected, that is it finished
/// the JSEP offer/answer exchange successfully.
using mrsPeerConnectionConnectedCallback = void(MRS_CALL*)(void* user_data);

/// Callback fired when a local SDP message has been prepared and is ready to be
/// sent by the user via the signaling service.
using mrsPeerConnectionLocalSdpReadytoSendCallback =
    void(MRS_CALL*)(void* user_data, const char* type, const char* sdp_data);

/// Callback fired when an ICE candidate has been prepared and is ready to be
/// sent by the user via the signaling service.
using mrsPeerConnectionIceCandidateReadytoSendCallback =
    void(MRS_CALL*)(void* user_data,
                    const char* candidate,
                    int sdpMlineindex,
                    const char* sdpMid);

/// State of the ICE connection.
/// See https://www.w3.org/TR/webrtc/#rtciceconnectionstate-enum.
/// Note that there is a mismatch currently due to the m71 implementation.
enum class mrsIceConnectionState : int32_t {
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
enum class mrsIceGatheringState : int32_t {
  kNew = 0,
  kGathering = 1,
  kComplete = 2,
};

/// Callback fired when the state of the ICE connection changed.
using mrsPeerConnectionIceStateChangedCallback =
    void(MRS_CALL*)(void* user_data, mrsIceConnectionState new_state);

/// Callback fired when a renegotiation of the current session needs to occur to
/// account for new parameters (e.g. added or removed tracks).
using mrsPeerConnectionRenegotiationNeededCallback =
    void(MRS_CALL*)(void* user_data);

/// Kind of media track. Equivalent to
/// webrtc::MediaStreamTrackInterface::kind().
enum class mrsTrackKind : uint32_t {
  kUnknownTrack = 0,
  kAudioTrack = 1,
  kVideoTrack = 2,
  kDataTrack = 3,
};

/// Callback fired when a remote audio track is added to a connection.
/// The |audio_track| and |audio_transceiver| handle hold a reference to the
/// underlying native object they are associated with, and therefore must be
/// released with |mrsLocalAudioTrackRemoveRef()| and
/// |mrsTransceiverRemoveRef()|, respectively, to avoid memory leaks.
using mrsPeerConnectionAudioTrackAddedCallback =
    void(MRS_CALL*)(mrsPeerConnectionInteropHandle peer,
                    mrsRemoteAudioTrackInteropHandle audio_track_wrapper,
                    mrsRemoteAudioTrackHandle audio_track,
                    mrsTransceiverInteropHandle transceiver_wrapper,
                    mrsTransceiverHandle transceiver);

/// Callback fired when a remote audio track is removed from a connection.
/// The |audio_track| and |audio_transceiver| handle hold a reference to the
/// underlying native object they are associated with, and therefore must be
/// released with |mrsLocalAudioTrackRemoveRef()| and
/// |mrsTransceiverRemoveRef()|, respectively, to avoid memory leaks.
using mrsPeerConnectionAudioTrackRemovedCallback =
    void(MRS_CALL*)(mrsPeerConnectionInteropHandle peer,
                    mrsRemoteAudioTrackInteropHandle audio_track_wrapper,
                    mrsRemoteAudioTrackHandle audio_track,
                    mrsTransceiverInteropHandle transceiver_wrapper,
                    mrsTransceiverHandle transceiver);

/// Callback fired when a remote video track is added to a connection.
/// The |video_track| and |video_transceiver| handle hold a reference to the
/// underlying native object they are associated with, and therefore must be
/// released with |mrsLocalVideoTrackRemoveRef()| and
/// |mrsTransceiverRemoveRef()|, respectively, to avoid memory leaks.
using mrsPeerConnectionVideoTrackAddedCallback =
    void(MRS_CALL*)(mrsPeerConnectionInteropHandle peer,
                    mrsRemoteVideoTrackInteropHandle video_track_wrapper,
                    mrsRemoteVideoTrackHandle video_track,
                    mrsTransceiverInteropHandle transceiver_wrapper,
                    mrsTransceiverHandle transceiver);

/// Callback fired when a remote video track is removed from a connection.
/// The |video_track| and |video_transceiver| handle hold a reference to the
/// underlying native object they are associated with, and therefore must be
/// released with |mrsLocalVideoTrackRemoveRef()| and
/// |mrsTransceiverRemoveRef()|, respectively, to avoid memory leaks.
using mrsPeerConnectionVideoTrackRemovedCallback =
    void(MRS_CALL*)(mrsPeerConnectionInteropHandle peer,
                    mrsRemoteVideoTrackInteropHandle video_track_wrapper,
                    mrsRemoteVideoTrackHandle video_track,
                    mrsTransceiverInteropHandle transceiver_wrapper,
                    mrsTransceiverHandle transceiver);

/// Callback fired when a data channel is added to the peer connection after
/// being negotiated with the remote peer.
using mrsPeerConnectionDataChannelAddedCallback =
    void(MRS_CALL*)(mrsPeerConnectionInteropHandle peer,
                    mrsDataChannelInteropHandle data_channel_wrapper,
                    mrsDataChannelHandle data_channel);

/// Callback fired when a data channel is remoted from the peer connection.
using mrsPeerConnectionDataChannelRemovedCallback =
    void(MRS_CALL*)(mrsPeerConnectionInteropHandle peer,
                    mrsDataChannelInteropHandle data_channel_wrapper,
                    mrsDataChannelHandle data_channel);

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
using mrsAudioFrameCallback = void(MRS_CALL*)(void* user_data,
                                              const mrsAudioFrame& frame);

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
enum class mrsIceTransportType : int32_t {
  kNone = 0,
  kRelay = 1,
  kNoHost = 2,
  kAll = 3
};

/// Bundle policy. See webrtc::PeerConnectionInterface::BundlePolicy.
/// Currently values are aligned, but kept as a separate structure to allow
/// backward compatilibity in case of changes in WebRTC.
enum class mrsBundlePolicy : int32_t {
  kBalanced = 0,
  kMaxBundle = 1,
  kMaxCompat = 2
};

/// SDP semantic (protocol dialect) for (re)negotiating a peer connection.
/// This cannot be changed after the connection is established.
enum class mrsSdpSemantic : int32_t {
  /// Unified Plan - default and recommended. Standardized in WebRTC 1.0.
  kUnifiedPlan = 0,
  /// Plan B - deprecated and soon to be removed. Do not use unless for
  /// compability with an older implementation. This is non-standard.
  kPlanB = 1
};

/// Configuration to intialize a peer connection object.
struct mrsPeerConnectionConfiguration {
  /// ICE servers, encoded as a single string buffer.
  /// The syntax for the encoded string is:
  ///   string = blocks
  ///   blocks = block [ "\n\n" blocks ]
  ///   block = lines
  ///   lines = line [ "\n" lines ]
  ///   line = url | keyvalue
  ///   url = <Some ICE server URL>
  ///   keyvalue = key ":" value
  ///   key = "username" | "password"
  ///   value = <Some username/password value>
  /// Example of encoded string, with formatting for clarity:
  ///   https://stun1.l.google.com:19302\n
  ///   \n
  ///   https://stun2.l.google.com:19302\n
  ///   username:my_user_name\n
  ///   password:my_password\n
  ///   \n
  ///   https://stun3.l.google.com:19302
  const char* encoded_ice_servers = nullptr;

  /// ICE transport type for the connection.
  mrsIceTransportType ice_transport_type = mrsIceTransportType::kAll;

  /// Bundle policy for the connection.
  mrsBundlePolicy bundle_policy = mrsBundlePolicy::kBalanced;

  /// SDP semantic for connection negotiation.
  /// Do not use Plan B unless there is a problem with Unified Plan.
  mrsSdpSemantic sdp_semantic = mrsSdpSemantic::kUnifiedPlan;
};

/// Create a peer connection and return a handle to it.
/// On UWP this must be invoked from another thread than the main UI thread.
/// The newly-created peer connection native resource is reference-counted, and
/// has a single reference when this function returns. Additional references may
/// be added with |mrsPeerConnectionAddRef| and removed with
/// |mrsPeerConnectionRemoveRef|. When the last reference is removed, the native
/// object is destroyed.
MRS_API mrsResult MRS_CALL
mrsPeerConnectionCreate(mrsPeerConnectionConfiguration config,
                        mrsPeerConnectionInteropHandle interop_handle,
                        mrsPeerConnectionHandle* peerHandleOut) noexcept;

/// Callbacks needed to allow the native implementation to interact with the
/// interop layer, and in particular to react to events which necessitate
/// creating a new interop wrapper for a new native instance (whose creation was
/// not initiated by the interop, so for which the native instance is created
/// first).
struct mrsPeerConnectionInteropCallbacks {
  /// Construct an interop object for a Transceiver instance.
  mrsTransceiverCreateObjectCallback transceiver_create_object{};

  /// Finish the construction of the interop object of a Transceiver.
  mrsTransceiverFinishCreateCallback transceiver_finish_create{};

  /// Construct an interop object for a RemoteAudioTrack instance.
  mrsRemoteAudioTrackCreateObjectCallback remote_audio_track_create_object{};

  /// Construct an interop object for a RemoteVideooTrack instance.
  mrsRemoteVideoTrackCreateObjectCallback remote_video_track_create_object{};

  /// Construct an interop object for a DataChannel instance.
  mrsDataChannelCreateObjectCallback data_channel_create_object{};
};

/// Register the interop callbacks necessary to make interop work. To
/// unregister, simply pass nullptr as the callback pointer. Only one set of
/// callbacks can be registered at a time.
MRS_API mrsResult MRS_CALL mrsPeerConnectionRegisterInteropCallbacks(
    mrsPeerConnectionHandle peerHandle,
    mrsPeerConnectionInteropCallbacks* callbacks) noexcept;

/// Register a callback invoked once connected to a remote peer. To unregister,
/// simply pass nullptr as the callback pointer. Only one callback can be
/// registered at a time.
MRS_API void MRS_CALL mrsPeerConnectionRegisterConnectedCallback(
    mrsPeerConnectionHandle peerHandle,
    mrsPeerConnectionConnectedCallback callback,
    void* user_data) noexcept;

/// Register a callback invoked when a local message is ready to be sent via the
/// signaling service to a remote peer. Only one callback can be registered at a
/// time.
MRS_API void MRS_CALL mrsPeerConnectionRegisterLocalSdpReadytoSendCallback(
    mrsPeerConnectionHandle peerHandle,
    mrsPeerConnectionLocalSdpReadytoSendCallback callback,
    void* user_data) noexcept;

/// Register a callback invoked when an ICE candidate message is ready to be
/// sent via the signaling service to a remote peer. Only one callback can be
/// registered at a time.
MRS_API void MRS_CALL mrsPeerConnectionRegisterIceCandidateReadytoSendCallback(
    mrsPeerConnectionHandle peerHandle,
    mrsPeerConnectionIceCandidateReadytoSendCallback callback,
    void* user_data) noexcept;

/// Register a callback invoked when the ICE connection state changes. Only one
/// callback can be registered at a time.
MRS_API void MRS_CALL mrsPeerConnectionRegisterIceStateChangedCallback(
    mrsPeerConnectionHandle peerHandle,
    mrsPeerConnectionIceStateChangedCallback callback,
    void* user_data) noexcept;

/// Register a callback fired when a renegotiation of the current session needs
/// to occur to account for new parameters (e.g. added or removed tracks).
MRS_API void MRS_CALL mrsPeerConnectionRegisterRenegotiationNeededCallback(
    mrsPeerConnectionHandle peerHandle,
    mrsPeerConnectionRenegotiationNeededCallback callback,
    void* user_data) noexcept;

/// Register a callback fired when a remote audio track is added to the current
/// peer connection.
/// Note that the arguments include some object handles, which each hold a
/// reference to the corresponding object and therefore must be released, even
/// if the user does not make use of them in the callback.
MRS_API void MRS_CALL mrsPeerConnectionRegisterAudioTrackAddedCallback(
    mrsPeerConnectionHandle peerHandle,
    mrsPeerConnectionAudioTrackAddedCallback callback,
    void* user_data) noexcept;

/// Register a callback fired when a remote audio track is removed from the
/// current peer connection.
/// Note that the arguments include some object handles, which each hold a
/// reference to the corresponding object and therefore must be released, even
/// if the user does not make use of them in the callback.
MRS_API void MRS_CALL mrsPeerConnectionRegisterAudioTrackRemovedCallback(
    mrsPeerConnectionHandle peerHandle,
    mrsPeerConnectionAudioTrackRemovedCallback callback,
    void* user_data) noexcept;

/// Register a callback fired when a remote video track is added to the current
/// peer connection.
/// Note that the arguments include some object handles, which each hold a
/// reference to the corresponding object and therefore must be released, even
/// if the user does not make use of them in the callback.
MRS_API void MRS_CALL mrsPeerConnectionRegisterVideoTrackAddedCallback(
    mrsPeerConnectionHandle peerHandle,
    mrsPeerConnectionVideoTrackAddedCallback callback,
    void* user_data) noexcept;

/// Register a callback fired when a remote video track is removed from the
/// current peer connection.
/// Note that the arguments include some object handles, which each hold a
/// reference to the corresponding object and therefore must be released, even
/// if the user does not make use of them in the callback.
MRS_API void MRS_CALL mrsPeerConnectionRegisterVideoTrackRemovedCallback(
    mrsPeerConnectionHandle peerHandle,
    mrsPeerConnectionVideoTrackRemovedCallback callback,
    void* user_data) noexcept;

/// Register a callback fired when a remote data channel is removed from the
/// current peer connection.
MRS_API void MRS_CALL mrsPeerConnectionRegisterDataChannelAddedCallback(
    mrsPeerConnectionHandle peerHandle,
    mrsPeerConnectionDataChannelAddedCallback callback,
    void* user_data) noexcept;

/// Register a callback fired when a remote data channel is removed from the
/// current peer connection.
MRS_API void MRS_CALL mrsPeerConnectionRegisterDataChannelRemovedCallback(
    mrsPeerConnectionHandle peerHandle,
    mrsPeerConnectionDataChannelRemovedCallback callback,
    void* user_data) noexcept;

/// Kind of video profile. Equivalent to org::webRtc::VideoProfileKind.
enum class mrsVideoProfileKind : int32_t {
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

enum class mrsTransceiverStateUpdatedReason : int32_t {
  kLocalDesc,
  kRemoteDesc,
  kSetDirection
};

/// Flow direction of the media inside the transceiver. This maps to whether
/// local and/or remote tracks are attached to the transceiver. The local
/// track corresponds to the send direction, and the remote track to the
/// receive direction.
enum class mrsTransceiverDirection : int32_t {
  kSendRecv = 0,
  kSendOnly = 1,
  kRecvOnly = 2,
  kInactive = 3
};

/// Same as |mrsTransceiverDirection|, but including optional unset.
enum class mrsTransceiverOptDirection : int32_t {
  kNotSet = -1,
  kSendRecv = 0,
  kSendOnly = 1,
  kRecvOnly = 2,
  kInactive = 3
};

/// Media kind for tracks and transceivers.
enum class mrsMediaKind : uint32_t { kAudio = 0, kVideo = 1 };

/// Configuration for creating a new transceiver.
struct mrsTransceiverInitConfig {
  /// Name of the transceiver. This must be a valid SDP token; see
  /// |mrsSdpIsValidToken()|.
  const char* name = nullptr;

  /// Initial desired direction of the transceiver media when created.
  mrsTransceiverDirection desired_direction =
      mrsTransceiverDirection::kSendRecv;

  /// Semi-colon separated list of stream IDs associated with the transceiver.
  const char* stream_ids = nullptr;

  /// Handle of the transceiver interop wrapper, if any, which will be
  /// associated with the native transceiver object.
  mrsTransceiverInteropHandle transceiver_interop_handle{};
};

using mrsRequestExternalI420AVideoFrameCallback =
    mrsResult(MRS_CALL*)(void* user_data,
                         mrsExternalVideoTrackSourceHandle source_handle,
                         uint32_t request_id,
                         int64_t timestamp_ms);

using mrsRequestExternalArgb32VideoFrameCallback =
    mrsResult(MRS_CALL*)(void* user_data,
                         mrsExternalVideoTrackSourceHandle source_handle,
                         uint32_t request_id,
                         int64_t timestamp_ms);

/// Configuration for creating a new transceiver interop wrapper when the
/// implementation initiates the creating, generally as a result of applying a
/// remote description.
struct mrsTransceiverWrapperInitConfig {
  /// Transceiver name. This is always a valid SDP token.
  const char* name{nullptr};

  /// Media kind the transceiver is transporting.
  mrsMediaKind media_kind{mrsMediaKind::kAudio};

  /// Zero-based media line index for the transceiver. In Unified Plan, this is
  /// the index of the m= line in the SDP offer/answer as determined when adding
  /// the transceiver. This is provided by the implementation and is immutable
  /// (since we don't support stopping transceivers, so m= lines are not
  /// recycled). For Plan B this is still used but is only the index of the
  /// transceiver in the collection of the peer connection (like in Unified
  /// Plan), without any relation to the SDP offer/answer.
  int mline_index{-1};

  /// Initial desired direction when the transceiver is created. This is
  /// generally set to the current value on the implementation object, to keep
  /// the interop wrapper in sync.
  mrsTransceiverDirection initial_desired_direction{
      mrsTransceiverDirection::kSendRecv};
};

struct mrsRemoteAudioTrackConfig {
  const char* track_name{};
};

struct mrsRemoteVideoTrackConfig {
  const char* track_name{};
};

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
    mrsPeerConnectionHandle peerHandle,
    mrsDataChannelInteropHandle dataChannelInteropHandle,
    mrsDataChannelConfig config,
    mrsDataChannelCallbacks callbacks,
    mrsDataChannelHandle* dataChannelHandleOut) noexcept;

MRS_API mrsResult MRS_CALL mrsPeerConnectionRemoveDataChannel(
    mrsPeerConnectionHandle peerHandle,
    mrsDataChannelHandle dataChannelHandle) noexcept;

MRS_API mrsResult MRS_CALL
mrsDataChannelSendMessage(mrsDataChannelHandle dataChannelHandle,
                          const void* data,
                          uint64_t size) noexcept;

/// Add a new ICE candidate received from a signaling service.
MRS_API mrsResult MRS_CALL
mrsPeerConnectionAddIceCandidate(mrsPeerConnectionHandle peerHandle,
                                 const char* sdp_mid,
                                 const int sdp_mline_index,
                                 const char* candidate) noexcept;

/// Create a new JSEP offer to try to establish a connection with a remote peer.
/// This will generate a local offer message, then fire the
/// "LocalSdpReadytoSendCallback" callback, which should send this message via
/// the signaling service to a remote peer.
MRS_API mrsResult MRS_CALL
mrsPeerConnectionCreateOffer(mrsPeerConnectionHandle peerHandle) noexcept;

/// Create a new JSEP answer to a received offer to try to establish a
/// connection with a remote peer. This will generate a local answer message,
/// then fire the "LocalSdpReadytoSendCallback" callback, which should send this
/// message via the signaling service to a remote peer.
MRS_API mrsResult MRS_CALL
mrsPeerConnectionCreateAnswer(mrsPeerConnectionHandle peerHandle) noexcept;

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
mrsPeerConnectionSetBitrate(mrsPeerConnectionHandle peer_handle,
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
mrsPeerConnectionSetRemoteDescriptionAsync(mrsPeerConnectionHandle peerHandle,
                                           const char* type,
                                           const char* sdp,
                                           ActionCallback callback,
                                           void* user_data) noexcept;

/// Close a peer connection, removing all tracks and disconnecting from the
/// remote peer currently connected. This does not invalidate the handle nor
/// destroy the native peer connection object, but leaves it in a state where it
/// can only be destroyed.
MRS_API mrsResult MRS_CALL
mrsPeerConnectionClose(mrsPeerConnectionHandle peerHandle) noexcept;

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
using mrsPeerConnectionGetSimpleStatsCallback =
    void(MRS_CALL*)(void* user_data, mrsStatsReportHandle stats_report);

/// Called by mrsStatsReportGetObjects for every instance of the requested stats
/// type.
using mrsStatsReportGetObjectCallback =
    void(MRS_CALL*)(void* user_data, const void* stats_object);

/// Get a stats report for the connection.
/// The report passed to the callback must be released when finished through
/// mrsStatsReportRemoveRef.
MRS_API mrsResult MRS_CALL mrsPeerConnectionGetSimpleStats(
    mrsPeerConnectionHandle peer_handle,
    mrsPeerConnectionGetSimpleStatsCallback callback,
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
