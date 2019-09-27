# Add local media tracks

Now that the peer connection is initialized, there are two possible paths, which can be both used:
- Immediately adding local audio and/or video tracks to the peer connection, so that they are available right away when the connection will be established with the remote peer.
- Waiting for the connection to be established, and add the local media tracks after that.

The first case is benefical in the sense that media tracks will be immediately negotiated during the connection establishing, without the need for an extra negotiation specific for the tracks. However it requires knowing in advance that the tracks are used. Conversely, the latter case corresponds to a scenario like late joining, where the user or the application can control when to add or remove local tracks, at the expense of requiring an extra network negotiation each time the list of tracks is changed.

In this tutorial, we add the local media tracks right away for simplicity.

Continue editing the `Program.cs` file and append the following:

1. Use the [`AddLocalVideoTrackAsync()`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.AddLocalVideoTrackAsync(Microsoft.MixedReality.WebRTC.PeerConnection.LocalVideoTrackSettings)) method to add to the peer connection a local video track sending to the remote peer some video frames obtained from a local video capture device (webcam).
   ```cs
   await pc.AddLocalVideoTrackAsync();
   ```
   This method optionally takes a [`LocalVideoTrackSettings`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.LocalVideoTrackSettings) object to configure the video capture. In this tutorial, we leave that object out and use the default settings, which will open the first available webcam with its default resolution and framerate. This is generally acceptable, although on mobile devices like HoloLens you probably want to limit the resolution and framerate to reduce the power consumption and save on battery.

2. Use the [`AddLocalAudioTrackAsync()`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.AddLocalAudioTrackAsync) method to add to the peer connection a local audio track sending to the remote peer some audio frames obtained from a local audio capture device (microphone).
   ```cs
   await pc.AddLocalAudioTrackAsync();
   ```
   Unlike for the video track, the audio track currently does not offer any configuration option, and will always use the first available audio capture device.

Run the application again. This time, there is no visible difference in the terminal window, except some extra delay to open the audio and video devices; this delay varies greatly depending on the number of capture devices on the host machine, but is generally within a few seconds too. Additionally, if the webcam or microphone have a LED indicating recording, it should briefly turn ON when the capture device starts recording, and immediately stop when the program reaches its end and the peer connection and its tracks are automatically shut down.

----

Next : [A custom signaling solution](helloworld-cs-signaling-core3.md)
