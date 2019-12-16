# Changelog

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.2] - 2019-12-04

8c958f9723bcf382cbc495d0828e88d9ff3aed42

### Fixed

- (df3736a,e53cb8e,8c958f9) Integrate an upstream workaround for the H.264 encoder on HoloLens 1 introducing some artifacts when the video frame height is not a multiple of 16 pixels. By default MixedReality-WebRTC when running on HoloLens 1 will crop the frame such that the height becomes a multiple of 16 pixels to prevent those artifacts. This default behavior can be changed with [`PeerConnection::SetFrameHeightRoundMode()`](https://github.com/microsoft/MixedReality-WebRTC/blob/8c958f9723bcf382cbc495d0828e88d9ff3aed42/libs/Microsoft.MixedReality.WebRTC.Native/include/peer_connection.h#L272) to pad the image instead, or altogether disabled.
- (df3736a) Improve the dynamic rate at which the H.264 encoder on UWP is updating its target bitrate, decreasing the update delay from 15 seconds to 5 seconds to increase its reactivity to changes.
- (090dead) Integrate an upstream change to avoid a crash when closing the video capturer on UWP under heavy CPU load or other constraints affecting the timing of the async Media Foundation call. (#134)

## [1.0.1] - 2019-11-08

7e8a97cfd9df248f3ea1a4304fa0e7b24b883503

### Fixed

- (f5bf1d9) Integrate upstream change fixing the "green band" artifact in the H.264 decoder on UWP when the resolution selected gets padded by the decoder, which often happens with resolution heights not a multiple of 16px.
- (c3e1107) Fix crash in C# due to NULL pointer dereferencing following any failure to initializing the peer connection. (#122)
- (ab67d06) Fix ARGB32 local and remote callbacks not firing due to missing registration. (#120)
- (59c425e) [TestAppUWP] Handle remote video resolution changes by resizing the Media Foundation video stream source to the new frame size.
- (7d7e8e5) Remove '.' from Unity project name to work around Unity bug in project generation. (#77)
- (3036c88) Add single-pass stereo instanced rendering support to Unity shaders to fix rendering in HoloLens 1 and 2 when using instanced rendering. (#110)

## [1.0.0] - 2019-10-30

deeccbbf269192cd272fdb9bc1822620c3c13a3b

### Fixed

- (46ec05d) Fix bug in `I420AVideoFrame.CopyTo()` when invoked with non-empty padding (stride > width) leading to a buffer overflow and likely crash.

## [1.0.0-rc2] - 2019-10-25

bbb9e56f197b4962990d13736fd6ab7f9e530631

### Fixed

- (b9ae69b, 7e463c0) Integrate upstream fixes for the H.264 hardware encoder stalls. (#74)
- (8782834) Integrate an upstream fix for a race condition on UWP in the WebRTC task queue initializing via APC leading to a deadlock. (#95)
- (8863ec6) Fix crash in interop call to `mrsPeerConnectionAddDataChannel` when passing a NULL channel name.
- (8b84e21) Guard data channel creation against race condition. (#89)
- (0e3cf8e) Avoid unnecessary rebuilds of TestAppUWP when there is no code change. (#112)
- (766d27e) Fix solution build error on first build due to missing project build dependency.

### Changed

- (89fc337) Promoted the C++ classes headers as public API by moving them to the `include/` folder of `libs/Microsoft.MixedReality.WebRTC.Native/`. Conversely, demoted `api.h` to private and renamed to `interop_api.h` to reflect the fact it is an internal API for interop with C# and it is not part of the MixedReality-WebRTC public API (as in, changes in the interop API do not constitute a breaking change from the point of view of MixedReality-WebRTC release versioning).
- (5740ab7,d26706d) Make the local peer ID for `node-dss` configurable by the user, and default the local peer ID to the machine name. Drop usage of generated IDs to make it clear that the user is in charge of chosing those IDs and ensure their unicity. (#38)
- (1d34965) Make the `AudioSource` and `VideoSource` Unity components, which serve as base classes, abstract to prevent Unity from listing them in the list of components and prevent the user from trying to instantiate them. (#103)
- (2b44e67) Replaced all uses of `std::lock_guard` with `std::scoped_lock`.

### Added

- (28dfdf1) Expose `webrtc::PeerConnection::SetBitrates()` to be able in particular to configure the initial bitrate of the video encoding to work around #107.
- (8782834) Integrate 2 upstream optimizations in the UWP local video capture code to reduce CPU usage and memory allocations.
- (fa268d2) Expose audio and video track mute in TestAppUWP.

## [1.0.0-rc1] - 2019-10-16

fff8fbdf9f24a5e133211d4fd15dd77f7ac19135

### Fixed

- (1a16367) Fixed video capture device selection on Windows Desktop.
- (9a2996d) Fix off-by-one error in SdpForceCodecs use. (#94)
- (0f97000) Reuse video frame scratch buffer for deferred frames to reduce garbage collection. (#97)

### Added

- (08e83d3) Add the ability to toggle ON or OFF the on-screen recording indicator when Mixed Reality Capture is active on HoloLens. (#96)
- .NET Core 3.0 sample app with custom named pipe-based signaling solution.

## [0.2.3-preview-20191011.1] - 2019-10-11

0f97000f715d13b8dfd2f09685c81e5d8c5406bd

### Fixed

- (7f47b90) Integrate a fix for a misconfiguration of the average bitrate of the H.264 video encoder, preventing the encoder from starting in some situation. (#74)
- (807a024) Integrate a fix for video profile selection when selecting some video capture formats with a framerate not rounded to the same value the video capture device expects. (#90)
- (0601230) Fix crash in TestAppUWP when pressing the "Start local video" button without selecting a capture format. A default format is now always selected, and the app fails gracefully in any case.
- (c4f8132) Copy native DLLs to the Unity plugins folder after the build using an MSBuild task instead of a custom `xcopy`-based script, to avoid invalidating the build and forcing a rebuild even when the project didn't change.
- (a81368c) Remove `Org.WebRtc.dll` from the `Microsoft.MixedReality.WebRTC.Native.Core.WinRT` NuGet package to fix the duplicate path package installing error in Visual Studio. (#87)
- (1d4bd2f) Fix thread-safety issue on initializing the peer connection factory from multiple threads in parallel when creating a `PeerConnection` object and/or using any of the static methods to enumerate the video devices and formats. (#81)
- Remove use of `std::string` across the API boundaries to fix crashes when using mismatching C++ standard libraries.
- (1bc2ca6) Add missing wiring of the `BufferingChanged` C# event of `DataChannel`. (#35)

### Added

- .NET Core 3.0 sample app with custom named pipe-based signaling solution.

## [0.2.0-preview] - 2019-09-25

1d4bd2fee5ebe0daddf0b1e2be772809fd057410

Initial package.
