# Installation

## C# library

The C# library is consumed as a NuGet package by adding a dependency to that package in your C# project.

In Visual Studio 2019:

- Right-click on the C# project > **Manage NuGet Packages...** to open the NuGet package manager window.
- In the search bar, type "Microsoft.MixedReality.WebRTC". You may need to check the **Include prerelease** option.
- Select the NuGet package to install:
  - For a C# Desktop project, choose `Microsoft.MixedReality.WebRTC`.
  - For a C# UWP project, choose `Microsoft.MixedReality.WebRTC.UWP`.
- In the right panel, choose a version and click the **Install** button.

![Install](install-1.png)

This will add a dependency to the currently selected C# project. If multiple projects are using the MixedReality-WebRTC library, this process must be repeated for each project.

## C library

The C library is consumed as a NuGet package by adding a dependency to that package in your C/C++ project. The C library is often referred to as the **native** library or **implementation** library.

> [!IMPORTANT]
> The C++ library as distributed in the NuGet pacakges v1.x requires some internal headers from the Google's WebRTC implementation to be used, which are **not** shipped with the NuGet packages. See bug [#123](https://github.com/microsoft/MixedReality-WebRTC/issues/123) for details. A workaround is to clone the repository _recursively_ to get those headers (`git clone --recursive`). This defect is fixed in the C library starting from v2.0.

In Visual Studio 2019:

- Right-click on the C++ project > **Manage NuGet Packages...** to open the NuGet package manager window.
- Select the **Browse** tab.
- In the search bar, type "Microsoft.MixedReality.WebRTC.Native". You may need to check the **Include prerelease** option.
- Select the NuGet package to install:
  - For a C++ Desktop project, choose `Microsoft.MixedReality.WebRTC.Native.Desktop`.
  - For a C++ UWP project, choose `Microsoft.MixedReality.WebRTC.Native.UWP`.
- In the right panel, choose a version and click the **Install** button.

![Install](install-2.png)

This will add a dependency to the currently selected C++ project. If multiple projects are using the MixedReality-WebRTC library, this process must be repeated for each project.

## Unity library

Starting from v2.0, a [UPM package](https://docs.unity3d.com/Manual/Packages.html) named `com.microsoft.mixedreality.webrtc` is the main distribution method for the Unity library.

This package is currently distributed as [an on-disk package (local package)](https://docs.unity3d.com/Manual/upm-ui-local.html) and requires the user to first build the C and C# libraries. The package manifest is located at `libs/unity/library/package.json`. There is currently no prebuilt UPM package including prebuilt binaries (yet).

### Building the C and C# libraries

Follow [the building instructions](building.md) to generate the `Microsoft.MixedReality.WebRTC.dll` C# assembly as well as any build variant of the `mrwebrtc` native C library needed for the Unity project to deploy on the platform the user targets.

- The Unity editor requires the x86_64 architecture of the Windows Desktop (Win32) variant.
- HoloLens 1 requires the UWP x86 variant.
- HoloLens 2 requires the UWP ARM variant (32-bit). ARM64 is not available.
- Android deployment requires the arm64-v8a variant. Other ARM architectures are not supported.

The Visual Studio solution used to build the Windows binaries (Desktop and UWP) will automatically copy the DLLs at the right location in the local git repository. If building Android from a Linux distribution (recommended) the `mrwebrtc.aar` archive needs to be manually copied to the Windows git repository.

Unlike the C native library, the C# library has only one common variant for all build architectures and platforms. However it is recommended to place the C# library assembly `Microsoft.MixedReality.WebRTC.dll` inside the `Win32/x86_64` sub-folder of the Unity plugins (the folder used by the Unity editor), as past experience showed that other layouts may lead to DLL discovery issue.

The above steps should result in the following hierarchy on disk, if building all possible build variants:

```sh
libs/unity/library/Runtime/Plugins/
├─ arm64-v8a.meta
├─ arm64-v8a/
|  ├─ mrwebrtc.aar
|  └─ mrwebrtc.aar.meta
├─ Win32.meta
├─ Win32/
|  ├─ x86.meta
|  ├─ x86/
|  |  ├─ mrwebrtc.dll
|  |  └─ mrwebrtc.dll.meta
|  ├─ x86_64.meta
|  └─ x86_64/
|     ├─ mrwebrtc.dll
|     ├─ mrwebrtc.dll.meta
|     ├─ Microsoft.MixedReality.WebRTC.dll
|     └─ Microsoft.MixedReality.WebRTC.dll.meta
├─ WSA.meta
└─ WSA/
   ├─ x86.meta
   ├─ x86/
   |  ├─ mrwebrtc.dll
   |  └─ mrwebrtc.dll.meta
   ├─ x86_64.meta
   ├─ x86_64/
   |  ├─ mrwebrtc.dll
   |  └─ mrwebrtc.dll.meta
   ├─ ARM.meta
   └─ ARM/
      ├─ mrwebrtc.dll
      └─ mrwebrtc.dll.meta
```

> [!NOTE]
> The repository also contains some `.meta` files for the debug symbols databases (`.pdb`). The PDB are only necessary for debugging, thus the `.pdb.meta` files can be deleted. The other `.meta` files however need to be carefully safeguarded, as they contain the configuration needed for Unity to correctly deploy the binaries depending on the target platform.

### Importing the UPM package

Follow [the official Unity instructions](https://docs.unity3d.com/Manual/upm-ui-local.html) to import the library package into a Unity project after having compiled the library binaries, using the `libs/unity/library/package.json` file.

> [!NOTE]
> If you already imported the library package into a Unity project **before** building the C and C# libraries, the Unity editor will find the `.meta` files without a corresponding asset and will attempt to delete those files thinking they are stale. Then once built the Unity editor will regenerate some new `.meta` files with a default configuration; unfortunately this configuration is generally wrong, and will prevent correct deployment and/or generate some error. You can close the Unity editor and revert its changes with `git reset` to restore the original `.meta` files checked in the GitHub repository, which contain the correct configuration for each binary file.

For more information about the `.meta` files and the per-platform configuration of the native library, see [Importing MixedReality-WebRTC](https://microsoft.github.io/MixedReality-WebRTC/manual/helloworld-unity-importwebrtc.html) in the Unity tutorial.

The repository also contains an optional package `com.microsoft.mixedreality.webrtc.samples` imported via the `libs/unity/samples/package.json` file. This is an optional package containing some samples to show how to use the Unity library. This samples package depends on the library package.
