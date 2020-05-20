# A custom signaling solution

_Signaling_ is the process of communicating with a remote endpoint with the intent of establishing a peer-to-peer connection. The WebRTC standard does not enforce any specific protocol or solution for WebRTC signaling; instead it simply states that some opaque messages must be transported between the remote peers by whatever mean the developer choses, its _signaling solution_.

In general, the signaling solution involves a third-party server in addition of the two peers trying to connect to each other. Using a third-party server may seem counter-intuitive at first when dealing with peer-to-peer connection, but in general that third-party server is an easy-to-reach server (public IP) which acts as a relay and enables WebRTC to discover a direct route between the two peers even in complex network scenarios (one or both peers behind a NAT) where it would otherwise be impossible for the two peers to directly discover each other. The service provided by the signaling server is also sometimes referred to as some _discovery service_ or _identity service_ (because it makes the identity of each peer available to the other).

## NamedPipeSignaler

In this tutorial we use the `NamedPipeSignaler` found in `examples/TestNetCoreConsole/NamedPipeSignaler` in the GitHub repository. This is a simple signaling solution based as the name implies on named pipes, which allows local peer discovery and connection out of the box on a local host without any configuration. This is not a production-ready solution, but for this tutorial it has the benefit of being very simple, sidestepping any networking configuration and potential issue.

### Install

The easiest way to consume the `NamedPipeSignaler` class in the `TestNetCoreConsole` sample app is to copy the `examples/TestNetCoreConsole/NamedPipeSignaler.cs` file alongside the `TestNetCoreConsole.csproj` project. This avoids the need for any reference setup in the project, or any other kind of project configuration.

### Pipe creation

There is no need to understand how the `NamedPipeSignaler` class works for this tutorial. But for the sake of curiosity, this is how the connection is established (the reader can skip to the **Setup the signaler** section below if not interested):

- Try to create a pipe server.
  - If that succeeds, then this peer is the first peer and will _act as server_.
  - If that fails, then another peer already created that pipe server, so this peer will _act as client_.
- If acting as server:
  - Wait for the remote peer to connect its client pipe to this server.
  - Create a _reverse_ pipe client and connect to the _reverse_ pipe server of the remote peer.
- If acting as client:
  - Connect to the pipe server created by the other peer.
  - Create a _reverse_ pipe server, and wait for the server to connect back with its _reverse_ pipe client.
- At this point, both peer have a client pipe for sending data and a server pipe for receiving data, and can communicate.
- Start a background task to read incoming messages from the remote peer, and wait.

We note here that despite WebRTC relying on peer-to-peer connection, the two peers are not strictly equal. This is not only due to the fact that this particular signaling solution is assymetric, but also to the assymetric nature of establishing a WebRTC connection. In general we refer to the peer initiating the connection as the _caller_ and the other peer as the _callee_.

## Setup the signaler

Continue editing the `Program.cs` file and append the following:

1. Create a signaler associated with the existing peer connection.

   ```cs
   var signaler = new NamedPipeSignaler.NamedPipeSignaler(pc, "testpipe");
   ```

2. Connect handlers to the signaler's messages, and forward them to the peer connection.

   ```cs
   signaler.SdpMessageReceived += async (SdpMessage message) => {
       // Note: we use 'await' to ensure the remote description is applied
       // before calling CreateAnswer(). Failing to do so will prevent the
       // answer from being generated, and the connection from establishing.
       await pc.SetRemoteDescriptionAsync(message);
       if (message.Type == SdpMessageType.Offer)
       {
           pc.CreateAnswer();
       }
   };

   signaler.IceCandidateReceived += (IceCandidate candidate) => {
       pc.AddIceCandidate(candidate);
   };
   ```

   In addition of forwarding the messages to the peer connection, we also automatically call [`PeerConnection.CreateAnswer()`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.CreateAnswer) on the _callee_ peer as soon as the remote offer received from the _caller_ has been applied. This ensures the minimum amount of latency, but also means the _callee_ automatically accepts any incoming call. Alternatively, a typical application would display some user feedback and wait for confirmation to accept the incoming call.

3. Start the signaler and connect it to the remote peer's signaler.

   ```cs
   await signaler.StartAsync();
   ```

   This last call will block until the two signalers are connected with each other.

At this point the signaler is functional. However as pointed above it will wait for a second instance of the `TestNetCoreConsole` app to connect. Currently unless the local machine has at least 2 webcams and 2 microphones then this cannot work because both instances will attempt to capture the webcam and microphone, and one of them will fail to do so and terminate before the program even reach the point where the signaler starts.

## Optional audio and video capture

In order to test the signaler with 2 instances of `TestNetCoreConsole` and a single microphone and webcam, we need one of those instances *not* to attempt to open the audio and video capture devices. For this, we had some command-line arguments to control the audio and video capture.

Continue editing the `Program.cs` file:

1. At the top of the `Main` function, check if the audio and video capture arguments are present on the command-line arguments provided by the user. We name those arguments `-v`/`--video` to enable video capture, and `-a`/`--audio` to enable audio capture.

   ```cs
   bool needVideo = Array.Exists(args, arg => (arg == "-v") || (arg ==    "--video"));
   bool needAudio = Array.Exists(args, arg => (arg == "-a") || (arg ==    "--audio"));
   ```

2. Wrap the calls to `AddLocal(Audio|Video)TrackAsync` into `if` blocks using the boolean just defined. We also print some console message, so that the user can confirm whether the flags were indeed taken into account. This is useful to avoid mistakes since we will be running 2 instances of the app, one with the flags and one without. We also move the code for the transeivers inside that block.

   ```cs
   // Record video from local webcam, and send to remote peer
   if (needVideo)
   {
       Console.WriteLine("Opening local webcam...");
       localVideoTrack = await LocalVideoTrack.CreateFromDeviceAsync();
       videoTransceiver = pc.AddTransceiver(MediaKind.Video);
       videoTransceiver.DesiredDirection = Transceiver.Direction.SendReceive;
       videoTransceiver.LocalVideoTrack = localVideoTrack;
   }

   // Record audio from local microphone, and send to remote peer
   if (needAudio)
   {
       Console.WriteLine("Opening local microphone...");
       localAudioTrack = await LocalAudioTrack.CreateFromDeviceAsync();
       audioTransceiver = pc.AddTransceiver(MediaKind.Audio);
       audioTransceiver.DesiredDirection = Transceiver.Direction.SendReceive;
       audioTransceiver.LocalAudioTrack = localAudioTrack;
   }
   ```

## Establishing a signaler connection

At this point the sample app is ready to establish a _signaler_ connection. That is, 2 instances of the `TestNetCoreConsole` app can be launched, and their `NamedPipeSignaler` instances will connect to each other. Note however that we are not done yet with the peers, so the WebRTC peer-to-peer connection itself will not be established yet.

Start 2 instances of the sample app:

- one with the audio/video flags, the _capturer_
- one without any flag, the _receiver_

**Terminal #1 (capturer)**

```shell
dotnet run TestNetCoreConsole -- --audio --video
```

**Terminal #2 (receiver)**

```shell
dotnet run TestNetCoreConsole
```

The two terminals should print some messages and eventually indicate that the signaler connection was successful:

```shell
Signaler connection established.
```

![Signaler connected](cs5.png)

----

Next : [Establishing a WebRTC connection](helloworld-cs-connection-core3.md)
