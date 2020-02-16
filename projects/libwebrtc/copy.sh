#!/bin/bash

#=============================================================================
# Global config

set -o errexit
set -o nounset
set -o pipefail

#-----------------------------------------------------------------------------
function check-err() {
    rv=$?
    [[ $rv != 0 ]] && echo "$0 exited with code $rv. Run with -v to get more info."
    exit $rv
}

trap "check-err" INT TERM EXIT

#-----------------------------------------------------------------------------

BUILD_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

source "$BUILD_DIR/lib.sh"

#=============================================================================
# Functions

#-----------------------------------------------------------------------------
function usage() {
    cat <<EOF
Usage:
    $0 [OPTIONS]

Copies libwebrtc build artifacts to the Unity sample project.

OPTIONS:
    -h              Show this message.
    -v              Verbose mode. Print all executed commands.
EOF
}

#-----------------------------------------------------------------------------
function verify-arguments() {
    VERBOSE=${VERBOSE:-0}
    # Print all executed commands?
    [ $VERBOSE = 1 ] && set -x || true
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
        local dst="../../libs/Microsoft.MixedReality.WebRTC.Unity/Assets/Plugins/Android/$arch/"
        echo "Copying libwebrtc.aar to Unity sample scene."
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
while getopts vh OPTION; do
    case ${OPTION} in
    v) VERBOSE=1 ;;
    h | ?) usage && exit 0 ;;
    esac
done

# Read config from file.
read-config

# Read build config so we know where to read files from.
read-build-config

# Ensure all arguments have reasonable values.
verify-arguments

# Copy build outputs.
copy-artifacts-to-unity-sample

echo -e "\e[39m\e[1mCopy complete.\e[0m\e[39m"
echo -e "\e[39m\e[1mNext step:\e[0m Build MixedReality-WebRTC for $TARGET_OS/$TARGET_CPU.\e[39m"
