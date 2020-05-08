# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

<#
.SYNOPSIS
Build the libwebrtc library consumed by the native mrwebrtc library.

.DESCRIPTION
This script builds one build triple variant (platform, architecture, configuration) of
the low-level native implementation of WebRTC (libwebrtc).

.PARAMETER BuildPlatform
The build platform to build for.

.PARAMETER BuildArch
The build architecture to build for. Valid values depend on the build platform.

.PARAMETER BuildConfig
The build configuration to build for (Debug or Release).

.EXAMPLE
build.ps1 -BuildPlatform Win32 -BuildArch x64 -BuildConfig Debug

Build the Windows Desktop x64 Debug variant of libwebrtc (webrtc.lib).

.EXAMPLE
build.ps1 -BuildPlatform UWP -BuildArch ARM64 -BuildConfig Release

Build the Windows UWP ARM64 Release variant of libwebrtc (webrtc.lib).
#>

# Note - Unfortunately cannot use typed enums here because param
#        needs to be the first instruction of the script.
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet("Win32", "UWP")]
    [string]$BuildPlatform,

    [Parameter(Mandatory)]
    [ValidateSet("x86", "x64", "ARM64")]
    [string]$BuildArch,

    [Parameter(Mandatory)]
    [ValidateSet("Debug", "Release")]
    [string]$BuildConfig
)

# Run in different process to not affect the caller's $env:PATH.
# Note that we need to pass the argument to the new process.
& powershell -args "$BuildPlatform","$BuildArch","$BuildConfig" -Command {

    # Retrieve args
    $BuildPlatform = $args[0]
    $BuildArch = $args[1]
    $BuildConfig = $args[2]

    # Import library
    . .\mrwebrtc.ps1

    # Setup the build environment
    Initialize-BuildEnvironment

    # Build libwebrtc for the selected build triple
    Build-Libwebrtc "$BuildPlatform" "$BuildArch" "$BuildConfig"
}
