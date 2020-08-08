# Migration Guide

This guide details the main steps to migrate between major release versions.

## Migrate from 1.x to 2.x

The 2.0 release introduces some significant changes in the API compared to the 1.0 release in order to include support for multiple media tracks per peer connection.

### Old C++ library

Previously in the 1.x release the `Microsoft.MixedReality.WebRTC.Native.dll` module acted both as an implementation DLL for the C# library as well as a C++ library for direct use in end-user C++ applications. This double usage had diverging constraints which were making the internal implementation unnecessarily complex.

Starting from the 2.0 release, this module now exposes a pure C API providing the core implementation of MixedReality-WebRTC. This library can be used from C/C++ programs with ease, as the use of a C API:

- allows use from C programs, which was not previously possible;
- sidesteps C++ complications with DLLs (destructor, template and inlining, _etc._).

The library has been renamed to `mrwebrtc.dll` (`libmrwebrtc.so` on Android) to emphasize this change and the fact there is no C++ library anymore, only a C library.

### C# library

The C# library exposes a transceiver API very similar to the one found in the WebRTC 1.0 standard, and therefore familiarity with that standard helps understanding the API model. The API is not guaranteed to exactly follow the standard, but generally stays pretty close to it.

#### Standalone audio and video sources

Audio and video sources (webcam, microphone, external callback-based source) are now standalone objects not tied to any peer connection. Those objects, called _track sources_, can be reused by many audio and video tracks, which allows usage scenario such as sharing a single webcam or microphone device among multiple peer connections.

- Users must create the audio and video track source objects explicitly, independently of the peer connection.
  - Device-based (microphone) audio track sources are created with the class method [`DeviceAudioTrackSource.CreateAsync()`](xref:Microsoft.MixedReality.WebRTC.DeviceAudioTrackSource.CreateAsync(Microsoft.MixedReality.WebRTC.LocalAudioDeviceInitConfig)).
  - Video track sources are created with class methods such as [`DeviceVideoTrackSource.CreateAsync()`](xref:Microsoft.MixedReality.WebRTC.DeviceVideoTrackSource.CreateAsync(Microsoft.MixedReality.WebRTC.LocalVideoDeviceInitConfig)) for device-based sources (webcam) or [`ExternalVideoTrackSource.CreateFromI420ACallback()`](xref:Microsoft.MixedReality.WebRTC.ExternalVideoTrackSource.CreateFromI420ACallback(Microsoft.MixedReality.WebRTC.I420AVideoFrameRequestDelegate)) for custom callback-based sources.
- Users are owning those track source objects and must ensure they stay alive while in use by any audio or video track, and are properly disposed after use (`IDisposable`).

#### Standalone local track objects

Audio and video tracks are now standalone objects, not owned by a peer connection, and not initially tied to any peer connection but bound to one on first use.

- Users must create the audio and video track objects explicitly, independently of the peer connection. Those tracks are created from a track source (see above).
  - Audio tracks are created with [`LocalAudioTrack.CreateFromSource()`](xref:Microsoft.MixedReality.WebRTC.LocalAudioTrack.CreateFromSource(Microsoft.MixedReality.WebRTC.AudioTrackSource,Microsoft.MixedReality.WebRTC.LocalAudioTrackInitConfig)).
  - Video tracks are created with [`LocalVideoTrack.CreateFromSource()`](xref:Microsoft.MixedReality.WebRTC.LocalVideoTrack.CreateFromSource(Microsoft.MixedReality.WebRTC.VideoTrackSource,Microsoft.MixedReality.WebRTC.LocalVideoTrackInitConfig)).
- Users are owning those track objects and must ensure they stay alive while in use by the peer connection they are bound to on first use (when first assigned to a transceiver; see below), and are properly disposed after use (`IDisposable`).

Note that remote tracks remain owned by the peer connection which created them in response to an SDP offer or answer being received and applied, like in the previous 1.0 API.

#### Transceivers

Previously in the 1.0 API the peer connection was based on an API similar to the track-based API of the pre-standard WebRTC specification. The 2.0 release introduces a different _transceiver_-based API for manipulating audio and video tracks, which is more closely based on the WebRTC 1.0 standard.

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

Migrating from the 1.x release, users typically:

- On the **offering peer**, replace calls to `PeerConnection.AddLocalAudioTrack()` with:
  - a call to [`DeviceAudioTrackSource.CreateAsync()`](xref:Microsoft.MixedReality.WebRTC.DeviceAudioTrackSource.CreateAsync(Microsoft.MixedReality.WebRTC.LocalAudioDeviceInitConfig)) to create the device-based (microphone) audio track source.
  - a call to [`LocalAudioTrack.CreateFromSource()`](xref:Microsoft.MixedReality.WebRTC.LocalAudioTrack.CreateFromSource(Microsoft.MixedReality.WebRTC.AudioTrackSource,Microsoft.MixedReality.WebRTC.LocalAudioTrackInitConfig)) to create the audio track that will bind the microphone audio of that source to the transceiver sending it to the remote peer.
  - a call to [`PeerConnection.AddTransceiver()`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.AddTransceiver(Microsoft.MixedReality.WebRTC.MediaKind,Microsoft.MixedReality.WebRTC.TransceiverInitSettings)) to add an audio transceiver.
  - assigning the [`Transceiver.LocalAudioTrack`](xref:Microsoft.MixedReality.WebRTC.Transceiver.LocalAudioTrack) property to the audio track.
  - setting the [`Transceiver.DesiredDirection`](xref:Microsoft.MixedReality.WebRTC.Transceiver.DesiredDirection) to [`Direction.SendReceive`](xref:Microsoft.MixedReality.WebRTC.Transceiver.Direction.SendReceive) or [`Direction.SendOnly`](xref:Microsoft.MixedReality.WebRTC.Transceiver.Direction.SendOnly) depending on whether they expect to also receive an audio track from the remote peer.
- On the **answering peer**, replace calls to `PeerConnection.AddLocalAudioTrack()` in the same way as on the offering peer, creating a track source and a track. However, do not immediately call [`PeerConnection.AddTransceiver()`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.AddTransceiver(Microsoft.MixedReality.WebRTC.MediaKind,Microsoft.MixedReality.WebRTC.TransceiverInitSettings)), but instead wait for the offer to create the transceiver. This requires some coordination, either implicit (pre-established transceiver order) or explicit (user communication between the 2 peers, for example using data channels), to determine on each peer which transceiver to use for which track. Note that the Unity library uses implicit coordination via media lines (declarative model).
- Proceed similarly for video tracks.

#### Signaling

- SDP messages are now encapsulated in a data structure for clarity ([`SdpMessage`](xref:Microsoft.MixedReality.WebRTC.SdpMessage) and [`IceCandidate`](xref:Microsoft.MixedReality.WebRTC.IceCandidate)).

### Unity integration

Unlike the C# library, which stays close to the WebRTC 1.0 in terms of transceiver behavior, the Unity integration takes a step back and build some convenience features on top of the transceiver API of the C# library. For the user, this avoids having to deal manually with pairing transceivers and tracks, and relying instead on a much simpler declarative model.

Users are encouraged to follow the updated [Unity tutorial](unity/helloworld-unity.md) to understand how to setup a peer connection with this new API.

#### Peer connection

The [`PeerConnection`](xref:Microsoft.MixedReality.WebRTC.Unity.PeerConnection) component now holds a collection of _media lines_, which can be described as a sort of "transceiver intent". These describe the final result the user intend to produce after an SDP session is fully negotiated in terms of transceivers and their attached tracks. The component internally manages adding transceivers when needed to match the user's media line description, as well as creating and destroying local sender tracks when a source is assigned by the user to the media line or removed from it.

#### Signaler

- The `PeerConnection.Signaler` property has been removed; the [`Signaler`](xref:Microsoft.MixedReality.WebRTC.Unity.Signaler) component is now only a helper to creating custom signaling solution, but is not required anymore.
- As a result, the [`Signaler`](xref:Microsoft.MixedReality.WebRTC.Unity.Signaler) component now has a [`Signaler.PeerConnection`](xref:Microsoft.MixedReality.WebRTC.Unity.Signaler.PeerConnection) property which must be set up by the user. This is true in particular for the [`NodeDssSignaler`](xref:Microsoft.MixedReality.WebRTC.Unity.NodeDssSignaler) component which derives from the [`Signaler`](xref:Microsoft.MixedReality.WebRTC.Unity.Signaler) abstract base component.

#### Video

- The `LocalVideoSource` component, which was previously using a webcam to capture video frames, has been renamed into the more explicit [`WebcamSource`](xref:Microsoft.MixedReality.WebRTC.Unity.WebcamSource) component.
- The [`WebcamSource`](xref:Microsoft.MixedReality.WebRTC.Unity.WebcamSource) component derives from the abstract [`VideoTrackSource`](xref:Microsoft.MixedReality.WebRTC.Unity.VideoTrackSource) component, which is also the base class for the callback-based sources like the [`SceneVideoSource`](xref:Microsoft.MixedReality.WebRTC.Unity.SceneVideoSource) component  (previously: `SceneVideoSender`) which captures its video frame from the rendering of any Unity Camera component.
- The `RemoteVideoSource` component has been renamed into the [`VideoReceiver`](xref:Microsoft.MixedReality.WebRTC.Unity.VideoReceiver) component.
- The `VideoSource` component has been split into [`VideoTrackSource`](xref:Microsoft.MixedReality.WebRTC.Unity.VideoTrackSource) for local sources and [`VideoReceiver`](xref:Microsoft.MixedReality.WebRTC.Unity.VideoReceiver) for remote sources.
- The `MediaPlayer` component has been renamed into the [`VideoRenderer`](xref:Microsoft.MixedReality.WebRTC.Unity.VideoRenderer) component for clarity, has it only deal with video rendering and not audio. This also prevent a name collision with the Unity built-in component. This component can render any video source exposing some method taking an [`IVideoSource`](xref:Microsoft.MixedReality.WebRTC.IVideoSource), and most notably [`VideoTrackSource`](xref:Microsoft.MixedReality.WebRTC.Unity.VideoTrackSource) (local video) and  [`VideoReceiver`](xref:Microsoft.MixedReality.WebRTC.Unity.VideoReceiver) (remote video).

#### Audio

- The `LocalAudioSource` component, which was previously using a microphone to capture audio frames, as been renamed into the more explicit [`MicrophoneSource`](xref:Microsoft.MixedReality.WebRTC.Unity.MicrophoneSource) component.
- The [`MicrophoneSource`](xref:Microsoft.MixedReality.WebRTC.Unity.MicrophoneSource) component derives from the abstract [`AudioTrackSource`](xref:Microsoft.MixedReality.WebRTC.Unity.AudioTrackSource) component to enable future customizing usages and for symmetry with video.
- The `RemoteAudioSource` component has been renamed into the [`AudioReceiver`](xref:Microsoft.MixedReality.WebRTC.Unity.AudioReceiver) component. This component now forwards its audio to a local [`Unity.AudioSource`](https://docs.unity3d.com/ScriptReference/AudioSource.html) component on the same [`GameObject`](https://docs.unity3d.com/ScriptReference/GameObject.html), which allows the audio to be injected into the Unity DSP pipeline and mixed with other audio sources from the scene. This in particular enables usage scenarios such as _spatial audio_ (3D localization of audio sources for increased audio immersion).
- The `AudioSource` component has been split into [`AudioTrackSource`](xref:Microsoft.MixedReality.WebRTC.Unity.AudioTrackSource) for local sources and [`AudioReceiver`](xref:Microsoft.MixedReality.WebRTC.Unity.AudioReceiver) for remote sources.
