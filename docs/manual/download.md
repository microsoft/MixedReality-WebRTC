# Downloading MixedReality-WebRTC

The **Unity library** and its **optional samples** are distributed as UPM packages. The library package contains all binaries for Windows Desktop, UWP, and Android.

The **C/C++ library** and **C# library** are distributed as NuGet packages for Windows Desktop and UWP.

## Unity library

UPM packages are available for download on [the GitHub Releases page](https://github.com/microsoft/MixedReality-WebRTC/releases). Alternatively, the packages can be installed directly from [the Mixed Reality UPM package registry](https://dev.azure.com/aipmr/MixedReality-Unity-Packages/_packaging?_a=feed&feed=Unity-packages) by Unity without needing a manual download; see [Installation](installation.md#unity-library) for details.

The library package contains prebuilt binaries for all supported Unity platforms: Windows Desktop (x86, x64), UWP (x86, x64, ARM), and Android (ARM64).

See [Installation](installation.md#unity-library) for details on how to use those packages in an existing Unity project, whether downloaded manually or directly through the Unity Package Manager (UPM).

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
