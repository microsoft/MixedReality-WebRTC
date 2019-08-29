# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See LICENSE in the project root for license information.

# Map build* variables to script* variables
#   build* variables are for MSVC and use uppercase : ARM(64), Debug|Release, Win32|UWP
#   script* variables are for the Google Python scripts and use lowercase: arm(64), debug|release, win|winuwp
# See https://github.com/webrtc-uwp/webrtc-scripts/blob/3f2eb87b1baa3f49c6341d144e0b3c3be11c78ae/defaults.py#L17

param(
    [Parameter(Position=0)]
    [ValidateSet('Win32','UWP')]
    [string]$BuildPlatform,

    [Parameter(Position=1)]
    [ValidateSet('x86','x64','ARM')]
    [string]$BuildArch,

    [Parameter(Position=2)]
    [ValidateSet('Debug','Release')]
    [string]$BuildConfig
)

# buildPlatform = Win32|UWP
# scriptPlatform = win|winuwp
Write-Host "buildPlatform = $BuildPlatform"
if ($BuildPlatform -eq "Win32") {
    Write-Host "##vso[task.setvariable variable=scriptPlatform;]win"
}
elseif ($BuildPlatform -eq "UWP") {
    Write-Host "##vso[task.setvariable variable=scriptPlatform;]winuwp"
}
else {
    Write-Host "##vso[task.complete result=Failed;]Unknown build platform '$BuildPlatform'."
}

# buildArch = x86|x64|ARM
# scriptArch = x86|x64|arm
Write-Host "buildArch = $BuildArch"
if ($BuildArch -eq "x86") {
    Write-Host "##vso[task.setvariable variable=scriptArch; ]x86"
}
elseif ($BuildArch -eq "x64") {
    Write-Host "##vso[task.setvariable variable=scriptArch; ]x64"
}
elseif ($BuildArch -eq "ARM") {
    Write-Host "##vso[task.setvariable variable=scriptArch; ]arm"
}
else {
    Write-Host "##vso[task.complete result=Failed; ]Unknown build architecture '$BuildArch'."
}

# buildConfig = Debug|Release
# scriptConfig = debug|release
Write-Host "buildConfig = $BuildConfig"
if ($BuildConfig -eq "Debug") {
    Write-Host "##vso[task.setvariable variable=scriptConfig; ]debug"
}
elseif ($BuildConfig -eq "Release") {
    Write-Host "##vso[task.setvariable variable=scriptConfig; ]release"
}
else {
    Write-Host "##vso[task.complete result=Failed; ]Unknown build config '$BuildConfig'."
}
