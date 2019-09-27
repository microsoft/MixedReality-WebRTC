---
uid: index
title: Index
---
# MixedReality-WebRTC 0.1 documentation

_Note_ : **The documentation writing is in progress**. Some links are not active when the associated page is not available yet.

## User Manual

[Introduction](manual/introduction.md)

- Getting started
  - [Download](manual/download.md)
  - [Installation](manual/installation.md)
  - [Building from sources](manual/building.md)
  - [C# tutorial (Desktop)](manual/cs/helloworld-cs-core3.md)
  - C# tutorial (UWP)
  - [Unity tutorial](manual/helloworld-unity.md)
- C# library (under writing)
  - [Feature Overview](manual/cs/cs.md)
  - [Tutorial (Desktop)](manual/cs/helloworld-cs-core3.md)
  - Tutorial (UWP)
  - [Peer Connection](manual/cs/cs-peerconnection.md)
  - [Signaling](manual/cs/cs-signaling.md)
  - Audio
    - Streaming
    - Rendering
  - Video
    - Streaming
    - Rendering
- Unity integration
  - [Feature Overview](manual/unity-integration.md)
  - [Tutorial](manual/helloworld-unity.md)
  - Samples
  - [Peer Connection](manual/unity-peerconnection.md)
  - [Signaler](manual/unity-signaler.md)
  - [Media Player](manual/unity-mediaplayer.md)
  - Audio
    - [LocalAudioSource](manual/unity-localaudiosource.md)
    - [RemoteAudioSource](manual/unity-remoteaudiosource.md)
  - Video
    - [LocalVideoSource](manual/unity-localvideosource.md)
    - [RemoteVideoSource](manual/unity-remotevideosource.md)
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
   3. [LocalVideoSource](xref:Microsoft.MixedReality.WebRTC.Unity.LocalVideoSource)
      - The LocalVideoSource component provides access to the local webcam for local rendering and remote streaming.
