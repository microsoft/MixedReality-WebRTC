# Unity `RemoteAudioSource` component

The [`RemoteAudioSource`](xref:Microsoft.MixedReality.WebRTC.Unity.RemoteAudioSource) Unity component represents a single audio track received from the remote peer through an established peer connection.

![The RemoteAudioSource Unity component](unity-remoteaudiosource.png)

> [!Important]
> FIXME: This component is not currently functional. The remote audio data is currently sent directly to the local audio out device by the internal WebRTC implementation without any configuration possible. See issue [#92](https://github.com/microsoft/MixedReality-WebRTC/issues/92) for details.

| Property | Description |
|---|---|
| **Audio track** | |
| PeerConnection | Reference to the [`PeerConnection`](xref:Microsoft.MixedReality.WebRTC.Unity.PeerConnection) instance which contains the remote audio track. |
| AutoPlayOnAdded | Automatically start playback of the audio track when added. This corresponds to registering an audio frame callback with the [`PeerConnection`](xref:Microsoft.MixedReality.WebRTC.Unity.PeerConnection) instance pointed by the [`RemoteAudioSource.PeerConnection`](xref:Microsoft.MixedReality.WebRTC.Unity.RemoteAudioSource.PeerConnection) property. |
| AudioTrackAdded | FIXME: This event is not currently fired. |
| AudioTrackRemoved | FIXME: This event is not currently fired. |
| **Events** | |
| AudioStreamStarted | FIXME: This event is not currently fired. |
| AudioStreamStopped | FIXME: This event is not currently fired. |
