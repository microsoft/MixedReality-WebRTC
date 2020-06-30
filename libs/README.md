# Libraries

This folder contains the MixedReality-WebRTC libraries for C/C++, C#, and Unity.

## [`mrwebrtc/`](./mrwebrtc)

Native implementation library exposing a C API. Built with the Visual Studio solution located at the root of the repository (Windows), or the Android Studio project located in `tools/build/android` (Unity Android).

## [`Microsoft.MixedReality.WebRTC/`](./Microsoft.MixedReality.WebRTC)

C# library (.NET Standard 2.0) building upon `mrwebrtc` to provide integration into any C# project.

- [Documentation](https://microsoft.github.io/MixedReality-WebRTC/manual/cs/cs.html)
- Tutorials:
  - [Windows Desktop](https://microsoft.github.io/MixedReality-WebRTC/manual/cs/helloworld-cs-core3.html)
  - [UWP](https://microsoft.github.io/MixedReality-WebRTC/manual/cs/helloworld-cs-uwp.html)
- [API reference](https://microsoft.github.io/MixedReality-WebRTC/api/Microsoft.MixedReality.WebRTC.html)

## [`unity/`](./unity)

Unity packages (library and samples) providing an integration of the C# library via components and utilities.

- [Documentation](https://microsoft.github.io/MixedReality-WebRTC/manual/unity-integration.html)
- [Tutorial](https://microsoft.github.io/MixedReality-WebRTC/manual/helloworld-unity.html)
- [API reference](https://microsoft.github.io/MixedReality-WebRTC/api/Microsoft.MixedReality.WebRTC.Unity.html)
