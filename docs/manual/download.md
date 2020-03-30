# Downloading MixedReality-WebRTC

MixedReality-WebRTC is primarily distributed as NuGet packages hosted on [nuget.org](https://nuget.org). Those packages are signed by Microsoft. Generally you do not want to download those packages directly, but instead add a reference inside a Visual Studio project and let Visual Studio do the installation. See [Installation](installation.md) for details.

Alternatively, the libraries can be compiled from source if wanted. See [Building from sources](building.md) for details on this process.

## C# library

The C# library is distributed as two separate packages for Windows Desktop and Windows UWP:

- [Windows Desktop package `Microsoft.MixedReality.WebRTC`](https://www.nuget.org/packages/Microsoft.MixedReality.WebRTC)
- [Windows UWP package `Microsoft.MixedReality.WebRTC.UWP`](https://www.nuget.org/packages/Microsoft.MixedReality.WebRTC.UWP)

> [!NOTE]
> As per existing C# NuGet packages convention, and unlike the C++ library below, the Desktop package has no suffix, and the UWP package adds a `.UWP` suffix.

The C# library packages contain the C# assembly `Microsoft.MixedReality.WebRTC` as well as the per-architecture native DLLs. Therefore those packages are standalone, and there is no need to also reference the C++ library packages in your project.

## C++ library

The C++ library is distributed as two separate packages for Windows Desktop and Windows UWP:

- [Windows Desktop package `Microsoft.MixedReality.WebRTC.Native.Desktop`](https://www.nuget.org/packages/Microsoft.MixedReality.WebRTC.Native.Desktop)
- [Windows UWP package `Microsoft.MixedReality.WebRTC.Native.UWP`](https://www.nuget.org/packages/Microsoft.MixedReality.WebRTC.Native.UWP)

The C++ packages contain the shared library `Microsoft.MixedReality.WebRTC.Native.dll` as well as its import library (`.lib`).

> [!NOTE]
> Unlike for the C# library above, the C++ library packages are named explicitly according to the target platform, adding either a `.Desktop` or `.UWP` suffix to the package name.

> [!IMPORTANT]
> The C++ library as distributed in the NuGet pacakges currently requires some internal headers from the Google's WebRTC implementation to be used, which are **not** shipped with the NuGet packages. See bug [#123](https://github.com/microsoft/MixedReality-WebRTC/issues/123) for details. A workaround is to clone the repository _recursively_ to get those headers.

## Unity integration

The Unity integration is not currently distributed in any particular packaged way. Instead, users can check out the GitHub repository and copy the relevant parts of the Unity sample project from [`libs/Microsoft.MixedReality.WebRTC.Unity/`](https://github.com/microsoft/MixedReality-WebRTC/tree/master/libs/Microsoft.MixedReality.WebRTC.Unity/). See [Installation](installation.md) for details.

> [!WARNING]
> The Unity scripts are using the API of the branch they are associated with. This means that the Unity scripts on the `master` branch are only working with the `master` branch API, and are therefore not compatible with the NuGet packages, which are only provided for stable releases (`release/xxx` branches).
