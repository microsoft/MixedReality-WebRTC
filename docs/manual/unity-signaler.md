# Unity `Signaler` component

The [`Signaler`](xref:Microsoft.MixedReality.WebRTC.Unity.Signaler) Unity component is an abstract base class used as an helper for implementing a custom component for a given signaling solution. It is not strictly required, but provides some utilities which make it easier to write an implementation.

| Property | Description |
|---|---|
| PeerConnection | A reference to the [`PeerConnection`](xref:Microsoft.MixedReality.WebRTC.Unity.PeerConnection) Unity component that this signaler should provide signaling for. |

## Implementing a custom signaler

A custom signaling solution can derive from the abstract base [`Signaler`](xref:Microsoft.MixedReality.WebRTC.Unity.Signaler) class for simplicity, or be any other Unity component or C# class.

When deriving from the [`Signaler`](xref:Microsoft.MixedReality.WebRTC.Unity.Signaler) class, a derived class needs to:

- Implement the [`SendMessageAsync()`](xref:Microsoft.MixedReality.WebRTC.Unity.Signaler.SendMessageAsync(Microsoft.MixedReality.WebRTC.Unity.Signaler.Message)) abstract method to send messages to the remote peer via the custom signaling solution.
- Handle incoming messages from the remote peer:
  - call [`HandleConnectionMessageAsync()`](xref:Microsoft.MixedReality.WebRTC.Unity.PeerConnection.HandleConnectionMessageAsync(System.String,System.String)) on the Unity peer connection component when receiving an SDP offer or answer message.
  - call [`AddIceCandidate()`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.AddIceCandidate(System.String,System.Int32,System.String)) on the underlying C# peer connection object to deliver ICE candidates to the local peer implementation.

When implementing a custom signaling solution from scratch **without** using the [`Signaler`](xref:Microsoft.MixedReality.WebRTC.Unity.Signaler) class, the custom implementation must, in addition of the above message handling, replace the work done by [`SendMessageAsync()`](xref:Microsoft.MixedReality.WebRTC.Unity.Signaler.SendMessageAsync(Microsoft.MixedReality.WebRTC.Unity.Signaler.Message)):

- Listen to the [`LocalSdpReadytoSend`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.LocalSdpReadytoSend) and [`IceCandidateReadytoSend`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.IceCandidateReadytoSend) events.
- Send to the remote peer, by whatever mean devised by the implementation, some messages containing the data of those events, such that the remote peer can handle them as described above.
