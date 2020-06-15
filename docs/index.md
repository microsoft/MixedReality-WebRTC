---
uid: index
title: Index
---
# MixedReality-WebRTC documentation (latest)

This is the MixedReality-WebRTC documentation for [the `master` branch](https://github.com/microsoft/MixedReality-WebRTC/tree/master/), which contains the latest features and API changes. This latest API is **incompatible with the v1.x NuGet packages**.

For the documentation corresponding to other branches, including [the `release/1.0` branch](https://github.com/microsoft/MixedReality-WebRTC/tree/release/1.0/) from which the NuGet 1.x packages are built, use the drop-down selection at the top right of this page.

## User Manual

[Introduction](manual/introduction.md)

- Getting started
  - [Download](manual/download.md)
  - [Installation](manual/installation.md)
  - [Migration Guide](manual/migration-guide.md)
  - [Building from sources](manual/building.md)
  - [C# tutorial (Desktop)](manual/cs/helloworld-cs-core3.md)
  - [C# tutorial (UWP)](manual/cs/helloworld-cs-uwp.md)
  - [Unity tutorial](manual/helloworld-unity.md)
- C# library
  - [Feature Overview](manual/cs/cs.md)
  - [Tutorial (Desktop)](manual/cs/helloworld-cs-core3.md)
  - [Tutorial (UWP)](manual/cs/helloworld-cs-uwp.md)
  - [Peer Connection](manual/cs/cs-peerconnection.md)
  - [Signaling](manual/cs/cs-signaling.md)
- Unity integration
  - [Feature Overview](manual/unity-integration.md)
  - [Tutorial](manual/helloworld-unity.md)
  - [Peer Connection](manual/unity-peerconnection.md)
  - [Signaler](manual/unity-signaler.md)
  - Audio
    - [`MicrophoneSource`](manual/unity-localaudiosource.md)
    - [`AudioReceiver`](manual/unity-remoteaudiosource.md)
  - Video
    - [`WebcamSource`](manual/unity-localvideosource.md)
    - [`VideoReceiver`](manual/unity-remotevideosource.md)
    - [`VideoRenderer`](manual/unity-mediaplayer.md)
- Advanced topics
  - [Building the Core dependencies from sources](manual/building-core.md)

## API reference

1. [C# library](xref:Microsoft.MixedReality.WebRTC)
   1. [PeerConnection](xref:Microsoft.MixedReality.WebRTC.PeerConnection)
      - The PeerConnection object is the API entry point to establish a remote connection.
   2. [VideoFrameQueue\<T\>](xref:Microsoft.MixedReality.WebRTC.VideoFrameQueue`1)
      - The video frame queue bridges a video source and a video sink.
   3. [DataChannel](xref:Microsoft.MixedReality.WebRTC.DataChannel)
      - Encapsulates a single data channel for transmitting raw blobs of bytes.
2. [Unity integration](xref:Microsoft.MixedReality.WebRTC.Unity)
   1. [PeerConnection](xref:Microsoft.MixedReality.WebRTC.Unity.PeerConnection)
      - The PeerConnection component builds on the same-named library class to expose a remote peer connection.
   2. [Signaler](xref:Microsoft.MixedReality.WebRTC.Unity.Signaler)
      - The abstract Signaler component is the base class for signaling implementations.
   3. [WebcamSource](xref:Microsoft.MixedReality.WebRTC.Unity.WebcamSource)
      - The WebcamSource component provides access to the local webcam for local rendering and remote streaming.
