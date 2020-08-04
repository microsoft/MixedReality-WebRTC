# Building from sources

> [!IMPORTANT]
>
> Building MixedReality-WebRTC from sources, and in particular the native `libwebrtc` implementation, is fairly involved, and is not required to use the various MixedReality-WebRTC libraries. Prebuilt binaries are available for C/C++ and C# project (NuGet packages) and Unity (UPM packages), which provide a much easier way to obtain and consume those libraries. See [Installation](installation.md) for details.

Building the C/C++ and C# libraries of MixedReality-WebRTC from sources involves two main steps:

1. Building the native C/C++ library for the specific platform(s) of interest:
   - [Building for Windows platforms (Desktop and UWP)](building-windows.md)
   - [Building for Android (Unity)](android/building-android.md)
2. [Building the platform-independent C# library](building-cslib.md).

On Windows, if you want to start right away with MixedReality-WebRTC, the recommended approach is to consume the precompiled binaries distributed as NuGet packages instead. See [Download](download.md) for details. There is currently no prebuilt Android binaries.
