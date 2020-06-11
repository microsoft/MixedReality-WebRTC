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

Sets configuration for building Chromium's WebRTC library.

OPTIONS:
    -h              Show this message.
    -v              Verbose mode. Print all executed commands.
    -d WORK_DIR     Where to setup the Chromium Depot Tools and clone the WebRTC repo.
    -b BRANCH       The name of the Git branch to clone. E.g.: "branch-heads/71"
    -t TARGET_OS    Target OS for cross compilation. Default is 'android'. Possible values are: 'linux', 'mac', 'win', 'android', 'ios'.
    -c TARGET_CPU   Target CPU for cross compilation. Default is determined by TARGET_OS. For 'android', it is 'arm64'. Possible values are: 'x86', 'x64', 'arm64', 'arm'.
    -s              Ignore disk space check and proceed anyway with low disk space.
    -u              Ignore unsupported platform check and attempt to install anyway.
    -f              Fast clone for CI (shallow clone, no history, no hooks)
EOF
}

#-----------------------------------------------------------------------------
function verify-arguments() {
    BRANCH=${BRANCH:-branch-heads/71}
    WORK_DIR=${WORK_DIR:-work}
    VERBOSE=${VERBOSE:-0}
    TARGET_OS=${TARGET_OS:-android}
    TARGET_CPU=${TARGET_CPU:-arm64}
    NO_DISK_SPACE=${NO_DISK_SPACE:-0}
    UNSUPPORTED=${UNSUPPORTED:-0}
    FAST_CLONE=${FAST_CLONE:-0}
    # Print all executed commands?
    [ "$VERBOSE" = 1 ] && set -x || true
}

#=============================================================================
# Main

# Read command line
while getopts d:b:t:c:vsufh OPTION; do
    case ${OPTION} in
    d) WORK_DIR=$OPTARG ;;
    b) BRANCH=$OPTARG ;;
    t) TARGET_OS=$OPTARG ;;
    c) TARGET_CPU=$OPTARG ;;
    v) VERBOSE=1 ;;
    s) NO_DISK_SPACE=1 ;;
    u) UNSUPPORTED=1 ;;
    f) FAST_CLONE=1 ;;
    h | ?) usage && exit 0 ;;
    esac
done

# Ensure all arguments have reasonable values
verify-arguments

# Check host requirements
[ "$NO_DISK_SPACE" = 0 ] && check-host-disk-space || true
[ "$UNSUPPORTED" = 0 ] && check-host-os || true

# Print a config summary
print-config

# Write file ./.config.sh
write-config ".config.sh"

echo -e "\e[39m\e[1mConfig complete.\e[0m\e[39m"
echo -e "\e[39m\e[1mNext step:\e[0m run ./checkout.sh\e[39m"
