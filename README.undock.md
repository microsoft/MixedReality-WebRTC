# Experimental undock branch

## Building

### Google WebRTC library

#### Setup `depot_tools`

```cmd
git clone https://chromium.googlesource.com/chromium/tools/depot_tools
cd depot_tools
set PATH=%CD%;%PATH%
cd ..
```

#### Clone the WebRTC repository

```cmd
set DEPOT_TOOLS_WIN_TOOLCHAIN=0
set GYP_MSVS_VERSION=2019
fetch --nohooks webrtc
cd webrtc
gclient sync
```

#### Tweak CRT

Currently WebRTC builds with static CRT. That doesn't work on UWP. To produce a build with dynamic CRT, it is necessary to tweak the `.gn` files and force the `static_crt` config:

- Edit `build\config\win\BUILD.gn`:
  - At the end of the `declare_args()` block, around line 43, add:
    ```
    # Use static CRT /MD(d) on Windows builds. Defaults to false, but is needed
    # as UWP doesn't support static CRT, only dynamic one.
    use_static_crt = false
    ```
  - In the `default_crt` config block, near line 462, modify the `if` block condition to read:
    ```
    if (!use_static_crt || current_os == "winuwp") {
      # [...]
      configs = [ ":dynamic_crt" ]
    } else {
      configs = [ ":static_crt" ]
    }
    ```

Note how the current code uses the `winuwp` OS variant to enable this. This is being removed though, as `winuwp` is being merged with `win`, so the condition needs to be replaced by another flag like the one we just added.

#### Build

In order to build `webrtc.lib`, run:

```cmd
cd src
gn args out\$(configuration)_$(cpu)[_win32]
```

Replace the build configuration and target cpu in the argument. Add `_win32` to build for Windows Desktop, leave empty to build for UWP. This particular shape for the folder name is not required for building WebRTC, but is used by the `Microsoft.MixedReality.Native` projects to find `webrtc.lib` when linking (see below).

For example, run `gn args out\debug_x64` to build for Debug UWP X64. This creates the `out\debug_x64\args.gn` file (GN configuration for the build) and opens a text editor to edit it:

```
target_os="winuwp"
is_clang=true
target_cpu="x64"
is_debug=true
rtc_include_tests=false
rtc_build_examples=false
rtc_win_video_capture_winrt=true
```

Close the editor. The `gn` process generates build files for `ninja` in `out\debug_x64`.

```cmd
ninja -C out\debug_x64
```

The library is in `out\debug_x64\obj\webrtc.lib`.

In order to build for a different target CPU, e.g. ARM64, repeat the process replacing the target name in the `target_cpu` line in args.gn:
```
target_cpu="arm64"
```

In order to build the Win32 version, replace the `target_os="winuwp"` line with:
```
target_os="win"
use_static_crt="false"
```

The release version can be built by setting `is_debug` to `false`.


### MixedReality-WebRTC Native/Core DLLs

Terminology fails us here because going forward we will split the C++ library ("native") from the low-level interop wrapper DLL, and link the latter statically with Google's libwebrtc ("core"). So there are discrepancies in naming. In particular the DLLs and VS projects are still named `Microsoft.MixedReality.WebRTC.Native.dll` even though they are unrelated with the C++ library (which doesn't exist in this branch). But "core" historically refers to the Google WebRTC + WebRTC UWP packaging, though we can probably reuse that term here.

#### Clone the MixedReality-WebRTC repository

```cmd
git clone https://github.com/microsoft/MixedReality-WebRTC.git -b experimental/undock mr-webrtc
```

#### Enable the ARM64 ClangCL toolchain

Follow the instructions in `libs/Microsoft.MixedReality.WebRTC.Native/install_vs_toolchains.md` to modify a local Visual Studio install and add experimental support for ClangCL toolchain on ARM64.

#### Build

- Open the Visual Studio Solution at the root of the repository.
- Build the 2 `*.Native.dll` projects.
  - Currently the UWP one will fail due to `webrtc.lib` using Desktop APIs.
  - The Desktop (Win32) one should succeed, and will copy its DLL to the Unity assets folder.
