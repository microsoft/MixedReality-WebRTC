# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Map build* variables to script* variables
#   build* variables are for MSVC and use uppercase : ARM(64), Debug|Release, Win32|UWP
#   script* variables are for the Google Python scripts and use lowercase: arm(64), debug|release, win|winuwp

param(
    [Parameter(Position=0)]
    [ValidateSet('Win32','UWP')]
    [string]$BuildPlatform,

    [Parameter(Position=1)]
    [ValidateSet('x86','x64','ARM','ARM64')]
    [string]$BuildArch,

    [Parameter(Position=2)]
    [ValidateSet('Debug','Release')]
    [string]$BuildConfig
)

# buildPlatform = Win32|UWP
# scriptPlatform = win|winuwp
Write-Host "buildPlatform = $BuildPlatform"
if ($BuildPlatform -eq "Win32") {
    $scriptPlatform = "win"
}
elseif ($BuildPlatform -eq "UWP") {
    $scriptPlatform = "winuwp"
}
else {
    Write-Host "##vso[task.complete result=Failed;]Unknown build platform '$BuildPlatform'."
}
Write-Host "##vso[task.setvariable variable=scriptPlatform;]$scriptPlatform"
Write-Host "scriptPlatform = $scriptPlatform"

# buildArch = x86|x64|ARM
# scriptArch = x86|x64|arm
Write-Host "buildArch = $BuildArch"
if ($BuildArch -eq "x86") {
    $scriptArch = "x86"
}
elseif ($BuildArch -eq "x64") {
    $scriptArch = "x64"
}
elseif ($BuildArch -eq "ARM") {
    $scriptArch = "arm"
}
elseif ($BuildArch -eq "ARM64") {
    $scriptArch = "arm64"
}
else {
    Write-Host "##vso[task.complete result=Failed; ]Unknown build architecture '$BuildArch'."
}
Write-Host "##vso[task.setvariable variable=scriptArch;]$scriptArch"
Write-Host "scriptArch = $scriptArch"

# buildConfig = Debug|Release
# scriptConfig = debug|release
Write-Host "buildConfig = $BuildConfig"
if ($BuildConfig -eq "Debug") {
    $scriptConfig = "debug"
}
elseif ($BuildConfig -eq "Release") {
    $scriptConfig = "release"
}
else {
    Write-Host "##vso[task.complete result=Failed; ]Unknown build config '$BuildConfig'."
}
Write-Host "##vso[task.setvariable variable=scriptConfig;]$scriptConfig"
Write-Host "scriptConfig = $scriptConfig"
