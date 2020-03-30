# Migration Guide

This guide details the main steps to migrate between major release versions.

## Migrate from 1.x to 2.x

The 2.0 release introduces some significant changes in the API compared to the 1.0 release in order to include support for multiple media tracks per peer connection.

### C# library

The C# library exposes a transceiver API very similar to the one found in the WebRTC 1.0 standard, and therefore familiarity with that standard helps understanding the API model. The API is not guaranteed to exactly follow the standard, but generally stays pretty close to it.

#### Standalone track objects

Audio and video tracks are now standalone objects, not tied to a peer connection.

- Users must create the audio and video track objects explicitly, independently of the peer connection.
  - Audio tracks are created with class methods such as [`LocalAudioTrack.CreateFromDeviceAsync()`](xref:Microsoft.MixedReality.WebRTC.LocalAudioTrack.CreateFromDeviceAsync(Microsoft.MixedReality.WebRTC.LocalAudioTrackSettings)).
  - Video tracks are created with class methods such as [`LocalVideoTrack.CreateFromDeviceAsync()`](xref:Microsoft.MixedReality.WebRTC.LocalVideoTrack.CreateFromDeviceAsync(Microsoft.MixedReality.WebRTC.LocalVideoTrackSettings)).
- Users are owning those track objects and must ensure they stay alive while in use by the peer connection, and are properly disposed after use (`IDisposable`).

#### Transceivers

Previously in 1.0 the peer connection was based on an API similar to the track-based API of the pre-standard WebRTC specification. The 2.0 release introduces a different _transceiver_-based API for manipulating audio and video track, which is more closely based on the WebRTC 1.0 standard.

- A _transceiver_ is a "media pipe" in charge of the encoding and transport of some audio or video tracks.
- Each transceiver has a media kind (audio **or** video), and a sender track slot and a receiver track slot. Audio tracks can be attached to _audio transceivers_ (transceivers with a [`Transceiver.MediaKind`](xref:Microsoft.MixedReality.WebRTC.Transceiver.MediaKind) property equal to [`MediaKind.Audio`](xref:Microsoft.MixedReality.WebRTC.MediaKind.Audio)). Conversely, video tracks can be attached to _video transceivers_ ([`MediaKind.Video`](xref:Microsoft.MixedReality.WebRTC.MediaKind.Video)).
- An empty sender track slot on a transceiver makes it send (if its direction include sending) empty data, that is black frames for video or silence for audio. An empty receiver track slot on a transceiver means the received media data, if any (depends on direction), is discarded by the implementation.
- A peer connection owns an ordered collection of audio and video transceivers. Users must create a transceiver with [`PeerConnection.AddTransceiver()`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.AddTransceiver(Microsoft.MixedReality.WebRTC.MediaKind,Microsoft.MixedReality.WebRTC.TransceiverInitSettings)). Transceivers cannot be removed; they stay attached to the peer connection until that peer connection is destroyed.
- Transceivers have a media _direction_ which indicates if they are currently sending and/or receiving media from the remote peer. This direction can be set by the user by changing the [`Transceiver.DesiredDirection`](xref:Microsoft.MixedReality.WebRTC.Transceiver.DesiredDirection) property.
- Changing a transceiver direction requires an SDP session renegotiation, and therefore changing the value of the [`Transceiver.DesiredDirection`](xref:Microsoft.MixedReality.WebRTC.Transceiver.DesiredDirection) property raises a [`PeerConnection.RenegotiationNeeded`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.RenegotiationNeeded) event. After the session has been renegotiated, the negotiated direction can be read from the [`Transceiver.NegotiatedDirection`](xref:Microsoft.MixedReality.WebRTC.Transceiver.NegotiatedDirection) read-only property.
- Media tracks are attached to and removed from transceivers. Unlike in 1.0, **this does not require any session negotiation**. Tracks can be transparently (from the point of view of the session) attached to a transceiver, detached from it, attached to a different transceiver, _etc._ without any of these raising a [`PeerConnection.RenegotiationNeeded`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.RenegotiationNeeded) event.

A typical workflow with the transceiver API is as follow:

1. The **offering peer** creates some transceivers and create an SDP offer.
2. The offer is sent to the **answering peer**.
3. The **answering peer** accepts the offer; this automatically creates the transceivers present in the offer that the **offering peer** created in step 1.
4. The **answering peer** optionally add more transceivers beyond the ones already existing.
5. The **answering peer** creates an SDP answer.
6. The answer is sent back to the **offering peer**.
7. The **offering peer** accepts the answer; this automatically creates any additional transceiver that the **answering peer** added in step 4.

Migrating from the 1.0 release, users typically:

- On the **offering peer**, replace calls to `PeerConnection.AddLocalAudioTrack()` with:
  - a call to [`LocalAudioTrack.CreateFromDeviceAsync()`](xref:Microsoft.MixedReality.WebRTC.LocalAudioTrack.CreateFromDeviceAsync(Microsoft.MixedReality.WebRTC.LocalAudioTrackSettings)) to create the audio track.
  - a call to [`PeerConnection.AddTransceiver()`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.AddTransceiver(Microsoft.MixedReality.WebRTC.MediaKind,Microsoft.MixedReality.WebRTC.TransceiverInitSettings)) to add an audio transceiver.
  - assigning the [`Transceiver.LocalAudioTrack`](xref:Microsoft.MixedReality.WebRTC.Transceiver.LocalAudioTrack) property to the audio track.
  - setting the [`Transceiver.DesiredDirection`](xref:Microsoft.MixedReality.WebRTC.Transceiver.DesiredDirection) to [`Direction.SendReceive`](xref:Microsoft.MixedReality.WebRTC.Transceiver.Direction.SendReceive) or [`Direction.SendOnly`](xref:Microsoft.MixedReality.WebRTC.Transceiver.Direction.SendOnly) depending on whether they expect to also receive an audio track from the remote peer.
- On the **answering peer**, replace calls to `PeerConnection.AddLocalAudioTrack()` with a call to [`LocalAudioTrack.CreateFromDeviceAsync()`](xref:Microsoft.MixedReality.WebRTC.LocalAudioTrack.CreateFromDeviceAsync(Microsoft.MixedReality.WebRTC.LocalAudioTrackSettings)) to create the audio track. However, do not immediately call [`PeerConnection.AddTransceiver()`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.AddTransceiver(Microsoft.MixedReality.WebRTC.MediaKind,Microsoft.MixedReality.WebRTC.TransceiverInitSettings)), but instead wait for the offer to create the transceiver. This requires some coordination, either implicit (pre-established transceiver order) or explicit (user communication between the 2 peers, for example using data channels), to determine on each peer which transceiver to use for which track.
- Proceed similarly for video tracks.

### Unity integration

Unlike the C# library, the Unity integration takes a step back and build some convenience features on top of the transceiver API of the C# library. For the user, this avoids having to deal manually with pairing transceivers and tracks, and relying instead on a much simpler declarative model.

Users are encouraged to follow the updated [Unity tutorial](helloworld-unity.md) to understand how to setup a peer connection with this new API.

#### Peer connection

The [`PeerConnection`](xref:Microsoft.MixedReality.WebRTC.Unity.PeerConnection) component now holds a collection of _media lines_, which can be described as a sort of "transceiver intent". These describe the final result the user intend to produce after an SDP session is fully negotiated in terms of transceivers and their attached tracks. The component internally manages adding transceivers when needed to match the user's media line description.

#### Signaling

- The `PeerConnection.Signaler` property has been removed; the [`Signaler`](xref:Microsoft.MixedReality.WebRTC.Unity.Signaler) component is now only a helper to creating custom signaling solution, but is not required anymore.
- As a result, the [`Signaler`](xref:Microsoft.MixedReality.WebRTC.Unity.Signaler) component now has a [`Signaler.PeerConnection`](xref:Microsoft.MixedReality.WebRTC.Unity.Signaler.PeerConnection) property which must be set up by the user. This is true in particular for the [`NodeDssSignaler`](xref:Microsoft.MixedReality.WebRTC.Unity.NodeDssSignaler) component which derives from the [`Signaler`](xref:Microsoft.MixedReality.WebRTC.Unity.Signaler) abstract base component.

#### Video

- The `LocalVideoSource` component, which was previously using a webcam to capture video frames, as been renamed into the more explicit [`WebcamSource`](xref:Microsoft.MixedReality.WebRTC.Unity.WebcamSource) component.
- The [`WebcamSource`](xref:Microsoft.MixedReality.WebRTC.Unity.WebcamSource) component derives from the abstract [`VideoSender`](xref:Microsoft.MixedReality.WebRTC.Unity.VideoSender) component, which is also the base class for the callback-based sources like the [`SceneVideoSender`](xref:Microsoft.MixedReality.WebRTC.Unity.SceneVideoSender) component which captures its video frame from the rendering of any Unity Camera component.
- The `RemoteVideoSource` component has been renamed into the [`VideoReceiver`](xref:Microsoft.MixedReality.WebRTC.Unity.VideoReceiver) component.
- The `VideoSource` component is now an interface :  [`IVideoSource`](xref:Microsoft.MixedReality.WebRTC.Unity.IVideoSource).

#### Audio

- The `LocalAudioSource` component, which was previously using a microphone to capture audio frames, as been renamed into the more explicit [`MicrophoneSource`](xref:Microsoft.MixedReality.WebRTC.Unity.MicrophoneSource) component.
- The [`MicrophoneSource`](xref:Microsoft.MixedReality.WebRTC.Unity.MicrophoneSource) component derives from the abstract [`AudioSender`](xref:Microsoft.MixedReality.WebRTC.Unity.AudioSender) component to enable future customizing usages.
- The `RemoteAudioSource` component has been renamed into the [`AudioReceiver`](xref:Microsoft.MixedReality.WebRTC.Unity.AudioReceiver) component.
- The `AudioSource` component is now an interface :  [`IAudioSource`](xref:Microsoft.MixedReality.WebRTC.Unity.IAudioSource).
