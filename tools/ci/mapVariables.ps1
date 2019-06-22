# Map build* variables to script* variables
#   build* variables are for MSVC and use uppercase : ARM(64), Debug|Release, Win32|UWP
#   script* variables are for the Google Python scripts and use lowercase: arm(64), debug|release, win|winuwp
# See https://github.com/webrtc-uwp/webrtc-scripts/blob/3f2eb87b1baa3f49c6341d144e0b3c3be11c78ae/defaults.py#L17

# buildPlatform = Win32|UWP
# scriptPlatform = win|winuwp
Write-Host "buildPlatform = $env:BUILDPLATFORM"
if ($env:BUILDPLATFORM -eq "Win32") {
    Write-Host "##vso[task.setvariable variable=scriptPlatform;]win"
}
elseif ($env:BUILDPLATFORM -eq "UWP") {
    Write-Host "##vso[task.setvariable variable=scriptPlatform;]winuwp"
}
else {
    Write-Host "##vso[task.complete result=Failed;]Unknown build platform '$env:BUILDPLATFORM'."
}

# buildArch = x86|x64|ARM|ARM64
# scriptArch = x86|x64|arm|arm64
Write-Host "buildArch = $env:BUILDARCH"
if ($env:BUILDARCH -eq "x86") {
    Write-Host "##vso[task.setvariable variable=scriptArch; ]x86"
}
elseif ($env:BUILDARCH -eq "x64") {
    Write-Host "##vso[task.setvariable variable=scriptArch; ]x64"
}
elseif ($env:BUILDARCH -eq "ARM64") {
    Write-Host "##vso[task.setvariable variable=scriptArch; ]arm"
}
elseif ($env:BUILDARCH -eq "ARM64") {
    Write-Host "##vso[task.setvariable variable=scriptArch; ]arm64"
}
else {
    Write-Host "##vso[task.complete result=Failed; ]Unknown build architecture '$env:BUILDARCH'."
}

# buildConfig = Debug|Release
# scriptConfig = debug|release
Write-Host "buildConfig = $env:BUILDCONFIG"
if ($env:BUILDCONFIG -eq "Debug") {
    Write-Host "##vso[task.setvariable variable=scriptConfig; ]debug"
}
elseif ($env:BUILDCONFIG -eq "Release") {
    Write-Host "##vso[task.setvariable variable=scriptConfig; ]release"
}
else {
    Write-Host "##vso[task.complete result=Failed; ]Unknown build config '$env:BUILDCONFIG'."
}
