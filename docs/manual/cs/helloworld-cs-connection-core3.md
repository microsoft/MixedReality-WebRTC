# Establishing a WebRTC connection

Now that the signaling solution is in place, the final step is to establish a peer connection.

Continue editing the `Program.cs` file and append the following:

1. For debugging purpose, and to understand what is going on with the connection, connect the [`Connected`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.Connected) and [`IceStateChanged`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.IceStateChanged) events to handlers printing messages to console.
   ```cs
   pc.Connected += () => {
       Console.WriteLine("PeerConnection: connected.");
   };

   pc.IceStateChanged += (IceConnectionState newState) => {
       Console.WriteLine($"ICE state: {newState}");
   };
   ```
   The [`Connected`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.Connected) event is invoked when the peer connection is established. The [`IceStateChanged`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.IceStateChanged) is invoked each time the ICE status changes. Note that the [`Connected`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.Connected) event can be invoked before the ICE status reaches its [`IceConnectionState.Connected`](xref:Microsoft.MixedReality.WebRTC.IceConnectionState) state.

2. To establish a WebRTC connection, one peer has to call [`CreateOffer()`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.CreateOffer), but not both. Since the signaler implementation `NamedPipeSignaler` already provides a way to distinguish between the two peers, we use that information to select which peer will automatically initiate the connection.
   ```cs
   if (signaler.IsClient)
   {
       Console.WriteLine("Connecting to remote peer...");
       pc.CreateOffer();
   }
   else
   {
       Console.WriteLine("Waiting for offer from remote peer...");
   }
   ```

3. In this state, the application is working but will terminate immediately once the peer connection is established. To prevent that, simply wait until the user press a key.
   ```cs
   Console.WriteLine("Press a key to terminate the application...");
   Console.ReadKey(true);
   ```

Run the two instances of the application again. This time both terminals print a large quantity of messages related to SDP and ICE message exchanges, and eventually establish a WebRTC peer connection. If launched with the audio or video capture flags, the capturer instance records those media and send them via the network to the other instance, which simply ignores them.
