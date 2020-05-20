# Adding local media tracks

Now that the peer connection is initialized, there are two possible paths depending on the usage scenario:

- Immediately adding local audio and/or video tracks to the peer connection, so that they are available right away when the connection will be established with the remote peer.
- Waiting for the connection to be established, and add the local media tracks after that.

The first case is benefical in the sense that media tracks will be immediately negotiated during the connection establishing, without the need for an extra negotiation specific to the tracks. However it requires knowing in advance that the tracks are used. Conversely, the latter case corresponds to a scenario like late joining, where the user or the application can control when to add or remove local tracks, at the expense of requiring an extra network negotiation each time the list of tracks changes.

In this tutorial, we add the local media tracks right away for simplicity.

Continue editing the `Program.cs` file and append the following:

1. Create some variables we need at the start of the `Main` method, before the `try` block:

   ```cs
   Transceiver audioTransceiver = null;
   Transceiver videoTransceiver = null;
   LocalAudioTrack localAudioTrack = null;
   LocalVideoTrack localVideoTrack = null;
   ```

2. Use the [`LocalVideoTrack.CreateFromDeviceAsync()`](xref:Microsoft.MixedReality.WebRTC.LocalVideoTrack.CreateFromDeviceAsync(Microsoft.MixedReality.WebRTC.LocalVideoTrackSettings)) method to create a new video track sending to the remote peer some video frames obtained from a local video capture device (webcam).

   ```cs
   localVideoTrack = await LocalVideoTrack.CreateFromDeviceAsync();
   ```

   This method optionally takes a [`LocalVideoTrackSettings`](xref:Microsoft.MixedReality.WebRTC.LocalVideoTrackSettings) object to configure the video capture. In this tutorial, we leave that object out and use the default settings, which will open the first available webcam with its default resolution and framerate. This is generally acceptable, although on mobile devices like HoloLens developers probably want to limit the capture resolution and framerate to reduce the power consumption and save on battery.

   Note that the local video track created is not associated with the peer connection yet; the [`LocalVideoTrack.CreateFromDeviceAsync()`](xref:Microsoft.MixedReality.WebRTC.LocalVideoTrack.CreateFromDeviceAsync(Microsoft.MixedReality.WebRTC.LocalVideoTrackSettings)) is a static method which does not reference any peer connection. The local video track will be bound to one specific peer connection later when added to a video transceiver. After that, it will stay implicitly bound to that peer connection, even if detached from its transceiver, and cannot be reused with another peer connection.

3. Use the [`LocalAudioTrack.CreateFromDeviceAsync()`](xref:Microsoft.MixedReality.WebRTC.LocalAudioTrack.CreateFromDeviceAsync(Microsoft.MixedReality.WebRTC.LocalAudioTrackSettings)) method to create an audio track sending to the remote peer some audio frames obtained from a local audio capture device (microphone).

   ```cs
   localAudioTrack = await LocalAudioTrack.CreateFromDeviceAsync();
   ```

   Unlike video capture, audio capture currently does not offer any configuration option, and will always use the first available audio capture device.

4. Use the [`PeerConnection.AddTransceiver()`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.AddTransceiver(Microsoft.MixedReality.WebRTC.MediaKind,Microsoft.MixedReality.WebRTC.TransceiverInitSettings)) method to add to the peer connection some audio and video transceivers, which are the transports through which the audio and video tracks are sent to the remote peer.

   ```cs
   videoTransceiver = pc.AddTransceiver(MediaKind.Video);
   videoTransceiver.LocalVideoTrack = localVideoTrack;
   videoTransceiver.DesiredDirection = Transceiver.Direction.SendReceive;

   audioTransceiver = pc.AddTransceiver(MediaKind.Audio);
   audioTransceiver.LocalAudioTrack = localAudioTrack;
   audioTransceiver.DesiredDirection = Transceiver.Direction.SendReceive;
   ```

   This binds the local media tracks to the peer connection forever, and instructs the native implementation to use those tracks while they are attached to a transceiver. This also ask to negotiate a bidirectional transport (send + receive) for each transceiver in order to both send the audio and video captured locally, and receive those the remote peer will send.

5. At the very end of the program, outside of the `try` / `catch` block, make sure to dispose of the audio and video tracks after the peer connection has been closed. The audio and video tracks are owned by the user, and must be disposed of manually.

   ```cs
   localAudioTrack?.Dispose();
   localVideoTrack?.Dispose();
   ```

Run the application again. This time, there is no visible difference in the terminal window, except some extra delay to open the audio and video devices; this delay varies greatly depending on the number of capture devices on the host machine, but is generally within a few seconds too. Additionally, if the webcam or microphone have a LED indicating recording, it should briefly turn ON when the capture device starts recording, and immediately stop when the program reaches its end and the peer connection and its tracks are automatically shut down.

----

Next : [A custom signaling solution](helloworld-cs-signaling-core3.md)
