# Downloading MixedReality-WebRTC

> [!IMPORTANT]
> The MixedReality-WebRTC project is currently under public preview. During this development phase, **precompiled binaries are not yet available**. Instead the libraries should be compiled from the `Microsoft.MixedReality.WebRTC.sln` Visual Studio solution located at the root of the repository. **See [Building from sources](building.md)** for details on this process.

Eventually we plan to release downloadable binaries, most likely in the form of NuGet packages, for C++ and C# libraries at least.

> [!NOTE]
> Unlike the MixedReality-WebRTC libraries, the input dependencies (`webrtc.lib` and `Org.WebRtc.winmd`) are already available as NuGet packages. This avoids having to build those dependencies from sources. The library projects in the `Microsoft.MixedReality.WebRTC.sln` Visual Studio solution already consume those NuGet packages. But packages for the project libraries themselves are not available at the moment.
