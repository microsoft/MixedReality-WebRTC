# C# `PeerConnection` class

The [`PeerConnection`](xref:Microsoft.MixedReality.WebRTC.PeerConnection) class is the entry point to using WebRTC. It encapsulates a connection between a local peer on the local physical device, and a remote peer on the same or, more generally, another physical device.

> [!NOTE]
> The Unity integration also has a [`PeerConnection`](xref:Microsoft.MixedReality.WebRTC.Unity.PeerConnection) component, which is based on this class.

## Initialization

A [`PeerConnection`](xref:Microsoft.MixedReality.WebRTC.PeerConnection) instance can be created with the default constructor, but initially most of its properties, methods, and events, cannot be used until it is initialized with a call to [`InitializeAsync()`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.InitializeAsync*). Once intialized, the  [`Initialized`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.Initialized) property returns `true` and it is safe to use the peer connection.

[`InitializeAsync()`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.InitializeAsync*) takes a [`PeerConnectionConfiguration`](xref:Microsoft.MixedReality.WebRTC.PeerConnectionConfiguration) and a [`CancellationToken`](xref:System.Threading.CancellationToken). The former allows configuring the peer connection about to be established, while the later allows cancelling that task while it is being processed.

[`PeerConnectionConfiguration`](xref:Microsoft.MixedReality.WebRTC.PeerConnectionConfiguration) contains several fields, but the most important are:

- [`IceServers`](xref:Microsoft.MixedReality.WebRTC.PeerConnectionConfiguration.IceServers) contains an optional collection of STUN and/or TURN servers used by the Interactive Connectivity Establishment (ICE) to establish a connection with the remote peer through routing devices (NATs). Without these, only direct connections to the remote peer can be established, which can be enough if the application knows in advance the network topology surrounding the two peers, but is generally recommended otherwise to increase the chances of establishing a connection over the Internet.
- [`SdpSemantic`](xref:Microsoft.MixedReality.WebRTC.PeerConnectionConfiguration.SdpSemantic) describes the semantic used by the Session Description Protocol (SDP) while trying to establish a connection. This is a compatibility feature, which allows connecting with older peers supporting only the deprecated _Plan B_ semantic. New code should always use the default _Unified Plan_, which is the only one accepted by the WebRTC 1.0 standard.

The other fields are for advanced use, and can be left with their default values.

Once the connection is initialized, two categories of actions can be performed, in any order:

- Attempt to establish a connection with the remote peer.
- Add some media tracks and/or data channels.

Media (audio and/or video) tracks and data channels added before the connection is established will be available immediately on connection. Other tracks and channels can be added later during the lifetime of the peer connection, while it remains open.

## Events

A [`PeerConnection`](xref:Microsoft.MixedReality.WebRTC.PeerConnection) exposes several categories of events:

- **Signaling events** are related to establishing a connection to the remote peer.
- **Media and data channel events** provide informational notifications about adding and removing transceivers, media tracks, and data channels.

In general it is strongly recommended to subscribe to these events before starting to establish a connection to the remote peer.

### Signaling events

| Event | Status* | Description |
|---|---|---|
| [`LocalSdpReadytoSend`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.LocalSdpReadytoSend) | Mandatory | Raised when the local peer finished crafting an SDP message, to request the user to send that message to the remote peer via its chosen signaling solution. Failing to handle this event prevents the peer-to-peer connection from establishing. |
| <span style="white-space: pre; word-break: break-word">[`IceCandidateReadytoSend`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.IceCandidateReadytoSend)</span> | Mandatory | Raised when the local peer finished crafting an ICE candidate message, to request the user to send that message to the remote peer via its chosen signaling solution. Failing to handle this event generally prevents the peer-to-peer connection from establishing. |
| [`Connected`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.Connected) | Recommended |Raised when the connection with the remote peer is established. This indicates that tracks and channel are ready for use, although ICE can continue its discovery in the background. This does **not** indicate that the session negotiation is completed yet; this event is raised on the answering peer as soon as it creates an answer and applies it, before it is sent to the remote peer, while it is only raised later on the offering peer when that answer is actually received and applied. |
| [`RenegotiationNeeded`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.IceCandidateReadytoSend) | Recommended | Raised when the peer connection detected that the current session is obsolete, and needs to be renegotiated. This is generally the result of some media tracks or data channels being added or removed, and **must** be handled to make those changes available to the remote peer. However, it is perfectly acceptable to ignore **some** of those events, if several changes are expected in a short period of time, to avoid triggering multiple unnecessary renegotiations. However a renegotiation eventually needs to happen for the newly added tracks and channel to become open. |
| [`IceStateChanged`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.IceStateChanged) | Optional | Raised when the state of the local ICE connection changed. |
| [`IceGatheringStateChanged`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.IceGatheringStateChanged) | Optional | Raised when the state of the ICE candidate gathering process changed. |

*_Status_ indicates the recommended subscription status for a working peer connection:

- **Mandatory** means those events must be handled, otherwise the connection cannot be established.
- **Recommended** means those events are typically handled, although not mandatory.
- **Optional** means those events are informational only, and it is entirely optional to subscribe to them.

### Media and data channel events

| Event | Status* | Description |
|---|---|---|
| [`TransceiverAdded`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.TransceiverAdded) | Optional | Raised when a transceiver was added to the peer connection, either manually by calling [`AddTransceiver()`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.AddTransceiver(Microsoft.MixedReality.WebRTC.MediaKind,Microsoft.MixedReality.WebRTC.TransceiverInitSettings)), or automatically by the implementation when a remote session description is applied with [`SetRemoteDescriptionAsync()`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.SetRemoteDescriptionAsync(Microsoft.MixedReality.WebRTC.SdpMessage)) and the remote peer added a new transceiver. |
| [`AudioTrackAdded`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.AudioTrackAdded) | Optional | Raised when an audio track was added on the remote peer, and received on the local peer after a session negotiation. This only concerns remotely-created tracks. Tracks whose creation was initied locally with _e.g._ [`LocalAudioTrack.CreateFromDeviceAsync()`](xref:Microsoft.MixedReality.WebRTC.LocalAudioTrack.CreateFromDeviceAsync(Microsoft.MixedReality.WebRTC.LocalAudioTrackSettings)) do not raise the [`AudioTrackAdded`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.AudioTrackAdded) event. |
| [`AudioTrackRemoved`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.AudioTrackRemoved) | Optional | Invoked when an audio track was removed on the remote peer, and a remove message was received on the local peer after a session negotiation. This only concerns remote tracks. Local tracks do not raise the [`AudioTrackRemoved`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.AudioTrackRemoved) event. |
| [`VideoTrackAdded`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.VideoTrackAdded) | Optional | Invoked when a video track was added on the remote peer, and received on the local peer after a session negotiation. This only concerns remotely-created tracks. Tracks whose creation was initied locally with _e.g._ [`LocalVideoTrack.CreateFromDeviceAsync()`](xref:Microsoft.MixedReality.WebRTC.LocalVideoTrack.CreateFromDeviceAsync(Microsoft.MixedReality.WebRTC.LocalVideoTrackSettings)) do not raise the [`VideoTrackAdded`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.VideoTrackAdded) event. |
| [`VideoTrackRemoved`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.VideoTrackRemoved) | Optional | Invoked when a video track was removed on the remote peer, and a remove message was received on the local peer after a session negotiation. This only concerns remote tracks. Local tracks do not raise the [`VideoTrackRemoved`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.VideoTrackRemoved) event. |
| [`DataChannelAdded`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.DataChannelAdded) | Optional | Invoked when a data channel was added to the local peer connection. This is always fired, irrelevant of how the data channel was initially created (in-band or out-of-band). |
| <span style="white-space: pre; word-break: break-word">[`DataChannelRemoved`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.DataChannelRemoved)</span> | Optional | Invoked when a data channel was removed from the local peer connection. This is always fired, irrelevant of how the data channel was initially created (in-band or out-of-band). |

*_Status_ - See above.

<!-- ### Frame events

| Event | Status* | Description |
|---|---|---|
| [`LocalVideoTrack.I420AVideoFrameReady`](xref:Microsoft.MixedReality.WebRTC.LocalVideoTrack.I420AVideoFrameReady) | Optional | _(Note that this event was moved to LocalVideoTrack)_ Invoked when a video frame, encoded in I420 format, has been captured by a local video capture device and is ready to be rendered locally. This is optional, as some application will chose to only transmit local video frames to the remote peer without rendering them, whereas others like a video chat application will often provide a feedback to the user showing the video from a local webcam. |
| [`I420ARemoteVideoFrameReady`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.I420ARemoteVideoFrameReady) | Optional | Invoked when a video frame, encoded in I420 format, has been received from the remote peer and is ready to be rendered locally. This is optional, as the local peer does not have control on video tracks added by the remote peer, although most applications will genrally want to render those frames (or do any other kind of procesing on them). |
| [`LocalVideoTrack.Argb32VideoFrameReady`](xref:Microsoft.MixedReality.WebRTC.LocalVideoTrack.Argb32VideoFrameReady) | Optional | _(Note that this event was moved to LocalVideoTrack)_ Variant of [`I420AVideoFrameReady`](xref:Microsoft.MixedReality.WebRTC.LocalVideoTrack.I420AVideoFrameReady) where the frame is encoded in raw ARGB 32-bits-per-pixel format. This generally requires an extra conversion from I420, performed internally. |
| <span style="white-space: pre; word-break: break-word">[`Argb32RemoteVideoFrameReady`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.Argb32RemoteVideoFrameReady)</span> | Optional | Variant of [`I420ARemoteVideoFrameReady`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.I420ARemoteVideoFrameReady) where the frame is encoded in raw ARGB 32-bits-per-pixel format. This generally requires an extra conversion from I420, performed internally. |

*_Status_ - See above.

> [!NOTE]
> It is generally recommended to use the I420 callbacks instead of the ARGB ones. Even in situations where the processing (_e.g._ local rendering) requires ARGB frames, I420 frames are smaller thanks to the chroma downsampling, so faster to upload to GPU. And GPU-based I420-to-ARGB conversion via a custom pixel shader (fragment shader) is more efficient than the CPU conversion provided by the ARGB callbacks, even with the use of SIMD. See [`YUVFeedShaderUnlit.shader`](https://github.com/microsoft/MixedReality-WebRTC/blob/master/libs/Microsoft.MixedReality.WebRTC.Unity/Assets/Microsoft.MixedReality.WebRTC.Unity/Shaders/YUVFeedShaderUnlit.shader) in the Unity integration for an example of such conversion shader. -->
