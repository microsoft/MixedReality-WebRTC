# Adding local video

There are two different aspects covered by the concept of _local video_:

- Capturing some video feed from a local camera to send it to the remote peer
- Displaying locally the captured video feed

Both are optional, although the second one alone simply corresponds to capturing a displaying a local webcam and doesn't require WebRTC. So we generally want the first one in a scenario where an application needs WebRTC. The second one is application-dependent, and even within one given application can be toggled ON and OFF by the user.

Both cases however are covered by the [`LocalVideoSource`](xref:Microsoft.MixedReality.WebRTC.Unity.LocalVideoSource) component. This components serves as a bridge between a local video capture device (camera), the peer connection, and an optional video player to render the video feed locally.

## Adding a local video source

Because there is generally a single local video source, there is no need to create a new game object, and in this tutorial for simplicity we will add a [`LocalVideoSource`](xref:Microsoft.MixedReality.WebRTC.Unity.LocalVideoSource) component to the same game object as the peer connection:

- In the **Hierarchy** window, select the game object with the peer connection component
- In the **Inspector** window, press the **Add Component** button at the bottom of the window, and select **MixedReality-WebRTC** > **LocalVideoSource**
- This component needs to know which peer connection to use. Once again, use the asset selection window to assign our peer connection to the **Peer Connection** property.

![Create a local video source assigned to our peer connection](helloworld-unity-8.png)

The local video source component contains several interesting properties:

- The **Auto Start Capture** property instructs the component to open the video capture device (webcam) automatically as soon as possible. This enables starting local video playback even before the peer connection is established.
- The **Enable Mixed Reality Capture** property tells the component it should attempt to open the video capture device with MRC enabled, if supported.
- The **Auto Add Track** property allows automatically adding a video track to the peer connection and start sending the video feed to the remote peer once the connection is established. If not checked, the user has to manually call some method to add that track.

These are good defaults values to start, and we will leave them as is.

## Adding a media player

We said before that the [`LocalVideoSource`](xref:Microsoft.MixedReality.WebRTC.Unity.LocalVideoSource) component covers both sending the video feed to the remote peer and displaying it locally. This is partially incorrect. The local video source plugs into the peer connection and the video capture device, and exposes some C# event to access the video frames produced by that video device. But it does not do any rendering itself.

In order to render the video frames of the local video capture device, MixedReality-WebRTC offers a simple [`MediaPlayer`](xref:Microsoft.MixedReality.WebRTC.Unity.MediaPlayer) component which uses a Unity [`Texture2D`](https://docs.unity3d.com/ScriptReference/Texture2D.html) object and renders the video frames to it. This texture is then applied to the material of a [`Renderer`](https://docs.unity3d.com/ScriptReference/Renderer.html) component to be displayed in Unity on a mesh.

Let's create a new game object with a [`MediaPlayer`](xref:Microsoft.MixedReality.WebRTC.Unity.MediaPlayer) component on it. As usual:

- In the **Hierarchy** window, select **Create** > **Create empty** to add a new [`GameObject`](https://docs.unity3d.com/ScriptReference/GameObject.html) to the scene. You can rename this object in the **Hierarchy** window directly (for example by pressing **F2** when selected), or by selecting it and going to the top of the **Inspector** window.
- In the **Inspector** window, press the **Add Component** button at the bottom of the window, and select **MixedReality-WebRTC** > **MediaPlayer**

This time however Unity will not create the component, and instead display a somewhat complex error message:

![Unity displays an error message when trying to create a MediaPlayer component](helloworld-unity-9.png)

What the message means is that the [`MediaPlayer`](xref:Microsoft.MixedReality.WebRTC.Unity.MediaPlayer) component requires a [`Renderer`](https://docs.unity3d.com/ScriptReference/Renderer.html) component on the same game object, and Unity lists all possible implementation of a renderer (all classes deriving from [`Renderer`](https://docs.unity3d.com/ScriptReference/Renderer.html)). Although all renderers might work, in our case the most simple is to add a [`MeshRenderer`](https://docs.unity3d.com/ScriptReference/MeshRenderer.html) component. If you are familiar with Unity, you also know that the renderer needs a source mesh in the form of a [`MeshFilter`](https://docs.unity3d.com/ScriptReference/MeshFilter.html) component.

So for each component, in the **Inspector** window, press the **Add Component** button at the bottom of the window, and select successively and in order:

1. **Mesh** > **MeshFilter**
2. **Mesh** > **MeshRenderer**
3. **MixedReality-WebRTC** > **MediaPlayer**

