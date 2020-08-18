#!/bin/bash
# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

#=============================================================================
# Global config

set -o errexit
set -o nounset
# This breaks checkout on ADO, throwing some SIGPIPE for no apparent reason.
# See http://www.pixelbeat.org/programming/sigpipe_handling.html for details.
#set -o pipefail

#-----------------------------------------------------------------------------
function check-err() {
    rv=$?
    [[ $rv != 0 ]] && echo "$0 exited with code $rv. Run with -v to get more info."
    exit $rv
}

trap "check-err" INT TERM EXIT

#=============================================================================
# Functions

#-----------------------------------------------------------------------------

BUILD_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

source "$BUILD_DIR/lib.sh"

#-----------------------------------------------------------------------------
function usage() {
    cat <<EOF
Usage:
    $0 [OPTIONS]

Builds Chromium's WebRTC component.

OPTIONS:
    -h              Show this message.
    -v              Verbose mode. Print all executed commands.
    -c BUILD_CONFIG Build configuration. Default is 'Release'. Possible values are: 'Debug', 'Release'.
    -p              Prepare the build (generate ninja files) without running it. This is generally not
                    needed, as the default is to both prepare and build. Mainly useful for CI.
    -b              Build without prepare. This requires a previous prepare pass. This is generally not
                    needed, as the default is to both prepare and build. Mainly useful for CI.
EOF
}

#-----------------------------------------------------------------------------
function verify-arguments() {
    VERBOSE=${VERBOSE:-0}
    PREPARE=${PREPARE:-0}
    BUILD=${BUILD:-0}
    if [ "$PREPARE" = 0 ] && [ "$BUILD" = 0 ]; then
      # Default without any of (-p,-b) is to do both
      PREPARE=1
      BUILD=1
    fi
    BUILD_CONFIG=${BUILD_CONFIG:-Release}
    DEPOT_TOOLS_DIR=$WORK_DIR/depot_tools
    PATH=$DEPOT_TOOLS_DIR:$DEPOT_TOOLS_DIR/python276_bin:$PATH
    # Print all executed commands?
    [ "$VERBOSE" = 1 ] && set -x || true
    echo -e "\e[39mBuild configuration: \e[96m$BUILD_CONFIG\e[39m"
    # Verify build config
    if [[ "$BUILD_CONFIG" != "Debug" && "$BUILD_CONFIG" != "Release" ]]; then
        echo -e "\e[39mUnknown build configuration: \e[91m$BUILD_CONFIG\e[39m"
        exit 1
    fi
}

#-----------------------------------------------------------------------------
function print-summary() {
    local config_path="$TARGET_OS/$TARGET_CPU/$BUILD_CONFIG"
    local outdir_full="$SRC_DIR/src/out/$config_path"

    case $TARGET_OS in
    "android")
        echo -e "\e[39mStatic library : \e[96m$outdir_full/obj/libwebrtc.a\e[39m"
        echo -e "\e[39mAndroid Archive: \e[96m$outdir_full/libwebrtc.aar\e[39m"
        echo -e "\e[39mHeader path    : \e[96m$SRC_DIR\e[39m"
        ;;
    *)
        echo "TODO: print build summary for $TARGET_OS"
        ;;
    esac
}

#-----------------------------------------------------------------------------
function write-cmakelists-config() {
    local filename="$BUILD_DIR/.libwebrtc.cmake"
    local libwebrtc_src_dir=$WORK_DIR
    local libwebrtc_out_dir=$SRC_DIR/src/out/$TARGET_OS/$TARGET_CPU/$BUILD_CONFIG
    local libwebrtc_src_dir_win=$libwebrtc_src_dir
    local libwebrtc_out_dir_win=$libwebrtc_out_dir
    # Attempt to discover the drive letter and convert it to Windows path format.
    # This is helpful in a hybrid environment like WSL2 where later parts of the
    # build happen Windows-side. This isn't a very robust check and may produce
    # invalid Windows paths. Hopefully only in the case where WSL2/Windows isn't
    # relevant.
    libwebrtc_src_dir_win=$(echo $libwebrtc_src_dir_win | sed -E -r 's!^/mnt/(.)/!\U\1\\:\\\\!')
    libwebrtc_out_dir_win=$(echo $libwebrtc_out_dir_win | sed -E -r 's!^/mnt/(.)/!\U\1\\:\\\\!')
    libwebrtc_src_dir_win=$(echo $libwebrtc_src_dir_win | sed -E 's!/!\\\\!g')
    libwebrtc_out_dir_win=$(echo $libwebrtc_out_dir_win | sed -E 's!/!\\\\!g')
    cat >$filename <<EOF
# Generated file. Do not edit.
if(CMAKE_HOST_SYSTEM_NAME STREQUAL "Windows")
    set(libwebrtc-src-dir $libwebrtc_src_dir_win)
    set(libwebrtc-out-dir $libwebrtc_out_dir_win)
else()
    set(libwebrtc-src-dir $libwebrtc_src_dir)
    set(libwebrtc-out-dir $libwebrtc_out_dir)
endif()
EOF
    # Also write the gradle config
    local libwebrtc_bin_dir=$SRC_DIR/src/out/$TARGET_OS/$TARGET_CPU
    # TODO - WSL2 path support
    filename="$BUILD_DIR/../android/mrwebrtc.gradle"
    cat >$filename <<EOF
// THIS FILE IS GENERATED - DO NOT EDIT - CHANGES WILL BE LOST

// Path to the bin folder of libwebrtc, excluding the build variant (Debug/Release)
gradle.ext.webrtcBinDir = '$libwebrtc_bin_dir'
EOF
}

#-----------------------------------------------------------------------------
function copy-artifacts-to-unity-sample() {
    echo -e "\e[39mCopying build artifacts for $TARGET_OS/$TARGET_CPU/$BUILD_CONFIG\e[39m"
    case $TARGET_OS in
    "android")
        local config_path="$TARGET_OS/$TARGET_CPU/$BUILD_CONFIG"
        local outdir="out/$config_path"
        local arch=""
        case "$TARGET_CPU" in
        "arm") arch="armeabi-v7a" ;;
        "arm64") arch="arm64-v8a" ;;
        "x86") arch="x86" ;;
        "x64") arch="x86_64" ;;
        esac
        local src="$SRC_DIR/src/$outdir/libwebrtc.aar"
        local dst="../../../libs/Microsoft.MixedReality.WebRTC.Unity/Assets/Plugins/$arch/"
        echo "Copying libwebrtc.aar to Unity sample project."
        mkdir -p "$dst" && cp "$src" "$_"
        ;;
    *)
        echo "Unsupported platform: $TARGET_OS"
        exit 1
        ;;
    esac
}

#=============================================================================
# Main

# Read command line
while getopts c:vpbh OPTION; do
    case ${OPTION} in
    c) BUILD_CONFIG=$OPTARG ;;
    v) VERBOSE=1 ;;
    p) PREPARE=1 ;;
    b) BUILD=1 ;;
    h | ?) usage && exit 0 ;;
    esac
done

# Read config from file
read-config

# Verify source is checked out and configs match
verify-checkout-config

# Ensure all arguments have reasonable values
verify-arguments

# Compile and package webrtc library
echo -e "\e[39mBuilding: \e[96m$TARGET_OS/$TARGET_CPU/$BUILD_CONFIG\e[39m"

# Generate the WebRTC Ninja build config files
[ $PREPARE = '0' ] || configure-build

# Run the WebRTC Ninja build
[ $BUILD = '0' ] || compile-webrtc

# Package the build artifacts for the current platform
[ $BUILD = '0' ] || package-webrtc

# Print paths to build artifacts
[ $BUILD = '0' ] || print-summary

# Write file ./.libwebrtc.cmake
write-cmakelists-config

# Write file ./.build.sh
write-build-config

# Copy build artifacts to Unity sample
[ $BUILD = '0' ] || copy-artifacts-to-unity-sample

if [ $BUILD = '0' ]; then
  echo -e "\e[39m\e[1mPrepare complete.\e[0m\e[39m"
else
  echo -e "\e[39m\e[1mBuild complete.\e[0m\e[39m"
fi
