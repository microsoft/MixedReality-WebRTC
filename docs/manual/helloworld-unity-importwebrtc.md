# Importing MixedReality-WebRTC

In order to use the Unity integration, the following pieces are required:

- Native implementation : `mrwebrtc.dll` (one variant per platform and architecture)
- C# library : `Microsoft.MixedReality.WebRTC.dll` (single universal module for all platforms and architectures)
- Unity integration scripts and assets

> [!NOTE]
> There is currently no pre-packaged distribution method for Unity, so users have to manually copy the relevant files from the `libs\Microsoft.MixedReality.WebRTC.Unity\Assets` folder of the GitHub repository into the `Assets` folder of their own Unity project.

## Copying the binaries

The binaries can be obtained either prebuilt from the NuGet packages, or from a local build of the Visual Studio solution provided in [the GitHub repository](https://github.com/microsoft/MixedReality-WebRTC/).

- **NuGet packages** do not require any build step, so are the most convenient approach. But they are generally only available for stable releases (_e.g._ [the `release/1.0` branch](https://github.com/microsoft/MixedReality-WebRTC/tree/release/1.0/)), so will be missing any newer development available on [the `master` branch](https://github.com/microsoft/MixedReality-WebRTC/tree/master/).

- **A local build** via the Visual Studio solution `Microsoft.MixedReality.WebRTC.sln` allows accessing the latest features, but may contain some API breaking changes since the latest stable release, so may require some code changes when upgrading an existing project using an earlier API version.

### NuGet packages

The native implementation and C# library of MixedReality-WebRTC are available precompiled via NuGet packages. See the [GitHub Releases page](https://github.com/microsoft/MixedReality-WebRTC/releases) for the latest packages.

The packages can be downloaded from [nuget.org](https://www.nuget.org/profiles/MicrosoftMR). Once downloaded, they can be extracted by simply renaming their extension from `.nupkg` to `.zip`, and using any standard ZIP archive extraction method. After the packages are extracted, the DLLs can be copied as detailed on the **Unity integration** section of the [installation page](installation.md).

### Local solution build

If the native implementation and C# library are compiled from sources as explained in the [Building](building.md) page, they are available in one of the sub-folders under the `bin/` folder at the root of the repository of the MixedReality-WebRTC project.

The C# library `Microsoft.MixedReality.WebRTC.dll` is a .NET Standard 2.0 library. This means it is compatible with all CPU architectures. This is often referred to as "AnyCPU", and the C# library is therefore available from `bin\AnyCPU\Debug` or `bin\AnyCPU\Release` depending on the build configuration which was compiled. In doubt you can use the `Release` configuration, which can provide better performance. This module needs to be copied somewhere into the `Assets\Plugins\` folder of the Unity project (if that folder doesn't exist you can create it). On Windows this can be done via the command line with `xcopy`, assuming that the git repository of MixedReality-WebRTC was cloned in `D:\mr-webrtc`:

```cmd
cd /D D:\testproj
xcopy D:/mr-webrtc/bin/AnyCPU/Release/Microsoft.MixedReality.WebRTC.dll Assets/Plugins/
```

For the native implementation `mrwebrtc.dll` things are a bit more complex. The DLL is compiled for a particular platform and architecture, in addition of the Debug or Release build config, and the correct variant needs to be used. On Windows, the Unity Editor needs a **64-bit Desktop** variant (`Debug` or `Release`); it is available from the `bin\Win32\x64\Release` folder (`Win32` is an alias for `Desktop`), and should be copied to the `Assets\Plugins\Win32\x86_64\` folder of the Unity project.

```cmd
cd /D D:\testproj
xcopy D:/mr-webrtc/bin/Win32/x64/Release/mrwebrtc.dll Assets/Plugins/Win32/x86_64/
```

## Configuring the import settings

When building the Unity application for a given platform, another variant than the one the Unity editor uses may be required to deploy on that platform. In order for the C# library to be truly platform-independent, the name of all native implementation library variants is the same: `mrwebrtc`. This allows the C# code to reference that DLL with [the same `DllImport` attribute path](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.dllimportattribute?view=netcore-2.1). But this also means that multiple files with the same name exist in different folders, and Unity needs to know which one is associated with which build variant, to be able to deploy the correct one only. This is done by configuring the platform associated with a DLL in the import settings in the Unity inspector.

> [!IMPORTANT]
> The `mrwebrtc.dll` module was in v1.0 named `Microsoft.MixedReality.WebRTC.Native.dll` when this module was also the C++ library. Those two have now been split apart. The `Microsoft.MixedReality.WebRTC.Native` name is still in use in some NuGet packages, so depending on the installation method one or the other name are used. However the Unity project must use the `mrwebrtc.dll` name only for the C# library to find it.

- In the **Project** window, select one of the `mrwebrtc.dll` files from the `Plugins` folder.
- The **Inspector** window now shows the import settings for the selected file, which contains option to configure the deploy platform(s) and architecture(s).

For example, by selecting in the **Inspector** window:

- **Any Platform** and **Exclude Platforms: WSAPlayer**, the DLL will be used by Unity on all platforms except UWP platforms. _WSAPlayer_ is the name Unity uses for its UWP standalone player.
- **CPU** equal to **x86_64**, Unity will only deploy that DLL when deploying on a 64-bit Intel architecture.

![Configure the import settings for a native implementation DLL](helloworld-unity-2.png)

This way, multiple variants of the same-named `mrwebrtc.dll` can co-exist in different sub-folders of `Assets/Plugins/` and Unity will deploy and use the correct variant on each platform.

> [!NOTE]
> If Unity complains about "_Multiple plugins with the same name 'mrwebrtc'_", then the configurations of the various `mrwebrtc.dll` files are not exclusive, and 2 or more files have been allowed for deploy on the same platform. Check the configuration of all variants again.

For **Windows Desktop**, the native implementation DLL variants are:

| Path | Any Platform | Exclude Platforms | CPU | OS | Example use |
|---|---|---|---|---|---|
| `Assets/Plugins/Win32/x86` | yes | WSAPlayer | x86 | Windows | 32-bit Windows Desktop application |
| `Assets/Plugins/Win32/x86_64` | yes | WSAPlayer | x86_64 | Windows | 64-bit Windows Desktop application, including the Unity Editor on Windows |

For **Windows UWP**, the native implementation DLL variants are:

| Path | Any Platform | Include Platforms | SDK | CPU | Example use |
|---|---|---|---|---|---|
| `Assets/Plugins/UWP/x86` | no | WSAPlayer | UWP | X86 | Microsoft HoloLens (1st gen) |
| `Assets/Plugins/UWP/x86_64` | no | WSAPlayer | UWP | X64 | 64-bit UWP Desktop app on Windows |
| `Assets/Plugins/UWP/ARM` | no | WSAPlayer | UWP | ARM | HoloLens 2 (compatibility) |

> [!NOTE]
> ARM64 is not currently available. The ARM (32-bit) architecture variant can be used as a fallback on HoloLens 2 and other devices which support running 32-bit ARM applications on ARM64 hardware.

![Configure the import settings for a native C++ DLL for UWP](helloworld-unity-3.png)

If all variants are installed, the resulting hierarchy should look like this:

```shell
Assets
+- Plugins
   +- Win32
   |  +- x86
   |  |  +- mrwebrtc.dll
   |  +- x86_64
   |     +- mrwebrtc.dll
   +- UWP
      +- x86
      |  +- mrwebrtc.dll
      +- x86_64
      |  +- mrwebrtc.dll
      +- ARM
      |  +- mrwebrtc.dll
      +- ARM64
         +- mrwebrtc.dll
```

## Importing the Unity integration

In order to import the Unity integration into your new Unity project, simply copy the following folders from `libs\Microsoft.MixedReality.WebRTC.Unity\Assets\` into your project's `Assets` folder:

- `Microsoft.MixedReality.WebRTC.Unity` : the core runtime integration, which must ship with the project.
- `Microsoft.MixedReality.WebRTC.Unity.Editor` : some Unity Editor utilities, not required at runtime.

After Unity finished processing the new files, the **Project** window should look like this:

![Import the Unity integration](helloworld-unity-4.png)

The other folders are optional and contain samples and tests. They can be imported for testing and prototyping, but are not required to ship a project with MixedReality-WebRTC:

- `Microsoft.MixedReality.WebRTC.Unity.Examples` : sample projects demonstrating the use of the Unity integration and its various components.
- `Microsoft.MixedReality.WebRTC.Unity.Tests.Editor` : Editor-only tests for the Unity integration.
- `Microsoft.MixedReality.WebRTC.Unity.Tests.Runtime` : runtime tests for the Unity integration.

----

Next : [Creating a peer connection](helloworld-unity-peerconnection.md)
