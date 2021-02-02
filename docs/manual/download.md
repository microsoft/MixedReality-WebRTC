# Downloading MixedReality-WebRTC

The **Unity library** and its **optional samples** are distributed as UPM packages. The library package contains all binaries for Windows Desktop, UWP, and Android.

The **C/C++ library** and **C# library** are distributed as NuGet packages for Windows Desktop and UWP.

## Unity library

UPM packages are distributed in two ways:

- Using the [Mixed Reality Feature Tool](https://aka.ms/MRFeatureToolDocs), a free Microsoft utility to manage Mixed Reality packages for Unity. This is the recommended way, which takes care of installing any required dependency and automatically download and install the package(s) into an existing Unity project.

- Via direct download on [the GitHub Releases page](https://github.com/microsoft/MixedReality-WebRTC/releases) for manual installation. See the Unity documentation [Installing a package from a local tarball file](https://docs.unity3d.com/Manual/upm-ui-tarball.html).

> [!NOTE]
> If an existing Unity project manifest already contains a `Microsoft Mixed Reality` entry in the `scopedRegistries` section, is is recommended that it be removed.

The library package contains prebuilt binaries for all supported Unity platforms:

- Windows Desktop (x86, x64)
- Windows UWP (x86, x64, ARM)
- Android (ARM64)

See [Installation](installation.md#unity-library) for more details on how to install and use those packages in an existing Unity project, whether via the Mixed Reality Feature Tool or manually via direct download on the GitHub Releases page.

## NuGet packages

| Platform | C# library | C/C++ library |
|---|---|---|
| Windows Desktop (x86, x64) | [`Microsoft.MixedReality.WebRTC`](https://www.nuget.org/packages/Microsoft.MixedReality.WebRTC) | [`mrwebrtc`](https://www.nuget.org/packages/mrwebrtc) |
| UWP (x86, x64, ARM) | [`Microsoft.MixedReality.WebRTC.UWP`](https://www.nuget.org/packages/Microsoft.MixedReality.WebRTC.UWP) | [`mrwebrtc_uwp`](https://www.nuget.org/packages/mrwebrtc_uwp) |


See [Installation](installation.md) for details on how to use NuGet packages in a Visual Studio project.

### Legacy v1 packages

> [!WARNING]
> The legacy v1 native packages are now **deprecated and unsupported**, and should not be used.

In v1 the native packages [`Microsoft.MixedReality.WebRTC.Native.Desktop`](https://www.nuget.org/packages/Microsoft.MixedReality.WebRTC.Native.Desktop) and [`Microsoft.MixedReality.WebRTC.Native.UWP`](https://www.nuget.org/packages/Microsoft.MixedReality.WebRTC.Native.UWP) contain a native library exposing a C++ interface. This caused several issues:

- Compatibility with various C++ compilers and CRT versions, which is a general problem when working with DLLs on Windows platforms.
- Architecture defect : some library headers were including some internal headers from the Google's WebRTC implementation, which were **not** shipped with those NuGet packages. See bug [#123](https://github.com/microsoft/MixedReality-WebRTC/issues/123) for details.

To avoid those issues, starting from 2.0.0 the native library exposes a pure C interface. To highlight this change and the fact the packages are native, some new package names are used.
