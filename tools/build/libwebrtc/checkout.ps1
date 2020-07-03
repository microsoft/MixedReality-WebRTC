# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Run in scoped block to not affect user's $env:PATH
& powershell {
    # Import library
    . .\mrwebrtc.ps1

    # Setup the build environment
    Initialize-BuildEnvironment

    # Download and setup the Google repository
    Install-GoogleRepository
}
