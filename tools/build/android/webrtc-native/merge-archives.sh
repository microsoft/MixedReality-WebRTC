#!/bin/bash
# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Merge libwebrtc.aar and mrwebrtc.aar:
# - Keep most of mrwebrtc.aar, including the native library mrwebrtc.so which
#   already contains the WebRTC implementation (linked against libwebrtc.a).
# - Copy the Java classes from libwebrtc.aar into mrwebrtc.aar.

#=============================================================================
# Global config

set -o errexit
set -o nounset
#set -o pipefail # Doesn't work with Gradle

CURRENT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

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
function usage() {
    cat <<EOF
Usage:
    $0 [OPTIONS]

Merge the libwebrtc.aar and mrwebrtc.aar archives.

OPTIONS:
    -h              Show this message.
    -v              Verbose mode. Print all executed commands.
    -m MRWEBRTC     Input archive mrwebrtc.aar.
    -o OUTPUT       Output archive. Defaults to 'mrwebrtc_merged.aar'.
EOF
}

#-----------------------------------------------------------------------------
function verify-arguments() {
    VERBOSE=${VERBOSE:-0}
    OUTPUT=${OUTPUT:-"./mrwebrtc_merged.aar"}
    # Print all executed commands?
    [ "$VERBOSE" = 1 ] && set -x || true
    # Check inputs
    if [ ! -f "$MRWEBRTC_AAR" ]; then
        echo -e "\e[31mERROR: input archive $MRWEBRTC_AAR is not a file\e[39m" >&2
        exit 1
    fi
}

#-----------------------------------------------------------------------------
function merge-archives() {
    # Create temporary working directory
    TEMP_DIR="$(cd "$(mktemp -d /tmp/mrwebrtc-merge.XXXXXXXX)" && pwd)"

    # Ensure output folder exists and rewrite OUTPUT as absolute path
    local OUTPUT_DIR="$(dirname "$OUTPUT")"
    mkdir -p "$OUTPUT_DIR"
    OUTPUT_DIR="$(cd "$OUTPUT_DIR" && pwd)"
    OUTPUT="$OUTPUT_DIR/$(basename $OUTPUT)"

    # Print configuration
    echo "Merging AAR archives of mrwebrtc and libwebrtc..."
    echo -e "\e[39mmrwebrtc.aar : \e[96m$MRWEBRTC_AAR\e[39m"
    echo -e "\e[39mOutput       : \e[96m$OUTPUT\e[39m"
    echo -e "\e[39mTemp dir     : \e[96m$TEMP_DIR\e[39m"

    # Find libwebrtc.aar
    source "$CURRENT_DIR/../../libwebrtc/.config.sh" # import WORK_DIR
    source "$CURRENT_DIR/../../libwebrtc/.build.sh"  # import BUILD_CONFIG
    LIBWEBRTC_AAR="$WORK_DIR/webrtc/src/out/android/arm64/$BUILD_CONFIG/libwebrtc.aar"
    if [ ! -f "$LIBWEBRTC_AAR" ]; then
        echo -e "\e[31mERROR: cannot find libwebrtc.aar; $LIBWEBRTC_AAR is not a file\e[39m" >&2
        exit 1
    fi
    echo -e "\e[39mFound libwebrtc.aar : \e[96m$LIBWEBRTC_AAR\e[39m"

    # Create unpack folders
    echo -e "\e[39mCreate unpack folders\e[39m"
    TMP_LIBWEBRTC="$TEMP_DIR/libwebrtc"
    TMP_MRWEBRTC="$TEMP_DIR/mrwebrtc"
    mkdir -p "$TMP_LIBWEBRTC/classes"
    mkdir -p "$TMP_MRWEBRTC/classes"

    # Unzip source archives to temp folders
    echo -e "\e[39mUnpack Android archives\e[39m"
    unzip -q "$MRWEBRTC_AAR" -d "$TMP_MRWEBRTC"
    unzip -q "$LIBWEBRTC_AAR" -d "$TMP_LIBWEBRTC"
    
    # Unzip classes.jar
    echo -e "\e[39mUnpack classes.jar\e[39m"
    cd "$TMP_MRWEBRTC/classes"
    jar -xf ../classes.jar
    cd "$TMP_LIBWEBRTC/classes"
    jar -xf ../classes.jar

    # Copy .java classes from libwebrtc into mrwebrtc
    echo -e "\e[39mCopy org.webrtc Java classes\e[39m"
    cp -R -t $TMP_MRWEBRTC/classes $TMP_LIBWEBRTC/classes/*

    # Repack classes.jar
    echo -e "\e[39mRepack classes.jar\e[39m"
    jar -cf "$TMP_MRWEBRTC/classes.jar" -C "$TMP_MRWEBRTC/classes" .
    rm -rf "$TMP_MRWEBRTC/classes"

    # Repack mrwebrtc.aar
    echo -e "\e[39mRepack as $OUTPUT\e[39m"
    jar -cf $OUTPUT -C "$TMP_MRWEBRTC" .
}

#=============================================================================
# Main

# Read command line
while getopts l:m:o:vh OPTION; do
    case ${OPTION} in
    l) LIBWEBRTC_AAR=$OPTARG ;;
    m) MRWEBRTC_AAR=$OPTARG ;;
    o) OUTPUT=$OPTARG ;;
    v) VERBOSE=1 ;;
    h | ?) usage && exit 0 ;;
    esac
done

# Ensure all arguments have reasonable values
verify-arguments

# Merge archives
merge-archives
