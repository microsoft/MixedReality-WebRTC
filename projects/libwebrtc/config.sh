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

Sets configuration for building Chromium's WebRTC library.

OPTIONS:
    -h              Show this message.
    -v              Verbose mode. Print all executed commands.
    -d WORK_DIR     Where to setup the Chromium Depot Tools and clone the WebRTC repo.
    -b BRANCH       The name of the Git branch to clone. E.g.: "branch-heads/71"
    -t TARGET_OS    Target OS for cross compilation. Default is 'android'. Possible values are: 'linux', 'mac', 'win', 'android', 'ios'.
    -c TARGET_CPU   Target CPU for cross compilation. Default is determined by TARGET_OS. For 'android', it is 'arm64'. Possible values are: 'x86', 'x64', 'arm64', 'arm'.
EOF
}

#-----------------------------------------------------------------------------
function verify-arguments() {
    BRANCH=${BRANCH:-}
    WORK_DIR=${WORK_DIR:-in}
    VERBOSE=${VERBOSE:-0}
    TARGET_OS=${TARGET_OS:-android}
    TARGET_CPU=${TARGET_CPU:-arm64}
    # Print all executed commands?
    [ "$VERBOSE" = 1 ] && set -x
}

#=============================================================================
# Main

# Read command line
while getopts d:b:t:c:v OPTION; do
    case ${OPTION} in
    d) WORK_DIR=$OPTARG ;;
    b) BRANCH=$OPTARG ;;
    t) TARGET_OS=$OPTARG ;;
    c) TARGET_CPU=$OPTARG ;;
    v) VERBOSE=1 ;;
    ?) usage && exit 1 ;;
    esac
done

# Ensure all arguments have reasonable values
verify-arguments

# Print a config summary
print-config

# Write file ./.config.sh
write-config ".config.sh"

echo -e "\e[39m\e[1mComplete.\e[0m\e[39m"
echo -e "\e[39m\e[1mNext step:\e[0m run ./checkout.sh\e[39m"
