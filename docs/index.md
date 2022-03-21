---
uid: index
title: Index
---
# MixedReality-WebRTC documentation (latest)

This is the MixedReality-WebRTC documentation for [the `master` branch](https://github.com/microsoft/MixedReality-WebRTC/tree/master/), which contains the latest features and API changes. In general this latest API is **incompatible with the NuGet packages**.

For the documentation corresponding to other branches, including [the `release/1.0` branch](https://github.com/microsoft/MixedReality-WebRTC/tree/release/1.0/) from which the NuGet 1.x packages are built, use the drop-down selection at the top right of this page.

<h2><b><span style="color:red">Caution</span></b></h2>

<span style="color:red">**MR-WebRTC has been deprecated. We're no longer committing development resources to it, taking pull requests for it, or planning a replacement for it.**</span>

- If you want to continue updating it, you must fork and maintain your own branch.

## User Manual

- C# library
  - [Feature Overview](manual/cs/cs.md)
  - [Tutorial (Desktop)](manual/cs/helloworld-cs-core3.md)
  - [Tutorial (UWP)](manual/cs/helloworld-cs-uwp.md)
  - [Peer Connection](manual/cs/cs-peerconnection.md)
  - [Signaling](manual/cs/cs-signaling.md)
- Unity integration
  - [Feature Overview](manual/unity/unity-integration.md)
  - [Tutorial](manual/unity/helloworld-unity.md)
  - [Peer Connection](manual/unity/unity-peerconnection.md)
  - [Signaler](manual/unity/unity-signaler.md)
  - Audio
    - [`MicrophoneSource`](manual/unity/unity-microphonesource.md)
    - [`AudioReceiver`](manual/unity/unity-audioreceiver.md)
  - Video
    - [`WebcamSource`](manual/unity/unity-localvideosource.md)
    - [`VideoReceiver`](manual/unity/unity-remotevideosource.md)
    - [`VideoRenderer`](manual/unity/unity-mediaplayer.md)
- Advanced topics
  - [Building the Core dependencies from sources](manual/building-core.md)

## API reference

1. [C# Library](xref:Microsoft.MixedReality.WebRTC)
   1. [PeerConnection](xref:Microsoft.MixedReality.WebRTC.PeerConnection)
      - The PeerConnection object is the API entry point to establish a remote connection.
   2. [Transceiver](xref:Microsoft.MixedReality.WebRTC.Transceiver)
      - The transceiver is the "pipe" which transports some audio or video between the two peers.
   3. [DataChannel](xref:Microsoft.MixedReality.WebRTC.DataChannel)
      - Encapsulates a single data channel for transmitting raw blobs of bytes.
2. [Unity Library](xref:Microsoft.MixedReality.WebRTC.Unity)
   1. [PeerConnection](xref:Microsoft.MixedReality.WebRTC.Unity.PeerConnection)
      - The PeerConnection component builds on the same-named library class to expose a remote peer connection.
   2. [WebcamSource](xref:Microsoft.MixedReality.WebRTC.Unity.WebcamSource)
      - The WebcamSource component provides access to the local webcam for local rendering and remote streaming.
   3. [MicrophoneSource](xref:Microsoft.MixedReality.WebRTC.Unity.MicrophoneSource)
      - The MicrophoneSource component provides access to the local microphone for audio streaming to the remote peer.
