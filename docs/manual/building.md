# Building from sources

The MixedReality-WebRTC libraries are built from the `Microsoft.MixedReality.WebRTC.sln` Visual Studio solution located at the root of the repository.

If you want to start right away with MixedReality-WebRTC, the recommended approach is to consume the precompiled binaries distributed as NuGet packages instead. See [Download](download.md) for details.

## Prerequisites

### Environment and tooling

- The NuGet packages for the input dependencies (see _Core input dependencies_ below) require in total approximately **10 GB of disk space**. Those dependencies contain unstripped `.lib` files much larger than the final compiled DLL libraries, for both the Debug and Release build configurations.

- Due to Windows paths length limit, it is recommended to **clone the source repository close to the root** of the filesystem, _e.g._ `C:\mr-webrtc\` or similar, as the recursive external dependencies create a deep hierarchy which may otherwise produce paths beyond the OS limit and result in build failure.

- The solution uses **Visual Studio 2019** with the following features:

  - The **MSVC v141 - VS 2017 C++ x64/x86 build tools** toolchain from Visual Studio **2017** is required to build the C++17 library of MixedReality-WebRTC. This will eventually be replaced with the Visual Studio **2019** compiler (v142 toolchain) once the project is upgraded to a Google milestone supporting Visual Studio 2019 (see details on [issue #14](https://github.com/microsoft/MixedReality-WebRTC/issues/14))

  - For ARM support, the **MSVC v141 - VS 2017 C++ ARM build tools** toolchain is also required.

  - The C# library requires a .NET Standard 2.0 compiler, like the Roslyn compiler available as part of Visual Studio when installing the **.NET desktop development** workload.

  - The UWP libraries and projects require UWP support from the compiler, available as part of Visual Studio when installing the **Universal Windows Platform development** workload.

- The Unity integration is officially supported for [Unity](https://unity3d.com/get-unity/download) version **2018.4.x** (LTS). However, although the 2018.4 LTS version is currently the only officially supported version, we do our best to keep things working on 2019+ versions too. Versions earlier than 2018.4.x may work but are not tested at all, and no support will be provided for those.

### Core input dependencies

The so-called _Core_ input dependencies are constituted of:

- `webrtc.lib` : A static library containing the Google implementation of the WebRTC standard.
- `Org.WebRtc.winmd` : A set of WinRT wrappers for accessing the WebRTC API from UWP.

Those dependencies require many extra prerequisites. They are also complex and time-consuming to build. Therefore to save time and avoid headache the MixedReality-WebRTC solution consumes those dependencies as precompiled NuGet packages [available from nuget.org](https://www.nuget.org/packages?q=Microsoft.MixedReality.WebRTC.Native.Core). Those NuGet packages are compiled from [the WebRTC UWP project](https://github.com/webrtc-uwp/webrtc-uwp-sdk) maintained by Microsoft. So **there is no extra setup for those**.

## Cloning the repository

The official repository containing the source code of MixedReality-WebRTC is [hosted on GitHub](https://github.com/microsoft/MixedReality-WebRTC). The latest developments are done on the `master` branch, while the latest stable release is a `release/*` branch.

Clone the choosen branch of the repository and its dependencies recursively, preferably close to the root of the filesystem (see prerequisites):

```cmd
git clone --recursive https://github.com/microsoft/MixedReality-WebRTC.git -b <branch_name> C:\mr-webrtc
```

Note that **this may take some time (> 5 minutes)** due to the large number of submodules in [the WebRTC UWP SDK repository](https://github.com/webrtc-uwp/webrtc-uwp-sdk) this repository depends on.

## Building the libraries

1. Open the `Microsoft.MixedReality.WebRTC.sln` Visual Studio solution located at the root of the freshly cloned repository.

2. Build the solution with F7 or **Build > Build Solution**

On successful build, the binaries will be generated in a sub-directory under `bin/`, and the relevant DLLs will be copied by a post-build script to `libs\Microsoft.MixedReality.WebRTC.Unity\Assets\Plugins\` for the Unity integration to consume them.

> [!IMPORTANT]
> **Be sure to build the solution before opening any Unity integration project.** As part of the build, the libraries are copied to the `Plugins` directory of the Unity integration project. There are already some associated `.meta` files, which have been committed to the repository, to inform Unity of the platform of each DLL. If the Unity project is opened first, before the DLLs are present, Unity will assume those `.meta` files are stale and will delete them, and then later will recreate some with a different default config once the DLLs are copied. This leads to errors about modules with duplicate names. See the [Importing MixedReality-WebRTC](https://microsoft.github.io/MixedReality-WebRTC/manual/helloworld-unity-importwebrtc.md) chapter of the "Hello, Unity World!" tutorial for more details.

## Testing the build

Test the newly built libraries by _e.g._ using the `VideoChatDemo` Unity integration sample:

1. Open the Unity project at `libs\Microsoft.MixedReality.WebRTC.Unity`.

2. Load the `Assets\Microsoft.MixedReality.WebRTC.Unity.Examples\VideoChatDemo` scene.

3. At the top center of the Unity editor window, press the **Play** button.

After a few seconds (depending on the host device) the left media player should display the video feed from the local webcam. The Unity console window should also display a message about the WebRTC library being initialized successfully. Note that **this does not test any remote connection**, but simply ensures that Unity can use the newly built C++ and C# libraries.

See the [Hello, Unity World!](https://microsoft.github.io/MixedReality-WebRTC/manual/helloworld-unity.html) tutorial for more details.

## Installing into an existing C# project

The C# library requires the C++ library, which contains the core WebRTC implementation. The setup is summarized in the following table:

| Source DLLs | How to add |
|---|---|
| `bin\netstandard2.0\Release\Microsoft.MixedReality.WebRTC.dll` | Include in "References" of your VS project |
| `bin\<platform>\<arch>\Release\mrwebrtc.dll` | Add as "Content" to the project, so that the Deploy step copies the DLL to the AppX folder alongside the application executable. See the [TestAppUWP project](https://github.com/microsoft/MixedReality-WebRTC/blob/d78ffa488fbf822377558ce44bbfa8316f0f85f7/examples/TestAppUwp/Microsoft.MixedReality.WebRTC.TestAppUWP.csproj#L74-L83) for an example, noting how it uses the `$(Platform)` and `$(Configuration)` Visual Studio variables to automatically copy the right DLL corresponding to the currently selected project configuration. |

where:

- `<platform>` is either `Win32` for a Desktop app, or `UWP` for a UWP app.
- `<arch>` is one of [`x86`, `x64`, `ARM`]. Note that `ARM` is only available on UWP.
