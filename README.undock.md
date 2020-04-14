# Experimental undock branch

## Building

### Google WebRTC library

The Google WebRTC implementation does not currently have full built-in support UWP. Therefore we use the building tools developed by the [WinRTC](https://github.com/microsoft/winrtc) project to build the WebRTC library for HoloLens and other platforms.

#### Clone the WinRTC repository

```
git clone https://github.com/microsoft/winrtc.git -b user/LoadLibrary/Arm64 winrtc
```

The `user/LoadLibrary/Arm64` branch must be used. It contains the necessary patches to build WebRTC on UWP ARM64.

#### Build the WebRTC library

Follow the instructions at [winrtc/patches_for_WebRTC_org/m80/readme.md](https://github.com/microsoft/winrtc/blob/user/LoadLibrary/Arm64/patches_for_WebRTC_org/m80/readme.md) to checkout and patch the WebRTC repository. When setting up a build through `gn gen`:
- set `target_os="winuwp"` to build WebRTC for UWP apps, `="win"` to build for Windows Desktop;
- set `target_cpu` to the target platform (`x64`, `x86`, `arm64`, `arm`);
- set `is_debug` to `true` for Debug or `false` for Release;
- set up the build in this path: `out\$(Configuration)_$(Platform)[_win32]\`, for example `out\Debug_x64` (this particular shape for the folder name is not required for building WebRTC, but is used by the `Microsoft.MixedReality.Native` projects to find `webrtc.lib` when linking - see below).

Setup and build WebRTC for the configurations you need as indicated in the WinRTC documentation.

### MixedReality-WebRTC components

#### Clone the MixedReality-WebRTC repository

The MixedReality-WebRTC repository must be cloned beside the `src` folder of the WebRTC clone. E.g. if in the previous steps `gclient` was run in `C:\webrtc-m80`:

```cmd
cd C:\webrtc-m80
git clone https://github.com/microsoft/MixedReality-WebRTC.git -b experimental/undock mr-webrtc
```

#### Build

- Open the Visual Studio Solution at the root of the MixedReality-WebRTC repository.
- Build the solution for the needed configuration(s).
