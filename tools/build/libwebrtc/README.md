# Building the Chromium WebRTC base library (libwebrtc)

Chromium's `libwebrtc` library underpins MixedReality-WebRTC. Here are the instructions to build it for the various supported platforms.

## Windows Desktop and UWP

1. [Powershell 5.1](https://docs.microsoft.com/en-us/powershell/scripting/install/installing-windows-powershell?view=powershell-5.1) is required to build libwebrtc. It is installed by default with Windows 10 and Windows Server 2016.

2. Open a Powershell console and change directory to the folder containing this README.

3. On first use only, run the checkout script `checkout.ps1`.

   ```powershell
   PS> .\checkout.ps1
   ```

   This command automatically:
   - Downloads the Google repository of libwebrtc inside `external/libwebrtc`
   - Checks out the M80 branch
   - Applies the UWP patches from [WinRTC](https://github.com/microsoft/winrtc) to allow building on UWP
   - Synchronizes dependencies (`gclient sync`)
   - Makes sure Python is installed, and install the `pywin32` module

4. For each build variant needed, run the build script `build.ps1` with the appropriate arguments. This command automatically does all steps necessary to build `webrtc.lib` from the existing checkout performed in step 3. above:
   - Create the output folder in `external/libwebrtc/src/out/[win|winuwp]/[x86|x64|arm64]/[debug|release]`
   - Generate the `args.gn` file for `gn`
   - Run `gn gen` if needed to generate the Ninja build files
   - Run `ninja` to build libwebrtc

   The syntax of the command is:

   ```powershell
   PS> .\build.ps1 -BuildPlatform [BuildPlatform] -BuildArch [BuildArch] -BuildConfig [BuildConfig]
   ```

   where:
   - `[BuildPlatform]` is `Win32` or `UWP`
   - `[BuildArch]` is one of `x86`, `x64`, or `ARM64`
   - `[BuildConfig]` is `Debug` or `Release`

   Example:

   ```powershell
   PS> .\build.ps1 -BuildPlatform Win32 -BuildArch x64 -BuildConfig Debug
   ```

5. Next Step: Build MixedReality-WebRTC itself.
    - Open the `Microsoft.MixedReality.WebRTC.sln` Visual Studio solution located at the root of the MixedReality-WebRTC repository, select the desired build variant, and build the solution.

## Android

1. A bash shell is required to build libwebrtc. See `ENV-UNIX.md` for instructions to configure your Unix build environment.

2. Decide where you want Chromium's Depot Tools and the WebRTC repos to be cloned. This location can be outside this folder structure, even on another drive. Ensure the drive has at least 40GB free.

3. Open a bash shell and change directory to the folder containing this README, then do the following:

    1. Run the configuration script: `./config.sh [options]`. This command writes a config file used by subsequent scripts.

        ```shell
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

        ```shell
        $ ./config.sh -d /mnt/d/build_webrtc -b branch-heads/71 -t android -c arm64
        ```

    2. Run the checkout script: `./checkout.sh`. This command clones the Chromium Depot Tools and the WebRTC repo. NOTE: this can take a long time and consume significant disk space (plan for 2+ hours and 40GB disk space). You may be prompted to accept third-party license agreements.

        ```shell
        Usage:
            ./checkout.sh [OPTIONS]

        Setup Chromium's Depot Tools and clone WebRTC repo.

        OPTIONS:
            -h              Show this message.
            -v              Verbose mode. Print all executed commands.
        ```

        Example:

        ```shell
        $ ./checkout.sh
        ```

    3. Run the build script: `./build.sh [options]`. This command builds the Chromium WebRTC base library (libwebrtc). It also writes the file `.libwebrtc.cmake` which contains configuration needed for subsequent CMake-based builds. This command also copies libwebrtc build artifacts to the Unity sample project.

        ```shell
        Usage:
            ./build.sh [OPTIONS]

        Builds the Chromium WebRTC component (libwebrtc).

        OPTIONS:
            -h              Show this message.
            -v              Verbose mode. Print all executed commands.
            -c BUILD_CONFIG Build configuration. Default is 'Release'. Possible values are: 'Debug', 'Release'.
        ```

        Example - Make a Release build:

        ```shell
        $ ./build.sh -c Release
        ```

4. Next Step: Build MixedReality-WebRTC itself.
    - Instructions for Android [here](../android/README.md).
