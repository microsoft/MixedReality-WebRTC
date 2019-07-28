# MixedReality-WebRTC

[![Licensed under the MIT License](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/microsoft/MixedReality-WebRTC/blob/master/LICENSE)
![Under active development](https://img.shields.io/badge/status-public_preview-red.svg)
[![Holodevelopers channel on Slack](https://img.shields.io/badge/slack-@holodevelopers-%23972299.svg?logo=slack)](https://holodevelopers.slack.com/messages/C1AK8E6CS/)

**⚠ PUBLIC PREVIEW ⚠**

**This repository currently contains a public preview under active development, intended for early adopters and to gather feedback. While in preview, the API is expected to be unstable and breaking changes will occur. See also the Known Issues section for details.**

MixedReality-WebRTC is a collection of libraries to help mixed reality app developers to integrate peer-to-peer real-time audio and video communication into their application and improve their collaborative experience.

- Enables **multi-track real-time audio / video / data communication** with a remote peer
- Provides an abstracted **signaling interface** to easily switch implementation
- Exposes an **API for C++ and C#** to integrate into existing apps
- Provides a set of **Unity3D components** for rapid prototyping and integration
- Supports Microsoft **HoloLens** (x86) and Microsoft **HoloLens 2** (ARM)
- Allows easy use of **[Mixed Reality Capture (MRC)](https://docs.microsoft.com/en-us/windows/mixed-reality/mixed-reality-capture)** to stream the view point of the user for multi-device experiences

MixedReality-WebRTC is part of the collection of repositories developed and maintained by the [Mixed Reality Sharing team](https://github.com/orgs/microsoft/teams/mixed-reality-sharing).

## Build Status

| Branch | WebRTC | C++ Library | C# Library | Docs |
|---|---|---|---|---|
| [`master`](https://github.com/microsoft/MixedReality-WebRTC/tree/master) | [m71](https://groups.google.com/forum/#!msg/discuss-webrtc/HUpIxlDlkSE/qR1nswqZCwAJ) | [![Build Status](https://dev.azure.com/aipmr/MixedReality-WebRTC-CI/_apis/build/status/mr-webrtc-cpp-ci?branchName=master)](https://dev.azure.com/aipmr/MixedReality-WebRTC-CI/_build/latest?definitionId=24&branchName=master) | [![Build Status](https://dev.azure.com/aipmr/MixedReality-WebRTC-CI/_apis/build/status/mr-webrtc-cs-ci?branchName=master)](https://dev.azure.com/aipmr/MixedReality-WebRTC-CI/_build/latest?definitionId=25&branchName=master) | [![Build Status](https://dev.azure.com/aipmr/MixedReality-WebRTC-CI/_apis/build/status/mr-webrtc-docs-ci?branchName=master)](https://dev.azure.com/aipmr/MixedReality-WebRTC-CI/_build/latest?definitionId=26&branchName=master) |
| [`feature/m75`](https://github.com/microsoft/MixedReality-WebRTC/tree/feature/m75) | [m75](https://groups.google.com/forum/#!msg/discuss-webrtc/_jlUbYjv-hQ/Wd2mQgpOAgAJ) | [![Build Status](https://dev.azure.com/aipmr/MixedReality-WebRTC-CI/_apis/build/status/mr-webrtc-cpp-ci?branchName=feature/m75)](https://dev.azure.com/aipmr/MixedReality-WebRTC-CI/_build/latest?definitionId=24&branchName=feature/m75) | [![Build Status](https://dev.azure.com/aipmr/MixedReality-WebRTC-CI/_apis/build/status/mr-webrtc-cs-ci?branchName=feature/m75)](https://dev.azure.com/aipmr/MixedReality-WebRTC-CI/_build/latest?definitionId=25&branchName=feature/m75) | [![Build Status](https://dev.azure.com/aipmr/MixedReality-WebRTC-CI/_apis/build/status/mr-webrtc-docs-ci?branchName=feature/m75)](https://dev.azure.com/aipmr/MixedReality-WebRTC-CI/_build/latest?definitionId=26&branchName=feature/m75) |

The current up-to-date branch with latest developments is the `master` branch. Initial support for WebRTC release M75 started on the `feature/m75` branch, but is not in a working state yet. See [#13](https://github.com/microsoft/MixedReality-WebRTC/issues/13) for details.

## Documentation

The official documentation is hosted at https://microsoft.github.io/MixedReality-WebRTC/.

- The [User Manual](https://microsoft.github.io/MixedReality-WebRTC/manual/) contains a general overview and some tutorials.
  - The [Hello, Unity world!](docs/manual/helloworld-unity.md) tutorial introduces the Unity integration by building a simple chat client.
  - The Hello, C# world! tutorial (under construction) introduces the C# API.
- An [API reference](https://microsoft.github.io/MixedReality-WebRTC/api/) is also available for the C# library and the Unity integration.

## Getting Started

MixedReality-WebRTC is a set of individual building blocks in the form of C++ and C# libraries building upon each other to deliver a consistent API to C++ and C# developers across its supported platforms, and a set of handful drop-in Unity3D components for easy integration.

### Overview

The overall architecture is as follow:

![MixedReality-WebRTC architecture](docs/manual/architecture.png)

| Library | Lang | Description |
|---|---|---|
| `Microsoft.MixedReality.WebRTC.Native` | C++17 | Native C++ library providing a low-level interface to the [underlying WebRTC implementation from Google](https://opensource.google.com/projects/webrtc). Compared to the API exposed by the Google implementation (`PeerConnection`), the current interface is simplified to remove the burden of setup and configuring. It also tries to prevent common threading errors with the UWP wrappers. |
| `Microsoft.MixedReality.WebRTC` | C# 7.3 | C# .Net Standard 2.0 library providing access to the same API as the native C++ library, exposed with familiar C# concepts such as `async` / `await` and `Task`. |
| `Microsoft.MixedReality.WebRTC.Unity` | C# 7.3 | Unity3D integration - a set of Unity [`MonoBehaviour` components](https://docs.unity3d.com/ScriptReference/MonoBehaviour.html) with almost no required setup, to enable rapid prototyping and simplify integration into an existing app. |
| `Microsoft.MixedReality.WebRTC.Unity.Examples` | C# 7.3 | Unity3D samples showcasing typical use scenarios like a peer-to-peer video chat app. |

MixedReality-WebRTC is currently available for Windows 10 Desktop and UWP, with or without Unity, with planned support for Unity deployment on iOS and Android.

_Note_ - In the following and elsewhere in this repository the term "Win32" is used as a synonym for "Windows Desktop", the historical Windows API for Desktop application development, and in opposition to the "Windows UWP" API. However Microsoft Windows versions older than Windows 10 with Windows SDK 17134 (April 2018 Update, 1803) are not officially supported for this project. In particular, older versions of Windows (Windows 7, Windows 8, etc.) are explicitly not supported.

### Binary packages

MixedReality-WebRTC is currently under development, and precompiled binary packages for the project's libraries are not yet available. See the _Building MixedReality-WebRTC_ sections below for compiling those libraries from sources.

### Sources

This repository follows the [Pitchfork Layout](https://api.csswg.org/bikeshed/?force=1&url=https://raw.githubusercontent.com/vector-of-bool/pitchfork/develop/data/spec.bs) in an attempt to standardize its hierarchy:

```sh
bin/               # Binary outputs (generated)
build/             # Intermediate build artifacts (generated)
docs/              # Documentation
+ manual/          # User manual
examples/          # Examples of use and sample apps
external/          # Third-party external dependencies (git submodules)
libs/              # Source code for the individual libraries
tests/             # Source code for feature tests
tools/             # Utility scripts
+ build/           # Build scripts
+ ci/              # CI Azure pipelines
+ patches/         # Patches applied by build.ps1
```

The `Microsoft.MixedReality.WebRTC.sln` Visual Studio 2019 solution located at the root of the repository contains several projects:

- The native C++ library, which can be compiled:
  - for Windows Desktop with the `Microsoft.MixedReality.WebRTC.Native.Win32` project
  - for UWP with the `Microsoft.MixedReality.WebRTC.Native.UWP` project
- A C++ unit tests project `Microsoft.MixedReality.WebRTC.Native.Tests`
- The C# library project `Microsoft.MixedReality.WebRTC`
- A C# unit tests project `Microsoft.MixedReality.WebRTC.Tests`
- A UWP C# sample app project `Microsoft.MixedReality.WebRTC.TestAppUWP` based on WPF and XAML which demonstrates audio / video / data communication by mean of a simple video chat app.

## Building MixedReality-WebRTC

The MixedReality-WebRTC projects consume their input dependencies as NuGet packages for simplicity, because those are complex to build and take a prohibitive amount of time. The steps below are recommended for most users who need to modify the source code of MixedReality-WebRTC without the need to change the input dependencies.

### Prerequisites

This section describes the prerequisites to build the MixedReality-WebRTC solution using the prebuilt binary input dependencies (NuGet packages). The solution is available at the root of the repository : [`Microsoft.MixedReality.WebRTC.sln`](https://github.com/microsoft/MixedReality-WebRTC/blob/master/Microsoft.MixedReality.WebRTC.sln).

- The NuGet packages for the input dependencies require in total approximately **10 GB of disk space**. Those dependencies contain unstripped `.lib` files much larger than the final compiled DLL libraries, for both the Debug and Release build configurations.

- Due to the Windows path length limit, it is recommended to **clone the source repository close to the root**, _e.g._ `C:\mr-webrtc\` or similar, as the recursive external dependencies create a deep hierarchy which may otherwise produce paths beyond the OS limit.

- **Visual Studio 2019** with the following features:

  - The **MSVC v142 - VS 2019 C++ x64/x86 build tools** toolchain is required to build the C++17 library of MixedReality-WebRTC. This is installed by default with the **Desktop development with C++** workload on Visual Studio 2019.

  - The C# library requires a .NET Standard 2.0 compiler, like the Roslyn compiler available as part of Visual Studio when installing the **.NET desktop development** workload.

  - The UWP libraries and projects require UWP support from the compiler, available as part of Visual Studio when installing the **Universal Windows Platform development** workload.

- The Unity integration has been tested on [Unity](https://unity3d.com/get-unity/download) **2018.3.x** and **2019.1.x**. Versions earlier than 2018.3.x may work but are not officially supported.

### Building

1. Check out the repository and its dependencies recursively, preferably close to the root (see prerequisites):

   ```cmd
   git clone --recursive https://github.com/microsoft/MixedReality-WebRTC.git C:\mr-webrtc
   ```

   Note that **this may take some time (> 5 minutes)** due to the large number of submodules in [the WebRTC UWP SDK repository](https://github.com/webrtc-uwp/webrtc-uwp-sdk) this repository depends on.

2. Build the MixedReality-WebRTC libraries

   - Open the `Microsoft.MixedReality.WebRTC.sln` Visual Studio solution located at the root of the repository.
   - Build the solution with F7 or **Build > Build Solution**

   On successful build, the binaries will be generated in a sub-directory under `bin/`, and the relevant DLLs will be copied by a post-build script to `libs\Microsoft.MixedReality.WebRTC.Unity\Assets\Plugins\` for Unity to consume them.

   _Note_ - At the moment the Unity plugins need to be manually configured in the Inspector window after Unity created a `.meta` files for them. Failing to do so will produce some duplicate assembly errors. See the [Hello, Unity World!](https://microsoft.github.io/MixedReality-WebRTC/manual/helloworld-unity.html) tutorial for the steps to follow.

3. Optionally test the installation

   Test the install by _e.g._ opening the Unity project at `libs\Microsoft.MixedReality.WebRTC.Unity`, loading the `Assets\Microsoft.MixedReality.WebRTC\Unity.Examples\SimpleVideoChat` scene and pressing **Play**. After a few seconds (depending on the machine) the left media player should display the video feed from the local webcam. The Unity console should also display a message about the WebRTC library being initialized successfully.

   See the [Hello, Unity World!](https://microsoft.github.io/MixedReality-WebRTC/manual/helloworld-unity.html) tutorial for more details.

_Note_ - Although this is **strongly discouraged** for most users due to its complexity, a detailed step-by-step tutorial on building from source **including** the _Core_ input dependencies is also available, see [Building from sources](building.md).

## Known Issues

The current version is a public preview under active development, which contains known issues being addressed:

- Mixed Reality Capture (MRC) currently does not work on HoloLens 2 out of the box. Enabling MRC silently fails, and falls back to a video stream without hologram rendering. This is due to a combination of things:
  - **MRC only works up to 1080p** (see the [Mixed reality capture for developers](https://docs.microsoft.com/en-us/windows/mixed-reality/mixed-reality-capture-for-developers) documentation), but the default resolution of the webcam on HoloLens2 is 2272x1278 (see the [Locatable Camera](https://docs.microsoft.com/en-us/windows/mixed-reality/locatable-camera) documentation). In order to access different resolutions, one need to use video profiles, which are not currently exposed by the WebRTC UWP SDK project. See [this issue](https://github.com/webrtc-uwp/webrtc-uwp-sdk/issues/170) for details.
  - **MRC requires special permission** to record the content of the screen:
    - For shared apps (2D slates), this corresponds to the `screenDuplication` [restricted capability](https://docs.microsoft.com/en-us/windows/uwp/packaging/app-capability-declarations#restricted-capabilities), which **cannot be obtained by third-party applications**.
    - For exclusive-mode apps (fullscreen), there is no particular UWP capability, but the recorded content is limited to the application's own content.
- HoloLens 2 exhibits performance issues thought to be due to:
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
