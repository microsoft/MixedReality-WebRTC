# Building the C# library

The MixedReality-WebRTC C# library is a platform-independent .NET Standard 2.0 library which relies on a platform-specific version of the C library to provide its WebRTC implementation. The C# library is built on Windows from the `Microsoft.MixedReality.WebRTC.sln` Visual Studio solution located at the root of the git repository. Building from a non-Windows environment is not supported.

This documentation assumes that the user has already built the native C/C++ library on Windows, and therefore has installed [its prerequisites](building-windows.md#prerequisites) and already cloned the git repository of MixedReality-WebRTC. If you only built the Android archive `mrwebrtc.aar` on a different Linux machine then you should install those prerequisites first on the current Windows machine.

> [!NOTE]
> If you have already built the entire solution for the native C/C++ library on Windows, then the C# library is already built. The steps below are mainly for Android-only users, although it is strongly recommended to build the Windows Desktop x64 configuration anyway to be able to run MixedReality-WebRTC in Play Mode in the Unity editor.

## Building the library

1. Open the `Microsoft.MixedReality.WebRTC.sln` Visual Studio solution located at the root of the cloned git repository.

2. Select a build configuration (Debug or Release) with the Solution Configuration drop-down widget under the menu bar. The architecture selection is irrelevant here; it is only relevant when building the native C library.

3. Build the `Microsoft.MixedReality.WebRTC` C# project by right-clicking on it and selecting the **Build** menu entry.

On successful build, the C# assembly will be generated in a sub-folder of `bin/netstandard2.0`, and the DLL will also be copied by a post-build script to `libs\unity\library\Runtime\Plugins\` for the Unity library to consume it.

> [!IMPORTANT]
> **Be sure to build the solution before opening any Unity library project.** As part of the build, the library is copied to the `Plugins` folder of the Unity library. There are already some associated `.meta` files, which have been committed to the git repository, to inform Unity of the platform of each DLL and how to deploy it. If the Unity project is opened first, before the DLLs are present, Unity will assume those `.meta` files are stale and will delete them, and then later will recreate some with a different default config once the DLLs are copied. This leads to errors about modules with duplicate names. See the [Building from Sources (Windows)](building-windows.md) page for more details.

## Testing the build

Test the newly built C# library by _e.g._ using the `TestAppUWP` sample application:

1. Build the `Microsoft.MixedReality.WebRTC.TestAppUWP` C# project from the same Visual Studio solution.

2. Run it by right-clicking on the project and selecting **Debug** > **Start New Instance** (or F5 if the project is configured as the Startup Project).

See the [Hello, C# World! (UWP)](cs/helloworld-cs-uwp.md) tutorial for more details.

## Installing into an existing C# project

The C# library requires the C library, which contains the core WebRTC implementation. The setup is summarized in the following table:

| Source DLLs | How to add |
|---|---|
| `bin\netstandard2.0\Release\Microsoft.MixedReality.WebRTC.dll` | Include in "References" of your VS project |
| `bin\<platform>\<arch>\Release\mrwebrtc.dll` | Add as "Content" to the project, so that the Deploy step copies the DLL to the AppX folder alongside the application executable. See the [TestAppUWP project](https://github.com/microsoft/MixedReality-WebRTC/blob/d78ffa488fbf822377558ce44bbfa8316f0f85f7/examples/TestAppUwp/Microsoft.MixedReality.WebRTC.TestAppUWP.csproj#L74-L83) for an example, noting how it uses the `$(Platform)` and `$(Configuration)` Visual Studio variables to automatically copy the right DLL corresponding to the currently selected project configuration. |

where:

- `<platform>` is either `Win32` for a Desktop app, or `UWP` for a UWP app.
- `<arch>` is one of [`x86`, `x64`, `ARM`]. Note that `ARM` is only available on UWP.
