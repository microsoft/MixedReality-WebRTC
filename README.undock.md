# Experimental undock branch

## Checkout

Clone the MixedReality-WebRTC repository recursively to checkout all submodules.

```cmd
git clone https://github.com/microsoft/MixedReality-WebRTC.git --recursive -b experimental/undock mr-webrtc
```

## Build

_TODO: Merge Android instructions here._

On Windows building involves two steps:

- Compile the underlying `libwebrtc` from Google, which provides the internal WebRTC implementation. On UWP, this implementation is augmented with a set of patches from the [WinRTC](https://github.com/microsoft/winrtc) project, as the Google repository does not provide UWP support.
- Compile the MixedReality-WebRTC libraries from the provided Visual Studio solution.

### Google WebRTC library

The Google WebRTC library (`libwebrtc`) can be easily compiled by running the `tools/build/build.py` build script:

```sh
cd tools/build/

# Windows Desktop (Win32) x64 Debug
python build.py --target=win --cpu=x64 --debug

# Windows UWP ARM64 Release
python build.py --target=winuwp --cpu=arm64

# etc.
# See `build.py --help` for more details
```

This will checkout the Google repository in `external/libwebrtc`, apply any patch if necessary (UWP), and build the given configuration.

The various configurations of `webrtc.lib` are available in `external/libwebrtc/src/out/<target>/<cpu>/<config>/obj/`, where:

- `<target>` is the target platform, either `win` or `winuwp` (`-t` option of `build.py`)
- `<cpu>` is the cpu architecture, one of `x86`, `x64`, `arm`, `arm64` (`-c` option of `build.py`)
- `<config>` is the build configuration, either `debug` or `release` (`-d` option of `build.py` or nothing, respectively)

### MixedReality-WebRTC libraries

- Open the Visual Studio Solution at the root of the MixedReality-WebRTC repository.
- Build the solution for the needed platform and architecture configuration(s).
