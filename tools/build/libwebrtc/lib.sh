# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

#=============================================================================
# Library functions

#-----------------------------------------------------------------------------
# Check build host disk space is at least 40GB
function check-host-disk-space() {
    echo -e "\e[39mChecking host disk space...\e[39m"
    local AVAIL=$(df -B1 --output=avail $(pwd) | tail -1)
    if (( $AVAIL < 40000000000 )); then
        echo -e "\e[31mERROR: Insufficent disk space (need at least 40GB)\n" \
        "$(df -h $(pwd))\e[39m" >&2
        exit 1
    fi
}

#-----------------------------------------------------------------------------
# Check build host OS version (required by Google scripts)
function check-host-os() {
    echo -e "\e[39mChecking host OS...\e[39m"
    # Same check as Chrome's install-build-deps.sh
    # https://chromium.googlesource.com/chromium/src/+/f34485ffde/build/install-build-deps.sh
    # This is for m71; ideally we'd want to have a dynamic check for the
    # actual branch selected.
    if ! which lsb_release > /dev/null; then
        echo "\e[32mERROR: lsb_release not found in \$PATH\e[39m" >&2
        exit 1;
    fi
    distro_codename=$(lsb_release --codename --short)
    distro_id=$(lsb_release --id --short)
    # TODO - These are the checks for M71 only, later (master) also support more
    # recent Ubuntu releases like 19.04.
    supported_codenames="(trusty|xenial|artful|bionic)"
    supported_ids="(Debian)"
    if [[ ! $distro_codename =~ $supported_codenames &&
          ! $distro_id =~ $supported_ids ]]; then
        echo -e "\e[31mERROR: The only distros supported by the Google scripts for M71 are\n" \
        "\tUbuntu 14.04 LTS (trusty)\n" \
        "\tUbuntu 16.04 LTS (xenial)\n" \
        "\tUbuntu 17.10 (artful)\n" \
        "\tUbuntu 18.04 LTS (bionic)\n" \
        "\tDebian 8 (jessie) or later\n" \
        "Attempting to install anyway might fail, so aborting.\e[39m" >&2
        exit 1
    fi
}

#-----------------------------------------------------------------------------
function print-config() {
    echo -e "\e[39mTarget OS: \e[96m$TARGET_OS\e[39m"
    echo -e "\e[39mTarget CPU \e[96m$TARGET_CPU\e[39m"
    echo -e "\e[39mGit Branch: \e[96m$BRANCH\e[39m"
    echo -e "\e[39mWorking dir: \e[96m$WORK_DIR\e[39m"
    [[ "$FAST_CLONE" == "1" ]] && echo -e "\e[96mUsing fast clone for CI\e[39m" || true
}

#-----------------------------------------------------------------------------
function read-config() {
    if [ ! -f "$BUILD_DIR/.config.sh" ]; then
        echo -e "\e[39mWebRTC configuration not set.\e[39m"
        echo -e "\e[39mRun ./config.sh first.\e[39m"
        exit 1
    fi
    source "$BUILD_DIR/.config.sh"
    SRC_DIR=$WORK_DIR/webrtc
    # Print a config summary
    echo -e "\e[39m\e[1mActive config:\e[0m\e[39m"
    print-config
}

#-----------------------------------------------------------------------------
function write-config() {
    # Ensure WORK_DIR is an absolute path so that various operations after
    # config do not depend on the location where config was called.
    WORK_DIR=$(realpath $WORK_DIR)
    local filename="$BUILD_DIR/$1"
    cat >$filename <<EOF
# Generated file. Do not edit.
TARGET_OS=$TARGET_OS
TARGET_CPU=$TARGET_CPU
BRANCH=$BRANCH
WORK_DIR=$WORK_DIR
FAST_CLONE=$FAST_CLONE
EOF
}

#-----------------------------------------------------------------------------
function verify-checkout-config() {
    if [ ! -f "$BUILD_DIR/.config.sh" ]; then
        echo -e "\e[39mWebRTC configuration not set.\e[39m"
        echo -e "\e[39mRun ./config.sh first.\e[39m"
        exit 1
    fi
    if [ ! -f "$BUILD_DIR/.checkout.sh" ]; then
        echo -e "\e[39mWebRTC checkout configuration not set.\e[39m"
        echo -e "\e[39mRun ./checkout.sh first.\e[39m"
        exit 1
    fi
    local config_sh=$(cat "$BUILD_DIR/.config.sh")
    local checkout_sh=$(cat "$BUILD_DIR/.checkout.sh")
    if [ "$checkout_sh" != "$config_sh" ]; then
        echo -e "\e[39mWebRTC configuration has changed since last checkout.\e[39m"
        echo -e "\e[39mRun ./checkout.sh again.\e[39m"
        exit 1
    fi
}

#-----------------------------------------------------------------------------
function write-build-config() {
    local filename="$BUILD_DIR/.build.sh"
    cat >$filename <<EOF
# Generated file. Do not edit.
BUILD_CONFIG=$BUILD_CONFIG
EOF
}

#-----------------------------------------------------------------------------
function read-build-config() {
    if [ ! -f "$BUILD_DIR/.build.sh" ]; then
        echo -e "\e[39mWebRTC build configuration not set.\e[39m"
        echo -e "\e[39mRun ./build.sh first.\e[39m"
        exit 1
    fi
    source "$BUILD_DIR/.build.sh"
}

#-----------------------------------------------------------------------------
function detect-host-os() {
    case "$OSTYPE" in
    darwin*) HOST_OS=${HOST_OS:-mac} ;;
    linux*) HOST_OS=${HOST_OS:-linux} ;;
    win32* | msys*) HOST_OS=${HOST_OS:-win} ;;
    *)
        echo -e "\e[91mBuilding on unsupported OS: $OSTYPE\e[39m" && exit 1
        ;;
    esac
}

#-----------------------------------------------------------------------------
function verify-host-os() {
    if [[ $TARGET_OS == 'android' && $HOST_OS != 'linux' ]]; then
        # As per notes at http://webrtc.github.io/webrtc-org/native-code/android/
        echo -e "\e[91mERROR: Android compilation is only supported on Linux.\e[39m"
        exit 1
    fi
}

#-----------------------------------------------------------------------------
function create-WORK_DIR-and-cd() {
    # Ensure directory exists
    mkdir -p $WORK_DIR
    WORK_DIR=$(cd $WORK_DIR && pwd -P)
    # cd to directory
    cd $WORK_DIR
    # mkdir webrtc subdir
    mkdir -p $SRC_DIR
}

#-----------------------------------------------------------------------------
function verify-host-platform-deps() {
    # TODO check for required software packages, environment variables, etc.
    :
}

#-----------------------------------------------------------------------------
function checkout-depot-tools() {
    echo -e "\e[39mCloning depot tools -- this may take some time\e[39m"
    if [ ! -d $DEPOT_TOOLS_DIR ]; then
        git clone -q $DEPOT_TOOLS_URL $DEPOT_TOOLS_DIR
        if [ $HOST_OS = 'win' ]; then
            # run gclient.bat to get python
            pushd $DEPOT_TOOLS_DIR >/dev/null
            ./gclient.bat
            popd >/dev/null
        fi
    else
        pushd $DEPOT_TOOLS_DIR >/dev/null
        git reset --hard -q
        popd >/dev/null
    fi
}

#-----------------------------------------------------------------------------
function checkout-webrtc() {
    echo -e "\e[39mCloning WebRTC source -- this may take a long time\e[39m"
    pushd $SRC_DIR >/dev/null

    # Fetch only the first-time, otherwise sync.
    local extra_fetch=""
    [[ "$FAST_CLONE" == "1" ]] && extra_fetch+="--no-history" || true
    if [ ! -d src ]; then
        echo -e "\e[39mDoing first-time WebRTC clone -- this may take a long time\e[39m"
        case $TARGET_OS in
        android)
            yes | fetch --nohooks $extra_fetch webrtc_android
            ;;
        ios)
            fetch --nohooks $extra_fetch webrtc_ios
            ;;
        *)
            fetch --nohooks $extra_fetch webrtc
            ;;
        esac
    fi

    # Checkout the specific revision after fetch.
    echo -e "\e[39mSyncing WebRTC deps -- this may take a long time\e[39m"
    local extra_sync=""
    [[ "$FAST_CLONE" == "1" ]] && extra_sync+=" --no-history --shallow --nohooks" || true
    gclient sync --force --revision $REVISION $extra_sync

    # Run hooks on specific revision to e.g. download the prebuilt gn
    # This takes 3.5 GB of disk, and most of it is useless for the build
    # Leaving commented for reference in case the below cause issue, as
    # this is ideally the proper (and only supported) way.
    # Note also that this upgrades the Google Play SDK and therefore
    # requires accepting a license ('yes |'), but we don't want to blindly
    # accept instead of the user, and can't manually accept on CI.
    #yes | gclient runhooks

    # Alternative version with smaller disk footprint: run a selected
    # set of hooks manually

    # Download gn prebuilt executable
    download_from_google_storage --no_resume --platform=linux\* --no_auth --bucket chromium-gn -s src/buildtools/linux64/gn.sha1

    # Download clang prebuilt executable
    python src/tools/clang/scripts/update.py

    # Install sysroot
    python src/build/linux/sysroot_scripts/install-sysroot.py --arch=amd64

    # Create LASTCHANGE and LASTCHANGE.committime
    python src/build/util/lastchange.py -o src/build/util/LASTCHANGE

    popd >/dev/null
}

#-----------------------------------------------------------------------------
function latest-rev() {
    git ls-remote $REPO_URL HEAD | cut -f1
}

#-----------------------------------------------------------------------------
function calc-git-revision() {
    echo -e "\e[39mFetching Git revision\e[39m"
    if [ ! -z $BRANCH ]; then
        REVISION=$(git ls-remote $REPO_URL --heads $BRANCH | head -n1 | cut -f1) ||
            { echo -e "\e[91mCound not get branch revision for $BRANCH\e[39m" && exit 1; }
        [ -z $REVISION ] && echo "Cound not get branch revision for $BRANCH\e[39m" && exit 1
    else
        REVISION=${REVISION:-$(latest-rev $REPO_URL)} ||
            { echo -e "\e[91mCould not get latest revision\e[39m" && exit 1; }
    fi
    echo -e "\e[39mRevision: \e[96m$REVISION\e[39m"
}

#-----------------------------------------------------------------------------
function verify-webrtc-deps() {
    echo -e "\e[39mVerifying WebRTC dependencies. You may be prompted to accept software licenses.\e[39m"
    case $HOST_OS in
    "linux")
        sudo $SRC_DIR/src/build/install-build-deps.sh --no-syms --no-arm --no-chromeos-fonts --no-nacl
        ;;
    esac

    if [ $TARGET_OS = 'android' ]; then
        sudo $SRC_DIR/src/build/install-build-deps-android.sh
    fi
}

#-----------------------------------------------------------------------------
function configure-build() {
    local config_path="$TARGET_OS/$TARGET_CPU/$BUILD_CONFIG"
    local outdir="out/$config_path"
    echo -e "\e[39mGenerating build configuration\e[39m"
    local args="\
is_component_build=false \
treat_warnings_as_errors=false \
enable_iterator_debugging=false \
use_rtti=true \
rtc_include_tests=false \
rtc_build_examples=false \
rtc_build_tools=false \
rtc_use_x11=false \
rtc_enable_protobuf=false \
target_os=\"$TARGET_OS\" \
target_cpu=\"$TARGET_CPU\""
    [[ "$BUILD_CONFIG" == "Debug" && "$TARGET_OS" == "android" ]] && args+=" android_full_debug=true" || true
    [[ "$BUILD_CONFIG" == "Debug" ]] && args+=" is_debug=true symbol_level=2" || true
    [[ "$BUILD_CONFIG" == "Release" ]] && args+=" is_debug=false symbol_level=0" || true
    echo -e "\e[90m$args\e[39m"

    pushd "$SRC_DIR/src" >/dev/null
    gn gen $outdir --args="$args"
    popd >/dev/null
}

#-----------------------------------------------------------------------------
function compile-webrtc() {
    local config_path="$TARGET_OS/$TARGET_CPU/$BUILD_CONFIG"
    local outdir="out/$config_path"
    echo -e "\e[39mCompiling WebRTC library\e[39m"

    pushd "$SRC_DIR/src/$outdir" >/dev/null
    ninja -C .
    popd >/dev/null
}

#-----------------------------------------------------------------------------
function package-android-archive() {
    echo -e "\e[39mPackaging Android Archive\e[39m"
    local config_path="$TARGET_OS/$TARGET_CPU/$BUILD_CONFIG"
    local outdir="out/$config_path"
    local arch=""
    case "$TARGET_CPU" in
    "arm") arch="armeabi-v7a" ;;
    "arm64") arch="arm64-v8a" ;;
    "x86") arch="x86" ;;
    "x64") arch="x86_64" ;;
    esac

    pushd "$SRC_DIR/src/$outdir" >/dev/null

    rm -f libwebrtc.aar
    [ -d .aar ] && rm -rf .aar || true
    mkdir .aar
    mkdir .aar/jni
    mkdir .aar/jni/$arch
    cp libjingle_peerconnection_so.so .aar/jni/$arch
    cp "$SRC_DIR/src/sdk/android/AndroidManifest.xml" .aar/AndroidManifest.xml
    cp lib.java/sdk/android/libwebrtc.jar .aar/classes.jar
    pushd .aar >/dev/null
    zip -r ../libwebrtc.aar *
    popd >/dev/null
    rm -rf .aar

    popd >/dev/null
}

#-----------------------------------------------------------------------------
function package-webrtc() {
    case "$TARGET_OS" in
    "android")
        package-android-archive
        ;;
    *)
        echo "Unsupported target for packaging: $TARGET_OS"
        ;;
    esac
}
