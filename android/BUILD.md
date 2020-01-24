# How to build WebRTC for Android

"Getting started with WebRTC source code is no easy walk in the park." -- Abraham Lincoln

## Setup Linux environment

The webrtc native library must be built from a Linux environment.

> RESEARCH is this still true?

0. Ensure your working drive has at least 30GB free.

1. On a Linux machine, open a bash shell.

    If working in WSL:

    WARNING -- you're better off using a real Linux instance than WSL. If you still want to try using WSL:

    You must use WSL2, available in Windows 10 builds 18917 or higher. WSL1 has a fatal bug relating to memory-mapped files. There is no workaround.

    WSL2 installation instructions: https://docs.microsoft.com/en-us/windows/wsl/wsl2-install

    If building on a Windows-accessible drive (USB or main disk), ensure the drive is mounted with "-o metadata", e.g.:

    `$> sudo mount -t drvfs D: /mnt/d -o metadata`
  
    This enables support for Linux-style file permissions (chown, chmod, etc).

    The filesystem must be case sensitive. There are three ways to set this flag. I recommend trying all three. The first: specify it in your `/etc/wsl.conf` file:

    ```
    sudo nano /etc/wsl.conf
    ## file contents should include this:
    [automount]
    options = "metadata,case=force"
    ```

    The conf file approach didn't work for me, but is the recommended solution on the web. Remaining two options:

    1. From the windows filesystem, use `fsutil.exe file setCaseSensitiveInfo`

    2. From the Linux side, try `setfattr -n system.wsl_case_sensitive -v 1 <path>` (but this also fails for me with the error "Operation not supported")

    I recommend trying ALL of these. One might stick.

2. Install prerequisite software packages

    `$> sudo apt-get install clang`

3. Make a folder where you'll clone the repo and cd to it.

## Install Chromium dev tools

> TODO Write a script to do handle everything from here down

_Overview of Chromium development for Android here: https://chromium.googlesource.com/chromium/src/+/master/docs/android_build_instructions.md_

### Clone the `depot_tools` repo
In your working directory:
```
git clone https://chromium.googlesource.com/chromium/tools/depot_tools.git
```

### Add `depot_tools` to your path
You may want to add this to your .bashrc. Be sure to replace `/path/to/depot_tools` with the actual path in your environment.
```
export PATH="$PATH:/path/to/depot_tools"
```

## Get the `webrtc` code, with Android dependencies

This will take a very long time.
```
mkdir webrtc && cd webrtc
fetch --nohooks --force webrtc_android
```

### Checkout the correct code revision (25118 is branch-heads/71)

> TODO Write a script to get the revision # from branch-head name.

```
gclient sync --nohooks --force --revision 25118
```

## Build the project

> RESEARCH Do we need to pass extra args to build_aar.py? `use_rtti=true`?

```
cd src
python tools_webrtc/android/build_aar.py --arch arm64-v8a --verbose --build-dir ./out/aar
```

## Copy build outputs
> TODO document where to find these, and where to copy them to.

* libwebrtc.aar
* libwebrtc_full.a
* headers


---
PREVIOUS INSTRUCTIONS


## Clone the `webrtcbuilds` repo

Really nice GitHub user `vsimon` wrote a wrapper script that makes building the webrtc library *slightly* less of a nightmare than building it directly from google's repo.

1. Clone the Altspace fork of this nice person's project: https://github.com/AltspaceVR/webrtcbuilds.git


2. cd to the `webrtcbuilds` folder.


### Edit the build script

1. Edit `util.sh`, line 274, to add the `use_rtti=true` build flag:

    ```
    local common_args="is_component_build=false rtc_include_tests=false treat_warnings_as_errors=false use_rtti=true"
    ```

    This enables cross-compiling for the `arm` architecture.

2. Edit `util.sh` to copy `third_party` headers.

    Modify line 345 to copy `third_party` headers, but still exclude `third_party/android_tools`:
    - 345: `find $headersSourceDir -path './third_party/android_tools*' -prune -o -name '*.h' -exec $CP --parents '{}' $headersDestDir ';'`

    This will cause the header copy operation take a long time. This could be improved by adding additional `-path './third_party/path/to/exclude*' -prune -o` terms.

3. Edit `util.sh` to disable zipping the output folder.

    The output folder with includes is massive in size, and we don't need it zipped anyway, so disable this step:
    - Line 375: Comment out the contents of the function `package::zip()`

### Sync the source and build the library

1. Run the build, 1st attempt.

    `$> ./build.sh -b branch-heads/71 -t android -c arm64 -n Release -e`

    #### Explanation of arguments

    `-b branch-heads/71`: This must match the Chromium release Microsoft.MixedReality.WebRTC depends on, currently that is M71.

    `-t android`: target OS

    `-c arm64`: target CPU

    `-n Release`: build configuration

    `-e`: Build with RTTI

    The sync+build will run for 5+ hours and then fail. Next steps will get it fixed.

2. Edit various BUILD.gn errors relating to RTTI as they're reported. Examples:

    - Issue: `ERROR at //third_party/icu/BUILD.gn:604:5: Item not found
    "//build/config/compiler:no_rtti",  # ICU uses RTTI.`

    - Solution: Comment out the offending line.

    - Issue: `ERROR at //third_party/icu/BUILD.gn:608:5: Duplicate item in list
    "//build/config/compiler:rtti",`

    - Solution: Comment out the offending line.

3. Edit `out/src/webrtc.gni`:

    - Set `rtc_build_examples = false`
    - Set `rtc_build_tools = false`
    - Set `rtc_use_x11 = false`
    - Set `rtc_enable_protobuf = false`

> * TODO: Move these settings to `args.gni`

4. Re-run the build. It should succeed this time.

5. Find the build output at `out/webrtcbuilds-<build-identifier>`.
    - Copy the build output to the MixedReality-WebRTC project:
        - Copy the `include` folder to `MixedReality-WebRTC/android/deps/webrtc`, overwriting if necessary.
        - Copy `lib/Release/libwebrtc_full.a` to `MixedReality-WebRTC/android/deps/webrtc`, overwriting if necessary.

6. Build `libwebrtc.aar`.
    - cd to `out/src`
    - Run `python tools_webrtc/android/build_aar.py --arch arm64-v8a --verbose --build-dir ./out/aar`
    - Wait for that to finish.
    - cd to `out/aar`
    - Copy `libwebrtc.aar` to the MixedReality-WebRTC project:
        - Copy `libwebrtc.aar` to `MixedReality-WebRTC/android/libwebrtc`, overwriting if necessary.

## Microsoft.MixedReality.WebRTC - Android build

1. Open the project in Android Studio.
    - Open the `MixedReality-WebRTC/android` folder in Android Studio.
    - ...


