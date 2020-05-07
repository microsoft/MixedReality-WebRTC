# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

enum BuildPlatform {
    Win32
    UWP
}

enum BuildArch {
    x86
    x64
    ARM64
}

enum BuildConfig {
    Debug
    Release
}

# Get the root folder of MixedReality-WebRTC
function Get-RootFolder {
    $repoRoot = Join-Path $PSScriptRoot "../../../" -Resolve
    return $repoRoot
}

# Get the <root>/external/ folder
function Get-ExternalFolder {
    $repoRoot = Get-RootFolder
    $externalFolder = Join-Path $repoRoot "external/"
    return $externalFolder
}

# Write a message for a task starting, with a specific color to make it
# stand out between long-running tasks themselves outputting lots of info.
function Write-TaskStart([string]$message) {
    Write-Host -Foreground Magenta "`n$message"
}

# Write a failure message without generating any Powershell error.
function Write-Failure([string]$message) {
    Write-Host -NoNewLine -Foreground Red "Error: "
    Write-Host $message
}

# Write a key-value pair with higlight on the value.
function Write-KeyValue([string]$key, [string]$value) {
    Write-Host -NoNewLine $key
    Write-Host -Foreground Blue $value
}

# Initialize the environment variables
function Initialize-BuildEnvironment {
    Write-TaskStart "Setting up environment..."

    $repoRoot = Get-RootFolder
    Write-KeyValue "Repository root: " $repoRoot

    # Add depot_tools folder to PATH
    $externalFolder = Join-Path $repoRoot "external/"
    $depotToolsPath = Join-Path $externalFolder "depot_tools"
    if (!($env:PATH.StartsWith($depotToolsPath))) {
        # Prepend depot_tools path as required by Chromium
        $env:PATH = "$depotToolsPath;$env:PATH"
        Write-Host "Updated PATH to include depot_tools location"
    }
    else {
        Write-Host "PATH already includes depot_tools location"
    }

    Write-Host "Disable depot_tools auto-update (DEPOT_TOOLS_UPDATE = 0)"
    $env:DEPOT_TOOLS_UPDATE = "0"

    Write-Host "Use local Windows toolchain (DEPOT_TOOLS_WIN_TOOLCHAIN = 0)"
    $env:DEPOT_TOOLS_WIN_TOOLCHAIN = "0"

    Write-Host "Use Visual Studio 2019 (GYP_MSVS_VERSION = 2019)"
    $env:GYP_MSVS_VERSION = "2019"
}

# Post-checkout clean-up
function Clear-PostCheckout([string]$SourceFolder) {
    # Cleanup temp _tools folder if left over.
    if (Test-Path "_tools") {
        Write-Host "Removing temporary _tools directory"
        Remove-Item -Path "_tools" -Recurse -Force
    }
    # Remove tools/* (except tools/clang/ and tools/protoc_wrapper/)
    $toolsFolder = Join-Path $SourceFolder "tools" -Resolve
    Rename-Item -Path $toolsFolder -NewName "_tools"
    $oldToolsFolder = Join-Path $SourceFolder "_tools" -Resolve
    New-Item -Path $toolsFolder -ItemType Directory | Out-Null
    Move-Item -Path $(Join-Path $oldToolsFolder "clang") -Destination $toolsFolder
    Move-Item -Path $(Join-Path $oldToolsFolder "protoc_wrapper") -Destination $toolsFolder
    Move-Item -Path $(Join-Path $oldToolsFolder "generate_stubs") -Destination $toolsFolder
    Remove-Item -Path $oldToolsFolder -Recurse -Force | Out-Null

    # Remove third_party/blink
    Remove-Item -Path $(Join-Path $SourceFolder "third_party/blink" -Resolve) -Force -Recurse | Out-Null

    # Remove third_party/catapult
    Remove-Item -Path $(Join-Path $SourceFolder "third_party/catapult" -Resolve) -Force -Recurse | Out-Null

    # Remove third_party/depot_tools
    Remove-Item -Path $(Join-Path $SourceFolder "third_party/depot_tools" -Resolve) -Force -Recurse | Out-Null

    # Remove third_party/node
    Remove-Item -Path $(Join-Path $SourceFolder "third_party/node" -Resolve) -Force -Recurse | Out-Null
}

# Install the Google repository of libwebrtc into external/libwebrtc
function Install-GoogleRepository {
    $externalFolder = Get-ExternalFolder

    # Prepare the folder
    $repoFolder = Join-Path $externalFolder "libwebrtc"
    New-Item $repoFolder -ItemType Directory -Force | Out-Null
    $repoFolder = Resolve-Path $repoFolder
    Write-Host "Installing Google's libwebrtc in $repoFolder"

    Push-Location $repoFolder

    # Download
    Write-TaskStart "Fetching Google repository, this might take some time..."
    try {
        Invoke-Expression "fetch --nohooks --no-history webrtc"
    }
    catch {
        Write-Failure "Failed to fetch Google repository 'webrtc'."
        throw
    }
    
    # Checkout + sync
    $libwebrtcFolder = Join-Path $repoFolder "src" -Resolve
    Set-Location $libwebrtcFolder
    try {
        Write-TaskStart "Checking out M80 branch, this might take some time..."
        Invoke-Expression "git checkout branch-heads/3987"
        Write-TaskStart "Synchronizing dependencies, this might take some time..."
        Invoke-Expression "gclient sync"
    }
    catch {
        Write-Failure "Failed to checkout M80 branch 'branch-heads/3987'."
        throw
    }

    # Delete sources not needed; this prevent security alerts on unused components,
    # and makes the overall checkout size smaller.
    Clear-PostCheckout -SourceFolder $libwebrtcFolder
    Remove-Item -Path $(Join-Path $externalFolder "depot_tools/external_bin/gsutil" -Resolve) -Force -Recurse | Out-Null

    # Apply patches
    $env:WEBRTCM80_ROOT = $libwebrtcFolder
    Write-TaskStart "Patching M80 for UWP..."
    Push-Location "../../winrtc/patches_for_WebRTC_org/m80"
    try {
        & .\patchWebRTCM80.cmd
    }
    catch {
        Write-Failure "Failed to apply UWP patches on M80 branch."
        throw
    }
    Pop-Location

    Pop-Location
}

# Install dependencies
function Install-Dependencies {
    Write-TaskStart "Installing dependencies..."

    # Check python
    try {
        Get-Command "python" -ErrorAction Stop | Out-Null
    }
    catch {
        Write-Failure "Cannot find Python. Please install from https://www.python.org/."
        exit 1
    }

    # Install pywin32
    # Make sure to use "python -m pip" and not "pip" to ensure that the same
    # Python version that is used to compile is also used to install. Otherwise
    # a user might have "pip" in its PATH for a Python version, and "python" for
    # another one, and install with "pip install" will succeed but import with
    # the other python version will still fail.
    Invoke-Expression "python -m pip install pywin32"
}

# Write the "args.gn" config file to the libwebrtc output folder
function Write-GnArgs([string]$Platform, [string]$Arch, [string]$Config, [string]$WebRTCBasePath) {
    if (!(Test-Path $WebRTCBasePath)) {
        throw "libwebrtc base path '$WebRTCBasePath' doesn't exist"
    }

    # Map MixedReality-WebRTC build triplet to libwebrtc build parameters
    switch -Exact ($Platform) {
        "Win32" { $target_os = "win" }
        "UWP" { $target_os = "winuwp" }
        Default { throw "Unknown platform '$Platform'" }
    }
    switch -Exact ($Arch) {
        "x86" { $target_cpu = "x86" }
        "x64" { $target_cpu = "x64" }
        "ARM64" { $target_cpu = "arm64" }
        Default { throw "Unknown architecture '$Arch'" }
    }
    switch -Exact ($Config) {
        "Debug" { 
            $is_debug = "true"
            $config_dir = "debug"
        }
        "Release" {
            $is_debug = "false"
            $config_dir = "release"
        }
        Default { throw "Unknown build config '$Config'" }
    }

    # Check if args.gn exists.
    # Only generate args.gn if it doesn't exist, otherwise this forces gn/ninja
    # to run a gn gen again and again, as the file timestamp change looks like
    # a build config change.
    $Folder = Join-Path $WebRTCBasePath "out\${target_os}\${target_cpu}\${config_dir}"
    New-Item $Folder -ItemType Directory -Force | Out-Null
    $FileName = Join-Path $Folder "args.gn"
    if (!(Test-Path $FileName)) {
        $Content = @"
# === WARNING GENERATED CONTENT ===
# This file was generated by $PSScriptRoot/build.ps1
# Any change will be overwritten when running that script again.
# You can still make change, but run 'gn/ninja' manually instead of using 'build.ps1'.

# Build target for $Platform $Arch
target_os="$target_os"
target_cpu="$target_cpu"

# Build variant ($Config)
is_debug=$is_debug

# Use MSVC toolchain (cl.exe)
use_lld=false
is_clang=false

# Force-include mandatory components for clarity
rtc_include_internal_audio_device=true
rtc_include_builtin_audio_codecs=true
rtc_include_builtin_video_codecs=true
rtc_libvpx_build_vp9=true
rtc_include_ilbc=true
rtc_include_opus=true
rtc_enable_sctp=true
rtc_disable_logging=false
rtc_disable_trace_events=false

# Disable proprietary codecs (MP3,MP4,OpenH264,AAC,...)
proprietary_codecs=false
rtc_use_h264=false # OpenH264

# Exclude unused modules to speed up build
rtc_include_tests=false
rtc_build_tools=false
rtc_build_examples=false
rtc_enable_protobuf=false
rtc_enable_external_auth=false

# Use WinRT video capturer for Windows Desktop and UWP
rtc_win_video_capture_winrt=true
"@
        Write-Host -NoNewline "Writing args.gn file to $FileName..."
        # Set-Content doesn't support UTF8NoBOM before PowerShell 6.0
        ## Set-Content -Path $FileName -Value $Content -Encoding UTF8NoBOM -Force
        $Utf8NoBomEncoding = New-Object System.Text.UTF8Encoding($False)
        [System.IO.File]::WriteAllText($FileName, $Content, $Utf8NoBomEncoding)
        Write-Host -ForegroundColor Green " Done"
    }

    # Print content of 'args.gn' for validation
    $Content = Get-Content $FileName -Encoding UTF8 -Raw
    Write-Host "`nContent of 'args.gn':"
    Write-Host -ForegroundColor Blue $Content

    # Return the build folder (without "args.gn")
    return $Folder
}

# Build libwebrtc for the given build triplet
function Build-Libwebrtc([string]$Platform, [string]$Arch, [string]$Config) {
    Write-TaskStart "Building libwebrtc for configuration:"
    Write-KeyValue "  Platform      = " $Platform
    Write-KeyValue "  Architecture  = " $Arch
    Write-KeyValue "  Build config  = " $Config
    Write-KeyValue "  External root = " $externalFolder
    Write-KeyValue "  Source folder = " $(Join-Path $externalFolder "libwebrtc/src")

    # Write args.gn
    try {
        $externalFolder = Get-ExternalFolder
        $libwebrtcFolder = Join-Path $externalFolder "libwebrtc/src" -Resolve
        Write-KeyValue "  Source folder = " $libwebrtcFolder
        $buildFolder = Write-GnArgs $Platform $Arch $Config $libwebrtcFolder
    }
    catch {
        $ex = $_.Exception
        $trace = $_.ScriptStackTrace
        Write-Failure "Failed to generate args.gn: $ex`nStack trace: $trace`n"
        throw
    }

    Push-Location $buildFolder

    try {
        # Generate Ninja files if needed.
        # If "build.ninja" already exists, don't force a generation (which takes
        # several seconds) and instead rely on ninja itself to detect if there
        # was a change and invoke gn as needed. Otherwise said, this is only
        # needed on the very first build.
        $ninjaBuildFile = Join-Path $buildFolder "build.ninja"
        if (!(Test-Path $ninjaBuildFile)) {
            Write-TaskStart "Generating build files..."
            Invoke-Expression "gn gen ."
        }
    
        # Build
        Write-TaskStart "Starting build..."
        Invoke-Expression "ninja"
    }
    catch {
        Write-Failure "Failed to build."
        Pop-Location
        throw
    }

    Pop-Location
}
