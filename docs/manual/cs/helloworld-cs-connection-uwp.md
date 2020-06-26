# Establishing a WebRTC connection

Now that the signaling solution is in place, the final step is to establish a peer connection.

Continue editing the `OnLoaded()` method and append after the [`InitializeAsync()`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.InitializeAsync(Microsoft.MixedReality.WebRTC.PeerConnectionConfiguration,CancellationToken)) call:

1. For debugging purpose, and to understand what is going on with the connection, connect the [`Connected`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.Connected) and [`IceStateChanged`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.IceStateChanged) events to handlers printing messages to the debugger. These messages are visible in the **Output** window of Visual Studio.

   ```cs
   _peerConnection.Connected += () => {
       Debugger.Log(0, "", "PeerConnection: connected.\n");
   };
   _peerConnection.IceStateChanged += (IceConnectionState newState) => {
       Debugger.Log(0, "", $"ICE state: {newState}\n");
   };
   ```

   The [`Connected`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.Connected) event is raised when the peer connection is established, that is when the underlying media transports are writable. There is a subtlety here, in that this does not necessarily correspond to an offer/answer pair exchange being completed; indeed on the callee (answering peer) once the answer is created everything is done and the transports are writable, but the answer has not been sent yet, so the caller (offering peer) needs to wait until it received and applied that answer for its transports to be writable, and therefore its [`Connected`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.Connected) event will be raised much later than the one of the callee (answering peer).


   The [`IceStateChanged`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.IceStateChanged) is raised each time the ICE status changes. Note that the [`Connected`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.Connected) event can be raised before the ICE status reaches its [`IceConnectionState.Connected`](xref:Microsoft.MixedReality.WebRTC.IceConnectionState) state, since the two processes occur in parallel and are partly independent.

2. In order to render the remote video, we also subscribe to the [`RemoteVideoTrack.I420AVideoFrameReady`](xref:Microsoft.MixedReality.WebRTC.RemoteVideoTrack.I420AVideoFrameReady) event. This requires accessing the remote video track, which is only available once the connection is established and the track created during the session negotiation (more precisely: when an offer or answer is applied with [`SetRemoteDescriptionAsync()`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.SetRemoteDescriptionAsync(Microsoft.MixedReality.WebRTC.SdpMessage)).

   ```cs
   _peerConnection.VideoTrackAdded += (RemoteVideoTrack track) => {
       _remoteVideoTrack = track;
       _remoteVideoTrack.I420AVideoFrameReady += RemoteVideo_I420AFrameReady;
   };
   ```

That event handler is similar to the one for the local video, using another video bridge.

1. Create a new set of fields for the remote video:

   ```cs
   private object _remoteVideoLock = new object();
   private bool _remoteVideoPlaying = false;
   private MediaStreamSource _remoteVideoSource;
   private VideoBridge _remoteVideoBridge = new VideoBridge(5);
   ```

   This time we increase the video buffer queue to 5 frames to avoid starving the video rendering pipeline as the remote video is more prone to delays due to network latency. This potentially introduces more latency in the overall video streaming process, but optimizing for latency is a complex topic well outside of the scope of this tutorial.

2. Modify the `OnMediaStreamSourceRequested()` event handler to dispatch either to the local or to the remote bridge:

   ```cs
   if (sender == _localVideoSource)
       videoBridge = _localVideoBridge;
   else if (sender == _remoteVideoSource)
       videoBridge = _remoteVideoBridge;
   else
       return;
   ```

3. Implement the handler with the newly created members:

   ```cs
   private void RemoteVideo_I420AFrameReady(I420AVideoFrame frame)
   {
       lock (_remoteVideoLock)
       {
           if (!_remoteVideoPlaying)
           {
               _remoteVideoPlaying = true;
               uint width = frame.width;
               uint height = frame.height;
               RunOnMainThread(() =>
               {
                   // Bridge the remote video track with the remote media player UI
                   int framerate = 30; // assumed, for lack of an actual value
                   _remoteVideoSource = CreateI420VideoStreamSource(width, height,
                       framerate);
                   var remoteVideoPlayer = new MediaPlayer();
                   remoteVideoPlayer.Source = MediaSource.CreateFromMediaStreamSource(
                       _remoteVideoSource);
                   remoteVideoPlayerElement.SetMediaPlayer(remoteVideoPlayer);
                   remoteVideoPlayer.Play();
               });
           }
       }
       _remoteVideoBridge.HandleIncomingVideoFrame(frame);
   }
   ```

4. In the `App_Suspending()` event handler, add a line to also clear the media player of the remote element.

   ```cs
   remoteVideoPlayerElement.SetMediaPlayer(null);
   ```

Finally, we need to change the UI to add the new `remoteVideoPlayerElement` XAML control displaying the remote video track. Open `MainPage.xaml` in the visual editor and edit it:

1. Add a new `<MediaPlayerElement>` tag for the remote video.

   ```cs
   <MediaPlayerElement x:Name="remoteVideoPlayerElement" />
   ```

   > [!WARNING]
   > Be sure to put the remote tag first, **before** the local one, so that the remote video is rendered first in the background, and the local one second on top of it. Otherwise the local video will be hidden by the remote one.

2. Change the tag for the local video to reduce its size and position it in the lower right corner of the window, like is typical for local video preview in a video chat application.

   ```cs
   <MediaPlayerElement x:Name="localVideoPlayerElement" Width="320" Height="240"
       HorizontalAlignment="Right" VerticalAlignment="Bottom" Margin="0,0,20,20" />
   ```

   Here we fix the size to 320x240 pixels, align the control to the lower right corner of the window, and add a 20px margin.

At this point, the sample application is functional, although there is no mechanism to initiate a call. You can either add a button or similar to call [`CreateOffer()`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.CreateOffer), or test this sample with the `TestAppUWP` available in the MixedReality-WebRTC repository in `examples/TestAppUWP`, which implements a "Create offer" button. Be sure to set the correct local and remote peer ID on **both** peers and have a `node-dss` server running before hitting the "Create offer" button, otherwise signaling will not work.

In either cases however, be sure to create transceivers only on the caller:

- If adding a [`CreateOffer()`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.CreateOffer) button to the application, then move the calls to [`AddTransceiver()`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.AddTransceiver(Microsoft.MixedReality.WebRTC.MediaKind,Microsoft.MixedReality.WebRTC.TransceiverInitSettings)) into the button handler, so that they are manually created on the caller only. On the callee, they will be created automatically in the same order when applying the offer received from the caller.
- If using the `TestAppUWP` to make a connection with, the `TestAppUWP` is the caller so the transceivers should be created inside that app, and the calls to [`AddTransceiver()`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.AddTransceiver(Microsoft.MixedReality.WebRTC.MediaKind,Microsoft.MixedReality.WebRTC.TransceiverInitSettings)) deleted from `App1`.
