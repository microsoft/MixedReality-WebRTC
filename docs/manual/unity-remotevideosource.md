# Unity `RemoteVideoSource` component

The [`RemoteVideoSource`](xref:Microsoft.MixedReality.WebRTC.Unity.RemoteVideoSource) Unity component represents a single video track received from the remote peer through an established peer connection.

![The RemoteVideoSource Unity component](unity-remotevideosource.png)

| Property | Description |
|---|---|
| **Video track** | |
| PeerConnection | Reference to the [`PeerConnection`](xref:Microsoft.MixedReality.WebRTC.Unity.PeerConnection) instance which contains the remote video track. |
| AutoPlayOnAdded | Automatically start playback of the video track when added. This corresponds to registering a video frame callback with the [`PeerConnection`](xref:Microsoft.MixedReality.WebRTC.Unity.PeerConnection) instance pointed by the [`RemoteVideoSource.PeerConnection`](xref:Microsoft.MixedReality.WebRTC.Unity.RemoteVideoSource.PeerConnection) property. |
| **Events** | |
| VideoStreamStarted | Event invoked when the remote video stream starts, after the track has been added to the peer connection. |
| VideoStreamStopped | Event invoked when the remote video stream stops, before the track is removed from the peer connection. |
