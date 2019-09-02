# Unity `LocalVideoSource` component

The [`LocalVideoSource`](xref:Microsoft.MixedReality.WebRTC.Unity.LocalVideoSource) Unity component represents a single video track obtaining its frames from a local video capture device. The component controls both the capture device and the track it feeds.

![The LocalVideoSource Unity component](unity-localvideosource.png)

| Property | Description |
|---|---|
| **Video track** | |
| PeerConnection | Reference to the [`PeerConnection`](xref:Microsoft.MixedReality.WebRTC.Unity.PeerConnection) instance a video track is added to. |
| AutoAddTrack | Automatically start video capture and add a video track to the [`PeerConnection`](xref:Microsoft.MixedReality.WebRTC.Unity.PeerConnection) instance pointed by the [`LocalVideoSource.PeerConnection`](xref:Microsoft.MixedReality.WebRTC.Unity.LocalVideoSource.PeerConnection) property once the peer connection is initialized. See [`PeerConnection.OnInitialized`](xref:Microsoft.MixedReality.WebRTC.Unity.PeerConnection.OnInitialized). |
| **Local video capture** | |
| AutoStartCapture | Automatically start video capture once the [`PeerConnection`](xref:Microsoft.MixedReality.WebRTC.Unity.PeerConnection) instance pointed by the [`LocalVideoSource.PeerConnection`](xref:Microsoft.MixedReality.WebRTC.Unity.LocalVideoSource.PeerConnection) property is initialized. See [`PeerConnection.OnInitialized`](xref:Microsoft.MixedReality.WebRTC.Unity.PeerConnection.OnInitialized). |
| PreferredVideoCodec | Optional choice of a preferred video codec to use for local video capture and SDP offering. This is implemented by filtering out all other video codecs when sending an SDP offer message if the original offer was already containing that codec. |
| EnableMixedRealityCapture | On platforms supporting Mixed Reality Capture (MRC) like HoloLens 1st generation and 2nd generation, instruct the video capture module to enable this feature and produce a video stream containing the holograms rendered over the raw webcam feed. This has no effect if the local device does not support MRC. |