# Building the Chromium WebRTC base library (libwebrtc)

Chromium's libwebrtc library underpins MixedReality-WebRTC. Here are the instructions to build it.

1. A bash shell is required to build libwebrtc.
    * **Linux, Android, Mac, and iOS**: See ENV-UNIX.md for instructions to configure your Unix build environment.
    * **Windows, UWP**: TBD - in development.

2. Decide where you want Chromium's Depot Tools and the WebRTC repos to be cloned. This location can be outside this folder structure, even on another drive. Ensure the drive has at least 40GB free.

3. Open a bash shell and change directory to the folder containing this README, then do the following:

    1. Run the configuration script: `./config.sh [options]`. This command writes a config file used by subsequent scripts.

        ```
        Usage:
            ./config.sh [OPTIONS]

        Sets configuration for building Chromium's WebRTC library.

        OPTIONS:
            -h              Show this message.
            -v              Verbose mode. Print all executed commands.
            -d WORK_DIR     Where to setup the Chromium Depot Tools and clone the WebRTC repo.
            -b BRANCH       The name of the Git branch to clone. E.g.: "branch-heads/71"
            -t TARGET_OS    Target OS for cross compilation. Default is 'android'. Possible values are: 'linux', 'mac', 'win', 'android', 'ios'.
            -c TARGET_CPU   Target CPU for cross compilation. Default is determined by TARGET_OS. For 'android', it is 'arm64'. Possible values are: 'x86', 'x64', 'arm64', 'arm'.
        ```

        Example - Set confuration to Android and WebRTC release M71:
        ```
        $ ./config.sh -d /mnt/d/build_webrtc -b branch-heads/71 -t android -c arm64
        ```

    2. Run the checkout script: `./checkout.sh`. This command clones the Chromium Depot Tools and the WebRTC repo. NOTE: this can take a long time and consume significant disk space (plan for 2+ hours and 40GB disk space). You may be prompted to accept third-party license agreements.

        ```
        Usage:
            ./checkout.sh [OPTIONS]

        Setup Chromium's Depot Tools and clone WebRTC repo.

        OPTIONS:
            -h              Show this message.
            -v              Verbose mode. Print all executed commands.
        ```

        Example:
        ```
        $ ./checkout.sh
        ```

    3. Run the build script: `./build.sh [options]`. This command builds the Chromium WebRTC base library (libwebrtc). It also writes the file `.libwebrtc.cmake` which contains configuration needed for subsequent CMake-based builds. This command also copies libwebrtc build artifacts to the Unity sample project.

        ```
        Usage:
            ./build.sh [OPTIONS]

        Builds the Chromium WebRTC component (libwebrtc).

        OPTIONS:
            -h              Show this message.
            -v              Verbose mode. Print all executed commands.
            -c BUILD_CONFIG Build configuration. Default is 'Release'. Possible values are: 'Debug', 'Release'.
        ```

        Example - Make a Release build:
        ```
        $ ./build.sh -c Release
        ```

4. Next Step: Build MixedReality.WebRTC.Native.
    - Instructions for Android [here](../android/README.md).
