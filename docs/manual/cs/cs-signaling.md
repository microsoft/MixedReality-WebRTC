# C# signaling

The C# library does not have a dedicated class for signaling. Instead, the [`PeerConnection`](xref:Microsoft.MixedReality.WebRTC.PeerConnection) class provides some events and methods to build upon in order to build a signaling solution.

A custom signaling solution needs to handle sending locally-prepared messages to the remote peer, for example via a separate TCP/IP connection, and dispatching messages received from the remote peer down to the local peer connection. Neither the WebRTC standard nor the MixedReality-WebRTC library specify the way those messages must be transmitted.

## Local to remote (send)

Local messages are generated under several circumstances by the local peer. The signaling solution must listen to the [`LocalSdpReadytoSend`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.LocalSdpReadytoSend) and [`IceCandidateReadytoSend`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.IceCandidateReadytoSend) events, and dispatch those messages to the remote peer by whatever way it choses.

```cs
peerConnection.LocalSdpReadyToSend += (SdpMessage message) => {
    MyCustomSignaling_SendSdp(message);
};

peerConnection.IceCandidateReadytoSend += (IceCandidate candidate) => {
    MyCustomSignaling_SendIce(candidate);
};
```

## Remote to local (receive)

Upon receiving the above messages, the signaling solution must:

- For SDP messages originating from a [`LocalSdpReadytoSend`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.LocalSdpReadytoSend) event raised on the remote peer and sent to the local peer, call the [`PeerConnection.SetRemoteDescriptionAsync()`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.SetRemoteDescriptionAsync(Microsoft.MixedReality.WebRTC.SdpMessage)) method to inform the local peer connection of the newly received session description.

  ```cs
  public void OnSdpMessageReceived(SdpMessage message) {
      await peerConnection.SetRemoteDescriptionAsync(message);
      // Optionally, and only after SetRemoteDescriptionAsync() completed:
      if (type == "offer") {
          peerConnection.CreateAnswer();
      }
  }
  ```

- For ICE messages originating from an [`IceCandidateReadytoSend`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.IceCandidateReadytoSend) event raised on the remote peer and sent to the local peer, call the [`PeerConnection.AddIceCandidate()`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.AddIceCandidate*) method to inform the local peer connection of the newly received ICE candidate.

  ```cs
  public void OnIceMessageReceived(IceCandidate candidate) {
      peerConnection.AddIceCandidate(candidate);
  }
  ```
