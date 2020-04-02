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

The C++ library is a standalone library building on top of the native implementation module `mrwebrtc.dll`, and distributed as sources only for C++ projects to integrate it and statically link with it.

To download the library, simply clone the MixedReality-WebRTC repository:

```shell
git clone https://github.com/microsoft/MixedReality-WebRTC.git
```

## Unity integration

The Unity integration is not currently distributed in any particular packaged way. Instead, users can check out the GitHub repository and copy the relevant parts of the Unity sample project from [`libs/Microsoft.MixedReality.WebRTC.Unity/`](https://github.com/microsoft/MixedReality-WebRTC/tree/master/libs/Microsoft.MixedReality.WebRTC.Unity/). See [Installation](installation.md) for details.
