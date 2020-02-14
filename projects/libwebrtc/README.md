# Building the Chromium WebRTC base library (libwebrtc)

Chromium's libwebrtc library underpins MixedReality-WebRTC. Here are the instructions to build it.

1. A bash shell is required to build libwebrtc.
    * **Linux, Android, Mac, and iOS**: See ENV-UNIX.md for instructions to configure your Unix build environment.
    * **Windows, UWP, Hololens**: See ENV-WIN.md for instructions to build on Windows (TBD).

2. Decide where you want Chromium's Depot Tools and the WebRTC repos to be cloned. This location can be outside this folder structure, even on another drive. Ensure the drive has at least 30GB free.

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

        Example:
        ```
        $ ./config.sh -d /mnt/d/build_webrtc -b branch-heads/71 -t android -c arm64
        ```

    2. Run the checkout script: `./checkout.sh`. This command clones the Chromium Depot Tools and the WebRTC repo. Warning: this can take a long time and consume significant disk space (plan for 2+ hours and 40GB disk space).

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

    3. Run the build script: `./build.sh [options]`. This command builds the Chromium WebRTC base library (libwebrtc). It also writes the file `.libwebrtc.cmake`, containing configuration needed for CMake-based builds (just Android currently).

        ```
        Usage:
            ./build.sh [OPTIONS]

        Builds the Chromium WebRTC component (libwebrtc).

        OPTIONS:
            -h              Show this message.
            -v              Verbose mode. Print all executed commands.
            -c BUILD_CONFIG Build configuration. Default is 'Release'. Possible values are: 'Debug', 'Release'.
        ```

        Example:
        ```
        $ ./build.sh Release
        ```

    4. Run the copy script: `./copy.sh`. This command copies libwebrtc build artifacts to Unity sample scene.

        ```
        Usage:
            ./copy.sh [OPTIONS]

        Copies Chromium's WebRTC build artifacts to the Unity sample.

        OPTIONS:
            -h              Show this message.
            -v              Verbose mode. Print all executed commands.
        ```

        Example:
        ```
        $ ./copy.sh
        ```

4. Next Step: Build MixedReality-WebRTC.
    - Instructions for Android [here](../android/README.md).
