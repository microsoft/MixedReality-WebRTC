# Creating a peer connection

From this point we start building the scene. Because the MixedReality-WebRTC components are installed, and because we work now almost exclusively inside Unity, for brevity we will use the term _component_ to designate a Unity component, that is a class deriving from [`MonoBehaviour`](https://docs.unity3d.com/ScriptReference/MonoBehaviour.html).

Create a new [`GameObject`](https://docs.unity3d.com/ScriptReference/GameObject.html) with a [`PeerConnection`](xref:Microsoft.MixedReality.WebRTC.Unity.PeerConnection) component:

- In [the **Hierarchy** window](https://docs.unity3d.com/Manual/Hierarchy.html), select **Create** > **Create Empty** to add a new [`GameObject`](https://docs.unity3d.com/ScriptReference/GameObject.html) to the scene.
- In [the **Inspector** window](https://docs.unity3d.com/Manual/UsingTheInspector.html), select **Add Component** > **MixedReality-WebRTC** > **PeerConnection** to add a [`PeerConnection`](xref:Microsoft.MixedReality.WebRTC.Unity.PeerConnection) component to that new object.
- At the top of the **Inspector** window, rename the newly-created game object to something memorable like "MyPeerConnection". You can also rename this object in the **Hierarchy** window directly (for example by pressing **F2** when selected).

![Create a new GameObject with a PeerConnection component](helloworld-unity-5.png)

The [`PeerConnection`](xref:Microsoft.MixedReality.WebRTC.Unity.PeerConnection) component provided by the Unity integration of MixedReality-WebRTC has various settings to configure its behaviour. For the moment you can leave the default values. We will come back to it later in particular to add some transceivers.

----

Next : [Creating a signaler](helloworld-unity-signaler.md)
