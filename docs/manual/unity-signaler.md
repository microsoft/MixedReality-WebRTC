# Unity `Signaler` component

The [`Signaler`](xref:Microsoft.MixedReality.WebRTC.Unity.Signaler) Unity component is an abstract base class for implementing a custom component for a given signaling solution. The [`PeerConnection`](xref:Microsoft.MixedReality.WebRTC.Unity.PeerConnection) Unity component takes a reference to an intance of a [`Signaler`](xref:Microsoft.MixedReality.WebRTC.Unity.Signaler) Unity component to delegate handling its signaling message.

| Property | Description |
|---|---|
| PeerConnection | A back reference to the [`PeerConnection`](xref:Microsoft.MixedReality.WebRTC.Unity.PeerConnection) Unity component that this signaler is attached to. This property is updated automatically after the peer connection is initialized. See [`PeerConnection.Signaler`](xref:Microsoft.MixedReality.WebRTC.Unity.PeerConnection.Signaler). |
| OnConnect | Event fired when the peer connection is established. Derived classes must invoke this event when appropriate. |
| OnDisconnect | Event fired when the peer connection is closed. Derived classes must invoke this event when appropriate. |
| OnMessage | Event fired when a signaling message is received. Derived classes must invoke this event when appropriate to deliver incoming messages to the [`PeerConnection`](xref:Microsoft.MixedReality.WebRTC.Unity.PeerConnection). |
| OnFailure | Event fired when an error occurs inside the signaler. Derived classes may invoke this event when appropriate. |

## Implementing a custom signaler

A custom signaling solution needs to derive from the abstract base [`Signaler`](xref:Microsoft.MixedReality.WebRTC.Unity.Signaler) class so it can be used by the [`PeerConnection`](xref:Microsoft.MixedReality.WebRTC.Unity.PeerConnection) Unity component.

Derived classes implementing a particular signaling solution must:
- invoke the [`OnConnect`](xref:Microsoft.MixedReality.WebRTC.Unity.Signaler.OnConnect) and [`OnDisconnect`](xref:Microsoft.MixedReality.WebRTC.Unity.Signaler.OnDisconnect) events to notify both the user and the peer connection of the state of signaling.
- invoke the [`OnMessage`](xref:Microsoft.MixedReality.WebRTC.Unity.Signaler.OnMessage) event and implement the [`SendMessageAsync`](xref:Microsoft.MixedReality.WebRTC.Unity.Signaler.SendMessageAsync(Microsoft.MixedReality.WebRTC.Unity.Signaler.Message)) method to respectively deliver incoming messages to the local peer and send outgoing message to the remote peer.

Aditionally it is recommended that implementations also invoke the [`OnFailure`](xref:Microsoft.MixedReality.WebRTC.Unity.Signaler.OnFailure) event so that the user can be notified.
