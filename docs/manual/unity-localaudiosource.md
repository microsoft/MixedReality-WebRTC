# Unity `LocalAudioSource` component

The [`LocalAudioSource`](xref:Microsoft.MixedReality.WebRTC.Unity.LocalAudioSource) Unity component represents a single audio track obtaining its data from a local audio capture device. The component controls both the capture device and the track it feeds.

![The LocalAudioSource Unity component](unity-localaudiosource.png)

| Property | Description |
|---|---|
| **Audio track** | |
| PeerConnection | Reference to the [`PeerConnection`](xref:Microsoft.MixedReality.WebRTC.Unity.PeerConnection) instance an audio track is added to. |
| AutoAddTrack | Automatically start audio capture and add an audio track to the [`PeerConnection`](xref:Microsoft.MixedReality.WebRTC.Unity.PeerConnection) instance pointed by the [`LocalAudioSource.PeerConnection`](xref:Microsoft.MixedReality.WebRTC.Unity.LocalAudioSource.PeerConnection) property once the peer connection is initialized. See [`PeerConnection.OnInitialized`](xref:Microsoft.MixedReality.WebRTC.Unity.PeerConnection.OnInitialized). |
| **Local audio capture** | |
| AutoStartCapture | Automatically start audio capture once the [`PeerConnection`](xref:Microsoft.MixedReality.WebRTC.Unity.PeerConnection) instance pointed by the [`LocalAudioSource.PeerConnection`](xref:Microsoft.MixedReality.WebRTC.Unity.LocalAudioSource.PeerConnection) property is initialized. See [`PeerConnection.OnInitialized`](xref:Microsoft.MixedReality.WebRTC.Unity.PeerConnection.OnInitialized).. |
| PreferredAudioCodec | Optional choice of a preferred audio codec to use for local audio capture and SDP offering. This is implemented by filtering out all other audio codecs when sending an SDP offer message if the original offer was already containing that codec. |
| **Events** | |
| AudioStreamStarted | Event invoked after the local audio stream started. |
| AudioStreamStopped | Event invoked before the local audio stream is stopped. |
