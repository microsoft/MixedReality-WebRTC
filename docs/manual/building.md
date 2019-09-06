# Building from sources

The MixedReality-WebRTC libraries are built from the `Microsoft.MixedReality.WebRTC.sln` Visual Studio solution located at the root of the repository.

## Prerequisites

### Environment and tooling

- The NuGet packages for the input dependencies (see _Core input dependencies_ below) require in total approximately **10 GB of disk space**. Those dependencies contain unstripped `.lib` files much larger than the final compiled DLL libraries, for both the Debug and Release build configurations.

- Due to Windows paths length limit, it is recommended to **clone the source repository close to the root** of the filesystem, _e.g._ `C:\mr-webrtc\` or similar, as the recursive external dependencies create a deep hierarchy which may otherwise produce paths beyond the OS limit and result in build failure.

- The solution uses **Visual Studio 2019** with the following features:

  - The **MSVC v142 - VS 2019 C++ x64/x86 build tools** toolchain is required to build the C++17 library of MixedReality-WebRTC. This is installed by default with the **Desktop development with C++** workload on Visual Studio 2019.

  - For ARM support, the **MSVC v142 - VS 2019 C++ ARM build tools** toolchain is also required.

  - The C# library requires a .NET Standard 2.0 compiler, like the Roslyn compiler available as part of Visual Studio when installing the **.NET desktop development** workload.

  - The UWP libraries and projects require UWP support from the compiler, available as part of Visual Studio when installing the **Universal Windows Platform development** workload.

- The Unity integration has been tested on [Unity](https://unity3d.com/get-unity/download) **2018.3.x** and **2019.1.x**. Versions earlier than 2018.3.x may work but are not officially supported.

### Core input dependencies

The so-called _Core_ input dependencies are constituted of:

- `webrtc.lib` : A static library containing the Google implementation of the WebRTC standard.
- `Org.WebRtc.winmd` : A set of WinRT wrappers for accessing the WebRTC API from UWP.

Those dependencies require many extra prerequisites. They are also complex and time-consuming to build. Therefore to save time and avoid headache the MixedReality-WebRTC solution consumes those dependencies as precompiled NuGet packages [available from nuget.org](https://www.nuget.org/packages?q=Microsoft.MixedReality.WebRTC.Native.Core). Those NuGet packages are compiled from [the WebRTC UWP project](https://github.com/webrtc-uwp/webrtc-uwp-sdk) maintained by Microsoft. So **there is no extra setup for those**.

## Cloning the repository

The official repository containing the source code of MixedReality-WebRTC is [hosted on GitHub](https://github.com/microsoft/MixedReality-WebRTC). The latest developments are done on the `master` branch. Currently the project is in public preview, and there is no release, so this is the only branch of interest.

Clone the `master` branch of the repository and its dependencies recursively, preferably close to the root of the filesystem (see prerequisites):

```cmd
git clone --recursive https://github.com/microsoft/MixedReality-WebRTC.git -b master C:\mr-webrtc
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

2. Load the `Assets\Microsoft.MixedReality.WebRTC\Unity.Examples\VideoChatDemo` scene.

3. At the top center of the Unity editor window, press the **Play** button.

After a few seconds (depending on the host device) the left media player should display the video feed from the local webcam. The Unity console window should also display a message about the WebRTC library being initialized successfully. Note that **this does not test any remote connection**, but simply ensures that Unity can use the newly built C++ and C# libraries.

See the [Hello, Unity World!](https://microsoft.github.io/MixedReality-WebRTC/manual/helloworld-unity.html) tutorial for more details.
