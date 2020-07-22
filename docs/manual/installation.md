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

## C/C++ library

The C/C++ library is consumed as a NuGet package by adding a dependency to that package in your C/C++ project. The C/C++ library is often referred to as the *native* library or *implementation* library.

In Visual Studio 2019:

- Right-click on the C++ project > **Manage NuGet Packages...** to open the NuGet package manager window.
- Select the **Browse** tab.
- In the search bar, type "Microsoft.MixedReality.WebRTC.Native". You may need to check the **Include prerelease** option.
- Select the NuGet package to install:
  - For a C++ Desktop project, choose `mrwebrtc`.
  - For a C++ UWP project, choose `mrwebrtc_uwp`.
- In the right panel, choose a version and click the **Install** button.

This will add a dependency to the currently selected C++ project. If multiple projects are using the MixedReality-WebRTC library, this process must be repeated for each project.

## Unity library

Starting from v2.0.0, a [UPM package](https://docs.unity3d.com/Manual/Packages.html) named `com.microsoft.mixedreality.webrtc` is the main distribution method for the Unity library.

This package is currently distributed as [an on-disk package (local package)](https://docs.unity3d.com/Manual/upm-ui-local.html) containing prebuilt binaries for all supported Unity platforms. Follow [the official Unity instructions](https://docs.unity3d.com/Manual/upm-ui-local.html) to import the library package into a Unity project via the Unity Package Manager (UPM) window.

An optional package `com.microsoft.mixedreality.webrtc.samples` is also available which contains some samples to show how to use the Unity library. This samples package depends on the library package. It should not be used in production.
