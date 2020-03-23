# Adding local video

There are two different aspects covered by the concept of _local video_:

- Capturing some video feed from a local camera to send it to the remote peer
- Displaying locally the captured video feed

Both are optional, although the second one alone simply corresponds to capturing a displaying a local webcam and doesn't require WebRTC. So we generally want the first one in a scenario where an application needs WebRTC. The second one is application-dependent, and even within one given application can be toggled ON and OFF by the user.

Both cases however are covered by the [`VideoSender`](xref:Microsoft.MixedReality.WebRTC.Unity.VideoSender) component. This components serves as a bridge between a local video track, the peer connection, and an optional video player to render the video feed locally. The video sender itself is an abstract class, whose most useful derived class is the [`WebcamSource`](xref:Microsoft.MixedReality.WebRTC.Unity.WebcamSource) component, which represents a video track capturing its video frames from a _local video capture device_ (or _webcam_ for short, as an abuse of language; but other video capture devices beyond webcams are supported, like the HoloLens camera).

## Adding a webcam source

For clarity we will create a new game object and add a [`WebcamSource`](xref:Microsoft.MixedReality.WebRTC.Unity.WebcamSource) component. It may sound superfluous at the moment to create a new game object, as we could add the webcam source to the same game object already owning the peer connection component, but this will prove more clear and easy to manipulate later.

- In the **Hierarchy** window, select **Create** > **Create Empty**.
- In the **Inspector** window, rename the newly-created game object to something memorable like "LocalMediaPlayer".
- Press the **Add Component** button at the bottom of the window, and select **MixedReality-WebRTC** > **WebcamSource**.

![Create a local video source assigned to our peer connection](helloworld-unity-8.png)

The local video source component contains several interesting properties, divided into 2 groups.

### Video capture properties

The video capture properties are related to the video capture itself, that is how the component access the webcam and captures video frames from it, independently of WebRTC.

- The **Start capture when enabled** checkbox, which corresponds to the [`MediaSender.AutoStartOnEnabled`](xref:Microsoft.MixedReality.WebRTC.Unity.MediaSender.AutoStartOnEnabled) property, instructs the component to open the video capture device (webcam) automatically as soon as possible, that is once both:
  - the component is enabled, that is [`MonoBehaviour.OnEnable()`](https://docs.unity3d.com/ScriptReference/MonoBehaviour.OnEnable.html) executed; and
  - the peer connection is initialized, that is the [`PeerConnection.Initialized`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.Initialized) event was raised.

  This enables starting local video playback even before the connection to the remote peer is established.

  If unchecked, then the user has to manually call [`MediaSender.StartCaptureAsync()`](xref:Microsoft.MixedReality.WebRTC.Unity.MediaSender.StartCaptureAsync).

- The **Capture format** combobox, which corresponds to the [`WebcamSource.FormatMode`](xref:Microsoft.MixedReality.WebRTC.Unity.WebcamSource.FormatMode) property, allows switching between two different modes to control how the _video capture format_ (resolution and framerate, essentially) is choosen:

  - In **Automatic** mode, the best video capture format is automatically selected. In general this means the first format available is chosen. On HoloLens devices however, some extra steps are taken to use the low-power video profile and reduce the capture resolution in order to save the battery and lower the CPU usage.

  - In **Manual** mode, the user can apply a set of constraints to the list of capture formats the device supports. This allows restricting the video capture formats the algorithm can choose from to a subset of the supported format, and even to a single known format. Note however that over-constraining the algorithm may result in no format being available, and the component failing to open the video capture device. In general constraints must match supported capture formats as enumerated by [`PeerConnection.GetVideoCaptureFormatsAsync()`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.GetVideoCaptureFormatsAsync(System.String)).

- The **Enable Mixed Reality Capture (MRC)** checkbox, which corresponds to the [`WebcamSource.EnableMixedRealityCapture`](xref:Microsoft.MixedReality.WebRTC.Unity.WebcamSource.EnableMixedRealityCapture) property, tells the component it should attempt to open the video capture device with MRC enabled, if supported. If the device does not support MRC, then this is silently ignored.

### WebRTC track properties

The WebRTC track properties are related to the WebRTC video track that the component manages to send the video frames to the remote peer.

- The **Track Name** property is the name of the WebRTC track. This can be left empty; the implementation will generate a valid name for it.

- The **Preferred Video Codec** property allows trying to force a particular video encoding for the track. This property takes the SDP name of the video codec (a list of predefined values are also available). Note that this codec will be used for the track **only if the implementation actually supports it**. Otherwise the default codec selected by the implementation will be used.

  > [!WARNING]
  > **The H.264 video codec is only available on UWP platforms** (including HoloLens). This means selecting this codec will prevent a UWP device from connecting with a non-UWP device if the UWP device makes the connection offer, as the non-UWP device will be unable to handle the H.264 request.
  >
  > More generally, for maximum compatibility, or when debugging some video issue, it is recommended to leave this setting to **None** to allow the implementation to maximize the chances of finding a compatible video codec that both peers support.

## Adding a media player

We said before that the [`WebcamSource`](xref:Microsoft.MixedReality.WebRTC.Unity.WebcamSource) component covers both sending the video feed to the remote peer and displaying it locally. This is partially incorrect. The webcam source plugs into the peer connection and the video capture device, and exposes some C# event to access the video frames produced by that video device. But it does not do any rendering itself.

In order to render the video frames of the local video capture device, MixedReality-WebRTC offers a simple [`MediaPlayer`](xref:Microsoft.MixedReality.WebRTC.Unity.MediaPlayer) component which uses a Unity [`Texture2D`](https://docs.unity3d.com/ScriptReference/Texture2D.html) object and renders the video frames to it. This texture is then applied to the material of a [`Renderer`](https://docs.unity3d.com/ScriptReference/Renderer.html) component to be displayed in Unity on a mesh.

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
- The built-in **Quad** mesh size is quite small for rendering a video, so go to the **Transform** component and increase the scale to `(5,5,1)`.
- In the **Mesh Renderer** component, expand the **Materials** array and set the first material **Element 0** to the  `YUVFeedMaterial` material located in the `Assets/Microsoft.MixedReality.WebRTC.Unity/Materials` folder. This instructs Unity to use that special material and its associated shader to render the video texture on the quad mesh. More on that later.
- In the **Media Player** component, set the **Video Source** property to the local video source component previously added to the same game object. This instructs the media player to connect to the local video source for retrieving the video frames that it will copy to the video texture for rendering.

This should result in a setup looking like this:

![Configuring the media player to render the local video source](helloworld-unity-10.png)

And the **Game** view should display a pink square, which materializes the quad mesh:

![The game view shows the quad in pink](helloworld-unity-11.png)

A word on the `YUVFeedMaterial` material here. The video frames coming from the local video source are encoded using the I420 format. Unity on the other hand, and more specifically the GPU it abstracts, generally don't support directly rendering I420-encoded textures. So the `YUVFeedMaterial` material is using a custom shader called `YUVFeedShader (Unlit)` to load the I420-encoded video frame from the video texture, and convert it to ARGB on the fly before rendering the quad. This GPU-based conversion is very efficient and avoids any software processing on the CPU before uploading the video texture to the GPU. This is how `LocalVideoSource` is able to directly copy the I420-encoded video frames coming from the WebRTC core implementation into a texture without further processing, and `MediaPlayer` is able to render them on a quad mesh.

## Test the local video

At this point the local audio source and the media player are configured to open the local video capture device (webcam) of the local machine the Unity Editor is running on, and display the video feed to that quad mesh in the scene.

Press the **Play** button in the Unity Editor. After a few seconds (depending on the device) the video should appear over the quad mesh.

![Local video feed rendering in the Unity editor](helloworld-unity-12.png)

----

Next : [Adding remote video](helloworld-unity-remotevideo.md)
