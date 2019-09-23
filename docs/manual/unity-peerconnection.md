# Unity `PeerConnection` component

The [`PeerConnection`](xref:Microsoft.MixedReality.WebRTC.Unity.PeerConnection) Unity component encapsulates a single peer connection between the local application and another remote Unity peer application.

> [!NOTE]
> The C# library also has a [`PeerConnection`](xref:Microsoft.MixedReality.WebRTC.PeerConnection) class, which this components build upon.

![The PeerConnection Unity component](unity-peerconnection.png)

| Property | Description |
|---|---|
| **Behavior settings** | |
| Auto Initialize On Start | Automatically initialize the peer connection when the [`MonoBehaviour.Start()`](https://docs.unity3d.com/ScriptReference/MonoBehaviour.Start.html). If not set, the user need to call [`InitializeAsync()`](xref:Microsoft.MixedReality.WebRTC.Unity.PeerConnection.InitializeAsync(System.Threading.CancellationToken)) manually before using the component. |
| Auto Log Errors To Unity Console | Add an event listener to the [`OnError`](xref:Microsoft.MixedReality.WebRTC.Unity.PeerConnection.OnError) event which calls [`Debug.LogError()`](https://docs.unity3d.com/ScriptReference/Debug.LogError.html) to display the error message in the Unity console. |
| **ICE servers** | |
| Ice Servers | A list of [`ConfigurableIceServer`](xref:Microsoft.MixedReality.WebRTC.Unity.ConfigurableIceServer) elements representing the list of ICE servers to use to establish the peer connection. The list can be empty, in which case only a local connection will be possible. |
| Ice Username | Optional user name for TURN servers authentication. |
| Ice Credential | Optional password for TURN servers authentication. |
| **Peer connection events** | |
| On Initialized | Event fired once the [`InitializeAsync()`](xref:Microsoft.MixedReality.WebRTC.Unity.PeerConnection.InitializeAsync(System.Threading.CancellationToken)) method returned successfully, to indicate that the peer connection component is ready for use. |
| On Shutdown | Event fired when [`Uninitialize()`](xref:Microsoft.MixedReality.WebRTC.Unity.PeerConnection.Uninitialize) is called, usually automatically during [`MonoBehaviour.OnDestroy()`](https://docs.unity3d.com/ScriptReference/MonoBehaviour.OnDestroy.html) |
| On Error | Event fired when an error occur in the peer connection. |