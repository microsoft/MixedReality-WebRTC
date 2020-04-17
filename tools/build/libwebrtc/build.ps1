# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Run in scoped block to not affect user's $env:PATH
& powershell {
    # Import library
    . .\mrwebrtc.ps1

    Initialize-BuildEnvironment
    Install-GoogleRepository
    Install-Dependencies
    #Build-Libwebrtc "Win32" "x64" "Debug"
    Build-Libwebrtc "UWP" "x64" "Debug"
}

