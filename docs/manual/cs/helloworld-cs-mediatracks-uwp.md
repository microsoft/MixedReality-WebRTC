# Add local media tracks

Now that the peer connection is initialized, there are two possible paths, which can be both used:

- Immediately adding local audio and/or video tracks to the peer connection, so that they are available right away when the connection will be established with the remote peer.
- Waiting for the connection to be established, and add the local media tracks after that.

The first case is benefical in the sense that media tracks will be immediately negotiated during the connection establishing, without the need for an extra negotiation specific for the tracks. However it requires knowing in advance that the tracks are used. Conversely, the latter case corresponds to a scenario like late joining, where the user or the application can control when to add or remove local tracks, at the expense of requiring an extra network negotiation each time the list of tracks is changed.

In this tutorial, we add the local media tracks right away for simplicity.

## Creating the tracks

Media tracks are slim objects which bridge a _media track source_, that is a media frame producer, on one hand and a _transceiver_, that is a "media pipe" to transport that media to the remote peer, on the other hand. Media track sources can be of various types, but the most common ones are device-based, meaning they produce some audio and video frames by capturing them from a device. The main classes for this are the [`DeviceAudioTrackSource`](xref:Microsoft.MixedReality.WebRTC.DeviceAudioTrackSource) and the [`DeviceVideoTrackSource`](xref:Microsoft.MixedReality.WebRTC.DeviceVideoTrackSource), which respectively open an audio or video capture device and use it to capture audio or video frames to provide them to one or more tracks.

Continue editing the `MainPage.xaml.cs` file and append in the `OnLoaded` method the following:

1. Create new private variables needed to store the tracks and their sources.

   ```cs
   DeviceAudioTrackSource _microphoneSource;
   DeviceVideoTrackSource _webcamSource;
   LocalAudioTrack _localAudioTrack;
   LocalVideoTrack _localVideoTrack;
   ```

2. Use the [`DeviceVideoTrackSource.CreateAsync()`](xref:Microsoft.MixedReality.WebRTC.DeviceVideoTrackSource.CreateAsync(Microsoft.MixedReality.WebRTC.LocalVideoDeviceInitConfig)) method to create a new video track source obtaining its frames from a local video capture device (webcam).

   ```cs
   _webcamSource = await DeviceVideoTrackSource.CreateAsync();
   ```

   This method optionally takes a [`LocalVideoDeviceInitConfig`](xref:Microsoft.MixedReality.WebRTC.LocalVideoDeviceInitConfig) object to configure the video capture. In this tutorial, we leave that object out and use the default settings, which will open the first available webcam with its default resolution and framerate. This is generally acceptable, although on mobile devices like HoloLens developers probably want to limit the capture resolution and framerate to reduce the power consumption and save on battery.

   The video track source is a standalone object, which can be used by multiple tracks, including from different peer connections. This allows sharing a local webcam among multiple conections.

3. From this source, create a local video track which will send those captured frames to the remote peer.

   ```cs
   var videoTrackConfig = new LocalVideoTrackInitConfig {
      trackName = "webcam_track"
   };
   _localVideoTrack = LocalVideoTrack.CreateFromSource(_webcamSource, videoTrackConfig);
   ```

   Note that the local video track created is not associated with the peer connection yet; the [`LocalVideoTrack.CreateFromSource()`](xref:Microsoft.MixedReality.WebRTC.LocalVideoTrack.CreateFromSource(Microsoft.MixedReality.WebRTC.VideoTrackSource,Microsoft.MixedReality.WebRTC.LocalVideoTrackInitConfig)) is a static method which does not reference any peer connection. The local video track will be bound to one specific peer connection later when added to a video transceiver. After that, it will stay implicitly bound to that peer connection, even if detached from its transceiver, and cannot be reused with another peer connection.

4. Use the [`DeviceAudioTrackSource.CreateAsync()`](xref:Microsoft.MixedReality.WebRTC.DeviceAudioTrackSource.CreateAsync(Microsoft.MixedReality.WebRTC.LocalAudioDeviceInitConfig)) method to create an audio track source obtaining its audio frames from a local audio capture device (microphone).

   ```cs
   _microphoneSource = await DeviceAudioTrackSource.CreateAsync();
   ```

   Again, the method optionally takes [`LocalAudioDeviceInitConfig`](xref:Microsoft.MixedReality.WebRTC.LocalAudioDeviceInitConfig) object to configure the audio capture, but we can ignore it to get the default settings.

5. Use the [`LocalAudioTrack.CreateFromSource()`](xref:Microsoft.MixedReality.WebRTC.LocalAudioTrack.CreateFromSource(Microsoft.MixedReality.WebRTC.AudioTrackSource,Microsoft.MixedReality.WebRTC.LocalAudioTrackInitConfig)) method to create an audio track sending to the remote peer those audio frames.

   ```cs
   var audioTrackConfig = new LocalAudioTrackInitConfig {
      trackName = "microphone_track"
   };
   _localAudioTrack = LocalAudioTrack.CreateFromSource(_microphoneSource, audioTrackConfig);
   ```

## Adding the transceivers

Continue editing the `MainPage.xaml.cs` file and append in the `OnLoaded` method the following:

1. Create 2 new [`Transceiver`](xref:Microsoft.MixedReality.WebRTC.Transceiver) variables, one for the audio feed and one for the video feed.

   ```cs
   Transceiver _audioTransceiver;
   Transceiver _videoTransceiver;
   ```

2. Use the [`AddTransceiver()`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.AddTransceiver(Microsoft.MixedReality.WebRTC.MediaKind,Microsoft.MixedReality.WebRTC.TransceiverInitSettings)) method to create the audio and video transceivers on the peer connection.

   ```cs
   _audioTransceiver = _peerConnection.AddTransceiver(MediaKind.Audio);
   _videoTransceiver = _peerConnection.AddTransceiver(MediaKind.Video);
   ```

   Note that the order of the calls to [`AddTransceiver()`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.AddTransceiver(Microsoft.MixedReality.WebRTC.MediaKind,Microsoft.MixedReality.WebRTC.TransceiverInitSettings)) matters. Here we create first an audio transceiver associated with the _media line index_ #0, and then a video transceiver associated with the _media line index_ #1. On the remote peer, when it receives the SDP offer from this local peer and applies it by calling [`SetRemoteDescriptionAsync()`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.SetRemoteDescriptionAsync(Microsoft.MixedReality.WebRTC.SdpMessage)), the transceivers will be created in that same order.

   > [!IMPORTANT]
   >
   > As is, the code will not produce the intended effect, because transceivers must be added only on the _offering_ (also known as _caller_) peer, and then are automatically created on the _answering_ (also known as _callee_) peer, so adding transceivers on both peers will result in twice the amount intended. The calls to [`AddTransceiver()`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.AddTransceiver(Microsoft.MixedReality.WebRTC.MediaKind,Microsoft.MixedReality.WebRTC.TransceiverInitSettings)) above need to be conditional to the caller, but this requires a mechanism that we will add only later in this tutorial when looking at establishing a connection. For now we leave this code as is, and will revisit it in the last chapter.

3. By default the transceivers have no track attached to them, and will send some empty media data (black frame for video, silence for audio). Attach the local tracks created prevoously to the transceivers, so that the WebRTC implementation uses them instead and send their media data to the remote peer.

   ```cs
   _audioTransceiver.LocalAudioTrack = _localAudioTrack;
   _videoTransceiver.LocalVideoTrack = _localVideoTrack;
   ```

At this point, if you run the application again, there is no visible difference, except some extra delay to open the audio and video devices; this delay varies greatly depending on the number of capture devices on the host machine, but is generally within a few seconds too, sometimes much less. Additionally, if the webcam or microphone have a LED indicating recording, it should turn ON when the capture device starts recording. But the captured audio and video are not visible. This is because the audio and video tracks are capturing frames from the webcam and microphone, to be sent later to the remote peer once connected to it, but there is no local rendering by default.

## Importing the `VideoBridge` utility

In order to display the local webcam feed as a feedback for the user, we need to collect the video frames captured by WebRTC via the webcam, and display them locally in the app using whichever technology we choose. In this tutorial for simplicity we use the [MediaPlayerElement](xref:Windows.UI.Xaml.Controls.MediaPlayerElement) XAML control, which is based on the Media Foundation framework. And to keep things simple, we also use [the `TestAppUwp.Video.VideoBridge` helper class](https://github.com/microsoft/MixedReality-WebRTC/blob/master/examples/TestAppUwp/Video/VideoBridge.cs) from the `TestAppUWP` sample application provided by MixedReality-WebRTC, which bridges a raw source of frames (WebRTC) with the [MediaPlayerElement](xref:Windows.UI.Xaml.Controls.MediaPlayerElement), taking care of the interoperability details for us.

The [`TestAppUwp.Video.VideoBridge`](https://github.com/microsoft/MixedReality-WebRTC/blob/master/examples/TestAppUwp/Video/VideoBridge.cs) helper class makes use of the [`StreamSamplePool`](https://github.com/microsoft/MixedReality-WebRTC/blob/master/examples/TestAppUwp/Video/StreamSamplePool.cs) class, which also needs to be imported. For the sake of simplicity in this tutorial we simply copy those two files.

1. Download the [`VideoBridge.cs`](https://github.com/microsoft/MixedReality-WebRTC/blob/master/examples/TestAppUwp/Video/VideoBridge.cs) and [`StreamSamplePool.cs`](https://github.com/microsoft/MixedReality-WebRTC/blob/master/examples/TestAppUwp/Video/StreamSamplePool.cs) from the MixedReality-WebRTC repository, or copy them from a local clone of the repository, and paste them into the current tutorial project.

2. Add a reference to the `App1.csproj` project by right-clicking on the project in the _Solution Explorer_ panel and selecting **Add** > **Existing Item...** (or using Shift+Alt+A), pointing the add dialog to the two newly copied files `VideoBridge.cs` and `StreamSamplePool.cs`.

![Import the video bridge into the C# project](cs-uwp13.png)

## Editing the local video UI

Double-click on the `MainPage.xaml` (or right-click > **Open**) to bring up the XAML visual editor of the `MainPage` page. The page is currently blank, similar to what is displayed when launching the application. We will add a video player to it.

![XAML visual editor for the MainPage page](cs-uwp12.png)

The XAML visual editor allows editing the XAML user interface both via the visual top panel and via the XAML code in the bottom panel. Which approach is best is generally up to personal preference, although for discoverability it is easier to drag and drop controls from the **Toolbox** panel. In this tutorial however we make use of the [MediaPlayerElement](xref:Windows.UI.Xaml.Controls.MediaPlayerElement) control which is not available from the toolbox.

Edit the `MainPage.xaml` code from the **XAML** panel of the editor, and inside the `<Grid>` element add a `<MediaPlayerElement>` node:

```xml
<Grid>
  <MediaPlayerElement x:Name="localVideoPlayerElement" />
</Grid>
```

This will create a media player which covers the entire surface of the application window.

## Bridging the track and its rendering

The key link between a raw source of video frames and the Media Foundation pipeline is the [`MediaStreamSource`](xref:Windows.Media.Core.MediaStreamSource) class, which wraps an external video source to deliver raw frames directly to the media playback pipeline.

Get back to the associated `MainPage.xaml.cs` and continue editing:

1. At the top of the file, import the following extra modules:

   ```cs
   using TestAppUWP.Video;
   using Windows.Media.Core;
   using Windows.Media.Playback;
   using Windows.Media.MediaProperties;
   ```

2. Create a new utility method `CreateI420VideoStreamSource()` which builds a [`MediaStreamSource`](xref:Windows.Media.Core.MediaStreamSource) instance to encaspulate a given video stream encoded in I420 format, which is the encoding in which WebRTC provides its raw video frames. This method will be reused later for the remote video too.

   ```cs
   private MediaStreamSource CreateI420VideoStreamSource(
       uint width, uint height, int framerate)
   {
       if (width == 0)
       {
           throw new ArgumentException("Invalid zero width for video.", "width");
       }
       if (height == 0)
       {
           throw new ArgumentException("Invalid zero height for video.", "height");
       }
       // Note: IYUV and I420 have same memory layout (though different FOURCC)
       // https://docs.microsoft.com/en-us/windows/desktop/medfound/video-subtype-guids
       var videoProperties = VideoEncodingProperties.CreateUncompressed(
           MediaEncodingSubtypes.Iyuv, width, height);
       var videoStreamDesc = new VideoStreamDescriptor(videoProperties);
       videoStreamDesc.EncodingProperties.FrameRate.Numerator = (uint)framerate;
       videoStreamDesc.EncodingProperties.FrameRate.Denominator = 1;
       // Bitrate in bits per second : framerate * frame pixel size * I420=12bpp
       videoStreamDesc.EncodingProperties.Bitrate = ((uint)framerate * width * height * 12);
       var videoStreamSource = new MediaStreamSource(videoStreamDesc);
       videoStreamSource.BufferTime = TimeSpan.Zero;
       videoStreamSource.SampleRequested += OnMediaStreamSourceRequested;
       videoStreamSource.IsLive = true; // Enables optimizations for live sources
       videoStreamSource.CanSeek = false; // Cannot seek live WebRTC video stream
       return videoStreamSource;
   }
   ```

The `CreateI420VideoStreamSource()` method references the [`SampleRequested`](xref:Windows.Media.Core.MediaStreamSource.SampleRequested) event, which is invoked by the Media Foundation playback pipeline when it needs a new frame. We use the `VideoBridge` helper class to serve those frames.

1. At the top of the `MainPage` class, define two new variables: a [`MediaStreamSource`](xref:Windows.Media.Core.MediaStreamSource) for wrapping the local video stream and exposing it to the Media Foundation playback pipeline, and a [`VideoBridge`](https://github.com/microsoft/MixedReality-WebRTC/blob/master/examples/TestAppUwp/Video/VideoBridge.cs) for managing the delivery of the video frames. The video bridge is initialized with a queue capacity of 3 frames, which is generally enough for local video as it is not affected by network latency.

   ```cs
   private MediaStreamSource _localVideoSource;
   private VideoBridge _localVideoBridge = new VideoBridge(3);
   ```

2. Implement the `OnMediaStreamSourceRequested()` callback using the video bridge. As we plan to reuse that callback for the remote video, the code finds the suitable video bridge based on the source which invoked the event.

   ```cs
   private void OnMediaStreamSourceRequested(MediaStreamSource sender,
       MediaStreamSourceSampleRequestedEventArgs args)
   {
       VideoBridge videoBridge;
       if (sender == _localVideoSource)
           videoBridge = _localVideoBridge;
       else
           return;
       videoBridge.TryServeVideoFrame(args);
   }
   ```

3. In the `OnLoaded()` method where the video track source was created, subscribe to the [`I420AVideoFrameReady`](xref:Microsoft.MixedReality.WebRTC.VideoTrackSource.I420AVideoFrameReady) event.

   ```cs
    _webcamSource = await DeviceVideoTrackSource.CreateAsync();
    _webcamSource.I420AVideoFrameReady += LocalI420AFrameReady;
   ```

4. Implement the event handler by enqueueing the newly captured video frames into the bridge, which will later deliver them when the Media Foundation playback pipeline requests them.

   ```cs
   private void LocalI420AFrameReady(I420AVideoFrame frame)
   {
       _localVideoBridge.HandleIncomingVideoFrame(frame);
   }
   ```

## Starting the media playback

The last part is to actually start the playback pipeline when video frames start to be received from WebRTC. This is done lazily for two reasons:

- to avoid starving the Media Foundation playback pipeline if the WebRTC video track source takes some time to start delivering frames, which it generally does compared to the expectation of the playback pipeline.
- to get access to the frame resolution, which is not otherwise available from WebRTC.

Unfortunately at this time the capture framerate is not available, so we assume a framerate of 30 frames per second (FPS).

1. At the top of the `MainPage` class, add a boolean field to indicate whether the local video is playing. This is protected by a lock, because the [`I420AVideoFrameReady`](xref:Microsoft.MixedReality.WebRTC.VideoTrackSource.I420AVideoFrameReady) and the [`SampleRequested`](xref:Windows.Media.Core.MediaStreamSource.SampleRequested) events can be fired in parallel from multiple threads.

   ```cs
   private bool _localVideoPlaying = false;
   private object _localVideoLock = new object();
   ```

2. Modify the `LocalI420AFrameReady()` event handler to start the media player when the first WebRTC frame arrives.

   ```cs
   private void LocalI420AFrameReady(I420AVideoFrame frame)
   {
       lock (_localVideoLock)
       {
           if (!_localVideoPlaying)
           {
               _localVideoPlaying = true;

               // Capture the resolution into local variable useable from the lambda below
               uint width = frame.width;
               uint height = frame.height;

               // Defer UI-related work to the main UI thread
               RunOnMainThread(() =>
               {
                   // Bridge the local video track with the local media player UI
                   int framerate = 30; // assumed, for lack of an actual value
                   _localVideoSource = CreateI420VideoStreamSource(
                       width, height, framerate);
                   var localVideoPlayer = new MediaPlayer();
                   localVideoPlayer.Source = MediaSource.CreateFromMediaStreamSource(
                       _localVideoSource);
                   localVideoPlayerElement.SetMediaPlayer(localVideoPlayer);
                   localVideoPlayer.Play();
               });
           }
       }
       // Enqueue the incoming frame into the video bridge; the media player will
       // later dequeue it as soon as it's ready.
       _localVideoBridge.HandleIncomingVideoFrame(frame);
   }
   ```

   Some of the work cannot be carried during the execution of this event handler, which is invoked from an unspecified worker thread, because access to XAML UI elements must be done exclusively on the main UWP UI thread. Therefore we use a helper method which schedule this work for execution on that thread.

   > [!NOTE]
   > The use of `_localVideoSource` from the `OnMediaStreamSourceRequested()` event handler is not protected by the `_localVideoLock` lock. This is because the event cannot be fired until well after the `_localVideoSource` has been assigned a new value, so there is no race condition concern here. And since `_localVideoSource` is not further modified, we avoid acquiring that lock in the `OnMediaStreamSourceRequested()` to reduce the chances of contention. The lock is actually not needed at all at this point, since `_localVideoPlaying` is also only modified in the current `LocalI420AFrameReady()` event handler. But a typical application will provide some UI like a button to start and stop the local video, and therefore needs to synchronize access to `_localVideoPlaying` and `_localVideoSource`, at which point `OnMediaStreamSourceRequested()` will also need to acquire this lock.

3. Implement the `RunOnMainThread()` helper using the [`Dispatcher`](xref:Windows.UI.Core.CoreWindow.Dispatcher) of the current window.

   ```cs
   private void RunOnMainThread(Windows.UI.Core.DispatchedHandler handler)
   {
       if (Dispatcher.HasThreadAccess)
       {
           handler.Invoke();
       }
       else
       {
           // Note: use a discard "_" to silence CS4014 warning
           _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, handler);
       }
   }
   ```

4. In the `App_Suspending()` event handler, clear the media player of the `localVideoPlayerElement` control so that it repaint itself and avoids keeping the last video frame when the video is turned off.

   ```cs
   localVideoPlayerElement.SetMediaPlayer(null);
   ```

At this point the [MediaPlayerElement](xref:Windows.UI.Xaml.Controls.MediaPlayerElement) control and the WebRTC local video track are connected together. Launch the application again; the local webcam starts capturing video frame, which are displayed in the main window of the application.

----

Next : [A custom signaling solution](helloworld-cs-signaling-uwp.md)
