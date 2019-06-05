// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#pragma once

#if defined(MR_SHARING_WIN)
#define MRS_API __declspec(dllexport)
#elif defined(MR_SHARING_ANDROID)
#define MRS_API __attribute__((visibility("default")))
#endif

#if defined(WINUWP)
// Non-API helper. Returned object can be deleted at any time in theory.
// In practice because it's provided by a global object it's safe.
//< TODO - Remove that, clean-up API, this is bad (c).
rtc::Thread* UnsafeGetWorkerThread();
#endif

extern "C" {

//
// Generic utilities
//

/// Opaque enumerator type.
struct mrsEnumerator;

/// Handle to an enumerator.
/// This must be freed after use with |mrsCloseEnum|.
using mrsEnumHandle = mrsEnumerator*;

/// Close an enumerator previously obtained from one of the EnumXxx() calls.
MRS_API void mrsCloseEnum(mrsEnumHandle* handleRef) noexcept;

//
// Video capture enumeration
//

/// Callback invoked for each enumerated video capture device.
using mrsVideoCaptureDeviceEnumCallback = void (*)(const char* id,
                                                   const char* name,
                                                   void* user_data);

/// Callback invoked on video capture device enumeration completed.
using mrsVideoCaptureDeviceEnumCompletedCallback = void (*)(void* user_data);

/// Enumerate the video capture devices asynchronously.
/// For each device found, invoke the mandatory |callback|.
/// At the end of the enumeration, invoke the optional |completedCallback| if it
/// was provided (non-null).
MRS_API void mrsEnumVideoCaptureDevicesAsync(
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
using PeerConnectionConnectedCallback = void (*)(void* user_data);

/// Callback fired when a local SDP message has been prepared and is ready to be
/// sent by the user via the signaling service.
using PeerConnectionLocalSdpReadytoSendCallback =
    void (*)(void* user_data, const char* type, const char* sdp_data);

/// Callback fired when an ICE candidate has been prepared and is ready to be
/// sent by the user via the signaling service.
using PeerConnectionIceCandidateReadytoSendCallback =
    void (*)(void* user_data,
             const char* candidate,
             int sdpMlineindex,
             const char* sdpMid);

/// Callback fired when a renegotiation of the current session needs to occur to
/// account for new parameters (e.g. added or removed tracks).
using PeerConnectionRenegotiationNeededCallback = void (*)(void* user_data);

/// Callback fired when a local or remote (depending on use) video frame is
/// available to be consumed by the caller, usually for display.
using PeerConnectionI420VideoFrameCallback =
    void (*)(void* user_data,
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

using PeerConnectionARGBVideoFrameCallback = void (*)(void* user_data,
                                                      const void* data,
                                                      const int stride,
                                                      const int frame_width,
                                                      const int frame_height);

using PeerConnectionDataChannelMessageCallback = void (*)(void* user_data,
                                                          const void* data,
                                                          const uint64_t size);

using PeerConnectionDataChannelBufferingCallback =
    void (*)(void* user_data,
             const uint64_t previous,
             const uint64_t current,
             const uint64_t limit);

using PeerConnectionDataChannelStateCallback = void (*)(void* user_data,
                                                        int state,
                                                        int id);

#if defined(WINUWP)
inline constexpr bool kNoExceptFalseOnUWP = false;
#else
inline constexpr bool kNoExceptFalseOnUWP = true;
#endif

/// Create a peer connection and return a handle to it.
/// On UWP this must be invoked from another thread than the main UI thread.
MRS_API PeerConnectionHandle mrsPeerConnectionCreate(
    const char** turn_urls,
    const int no_of_urls,
    const char* username,
    const char* credential,
    bool mandatory_receive_video) noexcept(kNoExceptFalseOnUWP);

/// Register a callback fired once connected to a remote peer.
/// To unregister, simply pass nullptr as the callback pointer.
MRS_API void mrsPeerConnectionRegisterConnectedCallback(
    PeerConnectionHandle peerHandle,
    PeerConnectionConnectedCallback callback,
    void* user_data) noexcept;

/// Register a callback fired when a local message is ready to be sent via the
/// signaling service to a remote peer.
MRS_API void mrsPeerConnectionRegisterLocalSdpReadytoSendCallback(
    PeerConnectionHandle peerHandle,
    PeerConnectionLocalSdpReadytoSendCallback callback,
    void* user_data) noexcept;

/// Register a callback fired when an ICE candidate message is ready to be sent
/// via the signaling service to a remote peer.
MRS_API void mrsPeerConnectionRegisterIceCandidateReadytoSendCallback(
    PeerConnectionHandle peerHandle,
    PeerConnectionIceCandidateReadytoSendCallback callback,
    void* user_data) noexcept;

/// Register a callback fired when a renegotiation of the current session needs
/// to occur to account for new parameters (e.g. added or removed tracks).
MRS_API void mrsPeerConnectionRegisterRenegotiationNeededCallback(
    PeerConnectionHandle peerHandle,
    PeerConnectionRenegotiationNeededCallback callback,
    void* user_data) noexcept;

/// Register a callback fired when a video frame is available from a local video
/// track, usually from a local video capture device (local webcam).
MRS_API void mrsPeerConnectionRegisterI420LocalVideoFrameCallback(
    PeerConnectionHandle peerHandle,
    PeerConnectionI420VideoFrameCallback callback,
    void* user_data) noexcept;

/// Register a callback fired when a video frame is available from a local video
/// track, usually from a local video capture device (local webcam).
MRS_API void mrsPeerConnectionRegisterARGBLocalVideoFrameCallback(
    PeerConnectionHandle peerHandle,
    PeerConnectionARGBVideoFrameCallback callback,
    void* user_data) noexcept;

/// Register a callback fired when a video frame from a video track was received
/// from the remote peer.
MRS_API void mrsPeerConnectionRegisterI420RemoteVideoFrameCallback(
    PeerConnectionHandle peerHandle,
    PeerConnectionI420VideoFrameCallback callback,
    void* user_data) noexcept;

/// Register a callback fired when a video frame from a video track was received
/// from the remote peer.
MRS_API void mrsPeerConnectionRegisterARGBRemoteVideoFrameCallback(
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
MRS_API bool mrsPeerConnectionAddLocalVideoTrack(
    PeerConnectionHandle peerHandle,
    const char* video_device_id,
    bool enable_mrc) noexcept(kNoExceptFalseOnUWP);

/// Add a local audio track from a local audio capture device (microphone) to
/// the collection of tracks to send to the remote peer.
MRS_API bool mrsPeerConnectionAddLocalAudioTrack(
    PeerConnectionHandle peerHandle) noexcept;

/// Add a new data channel.
/// This function has two distinct uses:
/// - If id < 0, then it adds a new in-band data channel with an ID that will be
/// selected by the WebRTC implementation itself, and will be available later.
/// In that case the channel is announced to the remote peer for it to create a
/// channel with the same ID.
/// - If id >= 0, then it adss a new out-of-band negotiated channel with the
/// given ID, and it is the responsibility of the app to create a channel with
/// the same ID on the remote peer to be able to use the channel.
MRS_API bool mrsPeerConnectionAddDataChannel(
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

MRS_API void mrsPeerConnectionRemoveLocalVideoTrack(
    PeerConnectionHandle peerHandle) noexcept;

MRS_API void mrsPeerConnectionRemoveLocalAudioTrack(
    PeerConnectionHandle peerHandle) noexcept;

MRS_API bool mrsPeerConnectionRemoveDataChannelById(
    PeerConnectionHandle peerHandle,
    int id) noexcept;

MRS_API bool mrsPeerConnectionRemoveDataChannelByLabel(
    PeerConnectionHandle peerHandle,
    const char* label) noexcept;

MRS_API bool mrsPeerConnectionSendDataChannelMessage(
    PeerConnectionHandle peerHandle,
    int id,
    const void* data,
    uint64_t size) noexcept;

/// Add a new ICE candidate received from a signaling service.
MRS_API bool mrsPeerConnectionAddIceCandidate(PeerConnectionHandle peerHandle,
                                              const char* sdp_mid,
                                              const int sdp_mline_index,
                                              const char* candidate) noexcept;

/// Create a new JSEP offer to try to establish a connection with a remote peer.
/// This will generate a local offer message, then fire the
/// "LocalSdpReadytoSendCallback" callback, which should send this message via
/// the signaling service to a remote peer.
MRS_API bool mrsPeerConnectionCreateOffer(
    PeerConnectionHandle peerHandle) noexcept;

/// Create a new JSEP answer to a received offer to try to establish a
/// connection with a remote peer. This will generate a local answer message,
/// then fire the "LocalSdpReadytoSendCallback" callback, which should send this
/// message via the signaling service to a remote peer.
MRS_API bool mrsPeerConnectionCreateAnswer(
    PeerConnectionHandle peerHandle) noexcept;

/// Set a remote description received from a remote peer via the signaling
/// service.
MRS_API bool mrsPeerConnectionSetRemoteDescription(
    PeerConnectionHandle peerHandle,
    const char* type,
    const char* sdp) noexcept;

/// Close a peer connection and free all resources associated with it.
MRS_API void mrsPeerConnectionClose(PeerConnectionHandle* peerHandle) noexcept;
}
