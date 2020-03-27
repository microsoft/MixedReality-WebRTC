# Build MixedReality-WebRTC for Android

## Prerequisites

1. Build the Chromium WebRTC base library. Instructions [here](../libwebrtc/README.md).

2. Install prerequisites:
    * Android Studio (https://developer.android.com/studio/index.html). Once installed, use the System Settings/Android SDK dialog to install the following packages:
        * SDKs 9.0 (Pie), 8.1 (Oreo)
        * CMake
        * NDK 16.1.4479499
    * JRE 1.8 (https://www.java.com/download/)

3. Add environment variable JAVA_HOME pointing to your JRE install directory. e.g.: `JAVA_HOME="C:\Program Files\Android\Android Studio\jre"`

## Build MixedReality-WebRTC

### Option 1: Android Studio

1. Open this folder in Android Studio.

2. Wait for Gradle initialization to complete.

3. Build the project: Build > Make Project.

### Option 2: Command line

1. Open a shell prompt to the folder containing this README.

2. Run `$./gradlew[.bat] assembleRelease`

### Artifacts produced by the build

* `./webrtc-native/build/outputs/aar/mrwebrtc.aar`

> Note: This file is automatically copied to the Unity sample project upon successful build.
