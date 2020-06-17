# Downloading MixedReality-WebRTC

MixedReality-WebRTC is primarily distributed as NuGet packages hosted on [nuget.org](https://nuget.org). Those packages are signed by Microsoft. Generally you do not want to download those packages directly, but instead add a reference inside a Visual Studio project and let Visual Studio do the installation. See [Installation](installation.md) for details.

Alternatively, the libraries can be compiled from source if wanted. See [Building from sources](building.md) for details on this process.

Starting from v2.0, the C library is only distributed as sources, to be linked statically inside a C/C++ project.

## C# library

The C# library is distributed as two separate packages for Windows Desktop and Windows UWP:

- [Windows Desktop package `Microsoft.MixedReality.WebRTC`](https://www.nuget.org/packages/Microsoft.MixedReality.WebRTC)
- [Windows UWP package `Microsoft.MixedReality.WebRTC.UWP`](https://www.nuget.org/packages/Microsoft.MixedReality.WebRTC.UWP)

> [!NOTE]
> As per existing C# NuGet packages convention, and unlike the C library below, the Desktop package has no suffix, and the UWP package adds a `.UWP` suffix.

The C# library packages contain the C# assembly `Microsoft.MixedReality.WebRTC` as well as the per-architecture native DLLs. Therefore those packages are standalone, and there is no need to reference any other package in your project.

## C library

The C library is the native implementation module `mrwebrtc.dll` of the C# library, but can also be used as a standalone library in a C/C++ program. It is distributed as sources only for C/C++ projects to integrate it and statically link with it.

To download the library, simply clone the MixedReality-WebRTC repository:

```shell
git clone https://github.com/microsoft/MixedReality-WebRTC.git
```

> [!IMPORTANT]
> The C++ library distributed in the NuGet packages v1.0.x has an architecture defect : it requires some internal headers from the Google's WebRTC implementation to be used, which are **not** shipped with those NuGet packages. See bug [#123](https://github.com/microsoft/MixedReality-WebRTC/issues/123) for details. A workaround is to clone the repository _recursively_ to get those headers (`git clone --recurse`).

## Unity library

The sources for the Unity library are distributed as a UPM package to be imported into a user project via the Unity Package Manager. Users can checkout the GitHub repository and point UPM to the `package.json` file from [`libs/unity/library`](https://github.com/microsoft/MixedReality-WebRTC/tree/master/libs/unity/library/) (library) and [`libs/unity/samples`](https://github.com/microsoft/MixedReality-WebRTC/tree/master/libs/unity/samples/) (optional demo samples). The library however requires the user to compile from sources the C and C# libraries, which the Unity library uses under the hood. Standalone UPM packages with precompiled binaries are not available at this time. See [Installation](installation.md) for details.

> [!WARNING]
> The Unity scripts are using the API of the branch they are associated with. This means that the Unity scripts on the `master` branch are only working with the `master` branch API, and are therefore not compatible with the NuGet packages, which are only provided for stable releases (`release/xxx` branches).
