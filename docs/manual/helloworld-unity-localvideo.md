# Adding local media

In this section we perform 3 tasks:

- Add local video
- Add local audio
- Add a media player to render the local video

## Local video

There are three different concepts covered by the term _local video_:

1. Locally generating some video frames, often by capturing them from a _video capture device_ (_e.g._ webcam) available on the local host, or alternatively obtaining those frames by any other mean (procedurally generated, captured from another source, _etc._). This corresponds to the concept of a _video source_ in WebRTC terminology.
2. Sending those video frames to the remote peer, which is the most interesting aspect and requires WebRTC, and corresponds to the concept of _video track_ in WebRTC terminology.
3. Accessing locally the captured video frames, for example to display them for user feedback. This is application-dependent, and even within one given application can generally be toggled ON and OFF by the user. This corresponds to the concept of _video frame callback_ in MixedReality-WebRTC, and is not an official WebRTC concept from the standard.

Capturing some video feed from a local video capture device is covered by the [`WebcamSource`](xref:Microsoft.MixedReality.WebRTC.Unity.WebcamSource) component, which represents a video track capturing its video frames from a webcam.

> [!NOTE]
> The term _webcam_ as used in the context of the [`WebcamSource`](xref:Microsoft.MixedReality.WebRTC.Unity.WebcamSource) component colloquially refers to any _local video capture device_, and is an abuse of language to shorten the latter. This term therefore includes other capture devices not strictly considered webcams, like for example the HoloLens 1 and HoloLens 2 cameras.

Because the [`WebcamSource`](xref:Microsoft.MixedReality.WebRTC.Unity.WebcamSource) component derives from the abstract [`VideoSender`](xref:Microsoft.MixedReality.WebRTC.Unity.VideoSender) component which encapsulates a WebRTC local video track, attaching that track to a peer connection covers the second concept of streaming video to a remote peer.

Finally, the [`VideoSender`](xref:Microsoft.MixedReality.WebRTC.Unity.VideoSender) component also serves as a bridge between a local video track and an optional video player to render the video feed locally, covering the last concept if needed.

## Adding a webcam source

For clarity we will create a new game object and add a [`WebcamSource`](xref:Microsoft.MixedReality.WebRTC.Unity.WebcamSource) component. It may sound superfluous at the moment to create a new game object, as we could add the webcam source to the same game object already owning the peer connection component, but this will prove more clear and easy to manipulate later.

- In the **Hierarchy** window, select **Create** > **Create Empty**.
- In the **Inspector** window, rename the newly-created game object to something memorable like "LocalMediaPlayer".
- Press the **Add Component** button at the bottom of the window, and select **MixedReality-WebRTC** > **Webcam Source**.

![Create a webcam source](helloworld-unity-8.png)

The webcam source component contains several interesting properties, divided into 2 groups. We detail these properties below for reference, but for now we can leave all of them to their default values.

> [!IMPORTANT]
> When using Mixed Reality Capture (MRC) on HoloLens, the Unity project needs to be configured to **Enable XR support**. This generates an exclusive-mode application (fullscreen), which is required to get access to MRC, for security and privacy reasons. Otherwise Unity by default generates a shared application (slate app), and MRC access will be denied by the OS.

### Video capture properties

The video capture properties are related to the video capture itself, that is how the component access the webcam and captures video frames from it, independently of WebRTC.

- The **Start capture when enabled** checkbox, which corresponds to the [`MediaSender.AutoStartOnEnabled`](xref:Microsoft.MixedReality.WebRTC.Unity.MediaSender.AutoStartOnEnabled) property, instructs the component to open the video capture device (webcam) automatically as soon as possible, that is once both:
  - the component is enabled, that is [`MonoBehaviour.OnEnable()`](https://docs.unity3d.com/ScriptReference/MonoBehaviour.OnEnable.html) executed; and
  - the peer connection is initialized, that is the [`PeerConnection.Initialized`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.Initialized) event was raised.

  This property is checked by default. This makes the component work out of the box, as a convenience for developers to get started. If unchecked, then the application has to explicitly call [`MediaSender.StartCaptureAsync()`](xref:Microsoft.MixedReality.WebRTC.Unity.MediaSender.StartCaptureAsync) once the two conditions above are true. This can be useful if the application wants to ensure an explicit user action to start video capture, for example where privacy is a concern.

- The **Capture format** combobox, which corresponds to the [`WebcamSource.FormatMode`](xref:Microsoft.MixedReality.WebRTC.Unity.WebcamSource.FormatMode) property, allows switching between two different modes to control how the _video capture format_ (resolution and framerate, essentially) is choosen:

  - In **Automatic** mode, the best video capture format is automatically selected.

    In general this means the first format available is chosen. On HoloLens devices however, some extra steps are taken internally to force the use of the low-power video profile and reduce the capture resolution in order to save the battery and lower the CPU usage.

  - In **Manual** mode, the user can apply a set of constraints to the list of capture formats the device supports.

    This allows restricting the video capture formats the selection algorithm can choose from to a subset of all the formats supported by the video capture device, and even to a single known format if needed. Note however that over-constraining the algorithm may result in no format being available, and the [`WebcamSource`](xref:Microsoft.MixedReality.WebRTC.Unity.WebcamSource) component failing to open the video capture device.

    In general it is recommended **not** to assign hard-coded resolution / framerate constraints at edit time, unless the application only target a specific kind of device with well-known immutable capture capabilities, and instead enumerate at runtime with [`PeerConnection.GetVideoCaptureFormatsAsync()`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.GetVideoCaptureFormatsAsync(System.String)) the capture formats actually supported by the video capture device, then use constraints to select one or some of those formats.

- The **Enable Mixed Reality Capture (MRC)** checkbox, which corresponds to the [`WebcamSource.EnableMixedRealityCapture`](xref:Microsoft.MixedReality.WebRTC.Unity.WebcamSource.EnableMixedRealityCapture) property, tells the component it should attempt to open the video capture device with MRC enabled, if supported. If the device does not support MRC, then this is silently ignored.

  See the important remark about MRC and exclusive mode application above.

### WebRTC track properties

The WebRTC track properties are related to the WebRTC video track that the component manages to send the video frames to the remote peer.

- The **Track Name** property, which corresponds to the [`VideoSender.Track`](xref:Microsoft.MixedReality.WebRTC.Unity.VideoSender.Track) field, is the name of the WebRTC track. This can be left empty; the implementation will generate a valid name for it.

- The **Preferred Video Codec** property, which corresponds to the [`VideoSender.PreferredVideoCodec`](xref:Microsoft.MixedReality.WebRTC.Unity.VideoSender.PreferredVideoCodec) field, allows attempting to force the use of a particular video codec for the track. This property takes the SDP name of the video codec (a list of predefined values are also available). Note that this codec will be used for the track **only if the implementation actually supports it**. Otherwise the default codec selected by the implementation will be used and the value supplied by the user ignored.

  > [!WARNING]
  > **The H.264 video codec is only available on UWP platforms** (including HoloLens). This means selecting this codec will prevent a UWP device from connecting with a non-UWP device if the UWP device makes the connection offer, as the non-UWP device will be unable to handle the H.264 request.
  >
  > More generally, for maximum compatibility, or when debugging some video issue, it is recommended to leave this setting to **None** to allow the implementation to maximize the chances of finding a compatible video codec that both peers support.

## Local audio

The concept of _local audio_ is very similar to the one of _local video_ and can also be decomposed into generating some audio frames, sending them to a remote peer through an audio track, and optionally outputting some local feedback to the user.

Capturing some audio feed from a local audio capture device is covered by the [`MicrophoneSource`](xref:Microsoft.MixedReality.WebRTC.Unity.MicrophoneSource) component, which represents an audio track capturing its audio frames from a microphone.

## Adding a microphone source

We continue to use the "LocalMediaPlayer" game object created earlier, and add a [`MicrophoneSource`](xref:Microsoft.MixedReality.WebRTC.Unity.MicrophoneSource) component to it.

- In the **Hierarchy** window, make sure the "LocalMediaPlayer" game object is selected.
- In the **Inspector** window, press the **Add Component** button at the bottom of the window, and select **MixedReality-WebRTC** > **Microphone Source**.

![Create a microphone source](helloworld-unity-8b.png)

The microphone source component contains few properties. In particular there is currently no option to configure the audio capture. We can leave all properties with their default values.

## Adding transceivers

So far the components added are only creating audio and video tracks, but there is no link with any peer connection. This link is achieved through _transceivers_.

A _transceiver_ is a "media pipe" (audio or video) through which the peer connection transport the media between the local and remote peers. The transceivers are configured on the [`PeerConnection`](xref:Microsoft.MixedReality.WebRTC.Unity.PeerConnection) component directly, and reference the various [`AudioSender`](xref:Microsoft.MixedReality.WebRTC.Unity.AudioSender) and [`VideoSender`](xref:Microsoft.MixedReality.WebRTC.Unity.VideoSender) components to be used to send and receive media through them.

Let's add an audio transceiver and a video tranceiver, and attach to them the microphone and webcam sources to instruct the peer connection to send those media to the remote peer:

- In the **Hierarchy** window, select the game object with the peer connection component.
- In the **Inspector** window, under the **Transceivers** list, press the **+ Audio** and the **+ Video** to add an audio transceiver and a video transceiver, respectively.
- For the first transceiver (audio), assign the **Audio Sender** property to the [`MicrophoneSource`](xref:Microsoft.MixedReality.WebRTC.Unity.MicrophoneSource) component created previously.
- For the second transceiver (video), assign the **Video Sender** property to the [`WebcamSource`](xref:Microsoft.MixedReality.WebRTC.Unity.WebcamSource) component created previously.

![Create a microphone source](helloworld-unity-8c.png)

Each transceiver displays an icon indicating the type of media (audio or video). Under that media icon, a direction icon indicates the desired direction of the transceiver based on the configured senders and receivers. Here we added a sender to each transceiver, therefore the direction icon points to the right (send) only.

## Adding a media player

We said before that the [`VideoSender`](xref:Microsoft.MixedReality.WebRTC.Unity.VideoSender) base component covers both sending the video feed to the remote peer and displaying it locally. This was a simplification, and is partially incorrect. The video sender plugs into the video track and exposes some C# event to access the video frames that the video track consumes. But it does not do any rendering itself.

In order to simplify rendering those video frames, MixedReality-WebRTC offers a simple [`MediaPlayer`](xref:Microsoft.MixedReality.WebRTC.Unity.MediaPlayer) component which uses some Unity [`Texture2D`](https://docs.unity3d.com/ScriptReference/Texture2D.html) to render the video frames. The textures are then applied to the material of a [`Renderer`](https://docs.unity3d.com/ScriptReference/Renderer.html) component via a custom shader to be displayed in Unity on a mesh.

> [!IMPORTANT]
> The local audio captured by the [`MicrophoneSource`](xref:Microsoft.MixedReality.WebRTC.Unity.MicrophoneSource) component is directly sent to the remote peer, and unlike video there is currently no possible way to access the raw audio frames captured by this component. This is a limitation of the underlying audio capture implementation (see issue [#92](https://github.com/microsoft/MixedReality-WebRTC/issues/92)). It is anyway critical to add this component to capture the audio and send it to the remote peer.

Let's add a [`MediaPlayer`](xref:Microsoft.MixedReality.WebRTC.Unity.MediaPlayer) component on our game object:

- In the **Inspector** window, press the **Add Component** button at the bottom of the window, and select **MixedReality-WebRTC** > **MediaPlayer**

This time however Unity will not create the component, and instead display a somewhat complex error message:

![Unity displays an error message when trying to create a MediaPlayer component](helloworld-unity-9.png)

What the message means is that the [`MediaPlayer`](xref:Microsoft.MixedReality.WebRTC.Unity.MediaPlayer) component requires a [`Renderer`](https://docs.unity3d.com/ScriptReference/Renderer.html) component on the same game object, and Unity lists all possible implementation of a renderer (all classes deriving from [`Renderer`](https://docs.unity3d.com/ScriptReference/Renderer.html)). Although all renderers might work, in our case the most simple is to add a [`MeshRenderer`](https://docs.unity3d.com/ScriptReference/MeshRenderer.html) component. If you are familiar with Unity, you also know that the renderer needs a source mesh in the form of a [`MeshFilter`](https://docs.unity3d.com/ScriptReference/MeshFilter.html) component.

So for each component, in the **Inspector** window, press the **Add Component** button at the bottom of the window, and select successively and in order:

1. **Mesh** > **MeshFilter**
2. **Mesh** > **MeshRenderer**
3. **MixedReality-WebRTC** > **MediaPlayer**

After that, set the component properties as follow:

- In the **Mesh Filter** component, set the **Mesh** property to the built-in Unity **Quad** mesh. This is a simple square mesh on which the texture containing the video feed will be applied.
- The built-in **Quad** mesh size is quite small for rendering a video, so go to the **Transform** component and increase the scale to `(4,3,1)`. This will produce a 4:3 aspect ratio for the mesh, which is generally, if not equal to, at least pretty close to the actual video frame aspect ratio, and therefore will reduce/prevent distorsions.
- In the **Mesh Renderer** component, expand the **Materials** array and set the first material **Element 0** to the  `YUVFeedMaterial` material located in the `Assets/Microsoft.MixedReality.WebRTC.Unity/Materials` folder. This instructs Unity to use that special material and its associated shader to render the video texture on the quad mesh. More on that later.
- In the **Media Player** component, set the **Video Source** property to the webcam source component previously added to the same game object. This instructs the media player to connect to the webcam source for retrieving the video frames that it will copy to the video texture for rendering.
- Note that the **Audio Source** property is left to **None**. There are 2 reasons for this:
  - The most important is that unlike video, in most contexts local audio feedback is generally not recommended from a user experience perspective, as hearing one's own voice while talking is distracting.
  - More practically, assigning the [`MicrophoneSource`](xref:Microsoft.MixedReality.WebRTC.Unity.MicrophoneSource) component has currently no effect due to issue [#92](https://github.com/microsoft/MixedReality-WebRTC/issues/92), as the [`MediaPlayer`](xref:Microsoft.MixedReality.WebRTC.Unity.MediaPlayer) component cannot access the raw audio frames captured by the microphone.

This should result in a setup looking like this:

![Configuring the media player to render the webcam source](helloworld-unity-10.png)

And the **Game** view should display a black rectangle, which materializes the quad mesh:

![The Game view shows the black quad](helloworld-unity-11.png)

A word about the `YUVFeedMaterial` material here. The video frames coming from the webcam source are encoded using the I420 format. This format contains 3 separate components : the luminance Y, and the chroma U and V. Unity on the other hand, and more specifically the GPU it abstracts, generally don't support directly rendering I420-encoded images. So the [`MediaPlayer`](xref:Microsoft.MixedReality.WebRTC.Unity.MediaPlayer) component is uploading the video data for the Y, U, and V channels into 3 separate [`Texture2D`](https://docs.unity3d.com/ScriptReference/Texture2D.html) objects, and the `YUVFeedMaterial` material is using a custom shader called `YUVFeedShader (Unlit)` to sample those textures and convert the YUV value to an ARGB value on the fly before rendering the quad with it. This GPU-based conversion is very efficient and avoids any software processing on the CPU before uploading the video data to the GPU. This is how `WebcamSource` is able to directly copy the I420-encoded video frames coming from the WebRTC core implementation into some textures without further processing, and `MediaPlayer` is able to render them on a quad mesh.

## Test the local video

At this point the webcam source and the media player are configured to open the local video capture device (webcam) of the local machine the Unity Editor is running on, and display the video feed to that quad mesh in the scene. The audio is also captured from the microphone, but since there is no feedback for this, there is no opportunity to observe it.

Press the **Play** button in the Unity Editor. After a few seconds or less (depending on the device) the video should appear over the quad mesh.

![Local video feed rendering in the Unity editor](helloworld-unity-12.png)

----

Next : [Adding remote video](helloworld-unity-remotevideo.md)
