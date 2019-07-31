---
uid: index
title: Index
---
# MixedReality-WebRTC 0.1 documentation (Coming soon!)

## User Manual

- Getting started (coming soon)
  - Download
  - Prerequisites
  - Building
- C# library (coming soon)
  - Feature Overview
  - Signaling
  - Audio
    - Streaming
    - Rendering
  - Video
    - Streaming
    - Rendering
- Unity integration
  - For now: <a href="https://microsoft.github.io/MixedReality-WebRTC/manual/helloworld-unity.html">Start here</a>
  - Feature Overview
  - Tutorial
  - Samples
  - Signaling
  - Audio
    - Streaming
    - Rendering
  - Video
    - Streaming
    - Rendering

## API documentation

1. [C# library](xref:Microsoft.MixedReality.WebRTC)
   1. [PeerConnection](xref:Microsoft.MixedReality.WebRTC.PeerConnection)
      - The PeerConnection object is the API entry point to establish a remote connection.
   2. [ISignaler](xref:Microsoft.MixedReality.WebRTC.ISignaler)
      - The signaler interface allows using different signaling implementations.
   4. [VideoFrameQueue\<T\>](xref:Microsoft.MixedReality.WebRTC.VideoFrameQueue`1)
      - The video frame queue bridges a video source and a video sink.
2. [Unity integration](xref:Microsoft.MixedReality.WebRTC.Unity)
   1. [PeerConnection](xref:Microsoft.MixedReality.WebRTC.Unity.PeerConnection)
      - The PeerConnection component builds on the same-named library class to expose a remote peer connection.
   2. [Signaler](xref:Microsoft.MixedReality.WebRTC.Unity.Signaler)
      - The abstract Signaler component is the base class for signaling implementations.
   3. [LocalVideoSource](xref:Microsoft.MixedReality.WebRTC.Unity.LocalVideoSource)
      - The LocalVideoSource component provides access to the local webcam for local rendering and remote streaming.
