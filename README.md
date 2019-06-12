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

## Components

MixedReality-WebRTC is a set of individual components in the form of C++ and C# libraries building upon each other to deliver a consistent API to C++ and C# developers across its supported platforms, and a set of handful drop-in Unity3D components for easy integration.

| Component | Lang | Description |
|---|---|---|
| `Microsoft.MixedReality.WebRTC.Native` | C++17 | Native C++ library providing a low-level interface to the [underlying WebRTC implementation from Google](https://opensource.google.com/projects/webrtc). Compared to the API exposed by the Google implementation (`PeerConnection`), the current interface is simplified to remove the burden of setup and configuring. |
| `Microsoft.MixedReality.WebRTC` | C# 7.3 | C# .Net Standard 2.0 library providing access to the same API as the native C++ library, exposed with familiar C# concepts such as `async` / `await` and `Task`. |
| `Microsoft.MixedReality.WebRTC.Unity` | C# 7.3 | Unity3D integration - a set of Unity `MonoBehaviour` components with almost no required setup, to enable rapid prototyping and simplify integration into an existing app. |
| `Microsoft.MixedReality.WebRTC.Unity.Examples` | C# 7.3 | Unity3D samples showcasing typical use scenarios like a video chat app. |

MixedReality-WebRTC is currently available for Windows Desktop (Win32) and Windows UWP, with planned support for iOS and Android.

## Build Status

MixedReality-WebRTC is under active development. The current development branch is [`dev`](https://github.com/microsoft/MixedReality-WebRTC/tree/dev) and uses the [m71](https://groups.google.com/forum/#!msg/discuss-webrtc/HUpIxlDlkSE/qR1nswqZCwAJ) release of WebRTC.

| Branch | WebRTC | Platform | Architecture | Build Status |
|---|---|---|---|---|
| [`dev`](https://github.com/microsoft/MixedReality-WebRTC/tree/dev) | [m71](https://groups.google.com/forum/#!msg/discuss-webrtc/HUpIxlDlkSE/qR1nswqZCwAJ) | Windows Win32 | x86 | - |
| [`dev`](https://github.com/microsoft/MixedReality-WebRTC/tree/dev) | [m71](https://groups.google.com/forum/#!msg/discuss-webrtc/HUpIxlDlkSE/qR1nswqZCwAJ) | Windows Win32 | x86_64 | [![Build status](https://microsoft.visualstudio.com/Analog/_apis/build/status/internal/middleware/mixedreality-webrtc/mr-webrtc-cs)](https://microsoft.visualstudio.com/Analog/_build/latest?definitionId=40611) |
| [`dev`](https://github.com/microsoft/MixedReality-WebRTC/tree/dev) | [m71](https://groups.google.com/forum/#!msg/discuss-webrtc/HUpIxlDlkSE/qR1nswqZCwAJ) | Windows Win32 | ARM | - |
| [`dev`](https://github.com/microsoft/MixedReality-WebRTC/tree/dev) | [m71](https://groups.google.com/forum/#!msg/discuss-webrtc/HUpIxlDlkSE/qR1nswqZCwAJ) | Windows Win32 | ARM64 | - |
| [`dev`](https://github.com/microsoft/MixedReality-WebRTC/tree/dev) | [m71](https://groups.google.com/forum/#!msg/discuss-webrtc/HUpIxlDlkSE/qR1nswqZCwAJ) | Windows UWP | x86 | - |
| [`dev`](https://github.com/microsoft/MixedReality-WebRTC/tree/dev) | [m71](https://groups.google.com/forum/#!msg/discuss-webrtc/HUpIxlDlkSE/qR1nswqZCwAJ) | Windows UWP | x86_64 | - |
| [`dev`](https://github.com/microsoft/MixedReality-WebRTC/tree/dev) | [m71](https://groups.google.com/forum/#!msg/discuss-webrtc/HUpIxlDlkSE/qR1nswqZCwAJ) | Windows UWP | ARM | - |
| [`dev`](https://github.com/microsoft/MixedReality-WebRTC/tree/dev) | [m71](https://groups.google.com/forum/#!msg/discuss-webrtc/HUpIxlDlkSE/qR1nswqZCwAJ) | Windows UWP | ARM64 | - |

## Getting Started

MixedReality-WebRTC is currently under development, and precompiled packages are not yet available. See the _Building MixedReality-WebRTC_ section below for compiling the libraries from sources.

This repository follows the [Pitchfork Layout](https://api.csswg.org/bikeshed/?force=1&url=https://raw.githubusercontent.com/vector-of-bool/pitchfork/develop/data/spec.bs) in an attempt to standardize its hierarchy:

```
bin/               Binary outputs (generated)
docs/              Documentation
examples/          Examples apps
external/          Third-party external dependencies
libs/              Source code for the component libraries
tests/             Source code for unit tests
tools/             Utility scripts
```

## Required Softwares

The following softwares are officially supported. Other versions might work, but were not tested.

| | | |
| :--- | :--- | :--- |
| [![Windows SDK 18362+](docs/MRTK170802_Short_17.png)](https://developer.microsoft.com/en-US/windows/downloads/windows-10-sdk) | [Windows SDK 18362+](https://developer.microsoft.com/en-US/windows/downloads/windows-10-sdk) | To develop apps for Windows Mixed Reality headsets, you need the Windows 10 Fall Creators Update |
| [![Visual Studio 2019](docs/MRTK170802_Short_19.png)](http://dev.windows.com/downloads) | [Visual Studio 2019](http://dev.windows.com/downloads) | Visual Studio is used for code editing, and deploying and building UWP app packages |
| [![Unity](docs/MRTK170802_Short_18.png)](https://unity3d.com/get-unity/download/archive) | [Unity 2019.1.x](https://unity3d.com/get-unity/download/archive) | The Unity 3D engine provides support for building mixed reality projects in Windows 10, and version 2019.1 adds support for UWP ARM64 for HoloLens 2 |

## Documentation

The API documentation is not available yet.

The overall architecture of the components is as follow:

![MixedReality-WebRTC architecture](docs/architecture.png)

## Building MixedReality-WebRTC

### Check out the MixedReality-WebRTC repository

```
git clone https://github.com/microsoft/MixedReality-WebRTC.git
```
 
### Check out the external dependencies

This may take some time (> 5 minutes) due to the large number of submodules in [the WebRTC UWP SDK repository](https://github.com/webrtc-uwp/webrtc-uwp-sdk) this repository depends on.

```
cd external\webrtc-uwp-sdk
git submodule update --init --recursive
```

### Build the WebRTC UWP SDK libraries:

For Win32 (Desktop) support:

- Open the **`WebRtc.Win32.sln`** Visual Studio solution located in `external\webrtc-uwp-sdk\webrtc\windows\solution\`
- In the menu bar, select the relevant solution platform and solution configuration. For the Unity editor, the **x64** binaries are required.
- Build the solution with F7 or **Build > Build Solution**

For UWP support:

- Open the **`WebRtc.Universal.sln`** Visual Studio solution located in `external\webrtc-uwp-sdk\webrtc\windows\solution\`
- In the menu bar, select the relevant solution platform and solution configuration. For HoloLens, the **x86** binaries are required. For HoloLens 2, the **ARM** binaries are required (ARM64 is not supported yet).
- Build the solution with F7 or **Build > Build Solution**

### Build the MixedReality-WebRTC libraries

- Open the `Microsoft.MixedReality.WebRTC.sln` Visual Studio solution located at the root of the repository.
- Build the solution with F7 or **Build > Build Solution**

On successful build, the binaries will be generated in a sub-directory under `bin/`, and the relevant DLLs will be copied by a post-build script to `libs\Microsoft.MixedReality.WebRTC.Unity\Assets\Plugins\` for Unity to consume them.

The `Microsoft.MixedReality.WebRTC.sln` Visual Studio solution contains several projects:
- The native C++ library, which can be compiled:
  - for Win32 (Desktop) with the `Microsoft.MixedReality.WebRTC.Native.Win32` project
  - for UWP with the `Microsoft.MixedReality.WebRTC.Native.UWP` project
- The C# library project `Microsoft.MixedReality.WebRTC`
- A C# unit tests project `Microsoft.MixedReality.WebRTC.Tests`
- A UWP C# sample app project `Microsoft.MixedReality.WebRTC.TestAppUWP` based on WPF and XAML

### Optionally test the install

Test the install by _e.g._ opening the Unity project at `libs\Microsoft.MixedReality.WebRTC.Unity`, loading the `Assets\Microsoft.MixedReality.WebRTC/Unity.Examples\SimpleVideoChat` scene and pressing Play. After a few seconds the left media player should display the video feed from the local webcam. The Unity console should also display a message about the WebRTC library being initialized successfully.

## Known Issues

The current version is a public preview under active development, which contains known issues being addressed:

- Mixed Reality Capture (MRC) currently does not work on HoloLens 2. Enabling MRC silently fails, and falls back to a video stream without hologram rendering.
- HoloLens 2 exhibits performance issues due to:
  - The [missing support (#157)](https://github.com/webrtc-uwp/webrtc-uwp-sdk/issues/157) for SIMD-accelerated YUV conversion in WebRTC UWP SDK.
  - The use of the highest available video resolution when opening the webcam with the default video profile. Support for selecting a different video profile is not available yet in WebRTC UWP SDK.
  - The use by default of the VP8 video codec, which is fairly CPU intensive.
- Some less powerful devices (_e.g._ laptop) may exhibit performance issues similar to HoloLens 2 when using multiple video tracks, for example one local and one remote (video chat), due to the same CPU intensive VP8 codec. This problem is amplified if audio is also used at the same time, which can be CPU intensive too.

## Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us the rights to use your contribution. For details, visit https://cla.microsoft.com.

When you submit a pull request, a CLA-bot will automatically determine whether you need to provide a CLA and decorate the PR appropriately (e.g., label, comment). Simply follow the instructions provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## Reporting security issues and bugs

MixedReality-WebRTC builds upon the WebRTC implementation provided by Google. Security issues and bugs related to this implementation should be reported to Google.

Security issues and bugs related to MixedReality-WebRTC itself or to WebRTC UWP SDK should be reported privately, via email, to the Microsoft Security Response Center (MSRC) secure@microsoft.com. You should receive a response within 24 hours. If for some reason you do not, please follow up via email to ensure we received your original message. Further information, including the MSRC PGP key, can be found in the [Security TechCenter](https://technet.microsoft.com/en-us/security/ff852094.aspx).
