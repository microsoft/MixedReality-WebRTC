# Importing MixedReality-WebRTC

In order to use the Unity library, the following pieces are required:

- Native implementation : `mrwebrtc.dll` (one variant per platform and architecture)
- C# library : `Microsoft.MixedReality.WebRTC.dll` (single universal module for all platforms and architectures)
- Unity library itself (scripts and assets)

> [!NOTE]
> There is currently no pre-packaged distribution method for Unity which include prebuilt binaries.

## Copying the binaries

The binaries can be obtained either prebuilt from the NuGet packages, or from a local build of the Visual Studio solution provided in [the GitHub repository](https://github.com/microsoft/MixedReality-WebRTC/).

- **NuGet packages** do not require any build step, so are the most convenient approach. But they are generally only available for stable releases (_e.g._ [the `release/1.0` branch](https://github.com/microsoft/MixedReality-WebRTC/tree/release/1.0/)), so will be missing any newer development available on [the `master` branch](https://github.com/microsoft/MixedReality-WebRTC/tree/master/).

- **A local build** via the Visual Studio solution `Microsoft.MixedReality.WebRTC.sln` allows accessing the latest features, but may contain some API breaking changes since the latest stable release, so may require some code changes when upgrading an existing project using an earlier API version. See [Building from Sources](../building.md) for details.

### NuGet packages

The native implementation and C# library of MixedReality-WebRTC are available precompiled via NuGet packages. See the [GitHub Releases page](https://github.com/microsoft/MixedReality-WebRTC/releases) for the latest packages.

The packages can be downloaded from [nuget.org](https://www.nuget.org/profiles/MicrosoftMR). Once downloaded, they can be extracted by simply renaming their extension from `.nupkg` to `.zip`, and using any standard ZIP archive extraction method. After the packages are extracted, the DLLs can be copied as detailed on the **Unity library** section of the [installation page](../installation.md).

### Local solution build

If the native implementation and C# library are compiled from sources as explained in the [Building from Sources](../building.md) page, they are available in one of the sub-folders under the `bin/` folder at the root of the repository of the MixedReality-WebRTC project.

The C# library `Microsoft.MixedReality.WebRTC.dll` is a .NET Standard 2.0 library. This means it is compatible with all CPU architectures. The C# library is available from `bin\netstandard2.0\Debug` or `bin\netstandard2.0\Release` depending on the build configuration which was compiled. In doubt you should use the `Release` configuration, which can provide better performance. This module needs to be copied somehwere into the `libs\unity\library\Runtime\Plugins\` folder of the git repository checkout. This is done automatically by the build process of the Visual Studio solution, but can also be done via the command line with `xcopy`, assuming that the git repository of MixedReality-WebRTC was cloned in `D:\mr-webrtc`:

```cmd
cd /D D:\testproj
xcopy D:/mr-webrtc/bin/netstandard2.0/Release/Microsoft.MixedReality.WebRTC.dll libs/unity/library/Runtime/Plugins/Win32/x86_64/
```

_Note_: By convention, and because of past experiences of getting errors when doing otherwise, the C# library is copied to the `Win32\x86_64` sub-folder where the native C library for the Unity editor is also located. It is unclear if this is best practice, as the Unity documentation is outdated on this topic.

For the native C library `mrwebrtc.dll` things are a bit more complex. The DLL is compiled for a particular platform and architecture, in addition of the Debug or Release build configurations, and the correct variant needs to be used. On Windows, the Unity Editor needs a **64-bit Desktop** variant (`Debug` or `Release`); it is available from the `bin\Win32\x64\Release` folder (`Win32` is an alias for `Desktop`), and should be copied to the `libs\unity\library\Runtime\Plugins\Win32\x86_64\` folder. Again this is done automatically by the Visual Studio build process, but can be done manually with the following commands:

```cmd
cd /D D:\testproj
xcopy D:/mr-webrtc/bin/Win32/x64/Release/mrwebrtc.dll libs/unity/library/Runtime/Plugins/Win32/x86_64/
```

## Importing the library

Once the necessary binaries have been added to `libs\unity\library\Runtime\Plugins`, the Unity library UPM package can be imported into any number of Unity projects.

> [!WARNING]
>
> It is critical to setup the binaries first, before importing into a project and opening the Unity editor, otherwise the import settings might get overwritten by Unity. See [Configuring the import settings](#configuring-the-import-settings) below for details.

The Unity library already has the proper layout on disk of a UPM package, and a `package.json` file is provided. This means the package can be imported simply by following [the official Unity instructions for installing a local package](https://docs.unity3d.com/Manual/upm-ui-local.html).

## Configuring the import settings

This step is normally not necessary.

The git repository comes with pre-configured `.meta` files already setup. However we detail below the manual steps to configure those `.meta` files for the sake of clarity, and in case Unity deleted those files because the binaries have not been built yet (although using `git reset` is a faster and safer way to recover them), or if users want to get rid of the UPM package and incorporate a custom copy of the MixedReality-WebRTC into their Unity project and modify it.

When building a Unity application for a given platform, another variant of the MixedReality-WebRTC native C library than the one the Unity editor uses may be required to deploy on that platform. In order for the C# library to be truly platform-independent, the name of all native C library variants is the same: `mrwebrtc`. This allows the C# code to reference that DLL with [the same `DllImport` attribute path](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.dllimportattribute?view=netcore-2.1). But this also means that multiple files with the same name exist in different folders, and Unity needs to know which one is associated with which build variant, to be able to deploy the correct one only. This is done by configuring the platform associated with a DLL in the import settings in the Unity inspector.

> [!IMPORTANT]
> The `mrwebrtc.dll` module was in v1.0 named `Microsoft.MixedReality.WebRTC.Native.dll` when this module was also the C++ library. The `Microsoft.MixedReality.WebRTC.Native` name is still in use in some NuGet packages, so depending on the installation method one or the other name are used. However the Unity project must use the `mrwebrtc.dll` name only for the C# library to find it.

When importing a UPM package into a Unity project, the content of the UPM package can be accessed from the **Packages** group of the **Project** window.

![Packages in the Project window](helloworld-unity-1b.png)

To configure the import settings:

- In the **Project** window, select one of the `mrwebrtc.dll` files from the `Plugins` folder.
- The **Inspector** window now shows the import settings for the selected file, which contains option to configure the deploy platform(s) and architecture(s).

For example, by selecting in the **Inspector** window:

- **Include Platforms: WSAPlayer**, the DLL will be used by Unity on UWP platforms. _WSAPlayer_ is the name Unity uses for its UWP standalone player.
- **CPU** equal to **x86_64**, Unity will only deploy that DLL when deploying on a 64-bit Intel architecture.

This way, multiple variants of the same-named `mrwebrtc.dll` can co-exist in different sub-folders of `Assets/Plugins/` and Unity will deploy and use the correct variant on each platform.

> [!NOTE]
> If Unity complains about "_Multiple plugins with the same name 'mrwebrtc'_", then the configurations of the various `mrwebrtc.dll` files are not exclusive, and 2 or more files have been allowed for deploying on the same platform. Check the configuration of all variants again.

For **Windows Desktop**, the native implementation DLL variants are:

| Path | Include Platforms | Settings | Example use |
|---|---|---|---|
| `Plugins/Win32/x86` | Standalone | x86 | 32-bit Windows Desktop application |
| `Plugins/Win32/x86_64` | Editor | CPU = x86_64, OS = Windows | Unity Editor on Windows |
| | Standalone | x86_64 | 64-bit Windows Desktop application |

For **Windows UWP**, the native implementation DLL variants are:

| Path | Include Platforms | SDK | CPU | Example use |
|---|---|---|---|---|
| `Plugins/UWP/x86` | WSAPlayer | UWP | X86 | Microsoft HoloLens (1st gen) |
| `Plugins/UWP/x86_64` | WSAPlayer | UWP | X64 | 64-bit UWP Desktop app on Windows |
| `Plugins/UWP/ARM` | WSAPlayer | UWP | ARM | HoloLens 2 (compatibility) |

For **Android**, the native implementation archive `mrwebrc.aar` is configured as:

| Path | Include Platforms | Example use |
|---|---|---|
| `Plugins/Android/arm64-v8a` | Android | Android phone |

In all cases the "Any Platform" setting must be unchecked.

> [!NOTE]
> ARM64 is not supported. The ARM (32-bit) architecture variant can be used as a fallback on HoloLens 2 and other devices which support running 32-bit ARM applications on ARM64 hardware.

![Configure the import settings for a native C++ DLL for UWP](helloworld-unity-3.png)

If all variants are installed, the resulting hierarchy should look like this in the **Project** window:

```shell
Plugins
├─ Android
|  └─ arm64-v8a
|     └─ mrwebrtc.aar
├─ Win32
|  ├─ x86
|  |  └─ mrwebrtc.dll
|  └─ x86_64
|     └─ mrwebrtc.dll
└─ UWP
   ├─ x86
   |  └─ mrwebrtc.dll
   ├─ x86_64
   |  └─ mrwebrtc.dll
   └─ ARM
      └─ mrwebrtc.dll
```

Note that the `.meta` files where the import settings are saved do not appear in the **Project** window.

----

Next : [Creating a peer connection](helloworld-unity-peerconnection.md)
