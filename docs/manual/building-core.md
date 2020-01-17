# Building the Core dependencies from sources

This document describes how to build the entire project from sources, including the so-called _Core_ dependencies:

- The low-level WebRTC C++ implementation `webrtc.lib` from Google.
- The UWP WinRT wrapper `Org.WebRtc.winmd` from the Microsoft WebRTC UWP team.

The dependencies require some heavy setup and take time to compile, therefore **it is strongly recommended to use the prebuilt binaries shipped as NuGet packages** instead of trying to build those from source.

**Windows Desktop**

These packages contain `webrtc.lib` built for Windows Desktop (`Win32`) for a given architecture and build config.

- [`Microsoft.MixedReality.WebRTC.Native.Core.Desktop.x86.Debug`](https://www.nuget.org/packages/Microsoft.MixedReality.WebRTC.Native.Core.Desktop.x86.Debug/)
- [`Microsoft.MixedReality.WebRTC.Native.Core.Desktop.x86.Release`](https://www.nuget.org/packages/Microsoft.MixedReality.WebRTC.Native.Core.Desktop.x86.Release/)
- [`Microsoft.MixedReality.WebRTC.Native.Core.Desktop.x64.Debug`](https://www.nuget.org/packages/Microsoft.MixedReality.WebRTC.Native.Core.Desktop.x64.Debug/)
- [`Microsoft.MixedReality.WebRTC.Native.Core.Desktop.x64.Release`](https://www.nuget.org/packages/Microsoft.MixedReality.WebRTC.Native.Core.Desktop.x64.Release/)

**Windows UWP**

These packages contain `webrtc.lib` built for Windows UWP for a given architecture and build config.

- [`Microsoft.MixedReality.WebRTC.Native.Core.UWP.x86.Debug`](https://www.nuget.org/packages/Microsoft.MixedReality.WebRTC.Native.Core.UWP.x86.Debug/)
- [`Microsoft.MixedReality.WebRTC.Native.Core.UWP.x86.Release`](https://www.nuget.org/packages/Microsoft.MixedReality.WebRTC.Native.Core.UWP.x86.Release/)
- [`Microsoft.MixedReality.WebRTC.Native.Core.UWP.x64.Debug`](https://www.nuget.org/packages/Microsoft.MixedReality.WebRTC.Native.Core.UWP.x64.Debug/)
- [`Microsoft.MixedReality.WebRTC.Native.Core.UWP.x64.Release`](https://www.nuget.org/packages/Microsoft.MixedReality.WebRTC.Native.Core.UWP.x64.Release/)
- [`Microsoft.MixedReality.WebRTC.Native.Core.UWP.ARM.Debug`](https://www.nuget.org/packages/Microsoft.MixedReality.WebRTC.Native.Core.UWP.ARM.Debug/)
- [`Microsoft.MixedReality.WebRTC.Native.Core.UWP.ARM.Release`](https://www.nuget.org/packages/Microsoft.MixedReality.WebRTC.Native.Core.UWP.ARM.Release/)

In addition, the [`Microsoft.MixedReality.WebRTC.Native.Core.UWP`](https://www.nuget.org/packages/Microsoft.MixedReality.WebRTC.Native.Core.UWP/) package (which should have been named `.WinRT` for consistency) references all the architecture-dependent ones for UWP for convenience, and also contains the platform-independent generated C++/WinRT headers necessary to consume the libraries.

**WinRT binding**

These packages contain the WinRT binding (`Org.WebRtc.dll`, `Org.WebRtc.winmd`, `Org.WebRtc.WrapperGlue.lib`) built for Windows UWP for a given architecture and build config.

- [`Microsoft.MixedReality.WebRTC.Native.Core.WinRT.x86.Debug`](https://www.nuget.org/packages/Microsoft.MixedReality.WebRTC.Native.Core.WinRT.x86.Debug/)
- [`Microsoft.MixedReality.WebRTC.Native.Core.WinRT.x86.Release`](https://www.nuget.org/packages/Microsoft.MixedReality.WebRTC.Native.Core.WinRT.x86.Release/)
- [`Microsoft.MixedReality.WebRTC.Native.Core.WinRT.x64.Debug`](https://www.nuget.org/packages/Microsoft.MixedReality.WebRTC.Native.Core.WinRT.x64.Debug/)
- [`Microsoft.MixedReality.WebRTC.Native.Core.WinRT.x64.Release`](https://www.nuget.org/packages/Microsoft.MixedReality.WebRTC.Native.Core.WinRT.x64.Release/)
- [`Microsoft.MixedReality.WebRTC.Native.Core.WinRT.ARM.Debug`](https://www.nuget.org/packages/Microsoft.MixedReality.WebRTC.Native.Core.WinRT.ARM.Debug/)
- [`Microsoft.MixedReality.WebRTC.Native.Core.WinRT.ARM.Release`](https://www.nuget.org/packages/Microsoft.MixedReality.WebRTC.Native.Core.WinRT.ARM.Release/)

In general **most users will want to follow [the steps in the readme](https://github.com/microsoft/MixedReality-WebRTC/blob/master/README.md) instead of the ones below** if there is no need to modify the input dependencies.

## Prerequisites

### General

- Python 2.7 must be installed as the default interpreter. That is, `python --version` must return a Python version equal to 2.7. It is strongly recommended to get a patch version >= 15, that is **Python 2.7.15+**, as some users reported to the WebRTC UWP team some spurious failures with earlier versions. Python 3.x does **not** work and should not be the default interpreter.

- A recent version of Perl is needed for some builds. On Windows you can install for example [Strawberry Perl](http://strawberryperl.com/), or any other equivalent distribution you want.

### Core WebRTC

_Core WebRTC_ refers to the C++ implementation of WebRTC maintained by Google and used by this project, whose source repository is https://webrtc.googlesource.com/src.

- [**Visual Studio 2017**](http://dev.windows.com/downloads) is required to compile the core WebRTC implementation from Google. Having the MSVC v141 toolchain installed inside another version of Visual Studio is unfortunately not enough (see [this issue](https://github.com/webrtc-uwp/webrtc-uwp-sdk/issues/175)), the actual IDE needs to be installed for the detection script to work. Selecting the **C++ Workload** alone is enough. If compiling for ARM or ARM64 architecture though, check the **Visual C++ compilers and libraries for ARM(64)** optional individual component.

- The **Windows SDK 10.0.17134** (also called 1803, or April 2018) is required to compile the Google WebRTC core implementation ([archive download](https://developer.microsoft.com/en-us/windows/downloads/sdk-archive)).

- As mentioned on the README of WebRTC UWP, the **Debugging Tools for Windows** are required:

  > When installing the SDK, include the feature **Debugging Tools for Windows** which is required by the preparation scripts. Note that the SDK installed as part of Visual Studio does not include this feature.

  If the SDK is already installed, this optional feature can be added with **Add or Remove Programs** > **Windows Software Development Kit - Windows 10.0.x** > **Modify** > Select **Change** then **Next** button > Check **Debugging Tools for Windows**.

### Core WebRTC UWP wrappers

The _UWP wrappers_ refer to the set of wrappers and other UWP-specific additional code made available by the WebRTC UWP team (Microsoft) on top of the core implementation, to allow access to the core WebRTC API.

- The **Windows SDK 10.0.17763** (also called 1809, or October 2018) is required to compile the UWP wrappers provided by the WebRTC UWP team ([archive download](https://developer.microsoft.com/en-us/windows/downloads/sdk-archive)), with the **Debugging Tools for Windows** as above.

- The UWP wrappers also require the v141 platform toolset for UWP, either from the **Universal Windows Platform development** workload in Visual Studio 2017, or from the optional component **C++ (v141) Universal Windows Platform tools** in [**Visual Studio 2019**](http://dev.windows.com/downloads).

- The UWP wrappers use C++/WinRT, so the [**C++/WinRT Visual Studio extension**](https://marketplace.visualstudio.com/items?itemName=CppWinRTTeam.cppwinrt101804264) must be installed from the marketplace.

### MixedReality-WebRTC C++ library

- The **MSVC v142 - VS 2019 C++ x64/x86 build tools** toolchain is required to build the C++17 library of MixedReality-WebRTC. This is installed by default with the **Desktop development with C++** workload on Visual Studio 2019.

_Note_ - Currently due to CI limitations some projects are downgraded to VS 2017, but will be reverted to VS 2019 eventually (see #14).

### Unity integration

The Unity integration has been tested on [Unity](https://unity3d.com/get-unity/download) **2018.3.x** and **2019.1.x**. Versions earlier than 2018.3.x may work but are not officially supported.

## Build steps

### Check out the repository and its dependencies

```sh
git clone --recursive https://github.com/microsoft/MixedReality-WebRTC.git
```

Note that **this may take some time (> 5 minutes)** due to the large number of submodules in [the WebRTC UWP SDK repository](https://github.com/webrtc-uwp/webrtc-uwp-sdk) this repository depends on.

### Build the WebRTC UWP SDK libraries

#### Using the build script

In order to simplify building, a PowerShell **build script** is available. The prerequisites still need to be installed manually before running it.

To use the script, simply run for example:

```sh
cd tools/build/
build.ps1 -BuildConfig Debug -BuildArch x64 -BuildPlatform Win32
```

_Note_ - Currently the build script assumes it runs from `tools/build/` only. It will fail if invoked from another directory.

Valid parameter values are:

- **BuildConfig** : Debug | Release
- **BuildArch** : x86 | x64 | ARM | ARM64
- **BuildPlatform** : Win32 | UWP

_Note_ - ARM and ARM64 are only valid for the UWP platform.

_Note_ - ARM64 is not yet available (see [#13](https://github.com/microsoft/MixedReality-WebRTC/issues/13)).

The manual steps are details below and can be skipped if running the build script. 

#### Manually

The WebRTC UWP project has [specific requirements](https://github.com/webrtc-uwp/webrtc-uwp-sdk/blob/master/README.md). In particular it needs Python 2.7.15+ installed **as default**, that is calling `python` from a shell without specifying a path launches that Python 2.7.15+ version.

_Note_ - Currently the Azure hosted agents with VS 2017 have Python 2.7.14 installed, but this is discouraged by the WebRTC UWP team as some spurious build errors might occur. The new VS 2019 build agents have Python 2.7.16 installed.

_Note_ - Currently the `libyuv` external dependency is incorrectly compiled with Clang instead of MSVC on ARM builds. This was an attempt to benefit from inline assembly, but this produces link errors (see [this issue](https://github.com/webrtc-uwp/webrtc-uwp-sdk/issues/157)). Until this is fixed, a patch is available under `tools\patches\libyuv_win_msvc_157.patch` which is applied by `build.ps1` but needs to be applied manually if `build.ps1` is not used. More generally **all patches under `tools\patches` need to be manually applied**.

For Windows 10 Desktop support (also called "Win32"):

- Open the **`WebRtc.Win32.sln`** Visual Studio solution located in `external\webrtc-uwp-sdk\webrtc\windows\solution\`
- In the menu bar, select the relevant solution platform and solution configuration. For the Unity editor, the **x64** binaries are required.
- **Build the `WebRtc.Win32.Native.Builder` project alone**, which generates some files needed by some of the other projects in the solution, by right-clicking on that project > **Build**. The other projects are samples and are not needed.

For UWP support:

- Open the **`WebRtc.Universal.sln`** Visual Studio solution located in `external\webrtc-uwp-sdk\webrtc\windows\solution\`
- In the menu bar, select the relevant solution platform and solution configuration. For HoloLens, the **x86** binaries are required. For HoloLens 2, the **ARM** binaries are required (ARM64 is not supported yet, see [#13](https://github.com/microsoft/MixedReality-WebRTC/issues/13)).
- **Build first the `WebRtc.UWP.Native.Builder` project alone**, which generates some files needed by some of the other projects in the solution, by right-clicking on that project > **Build**
- Next build the `Org.WebRtc` and `Org.WebRtc.WrapperGlue` projects. The other projects samples and are not needed.

### Build the MixedReality-WebRTC libraries

- Open the `Microsoft.MixedReality.WebRTC.sln` Visual Studio solution located at the root of the repository.
- Build the solution with F7 or **Build > Build Solution**

On successful build, the binaries will be generated in a sub-directory under `bin/`, and the relevant DLLs will be copied by a post-build script to `libs\Microsoft.MixedReality.WebRTC.Unity\Assets\Plugins\` for Unity to consume them.

The `Microsoft.MixedReality.WebRTC.sln` Visual Studio solution contains several projects:

- The native C++ library, which can be compiled:
  - for Windows Desktop with the `Microsoft.MixedReality.WebRTC.Native.Win32` project
  - for UWP with the `Microsoft.MixedReality.WebRTC.Native.UWP` project
- The C# library project `Microsoft.MixedReality.WebRTC`
- A C# unit tests project `Microsoft.MixedReality.WebRTC.Tests`
- A UWP C# sample app project `Microsoft.MixedReality.WebRTC.TestAppUWP` based on WPF and XAML

### Optionally test the installation

Test the install by _e.g._ opening the Unity project at `libs\Microsoft.MixedReality.WebRTC.Unity`, loading the `Assets\Microsoft.MixedReality.WebRTC.Unity.Examples\VideoChatDemo` scene and pressing **Play**. After a few seconds (depending on the machine) the left media player should display the video feed from the local webcam. The Unity console should also display a message about the WebRTC library being initialized successfully.

See the [Hello, Unity World!](https://microsoft.github.io/MixedReality-WebRTC/manual/helloworld-unity.html) tutorial for more details.
