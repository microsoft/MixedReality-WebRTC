# MixedReality-WebRTC

[![Licensed under the MIT License](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/microsoft/MixedReality-WebRTC/blob/master/LICENSE)
![Under active development](https://img.shields.io/badge/status-public_preview-red.svg)

**⚠ This repository currently contains a public preview under active development, intended for early adopters and to gather feedback. While in preview, the API is expected to be unstable and breaking changes will occur. See also the Known Issues section for details.**

MixedReality-WebRTC is a collection of components to help mixed reality app developers to integrate peer-to-peer audio and video streaming into their application and improve their collaborative experience.

- Enables **multi-track audio / video / data streaming** to a remote peer
- Provides an abstracted **signaling interface** to easily switch implementation
- Exposes an **API for C++ and C#** to integrate into existing apps
- Provides a set of **Unity3D components** for rapid prototyping and integration
- Supports Microsoft **HoloLens** (x86) and Microsoft **HoloLens 2** (ARM)
- Allows easy use of **[Mixed Reality Capture (MRC)](https://docs.microsoft.com/en-us/windows/mixed-reality/mixed-reality-capture)** to stream the view point of the user for multi-device experiences

MixedReality-WebRTC is part of the collection of repositories developed and maintained by the [Mixed Reality Sharing team](https://github.com/orgs/microsoft/teams/mixed-reality-sharing).

## Build Status

| Branch | WebRTC | C++ | C# | Docs |
|---|---|---|---|---|
| [`master`](https://github.com/microsoft/MixedReality-WebRTC/tree/master) | [m71](https://groups.google.com/forum/#!msg/discuss-webrtc/HUpIxlDlkSE/qR1nswqZCwAJ) | [![Build Status](https://dev.azure.com/aipmr/MixedReality-WebRTC-CI/_apis/build/status/mr-webrtc-cpp-ci?branchName=master)](https://dev.azure.com/aipmr/MixedReality-WebRTC-CI/_build/latest?definitionId=24&branchName=master) | [![Build Status](https://dev.azure.com/aipmr/MixedReality-WebRTC-CI/_apis/build/status/mr-webrtc-cs-ci?branchName=master)](https://dev.azure.com/aipmr/MixedReality-WebRTC-CI/_build/latest?definitionId=25&branchName=master) | [![Build Status](https://dev.azure.com/aipmr/MixedReality-WebRTC-CI/_apis/build/status/mr-webrtc-docs-ci?branchName=master)](https://dev.azure.com/aipmr/MixedReality-WebRTC-CI/_build/latest?definitionId=26&branchName=master) |
| [`dev`](https://github.com/microsoft/MixedReality-WebRTC/tree/dev) | [m71](https://groups.google.com/forum/#!msg/discuss-webrtc/HUpIxlDlkSE/qR1nswqZCwAJ) | [![Build Status](https://dev.azure.com/aipmr/MixedReality-WebRTC-CI/_apis/build/status/mr-webrtc-cpp-ci?branchName=dev)](https://dev.azure.com/aipmr/MixedReality-WebRTC-CI/_build/latest?definitionId=24&branchName=dev) | [![Build Status](https://dev.azure.com/aipmr/MixedReality-WebRTC-CI/_apis/build/status/mr-webrtc-cs-ci?branchName=dev)](https://dev.azure.com/aipmr/MixedReality-WebRTC-CI/_build/latest?definitionId=25&branchName=dev) | [![Build Status](https://dev.azure.com/aipmr/MixedReality-WebRTC-CI/_apis/build/status/mr-webrtc-docs-ci?branchName=dev)](https://dev.azure.com/aipmr/MixedReality-WebRTC-CI/_build/latest?definitionId=26&branchName=dev) |

## Components

MixedReality-WebRTC is a set of individual components in the form of C++ and C# libraries building upon each other to deliver a consistent API to C++ and C# developers across its supported platforms, and a set of handful drop-in Unity3D components for easy integration.

| Component | Lang | Description |
|---|---|---|
| `Microsoft.MixedReality.WebRTC.Native` | C++17 | Native C++ library providing a low-level interface to the [underlying WebRTC implementation from Google](https://opensource.google.com/projects/webrtc). Compared to the API exposed by the Google implementation (`PeerConnection`), the current interface is simplified to remove the burden of setup and configuring. |
| `Microsoft.MixedReality.WebRTC` | C# 7.3 | C# .Net Standard 2.0 library providing access to the same API as the native C++ library, exposed with familiar C# concepts such as `async` / `await` and `Task`. |
| `Microsoft.MixedReality.WebRTC.Unity` | C# 7.3 | Unity3D integration - a set of Unity `MonoBehaviour` components with almost no required setup, to enable rapid prototyping and simplify integration into an existing app. |
| `Microsoft.MixedReality.WebRTC.Unity.Examples` | C# 7.3 | Unity3D samples showcasing typical use scenarios like a video chat app. |

MixedReality-WebRTC is currently available for Windows 10 Desktop and UWP (18362+), with or without Unity, with planned support for Unity deployment on iOS and Android.

_Note_ - In the following and elsewhere in this repository the term "Win32" is used as a synonym for "Windows Desktop", the historical Windows API for Desktop application development, and in opposition to the "Windows UWP" API. However Microsoft Windows versions older than Windows 10 with Windows SDK 17134 (April 2018 Update, 1803) are not officially supported. In particular, older versions of Windows (Windows 7, Windows 8, etc.) are explicitly not supported. See _Required Softwares_ below for more information.

## Getting Started

MixedReality-WebRTC is currently under development, and precompiled packages are not yet available. See the _Prerequisites_ and _Building MixedReality-WebRTC_ sections below for compiling the libraries from sources.

This repository follows the [Pitchfork Layout](https://api.csswg.org/bikeshed/?force=1&url=https://raw.githubusercontent.com/vector-of-bool/pitchfork/develop/data/spec.bs) in an attempt to standardize its hierarchy:

```
bin/               Binary outputs (generated)
build/             Intermediate build artifacts (generated)
docs/              Documentation
examples/          Examples of use and sample apps
external/          Third-party external dependencies
libs/              Source code for the component libraries
tests/             Source code for unit tests
tools/             Utility scripts
```

## Prerequisites

### Core WebRTC

_Core WebRTC_ refers to the C++ implementation of WebRTC maintained by Google and used by this project, whose source repository is https://webrtc.googlesource.com/src.

[**Visual Studio 2017**](http://dev.windows.com/downloads) is required to compile the core WebRTC implementation from Google. Having the MSVC v141 toolchain installed inside another version of Visual Studio is unfortunately not enough (see [this issue](https://github.com/webrtc-uwp/webrtc-uwp-sdk/issues/175)), the actual IDE needs to be installed for the detection script to work. Selecting the **C++ Workload** alone is enough. If compiling for ARM or ARM64 architecture though, check the **Visual C++ compilers and libraries for ARM(64)** optional individual component.

The **Windows SDK 10.0.17134** (also called 1803, or April 2018) is required to compile the Google WebRTC core implementation ([archive download](https://developer.microsoft.com/en-us/windows/downloads/sdk-archive)).

### Core WebRTC UWP wrappers

The _UWP wrappers_ refer to the set of wrappers and other UWP-specific additional code made available by the WebRTC UWP team (Microsoft) on top of the core implementation, to allow access to the core WebRTC API 

The **Windows SDK 10.0.17763** (also called 1809, or October 2018) is required to compile the UWP wrappers provided by the WebRTC UWP team ([archive download](https://developer.microsoft.com/en-us/windows/downloads/sdk-archive)).

The UWP wrappers also require the v141 platform toolset for UWP, either from the **Universal Windows Platform development** workload in Visual Studio 2017, or from the optional component **C++ (v141) Universal Windows Platform tools** in [**Visual Studio 2019**](http://dev.windows.com/downloads).

### MixedReality-WebRTC C++ library

The **MSVC v142 - VS 2019 C++ x64/x86 build tools** toolchain is required to build the C++17 library of MixedReality-WebRTC. This is installed by default with the **Desktop development with C++** workload on Visual Studio 2019.

_Note_ - Currently due to CI limitations some projects are downgraded to VS 2017, but will be reverted to VS 2019 eventually (see [#14](https://github.com/microsoft/MixedReality-WebRTC/issues/14)).

### Unity integration

The Unity integration has been tested on [Unity](https://unity3d.com/get-unity/download) **2018.3.x** and **2019.1.x**. Versions earlier than 2018.3.x are not officially supported.

## Documentation

The official documentation is hosted at https://microsoft.github.io/MixedReality-WebRTC/.

- The [User Manual](https://microsoft.github.io/MixedReality-WebRTC/manual/) contains a general overview and some tutorials.
- An [API reference](https://microsoft.github.io/MixedReality-WebRTC/api/) is also available for the C# library and the Unity integration.

The overall architecture of the components is as follow:

![MixedReality-WebRTC architecture](docs/manual/architecture.png)

## Building MixedReality-WebRTC

### Check out the repository and its dependencies

```sh
git clone --recursive https://github.com/microsoft/MixedReality-WebRTC.git
```

Note that **this may take some time (> 5 minutes)** due to the large number of submodules in [the WebRTC UWP SDK repository](https://github.com/webrtc-uwp/webrtc-uwp-sdk) this repository depends on.

### Build the WebRTC UWP SDK libraries

#### Using the build script

In order to simplify building, a PowerShell **build script** is available. Simply run for example:

```sh
tools/build/build.ps1 -BuildConfig Debug -BuildArch x64 -BuildPlatform Win32
```

Valid parameter values are:

- **BuildConfig** : Debug | Release
- **BuildArch** : x86 | x64 | ARM | ARM64
- **BuildPlatform** : Win32 | UWP

_Note_ - ARM64 is not yet available (see [#13](https://github.com/microsoft/MixedReality-WebRTC/issues/13)).

The manual steps are details below and can be skipped if running the build script. The dependencies still need to be installed manually before running `build.ps1`.

#### Manually

The WebRTC UWP project has [specific requirements](https://github.com/webrtc-uwp/webrtc-uwp-sdk/blob/master/README.md). In particular it needs Python 2.7.15+ installed **as default**, that is calling `python` from a shell without specifying a path launches that Python 2.7.15+ version.

_Note_ - Currently the Azure hosted agents with VS 2017 have Python 2.7.14 installed, but this is discouraged by the WebRTC UWP team as some spurious build errors might occur. The new VS 2019 build agents have Python 2.7.16 installed, and will be used eventually.

_Note_ - Currently the `libyuv` external dependency is incorrectly compiled with Clang instead of MSVC on ARM builds. This was an attempt to benefit from inline assembly, but this produces link errors (see [this issue](https://github.com/webrtc-uwp/webrtc-uwp-sdk/issues/157)). Until this is fixed, a patch is available under `tools\patches\libyuv_win_msvc_157.patch` which is applied by `build.ps1` but needs to be applied manually if `build.ps1` is not used.

For Windows Desktop support (also called "Win32"):

- Open the **`WebRtc.Win32.sln`** Visual Studio solution located in `external\webrtc-uwp-sdk\webrtc\windows\solution\`
- In the menu bar, select the relevant solution platform and solution configuration. For the Unity editor, the **x64** binaries are required.
- **Build first the `WebRtc.Win32.Native.Builder` project alone**, which generates some files needed by some of the other projects in the solution, by right-clicking on that project > **Build**
- Build the rest of the solution with F7 or **Build > Build Solution**

For UWP support:

- Open the **`WebRtc.Universal.sln`** Visual Studio solution located in `external\webrtc-uwp-sdk\webrtc\windows\solution\`
- In the menu bar, select the relevant solution platform and solution configuration. For HoloLens, the **x86** binaries are required. For HoloLens 2, the **ARM** binaries are required (ARM64 is not supported yet, see [#13](https://github.com/microsoft/MixedReality-WebRTC/issues/13)).
- **Build first the `WebRtc.UWP.Native.Builder` project alone**, which generates some files needed by some of the other projects in the solution, by right-clicking on that project > **Build**
- Build the rest of the solution with F7 or **Build > Build Solution**

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

### Optionally test the install

Test the install by _e.g._ opening the Unity project at `libs\Microsoft.MixedReality.WebRTC.Unity`, loading the `Assets\Microsoft.MixedReality.WebRTC\Unity.Examples\SimpleVideoChat` scene and pressing **Play**. After a few seconds (depending on the machine) the left media player should display the video feed from the local webcam. The Unity console should also display a message about the WebRTC library being initialized successfully.

## Known Issues

The current version is a public preview under active development, which contains known issues being addressed:

- Mixed Reality Capture (MRC) currently does not work on HoloLens 2 out of the box. Enabling MRC silently fails, and falls back to a video stream without hologram rendering. This is due to a combination of things:
  - **MRC only works up to 1080p** (see the [Mixed reality capture for developers](https://docs.microsoft.com/en-us/windows/mixed-reality/mixed-reality-capture-for-developers) documentation), but the default resolution of the webcam on HoloLens2 is 2272x1278 (see the [Locatable Camera](https://docs.microsoft.com/en-us/windows/mixed-reality/locatable-camera) documentation). In order to access different resolutions, one need to use video profiles, which are not currently exposed by the WebRTC UWP SDK project. See [this issue](https://github.com/webrtc-uwp/webrtc-uwp-sdk/issues/170) for details.
  - **MRC requires special permission** to record the content of the screen:
    - For shared apps (2D slates), this corresponds to the `screenDuplication` [restricted capability](https://docs.microsoft.com/en-us/windows/uwp/packaging/app-capability-declarations#restricted-capabilities), which **cannot be obtained by third-party applications**.
    - For exclusive-mode apps (fullscreen), there is no particular UWP capability, but the recorded content is limited to the application's own content.
- HoloLens 2 exhibits performance issues due to:
  - The [missing support (#157)](https://github.com/webrtc-uwp/webrtc-uwp-sdk/issues/157) for SIMD-accelerated YUV conversion in WebRTC UWP SDK.
  - The use of the highest available video resolution when opening the webcam with the default video profile. Support for selecting a different video profile is not available yet in WebRTC UWP SDK. See [this issue](https://github.com/webrtc-uwp/webrtc-uwp-sdk/issues/170) for details.
  - The use by default of the VP8 video codec, which is fairly CPU intensive.
- The Debug config of WebRTC core implementation is knows to exhibit performance issues on most devices, including some higher end PCs. Using the Release config of the core WebRTC implementation usually prevents this issue.
- There is currently no clean C++ API; instead the C API used for C# P/Invoke can be used from C++ code, and opaque handles cast to C++ objects. An actual C++ API will eventually be exposed.

## Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us the rights to use your contribution. For details, visit https://cla.microsoft.com.

When you submit a pull request, a CLA-bot will automatically determine whether you need to provide a CLA and decorate the PR appropriately (e.g., label, comment). Simply follow the instructions provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## Reporting security issues and bugs

MixedReality-WebRTC builds upon the WebRTC implementation provided by Google. Security issues and bugs related to this implementation should be reported to Google.

Security issues and bugs related to MixedReality-WebRTC itself or to WebRTC UWP SDK should be reported privately, via email, to the Microsoft Security Response Center (MSRC) secure@microsoft.com. You should receive a response within 24 hours. If for some reason you do not, please follow up via email to ensure we received your original message. Further information, including the MSRC PGP key, can be found in the [Security TechCenter](https://technet.microsoft.com/en-us/security/ff852094.aspx).
