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

Setup Chromium's Depot Tools and clone WebRTC repo.

OPTIONS:
    -h              Show this message.
    -v              Verbose mode. Print all executed commands.
EOF
}

#-----------------------------------------------------------------------------
function verify-arguments() {
    VERBOSE=${VERBOSE:-0}
    REPO_URL="https://webrtc.googlesource.com/src"
    DEPOT_TOOLS_URL="https://chromium.googlesource.com/chromium/tools/depot_tools.git"
    DEPOT_TOOLS_DIR=$WORK_DIR/depot_tools
    PATH=$DEPOT_TOOLS_DIR:$DEPOT_TOOLS_DIR/python276_bin:$PATH
    # Print all executed commands?
    [ "$VERBOSE" = 1 ] && set -x || true
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

# Read config from file
read-config

# Ensure all arguments have reasonable values
verify-arguments

# What is the host platform?
detect-host-os

# Verify this is a supported build platform
verify-host-os

# Verify required platform-specific dependencies are present
verify-host-platform-deps

# Get the git revision number of the branch we're going to build
calc-git-revision

# Create $WORK_DIR and change directory to it
create-WORK_DIR-and-cd

# Checkout depot tools
checkout-depot-tools

# Checkout WebRTC source for the target platform
checkout-webrtc

# Verify WebRTC dependencies are installed
verify-webrtc-deps

# Write file ./.checkout.sh
write-config ".checkout.sh"

echo -e "\e[39m\e[1mCheckout complete.\e[0m\e[39m"
echo -e "\e[39m\e[1mNext step:\e[0m run ./build.sh\e[39m"
