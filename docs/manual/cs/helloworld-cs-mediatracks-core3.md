# Adding local media tracks

Now that the peer connection is initialized, there are two possible paths depending on the usage scenario:

- Immediately adding local audio and/or video tracks to the peer connection, so that they are available right away when the connection will be established with the remote peer.
- Waiting for the connection to be established, and add the local media tracks after that.

The first case is benefical in the sense that media tracks will be immediately negotiated during the connection establishing, without the need for an extra negotiation specific to the tracks. However it requires knowing in advance that the tracks are used. Conversely, the latter case corresponds to a scenario like late joining, where the user or the application can control when to add or remove local tracks, at the expense of requiring an extra network negotiation each time the list of tracks changes.

In this tutorial, we add the local media tracks right away for simplicity.

Continue editing the `Program.cs` file and append the following:

1. Create some variables we need at the start of the `Main` method, before the `try` block:

   ```cs
   AudioTrackSource microphoneSource = null;
   VideoTrackSource webcamSource = null;
   Transceiver audioTransceiver = null;
   Transceiver videoTransceiver = null;
   LocalAudioTrack localAudioTrack = null;
   LocalVideoTrack localVideoTrack = null;
   ```

2. Use the [`DeviceVideoTrackSource.CreateAsync()`](xref:Microsoft.MixedReality.WebRTC.DeviceVideoTrackSource.CreateAsync(Microsoft.MixedReality.WebRTC.LocalVideoDeviceInitConfig)) method to create a new video track source obtaining its frames from a local video capture device (webcam).

   ```cs
   webcamSource = await DeviceVideoTrackSource.CreateAsync();
   ```

   This method optionally takes a [`LocalVideoDeviceInitConfig`](xref:Microsoft.MixedReality.WebRTC.LocalVideoDeviceInitConfig) object to configure the video capture. In this tutorial, we leave that object out and use the default settings, which will open the first available webcam with its default resolution and framerate. This is generally acceptable, although on mobile devices like HoloLens developers probably want to limit the capture resolution and framerate to reduce the power consumption and save on battery.

   The video track source is a standalone object, which can be used by multiple tracks, including from different peer connections. This allows sharing a local webcam among multiple conections.

3. From this source, create a local video track which will send those captured frames to the remote peer.

   ```cs
   var videoTrackConfig = new LocalVideoTrackInitConfig {
      trackName = "webcam_track"
   };
   localVideoTrack = LocalVideoTrack.CreateFromSource(_webcamSource, videoTrackConfig);
   ```

   Note that the local video track created is not associated with the peer connection yet; the [`LocalVideoTrack.CreateFromSource()`](xref:Microsoft.MixedReality.WebRTC.LocalVideoTrack.CreateFromSource(Microsoft.MixedReality.WebRTC.VideoTrackSource,Microsoft.MixedReality.WebRTC.LocalVideoTrackInitConfig)) is a static method which does not reference any peer connection. The local video track will be bound to one specific peer connection later when added to a video transceiver. After that, it will stay implicitly bound to that peer connection, even if detached from its transceiver, and cannot be reused with another peer connection.

4. Use the [`DeviceAudioTrackSource.CreateAsync()`](xref:Microsoft.MixedReality.WebRTC.DeviceAudioTrackSource.CreateAsync(Microsoft.MixedReality.WebRTC.LocalAudioDeviceInitConfig)) method to create an audio track source obtaining its audio frames from a local audio capture device (microphone).

   ```cs
   microphoneSource = await DeviceAudioTrackSource.CreateAsync();
   ```

   Again, the method optionally takes [`LocalAudioDeviceInitConfig`](xref:Microsoft.MixedReality.WebRTC.LocalAudioDeviceInitConfig) object to configure the audio capture, but we can ignore it to get the default settings.

5. Use the [`LocalAudioTrack.CreateFromSource()`](xref:Microsoft.MixedReality.WebRTC.LocalAudioTrack.CreateFromSource(Microsoft.MixedReality.WebRTC.AudioTrackSource,Microsoft.MixedReality.WebRTC.LocalAudioTrackInitConfig)) method to create an audio track sending to the remote peer those audio frames.

   ```cs
   var audioTrackConfig = new LocalAudioTrackInitConfig {
      trackName = "microphone_track"
   };
   localAudioTrack = LocalAudioTrack.CreateFromDeviceAsync(microphoneSource, audioTrackConfig);
   ```

6. Use the [`PeerConnection.AddTransceiver()`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.AddTransceiver(Microsoft.MixedReality.WebRTC.MediaKind,Microsoft.MixedReality.WebRTC.TransceiverInitSettings)) method to add to the peer connection some audio and video transceivers, which are the transports through which the audio and video tracks are sent to the remote peer.

   ```cs
   videoTransceiver = pc.AddTransceiver(MediaKind.Video);
   videoTransceiver.LocalVideoTrack = localVideoTrack;
   videoTransceiver.DesiredDirection = Transceiver.Direction.SendReceive;

   audioTransceiver = pc.AddTransceiver(MediaKind.Audio);
   audioTransceiver.LocalAudioTrack = localAudioTrack;
   audioTransceiver.DesiredDirection = Transceiver.Direction.SendReceive;
   ```

   This binds the local media tracks to the peer connection forever, and instructs the native implementation to use those tracks while they are attached to a transceiver. This also ask to negotiate a bidirectional transport (send + receive) for each transceiver in order to both send the audio and video captured locally, and receive those the remote peer will send.

7. At the very end of the program, outside of the `try` / `catch` block, make sure to dispose of the audio and video tracks and sources after the peer connection has been closed. The audio and video tracks and sources are owned by the user, and must be disposed of manually. The media sources also must outlive the local tracks using them, so make sure to dispose of them last.

   ```cs
   localAudioTrack?.Dispose();
   localVideoTrack?.Dispose();
   microphoneSource?.Dispose();
   webcamSource?.Dispose();
   ```

Run the application again. This time, there is no visible difference in the terminal window, except some extra delay to open the audio and video devices; this delay varies greatly depending on the number of capture devices on the host machine, but is generally within a few seconds too. Additionally, if the webcam or microphone have a LED indicating recording, it should briefly turn ON when the capture device starts recording, and immediately stop when the program reaches its end and the peer connection and its tracks are automatically shut down.

----

Next : [A custom signaling solution](helloworld-cs-signaling-core3.md)
