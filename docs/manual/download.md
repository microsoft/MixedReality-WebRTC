# Downloading MixedReality-WebRTC

MixedReality-WebRTC is primarily distributed as NuGet packages hosted on [nuget.org](https://nuget.org). Those packages are signed by Microsoft. Generally you do not want to download those packages directly, but instead add a reference inside a Visual Studio project and let Visual Studio do the installation. See [Installation](installation.md) for details.

Alternatively, the libraries can be compiled from source if wanted. See [Building from sources](building.md) for details on this process.

Starting from v2.0, the C++ library is only distributed as sources, to be linked statically inside a C++ project.

## C# library

The C# library is distributed as two separate packages for Windows Desktop and Windows UWP:

- [Windows Desktop package `Microsoft.MixedReality.WebRTC`](https://www.nuget.org/packages/Microsoft.MixedReality.WebRTC)
- [Windows UWP package `Microsoft.MixedReality.WebRTC.UWP`](https://www.nuget.org/packages/Microsoft.MixedReality.WebRTC.UWP)

> [!NOTE]
> As per existing C# NuGet packages convention, and unlike the C++ library below, the Desktop package has no suffix, and the UWP package adds a `.UWP` suffix.

The C# library packages contain the C# assembly `Microsoft.MixedReality.WebRTC` as well as the per-architecture native DLLs. Therefore those packages are standalone, and there is no need to reference any other package in your project.

## C++ library

The C++ library architecture and distribution changed between the 1.0 and 2.0 releases.

### New standalone library (v2.0)

Starting from v2.0, the C++ library is a standalone source-only library building on top of the native implementation module `mrwebrtc.dll`. The legacy `Microsoft.MixedReality.WebRTC.Native.dll` previously containing the native implementation is gone. There is currently no precompiled binaries of that `mrwebrtc.dll` implementation module. The C++ library itself is only distributed as sources for C++ projects to integrate it and statically link with it.

### Legacy combined implementation (v1.0)

In v1.0, the C++ library and the native implementation are a single and same module `Microsoft.MixedReality.WebRTC.Native.dll`, distributed as two separate NuGet packages for Windows Desktop and Windows UWP:

- [Windows Desktop package `Microsoft.MixedReality.WebRTC.Native.Desktop`](https://www.nuget.org/packages/Microsoft.MixedReality.WebRTC.Native.Desktop)
- [Windows UWP package `Microsoft.MixedReality.WebRTC.Native.UWP`](https://www.nuget.org/packages/Microsoft.MixedReality.WebRTC.Native.UWP)

The C++ NuGet packages contain the shared library `Microsoft.MixedReality.WebRTC.Native.dll` as well as its import library (`.lib`).

> [!NOTE]
> Unlike for the C# library above, the C++ library packages are named explicitly according to the target platform, adding either a `.Desktop` or `.UWP` suffix to the package name.

## Unity integration

The Unity integration is not currently distributed in any particular packaged way. Instead, users can check out the GitHub repository and copy the relevant parts of the Unity sample project from [`libs/Microsoft.MixedReality.WebRTC.Unity/`](https://github.com/microsoft/MixedReality-WebRTC/tree/master/libs/Microsoft.MixedReality.WebRTC.Unity/). See [Installation](installation.md) for details.
