#!/bin/bash

#=============================================================================
# Global config

#set -o errexit
set -o nounset
set -o pipefail

BUILD_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

source "$BUILD_DIR/lib.sh"

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
    [ "$VERBOSE" = 1 ] && set -x
}

#=============================================================================
# Main

# Read command line
while getopts v OPTION; do
    case ${OPTION} in
    v) VERBOSE=1 ;;
    ?) usage && exit 1 ;;
    esac
done

# Read config from file.
read-config

# Read build config so we know where to read files from.
read-build-config

# Ensure all arguments have reasonable values.
verify-arguments

# TODO: Copy build outputs and write webrtc include path to config file.

echo -e "\e[39mCopying build artifacts for $TARGET_OS/$TARGET_CPU/$BUILD_CONFIG\e[39m"
case $TARGET_OS in
"android")
    echo ""
    ;;

esac

echo -e "\e[39m\e[1mComplete.\e[0m\e[39m"
echo -e "\e[39m\e[1mNext step:\e[0m Build the MixedReality-WebRTC library for $TARGET_OS/$TARGET_CPU.\e[39m"
