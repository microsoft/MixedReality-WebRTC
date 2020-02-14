# Build MixedReality-WebRTC for Android

## Prerequisites

1. Build the Chromium WebRTC base library. Instructions [here](../libwebrtc/README.md).

2. Install Android Studio (https://developer.android.com/studio/index.html). 

> TODO: Provide command line build instructions. Android Studio should not be a requirement.

## Build MixedReality-WebRTC

1. Open this folder in Android Studio.

2. Build the project.

3. Artifacts produced by the build:

    * `./webrtc-native/build/outputs/Microsoft.MixedReality.WebRTC.Native.aar`

4. Open a bash shell to the folder containing this README.

5. Run `./copy.sh` to copy the build artifacts to the Unity sample project.
