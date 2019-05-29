# WebRTC native binding for MRTK

This project `Microsoft.MixedReality.Toolkit.Networking.WebRtc.Native` wraps the native C++ implementation of WebRTC from Google and exposes a C style API to be consumed by the various MRTK wrappers implementing the MRTK networking interfaces. 

# Using

This library depends on the [WebRTC UWP SDK](https://github.com/webrtc-uwp/webrtc-uwp-sdk/tree/releases/m71) to provide both the native WebRTC implementation from Google, as well as a set of UWP wrappers on top of it.

The WebRTC UWP SDK repository is very large, and compiles into a somewhat large library too (`webrtc.lib` is over 500 MB), which makes it impractical at this time to take a full source dependency on. Instead, the binary DLL output of this project is directly checked in the repository for convenience, so it can be consumed by the various MRTK wrappers.

If you need to recompile this library, see the building instructions.

# Building

Building the `Microsoft.MixedReality.Toolkit.Networking.WebRtc.Native` library involves checking out a local copy of the [WebRTC UWP SDK](https://github.com/webrtc-uwp/webrtc-uwp-sdk/tree/releases/m71) and building it, then set up the various header and library paths inside the `vcxproj`.

## Build steps

**1. Clone the `webrtc-uwp-sdk` git repository**

See prerequisites and detailed instructions [from GitHub](https://github.com/webrtc-uwp/webrtc-uwp-sdk).

```sh
git clone --recursive https://github.com/webrtc-uwp/webrtc-uwp-sdk.git
```

**2. Compile the `webrtc.lib` library**

For UWP, this means compiling the `WebRtc.UWP.Native.Builder` project from the solution found in `webrtc-uwp-sdk/webrtc/windows/solutions/`.

**3. Point the native DLL paths to the existing repo clone**

Open the Visual Studio property sheet `project.props` located at the root of the `Microsoft.MixedReality.Toolkit.Networking.WebRtc.Native` project and edit the value of the `$(WebRTCCoreRepoPath)` MSBuild property to the folder where the WebRTC UWP SDK git repository was cloned, for example:

```xml
<WebRTCCoreRepoPath>C:\dev\webrtc-uwp-sdk\</WebRTCCoreRepoPath>
```

This will allow the various header and library paths to be found relative to it.

## Troubleshooting

### Unresolved type `mrtk_net_webrtc_Enumerator`

The library uses `clang-format` to automatically format the code. The rules are based on the official WebRTC repository from Google. These rules include some sorting of the headers blocks (set of consecutive `#include` directives) which can reorder the precompiled header (PCH) after any other C++ header, leading to compilation errors.

To fix this issue, ensure that `pch.h` is the first header included, and that it is alone in its own header block, that is it is followed by an empty line.

```cpp
#include <pch.h>
// blank line, or anything that is not a #include
#include "other.h"
```

`mrtk_net_webrtc_Enumerator` simply happens to be the first type in `api.h`, but is otherwise irrelevant to this issue.
