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

// P/Invoke uses stdcall by default. This can be changed, but Unity's IL2CPP
// does not understand the CallingConvention attribute and instead
// unconditionally forces stdcall. So use stdcall in the API to be compatible.
#if defined(MR_SHARING_WIN)
#define MRS_API __declspec(dllexport)
#define MRS_CALL __stdcall
#elif defined(MR_SHARING_ANDROID)
#define MRS_API __attribute__((visibility("default")))
#define MRS_CALL __attribute__((stdcall))
#endif

extern "C" {

//
// Errors
//

using mrsResult = std::uint32_t;

constexpr const mrsResult MRS_SUCCESS{0};

// Unknown generic error
constexpr const mrsResult MRS_E_UNKNOWN{0x80000000};

// Peer conection (0x0xx)
constexpr const mrsResult MRS_E_INVALID_PEER_HANDLE{0x80000001};
constexpr const mrsResult MRS_E_PEER_NOT_INITIALIZED{0x80000002};

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
    mrsVideoCaptureDeviceEnumCallback callback,
    void* callbackUserData,
    mrsVideoCaptureDeviceEnumCompletedCallback completedCallback,
    void* completedCallbackUserData) noexcept;

//
// Peer connection
//

/// Opaque handle to a PeerConnection object.
using PeerConnectionHandle = void*;

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

/// Callback fired when a renegotiation of the current session needs to occur to
/// account for new parameters (e.g. added or removed tracks).
using PeerConnectionRenegotiationNeededCallback =
    void(MRS_CALL*)(void* user_data);

/// Callback fired when a remote track is added to a connection.
using PeerConnectionTrackAddedCallback = void(MRS_CALL*)(void* user_data);

/// Callback fired when a remote track is removed from a connection.
using PeerConnectionTrackRemovedCallback = void(MRS_CALL*)(void* user_data);

/// Callback fired when a local or remote (depending on use) video frame is
/// available to be consumed by the caller, usually for display.
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

using PeerConnectionARGBVideoFrameCallback =
    void(MRS_CALL*)(void* user_data,
                    const void* data,
                    const int stride,
                    const int frame_width,
                    const int frame_height);

using PeerConnectionDataChannelMessageCallback =
    void(MRS_CALL*)(void* user_data, const void* data, const uint64_t size);

using PeerConnectionDataChannelBufferingCallback =
    void(MRS_CALL*)(void* user_data,
                    const uint64_t previous,
                    const uint64_t current,
                    const uint64_t limit);

using PeerConnectionDataChannelStateCallback = void(MRS_CALL*)(void* user_data,
                                                               int state,
                                                               int id);

#if defined(WINUWP)
inline constexpr bool kNoExceptFalseOnUWP = false;
#else
inline constexpr bool kNoExceptFalseOnUWP = true;
#endif

/// Create a peer connection and return a handle to it.
/// On UWP this must be invoked from another thread than the main UI thread.
MRS_API PeerConnectionHandle MRS_CALL mrsPeerConnectionCreate(
    const char** turn_urls,
    const int no_of_urls,
    const char* username,
    const char* credential,
    bool mandatory_receive_video) noexcept(kNoExceptFalseOnUWP);

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

/// Register a callback fired when a renegotiation of the current session needs
/// to occur to account for new parameters (e.g. added or removed tracks).
MRS_API void MRS_CALL mrsPeerConnectionRegisterRenegotiationNeededCallback(
    PeerConnectionHandle peerHandle,
    PeerConnectionRenegotiationNeededCallback callback,
    void* user_data) noexcept;

/// Register a callback fired when a remote track is added to the current peer
/// connection.
MRS_API void MRS_CALL mrsPeerConnectionRegisterTrackAddedCallback(
    PeerConnectionHandle peerHandle,
    PeerConnectionTrackAddedCallback callback,
    void* user_data) noexcept;

/// Register a callback fired when a remote track is removed from the current
/// peer connection.
MRS_API void MRS_CALL mrsPeerConnectionRegisterTrackRemovedCallback(
    PeerConnectionHandle peerHandle,
    PeerConnectionTrackRemovedCallback callback,
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

/// Add a local video track from a local video capture device (webcam) to
/// the collection of tracks to send to the remote peer.
/// |video_device_id| specifies the unique identifier of a video capture
/// device to open, as obtained by enumerating devices with
/// mrsEnumVideoCaptureDevicesAsync(), or null for any device.
/// |enable_mrc| allows enabling Mixed Reality Capture on HoloLens devices, and
/// is otherwise ignored for other video capture devices. On UWP this must be
/// invoked from another thread than the main UI thread.
MRS_API bool MRS_CALL mrsPeerConnectionAddLocalVideoTrack(
    PeerConnectionHandle peerHandle,
    const char* video_device_id,
    const char* video_profile_id,
    bool enable_mrc) noexcept(kNoExceptFalseOnUWP);

/// Add a local audio track from a local audio capture device (microphone) to
/// the collection of tracks to send to the remote peer.
MRS_API bool MRS_CALL
mrsPeerConnectionAddLocalAudioTrack(PeerConnectionHandle peerHandle) noexcept;

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
    int id,             // -1 for auto, >=0 for negotiated
    const char* label,  // optional, can be null or empty string
    bool ordered,
    bool reliable,
    PeerConnectionDataChannelMessageCallback message_callback,
    void* message_user_data,
    PeerConnectionDataChannelBufferingCallback buffering_callback,
    void* buffering_user_data,
    PeerConnectionDataChannelStateCallback state_callback,
    void* state_user_data) noexcept;

MRS_API void MRS_CALL mrsPeerConnectionRemoveLocalVideoTrack(
    PeerConnectionHandle peerHandle) noexcept;

MRS_API void MRS_CALL mrsPeerConnectionRemoveLocalAudioTrack(
    PeerConnectionHandle peerHandle) noexcept;

MRS_API bool MRS_CALL
mrsPeerConnectionRemoveDataChannelById(PeerConnectionHandle peerHandle,
                                       int id) noexcept;

MRS_API bool MRS_CALL
mrsPeerConnectionRemoveDataChannelByLabel(PeerConnectionHandle peerHandle,
                                          const char* label) noexcept;

MRS_API bool MRS_CALL
mrsPeerConnectionSendDataChannelMessage(PeerConnectionHandle peerHandle,
                                        int id,
                                        const void* data,
                                        uint64_t size) noexcept;

/// Add a new ICE candidate received from a signaling service.
MRS_API bool MRS_CALL
mrsPeerConnectionAddIceCandidate(PeerConnectionHandle peerHandle,
                                 const char* sdp_mid,
                                 const int sdp_mline_index,
                                 const char* candidate) noexcept;

/// Create a new JSEP offer to try to establish a connection with a remote peer.
/// This will generate a local offer message, then fire the
/// "LocalSdpReadytoSendCallback" callback, which should send this message via
/// the signaling service to a remote peer.
MRS_API bool MRS_CALL
mrsPeerConnectionCreateOffer(PeerConnectionHandle peerHandle) noexcept;

/// Create a new JSEP answer to a received offer to try to establish a
/// connection with a remote peer. This will generate a local answer message,
/// then fire the "LocalSdpReadytoSendCallback" callback, which should send this
/// message via the signaling service to a remote peer.
MRS_API bool MRS_CALL
mrsPeerConnectionCreateAnswer(PeerConnectionHandle peerHandle) noexcept;

/// Set a remote description received from a remote peer via the signaling
/// service.
MRS_API bool MRS_CALL
mrsPeerConnectionSetRemoteDescription(PeerConnectionHandle peerHandle,
                                      const char* type,
                                      const char* sdp) noexcept;

/// Close a peer connection and free all resources associated with it.
MRS_API void MRS_CALL
mrsPeerConnectionClose(PeerConnectionHandle* peerHandle) noexcept;

//
// SDP utilities
//

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
MRS_API bool MRS_CALL mrsSdpForceCodecs(const char* message,
                                        const char* audio_codec_name,
                                        const char* video_codec_name,
                                        char* buffer,
                                        size_t* buffer_size);

//
// Generic utilities
//

/// Optimized helper to copy a contiguous block of memory.
/// This is equivalent to the standard malloc() function.
MRS_API void MRS_CALL mrsMemCpy(void* dst, const void* src, size_t size);

/// Optimized helper to copy a block of memory with source and destination
/// stride.
MRS_API void MRS_CALL mrsMemCpyStride(void* dst,
                                      int dst_stride,
                                      const void* src,
                                      int src_stride,
                                      int elem_size,
                                      int elem_count);
}
