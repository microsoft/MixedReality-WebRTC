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

/// 8-bit boolean with optional value for interop API.
/// This is conceptually equivalent to std::optional<bool>.
enum class mrsOptBool : int8_t { kTrue = -1, kFalse = 0, kUnset = 0b01010101 };

//
// Generic utilities
//

/// Report live objects to debug output, and return the number of live objects.
MRS_API uint32_t MRS_CALL mrsReportLiveObjects() noexcept;

/// Available audio device modules for Windows Desktop. The audio device module
/// is the internal audio component responsible for audio capture and playback.
/// Those options are only meaningful for Windows Desktop; other platforms use a
/// predefined platform-dependent built-in module.
enum class mrsAudioDeviceModule : uint8_t {
  /// Legacy audio device module (ADM1) for backward compatibility. This is not
  /// recommended, unless there is an issue with the default new module (ADM2).
  kLegacy = 1,

  /// New CoreAudio-based audio device module (ADM2). This is the default and
  /// recommended audio module on Windows Desktop.
  kDefault = 2
};

/// Select the audio device module to use on Windows Desktop.
///
/// By default the new CoreAudio-based audio device module (ADM2) is used, which
/// provides better handling of unsupported devices than its predecessor (ADM1).
/// https://github.com/microsoft/MixedReality-WebRTC/issues/124
///
/// This function allows overwriting the default selection to force another
/// module, mostly as a safety net would ADM2 present some issue.
///
/// The audio device module is a global object used by all peer connections.
/// This function needs to be called before the peer connection factory is
/// initialized, and therefore before any other library call, as most calls will
/// initialize the peer connection factory internally. Therefore, the audio
/// device module cannot be changed after the library is initialized, and this
/// call will fail with |mrsResult::kInvalidOperation| if invoked too late.
///
/// This has no effect on UWP and non-Windows platforms, and will always
/// succeed if timely called before the library is initialized.
MRS_API mrsResult MRS_CALL
mrsLibraryUseAudioDeviceModule(mrsAudioDeviceModule adm) noexcept;

/// Get the audio device module to use on Windows Desktop.
/// By default the new CoreAudio-based audio device module (ADM2) is used.
MRS_API mrsAudioDeviceModule MRS_CALL mrsLibraryGetAudioDeviceModule() noexcept;

/// Global MixedReality-WebRTC library shutdown options.
enum class mrsShutdownOptions : uint32_t {
  kNone = 0,

  /// Log some report about live objects when trying to shutdown, to help
  /// debugging.
  kLogLiveObjects = 0x1,

  /// When forcing shutdown, either because |mrsForceShutdown()| is called or
  /// because the program terminates, and some objects are still alive, attempt
  /// to break into the debugger. This is not available on all platforms.
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

/// Opaque handle to a native interop object.
using mrsObjectHandle = void*;

/// Opaque handle to a native reference-counted interop object.
using mrsRefCountedObjectHandle = mrsObjectHandle;

/// Opaque handle to a native PeerConnection interop object.
using mrsPeerConnectionHandle = mrsRefCountedObjectHandle;

/// Opaque handle to a native Transceiver interop object.
using mrsTransceiverHandle = mrsObjectHandle;

/// Opaque handle to a native AudioTrackSource interop object.
using mrsAudioTrackSourceHandle = mrsRefCountedObjectHandle;

/// Opaque handle to a native VideoTrackSource interop object.
using mrsVideoTrackSourceHandle = mrsRefCountedObjectHandle;

/// Opaque handle to a native LocalAudioTrack interop object.
using mrsLocalAudioTrackHandle = mrsRefCountedObjectHandle;

/// Opaque handle to a native LocalVideoTrack interop object.
using mrsLocalVideoTrackHandle = mrsRefCountedObjectHandle;

/// Opaque handle to a native RemoteAudioTrack interop object.
using mrsRemoteAudioTrackHandle = mrsObjectHandle;

/// Opaque handle to a native RemoteVideoTrack interop object.
using mrsRemoteVideoTrackHandle = mrsObjectHandle;

/// Opaque handle to a native DataChannel interop object.
using mrsDataChannelHandle = mrsObjectHandle;

/// Opaque handle to a native ExternalVideoTrackSource interop object.
using mrsExternalVideoTrackSourceHandle = mrsVideoTrackSourceHandle;

/// Opaque handle to a native DeviceVideoTrackSource interop object.
using mrsDeviceVideoTrackSourceHandle = mrsVideoTrackSourceHandle;

/// Opaque handle to a native DeviceAudioTrackSource interop object.
using mrsDeviceAudioTrackSourceHandle = mrsAudioTrackSourceHandle;

//
// Video capture enumeration
//

/// Kind of video profile. Equal to org::webRtc::VideoProfileKind.
///
/// These are equivalent to the "known video profiles" found in the UWP
/// MediaCapture API, with the addition of the
/// |mrsVideoProfileKind::kUnspecified| placeholder value indicating "no
/// profile" or "default profile" depending on the context.
/// https://docs.microsoft.com/en-us/uwp/api/windows.media.capture.knownvideoprofile?view=winrt-19041
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

/// Video capture device info.
struct mrsVideoCaptureDeviceInfo {
  // Unique identifier of the capture device, used to start capture.
  const char* id;

  // Optional friendly name of the capture device, for UI display. If the device
  // does not have a friendly name, the implementation returns the same value as
  // |id|, to ensure this value is not an emtpy string.
  const char* name;
};

/// Video profile info.
struct mrsVideoProfileInfo {
  // Unique identifier of the video profile.
  const char* id;
};

/// Video capture format info.
struct mrsVideoCaptureFormatInfo {
  // Capture width, in pixels.
  uint32_t width;

  // Capture height, in pixels.
  uint32_t height;

  // Maximum capture framerate, in frames per seconds. Video capture devices
  // commonly have adaptive framerate based on luminosity, and this value
  // reports the maximum framerate the device supports.
  float framerate;

  // Capture format as a FOURCC code.
  // https://docs.microsoft.com/en-us/windows/win32/medfound/video-fourccs
  uint32_t fourcc;
};

/// Callback invoked for each enumerated video capture device.
using mrsVideoCaptureDeviceEnumCallback = void(
    MRS_CALL*)(void* user_data, const mrsVideoCaptureDeviceInfo* device_info);

/// Callback invoked on video capture device enumeration completed. If the
/// result is not |mrsResult::kSuccess| then some or all of the devices might
/// not have been enumerated.
using mrsVideoCaptureDeviceEnumCompletedCallback =
    void(MRS_CALL*)(void* user_data, mrsResult result);

/// Enumerate the video capture devices asynchronously.
///
/// If the enumeration starts successfully, that is the function returns
/// |mrsResult::kSuccess|, then for each device found the implementation invokes
/// the mandatory |enumCallback|. At the end of the enumeration, it invokes the
/// optional |completedCallback| if it was provided (non-null). Note that those
/// calls are asynchonous and not necessarily done before
/// |mrsEnumVideoCaptureDevicesAsync()| returned.
///
/// If the enumeration fails to start, the function returns an error code; in
/// that case no callback is invoked.
///
/// On UWP this must *not* be called from the main UI thread, otherwise a
/// |mrsResult::kWrongThread| error might be returned.
MRS_API mrsResult MRS_CALL mrsEnumVideoCaptureDevicesAsync(
    mrsVideoCaptureDeviceEnumCallback enumCallback,
    void* enumCallbackUserData,
    mrsVideoCaptureDeviceEnumCompletedCallback completedCallback,
    void* completedCallbackUserData) noexcept;

/// Callback invoked for each enumerated video capture device.
using mrsVideoProfileEnumCallback =
    void(MRS_CALL*)(void* user_data, const mrsVideoProfileInfo* profile_info);

/// Callback invoked on video profile enumeration completed. If the result is
/// not |mrsResult::kSuccess| then some or all of the profiles might not have
/// been enumerated.
using mrsVideoProfileEnumCompletedCallback = void(MRS_CALL*)(void* user_data,
                                                             mrsResult result);

/// Asynchronously enumerate the video profiles for the given capture device,
/// possibly restricted to the specified profile kind.
///
/// If the enumeration starts successfully, that is the function returns
/// |mrsResult::kSuccess|, then for each video profile found for the given
/// capture device the implementation invokes the mandatory |enumCallback|. At
/// the end of the enumeration, it invokes the optional |completedCallback| if
/// it was provided (non-null). Note that those calls are asynchonous and not
/// necessarily done before |mrsEnumVideoProfilesAsync()| returned.
///
/// If the enumeration fails to start, the function returns an error code; in
/// that case no callback is invoked.
///
/// If the video capture device does not support video profiles, this function
/// succeeds and returns an empty list.
///
/// On UWP this must *not* be called from the main UI thread, otherwise a
/// |mrsResult::kWrongThread| error might be returned.
MRS_API mrsResult MRS_CALL mrsEnumVideoProfilesAsync(
    const char* device_id,
    mrsVideoProfileKind profile_kind,
    mrsVideoProfileEnumCallback enumCallback,
    void* enumCallbackUserData,
    mrsVideoProfileEnumCompletedCallback completedCallback,
    void* completedCallbackUserData) noexcept;

/// Callback invoked for each enumerated video capture format.
using mrsVideoCaptureFormatEnumCallback = void(
    MRS_CALL*)(void* user_data, const mrsVideoCaptureFormatInfo* format_info);

/// Callback invoked on video capture format enumeration completed. If the
/// result is not |mrsResult::kSuccess| then some or all of the device formats
/// might not have been enumerated.
using mrsVideoCaptureFormatEnumCompletedCallback =
    void(MRS_CALL*)(void* user_data, mrsResult result);

/// Asynchronously enumerate the capture formats of a video capture device.
///
/// The capture formats are enumerated for the given video capture device:
/// - If the device does not support video profiles, |profile_id| and
/// |profile_kind| are both ignored, and all capture formats supported by the
/// specified device are enumerated.
/// - If the device supports video profiles, enumeration depends on the values
/// of the profile ID and kind. The profile ID string |profile_id| can be null
/// or an empty string, meaning no specified profile ID. The profile kind
/// |profile_kind| can be |mrsVideoProfileKind::Unspecified| to indicate no
/// specified profile kind. |profile_id| or |profile_kind| can be specified (but
/// not both) to limit the enumeration:
///   - If neither profile ID nor kind are specified, the capture formats for
///   the default video profile are enumerated.
///   - If profile ID is specified, only capture formats for that specific
///   profile are enumerated.
///   - If profile kind is specified, capture formats for all profiles
///   associated with the given kind are enumerated.
///
/// If the enumeration starts successfully, that is the function returns
/// |mrsResult::kSuccess|, then for each format found the implementation invokes
/// the mandatory |enumCallback|. At the end of the enumeration, it invokes the
/// optional |completedCallback| if it was provided (non-null). Note that those
/// calls are asynchonous and not necessarily done before
/// |mrsEnumVideoCaptureFormatsAsync()| returned. The |completedCallback|
/// contains the result of the overall enumeration, therefore it is recommended
/// to provide it. Only when it returns |mrsResult::kSuccess| is the caller
/// ensured all capture formats have been enumerated successfully.
///
/// If the enumeration fails to even start, the function returns an error code;
/// in that case no callback is invoked, not even the |completedCallback| if
/// provided.
///
/// On UWP this must *not* be called from the main UI thread, otherwise a
/// |mrsResult::kWrongThread| error might be returned.
MRS_API mrsResult MRS_CALL mrsEnumVideoCaptureFormatsAsync(
    const char* device_id,
    const char* profile_id,
    mrsVideoProfileKind profile_kind,
    mrsVideoCaptureFormatEnumCallback enumCallback,
    void* enumCallbackUserData,
    mrsVideoCaptureFormatEnumCompletedCallback completedCallback,
    void* completedCallbackUserData) noexcept;

//
// Peer connection
//

/// Type of SDP message.
enum class mrsSdpMessageType : int32_t { kOffer = 1, kAnswer = 2 };

/// ICE candidate.
struct mrsIceCandidate {
  /// Value of the "mid" attribute.
  const char* sdp_mid{nullptr};
  /// Raw candidate content.
  const char* content{nullptr};
  /// Media line index the candidate is associated with.
  int32_t sdp_mline_index{-1};
};

/// Callback invoked when the peer connection is connected, that is it finished
/// the JSEP offer/answer exchange successfully.
using mrsPeerConnectionConnectedCallback = void(MRS_CALL*)(void* user_data);

/// Callback invoked when a local SDP message has been prepared and is ready to
/// be sent by the user via the signaling service.
using mrsPeerConnectionLocalSdpReadytoSendCallback = void(
    MRS_CALL*)(void* user_data, mrsSdpMessageType type, const char* sdp_data);

/// Callback invoked when an ICE candidate has been prepared and is ready to be
/// sent by the user via the signaling service.
using mrsPeerConnectionIceCandidateReadytoSendCallback =
    void(MRS_CALL*)(void* user_data, const mrsIceCandidate* candidate);

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

/// Callback invoked when the state of the ICE connection changed.
using mrsPeerConnectionIceStateChangedCallback =
    void(MRS_CALL*)(void* user_data, mrsIceConnectionState new_state);

/// Callback invoked when a renegotiation of the current session needs to occur
/// to account for new parameters (e.g. added or removed tracks).
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

/// Information about a newly added remote audio track provided to the audio
/// track added callback.
struct mrsRemoteAudioTrackAddedInfo {
  /// Handle of the newly-created remote audio track.
  mrsRemoteAudioTrackHandle track_handle;

  /// Handle of the audio transeiver the track was added to.
  mrsTransceiverHandle audio_transceiver_handle;

  /// Name of the newly-added remote audio track.
  const char* track_name;
};

/// Information about a newly added remote video track provided to the video
/// track added callback.
struct mrsRemoteVideoTrackAddedInfo {
  /// Handle of the newly-created remote video track.
  mrsRemoteVideoTrackHandle track_handle;

  /// Handle of the video transeiver the track was added to.
  mrsTransceiverHandle audio_transceiver_handle;

  /// Name of the newly-added remote video track.
  const char* track_name;
};

/// Callback invoked when a remote audio track is added to a connection.
/// The |audio_track| and |audio_transceiver| handle hold a reference to the
/// underlying native object they are associated with, and therefore must be
/// released with |mrsRefCountedObjectRemoveRef()| to avoid memory leaks.
using mrsPeerConnectionAudioTrackAddedCallback =
    void(MRS_CALL*)(void* user_data, const mrsRemoteAudioTrackAddedInfo* info);

/// Callback invoked when a remote audio track is removed from a connection.
/// The |audio_track| and |audio_transceiver| handle hold a reference to the
/// underlying native object they are associated with, and therefore must be
/// released with |mrsRefCountedObjectRemoveRef()| to avoid memory leaks.
using mrsPeerConnectionAudioTrackRemovedCallback =
    void(MRS_CALL*)(void* user_data,
                    mrsRemoteAudioTrackHandle audio_track,
                    mrsTransceiverHandle transceiver);

/// Callback invoked when a remote video track is added to a connection.
/// The |video_track| and |video_transceiver| handle hold a reference to the
/// underlying native object they are associated with, and therefore must be
/// released with |mrsRefCountedObjectRemoveRef()| to avoid memory leaks.
using mrsPeerConnectionVideoTrackAddedCallback =
    void(MRS_CALL*)(void* user_data, const mrsRemoteVideoTrackAddedInfo* info);

/// Callback invoked when a remote video track is removed from a connection.
/// The |video_track| and |video_transceiver| handle hold a reference to the
/// underlying native object they are associated with, and therefore must be
/// released with |mrsRefCountedObjectRemoveRef()| to avoid memory leaks.
using mrsPeerConnectionVideoTrackRemovedCallback =
    void(MRS_CALL*)(void* user_data,
                    mrsRemoteVideoTrackHandle video_track,
                    mrsTransceiverHandle transceiver);

enum class mrsDataChannelConfigFlags : uint32_t {
  kNone = 0,
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

struct mrsDataChannelAddedInfo {
  mrsDataChannelHandle handle{nullptr};
  int id{0};
  mrsDataChannelConfigFlags flags{};
  const char* label{nullptr};
};

/// Callback invoked when a data channel is added to the peer connection. This
/// is called for both channels that are created locally and ones that are
/// created by the remote peer.
///
/// Use this callback to call |mrsDataChannelRegisterCallbacks| on new data
/// channels and to start listening for messages/state changes.
///
/// The data channel is initially in the |mrsDataChannelState::kConnecting|
/// state and will transition to |mrsDataChannelState::kOpen| when it is ready
/// for use. In order to know when the channel state changes you must pass
/// a valid |mrsDataChannelStateCallback| to |mrsDataChannelRegisterCallbacks|.
using mrsPeerConnectionDataChannelAddedCallback =
    void(MRS_CALL*)(void* user_data, const mrsDataChannelAddedInfo* info);

/// Callback invoked when a data channel is removed from the peer connection.
using mrsPeerConnectionDataChannelRemovedCallback =
    void(MRS_CALL*)(void* user_data, mrsDataChannelHandle data_channel);

using mrsI420AVideoFrame = Microsoft::MixedReality::WebRTC::I420AVideoFrame;

/// Callback invoked when a local or remote (depending on use) video frame is
/// available to be consumed by the caller, usually for display.
/// The video frame is encoded in I420 triplanar format (NV12).
using mrsI420AVideoFrameCallback =
    void(MRS_CALL*)(void* user_data, const mrsI420AVideoFrame& frame);

using mrsArgb32VideoFrame = Microsoft::MixedReality::WebRTC::Argb32VideoFrame;

/// Callback invoked when a local or remote (depending on use) video frame is
/// available to be consumed by the caller, usually for display.
/// The video frame is encoded in ARGB 32-bit per pixel.
using mrsArgb32VideoFrameCallback =
    void(MRS_CALL*)(void* user_data, const mrsArgb32VideoFrame& frame);

using mrsAudioFrame = Microsoft::MixedReality::WebRTC::AudioFrame;

/// Callback invoked when a local or remote (depending on use) audio frame is
/// available to be consumed by the caller, usually for local output.
using mrsAudioFrameCallback = void(MRS_CALL*)(void* user_data,
                                              const mrsAudioFrame& frame);

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
/// be added with |mrsRefCountedObjectAddRef| and removed with
/// |mrsRefCountedObjectRemoveRef|. When the last reference is removed, the
/// native object is destroyed.
MRS_API mrsResult MRS_CALL
mrsPeerConnectionCreate(const mrsPeerConnectionConfiguration* config,
                        mrsPeerConnectionHandle* peer_handle_out) noexcept;

/// Register a callback invoked once connected to a remote peer. To unregister,
/// simply pass nullptr as the callback pointer. Only one callback can be
/// registered at a time.
MRS_API void MRS_CALL mrsPeerConnectionRegisterConnectedCallback(
    mrsPeerConnectionHandle peer_handle,
    mrsPeerConnectionConnectedCallback callback,
    void* user_data) noexcept;

/// Register a callback invoked when a local message is ready to be sent via the
/// signaling service to a remote peer. Only one callback can be registered at a
/// time.
MRS_API void MRS_CALL mrsPeerConnectionRegisterLocalSdpReadytoSendCallback(
    mrsPeerConnectionHandle peer_handle,
    mrsPeerConnectionLocalSdpReadytoSendCallback callback,
    void* user_data) noexcept;

/// Register a callback invoked when an ICE candidate message is ready to be
/// sent via the signaling service to a remote peer. Only one callback can be
/// registered at a time.
MRS_API void MRS_CALL mrsPeerConnectionRegisterIceCandidateReadytoSendCallback(
    mrsPeerConnectionHandle peer_handle,
    mrsPeerConnectionIceCandidateReadytoSendCallback callback,
    void* user_data) noexcept;

/// Register a callback invoked when the ICE connection state changes. Only one
/// callback can be registered at a time.
MRS_API void MRS_CALL mrsPeerConnectionRegisterIceStateChangedCallback(
    mrsPeerConnectionHandle peer_handle,
    mrsPeerConnectionIceStateChangedCallback callback,
    void* user_data) noexcept;

/// Register a callback invoked when a renegotiation of the current session
/// needs to occur to account for new parameters (e.g. added or removed tracks).
MRS_API void MRS_CALL mrsPeerConnectionRegisterRenegotiationNeededCallback(
    mrsPeerConnectionHandle peer_handle,
    mrsPeerConnectionRenegotiationNeededCallback callback,
    void* user_data) noexcept;

/// Register a callback invoked when a remote audio track is added to the
/// current peer connection. Note that the arguments include some object
/// handles, which each hold a reference to the corresponding object and
/// therefore must be released, even if the user does not make use of them in
/// the callback.
MRS_API void MRS_CALL mrsPeerConnectionRegisterAudioTrackAddedCallback(
    mrsPeerConnectionHandle peer_handle,
    mrsPeerConnectionAudioTrackAddedCallback callback,
    void* user_data) noexcept;

/// Register a callback invoked when a remote audio track is removed from the
/// current peer connection.
/// Note that the arguments include some object handles, which each hold a
/// reference to the corresponding object and therefore must be released, even
/// if the user does not make use of them in the callback.
MRS_API void MRS_CALL mrsPeerConnectionRegisterAudioTrackRemovedCallback(
    mrsPeerConnectionHandle peer_handle,
    mrsPeerConnectionAudioTrackRemovedCallback callback,
    void* user_data) noexcept;

/// Register a callback invoked when a remote video track is added to the
/// current peer connection. Note that the arguments include some object
/// handles, which each hold a reference to the corresponding object and
/// therefore must be released, even if the user does not make use of them in
/// the callback.
MRS_API void MRS_CALL mrsPeerConnectionRegisterVideoTrackAddedCallback(
    mrsPeerConnectionHandle peer_handle,
    mrsPeerConnectionVideoTrackAddedCallback callback,
    void* user_data) noexcept;

/// Register a callback invoked when a remote video track is removed from the
/// current peer connection.
/// Note that the arguments include some object handles, which each hold a
/// reference to the corresponding object and therefore must be released, even
/// if the user does not make use of them in the callback.
MRS_API void MRS_CALL mrsPeerConnectionRegisterVideoTrackRemovedCallback(
    mrsPeerConnectionHandle peer_handle,
    mrsPeerConnectionVideoTrackRemovedCallback callback,
    void* user_data) noexcept;

/// Register a callback invoked when a remote data channel is removed from the
/// current peer connection.
MRS_API void MRS_CALL mrsPeerConnectionRegisterDataChannelAddedCallback(
    mrsPeerConnectionHandle peer_handle,
    mrsPeerConnectionDataChannelAddedCallback callback,
    void* user_data) noexcept;

/// Register a callback invoked when a remote data channel is removed from the
/// current peer connection.
MRS_API void MRS_CALL mrsPeerConnectionRegisterDataChannelRemovedCallback(
    mrsPeerConnectionHandle peer_handle,
    mrsPeerConnectionDataChannelRemovedCallback callback,
    void* user_data) noexcept;

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
  /// Optional name of the transceiver. This must be a valid SDP token; see
  /// |mrsSdpIsValidToken()|. If no name is provided (empty or null string),
  /// then the implementation will generate a random one.
  const char* name{nullptr};

  /// Kind of media the transceiver transports.
  mrsMediaKind media_kind{(mrsMediaKind)-1};  // invalid value to catch errors

  /// Initial desired direction of the transceiver media when created.
  mrsTransceiverDirection desired_direction{mrsTransceiverDirection::kSendRecv};

  /// Optional semi-colon separated list of stream IDs associated with the
  /// transceiver, or null/empty string for none.
  const char* stream_ids{nullptr};

  /// Optional user data.
  void* user_data{nullptr};
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
  /// Optional name of the transceiver. This must be a valid SDP token; see
  /// |mrsSdpIsValidToken()|. If no name is provided (empty or null string),
  /// then the implementation will generate a random one.
  const char* name{nullptr};

  /// Kind of media the transceiver transports.
  mrsMediaKind media_kind{};

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

struct mrsDataChannelConfig {
  int32_t id = -1;  // -1 for auto; >=0 for negotiated
  mrsDataChannelConfigFlags flags{};
  const char* label{};  // optional; can be null or empty string
};

/// Add a new data channel to a peer connection.
///
/// The initial configuration of the data channel is provided by |config|, and
/// is mandatory. The function returns in |data_channel_handle_out| the handle
/// to the newly-created data channel after it was added to the peer connection.
///
/// The type of data channel created depends on the |config.id| value:
/// - If |config.id| < 0, then it adds a new in-band data channel with an ID
/// that will be selected by the WebRTC implementation itself, and will be
/// available later. In that case the channel is announced to the remote peer
/// for it to create a channel with the same ID. This requires a renegotiation.
/// Once the renegotiation is completed, the ID is available on both peers.
/// - If |config.id| >= 0, then it adds a new out-of-band negotiated channel
/// with the given ID, and it is the responsibility of the app to create a
/// channel with the same ID on the remote peer to be able to use the channel.
MRS_API mrsResult MRS_CALL mrsPeerConnectionAddDataChannel(
    mrsPeerConnectionHandle peer_handle,
    const mrsDataChannelConfig* config,
    mrsDataChannelHandle* data_channel_handle_out) noexcept;

/// Remove an existing data channel from a peer connection and destroy it. If
/// the channel was an in-band data channel, then the change triggers a
/// renegotiation needed event.
MRS_API mrsResult MRS_CALL mrsPeerConnectionRemoveDataChannel(
    mrsPeerConnectionHandle peer_handle,
    mrsDataChannelHandle data_channel_handle) noexcept;

/// Add a new ICE candidate received from a signaling service. This function
/// must be called by the user each time an ICE candidate was received from the
/// remote peer, to inform the WebRTC implementation of that candidate.
MRS_API mrsResult MRS_CALL
mrsPeerConnectionAddIceCandidate(mrsPeerConnectionHandle peer_handle,
                                 const mrsIceCandidate* candidate) noexcept;

/// Create a new JSEP offer to try to establish a connection with a remote peer.
/// This will generate a local offer message, then invoke the
/// |LocalSdpReadytoSendCallback| callback, which should send to the remote peer
/// this message via the signaling service the user implemented.
///
/// Creating an offer is only possible if there is no exchange pending.
/// Therefore the user must wait for a previous exchange to complete in order to
/// be able to initiate a new offer.
MRS_API mrsResult MRS_CALL
mrsPeerConnectionCreateOffer(mrsPeerConnectionHandle peer_handle) noexcept;

/// Create a new JSEP answer to a received offer, either to try to establish a
/// new connection with a remote peer, or to update an existing session with
/// changes. This will generate a local answer message, then invoke the
/// |LocalSdpReadytoSendCallback| callback, which should send to the remote peer
/// this message via the signaling service the user chose.
///
/// Creating an answer is only possible if the local peer already applied a
/// remote offer via |mrsPeerConnectionSetRemoteDescriptionAsync| and the async
/// callback completed successfully.
///
/// Note that the answer will only negotiate the transceivers present in the
/// offer. This aligns with the JSEP protocol JSEP 5.3.1 stating that "If there
/// are more RtpTransceivers than there are m= sections, the unmatched
/// RtpTransceivers will need to be associated in a subsequent offer.".
/// https://tools.ietf.org/html/draft-ietf-rtcweb-jsep-25#page-50
/// This means that if the callee has any transceiver not yet associated with a
/// media line, those transceivers will *not* be part of the answer, and a
/// subsequent offer from that peer needs to be sent to negotiate those
/// transceivers after the current session negotation is completed.
MRS_API mrsResult MRS_CALL
mrsPeerConnectionCreateAnswer(mrsPeerConnectionHandle peer_handle) noexcept;

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

/// Callback invoked when |mrsPeerConnectionSetRemoteDescriptionAsync()|
/// completed, successfully or not. The |error_message| parameter is only
/// relevant if |result| contains an error code.
using mrsRemoteDescriptionAppliedCallback = void(
    MRS_CALL*)(void* user_data, mrsResult result, const char* error_message);

/// Set a remote description received from a remote peer via the signaling
/// solution implemented by the user. Once the remote description is applied,
/// the action callback is invoked to signal the caller it is safe to continue
/// the negotiation, and in particular it is safe to call |CreateAnswer()|.
MRS_API mrsResult MRS_CALL mrsPeerConnectionSetRemoteDescriptionAsync(
    mrsPeerConnectionHandle peer_handle,
    mrsSdpMessageType type,
    const char* sdp,
    mrsRemoteDescriptionAppliedCallback callback,
    void* user_data) noexcept;

/// Close a peer connection, removing all tracks and disconnecting from the
/// remote peer currently connected. This does not invalidate the handle nor
/// destroy the native peer connection object, but leaves it in a state where it
/// can only be destroyed by calling |mrsRefCountedObjectRemoveRef()|.
MRS_API mrsResult MRS_CALL
mrsPeerConnectionClose(mrsPeerConnectionHandle peer_handle) noexcept;

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

/// Force audio and video codecs when advertizing capabilities in an SDP offer.
///
/// This is a workaround for the lack of access to codec selection in m71.
/// Instead of selecting codecs in code, this can be used to intercept a
/// generated SDP offer before it is sent to the remote peer, and modify it by
/// removing the codecs the user does not want. This procedure is explicitly
/// allowed by the JSEP specification (section 5.4):
///     After calling setLocalDescription with an offer or answer, the
///     application MAY modify the SDP to reduce its capabilities before sending
///     it to the far side, as long as it follows the rules above that define a
///     valid JSEP offer or  answer. Likewise, an application that has received
///     an offer or answer from a peer MAY modify the received SDP, subject to
///     the same constraints, before calling setRemoteDescription.
/// https://tools.ietf.org/html/draft-ietf-rtcweb-jsep-25#section-5.4
///
/// Codec names are compared to the list of supported codecs in the input
/// message string, and if found then other codecs are pruned out. If the codec
/// name is not found, the codec is assumed to be unsupported, and as a fallback
/// mechanism the original message is not modified.
///
/// On return, the SDP offer message string to be sent via the signaler is
/// stored into the output buffer pointed to by |buffer|.
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


/// H.264 encoding profile.
enum class mrsH264Profile : int32_t {
  kProfileConstrainedBaseline,
  kProfileBaseline,
  kProfileMain,
  kProfileConstrainedHigh,
  kProfileHigh,
};

/// Rate control mode for the Media Foundation H.264 encoder. See
/// https://docs.microsoft.com/en-us/windows/win32/medfound/h-264-video-encoder
/// for details.
enum class mrsH264RcMode : int32_t {
  kUnset = -1,
  kCBR = 0,
  kVBR = 1,
  kQuality = 2
};

/// Configuration for the Media Foundation H.264 encoder.
struct mrsH264Config {

  /// H.264 profile.
  /// Note: by default we should use what's passed by WebRTC on codec
  /// initialization (which seems to be always ConstrainedBaseline), but we use
  /// Baseline to avoid changing behavior compared to earlier versions.
  mrsH264Profile profile = mrsH264Profile::kProfileBaseline;

  /// Rate control mode.
  mrsH264RcMode rc_mode = mrsH264RcMode::kUnset;

  /// If set to a value between 0 and 51, determines the max QP to use for
  /// encoding.
  int max_qp = -1;

  /// If set to a value between 0 and 100, determines the target quality value.
  /// The effect of this depends on the encoder and on the rate control mode
  /// chosen. In the Quality RC mode this will be the target for the whole
  /// stream, while in VBR it might be used as a target for individual frames
  /// while the average quality of the stream is determined by the target
  /// bitrate.
  int quality = -1;
};

/// Set the configuration used by the H.264 encoder.
///
/// The passed value will apply to all tracks that start streaming, from any
/// PeerConnection created by the application, after the call to this function.
MRS_API mrsResult MRS_CALL mrsSetH264Config(const mrsH264Config* config);


}  // extern "C"
