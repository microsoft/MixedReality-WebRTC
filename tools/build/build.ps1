# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See LICENSE in the project root for license information.

param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$BuildConfig,
    
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$BuildArch,
    
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$BuildPlatform
)

enum BuildConfig {
    Debug
    Release
}

enum BuildArch {
    x86
    x64
    ARM
}

enum BuildPlatform {
    Win32
    UWP
}

if (!$BuildConfig)
{
    $BuildConfig = "Release"
}
elseif (![System.Enum]::GetNames([BuildConfig]).Contains($BuildConfig))
{
    Write-Host -ForegroundColor Red "Invalid build config '$BuildConfig'"
    exit 1
}

if (!$BuildArch)
{
    $BuildArch = "x64"
}
elseif (![System.Enum]::GetNames([BuildArch]).Contains($BuildArch))
{
    Write-Host -ForegroundColor Red "Invalid build architecture '$BuildArch'"
    exit 1
}

switch ($BuildPlatform)
{
    Win32 { $ScriptPlatform = "win" }
    UWP { $ScriptPlatform = "winuwp" }
    default {
        Write-Host -ForegroundColor Red "Invalid build platform '$BuildPlatform'"
        exit 1
    }
}

function Test-WindowsSDK
{
    $win10 = Get-Item "hklm:\SOFTWARE\WOW6432Node\Microsoft\Microsoft SDKs\Windows\v10.0" -ErrorAction Ignore
    if (!$win10)
    {
        Write-Host "No Windows SDK installed" -ForegroundColor Red
        exit 1
    }

    $ver = Get-ChildItem (Join-Path $win10.GetValue("InstallationFolder") "Include")
    $has17134 = $false
    $has17763 = $false
    switch -Regex ($ver)
    {
        '17134' { $has17134 = $true }
        '17763' { $has17763 = $true }
    }
    Write-Host -NoNewline "Windows SDK 1803 10.0.17134 (April 2018)   : "
    if (!$has17134)
    {
        Write-Host "Missing" -ForegroundColor Red
        exit
    }
    Write-Host "OK" -ForegroundColor Green
    Write-Host -NoNewline "Windows SDK 1809 10.0.17763 (October 2018) : "
    if (!$has17763)
    {
        Write-Host "Missing" -ForegroundColor Red
        exit
    }
    Write-Host "OK" -ForegroundColor Green
}

function Build-CoreWebRTC
{
    param(
        [Parameter(Mandatory)]
        [string]$BuildConfig,
        
        [Parameter(Mandatory)]
        [string]$BuildArch,
        
        [Parameter(Mandatory)]
        [string]$ScriptPlatform
    )
    $cmd = "python ..\..\external\webrtc-uwp-sdk\scripts\run.py -a prepare build -t webrtc"
    $cmd = "$cmd -p $ScriptPlatform --cpus $BuildArch -c $BuildConfig --noColor --noWrapper"
    # UWP uses C++17 due to C++/WinRT dependency (see WebRTC UWP SDK project)
    # FIXME - Until we have NuGet packages for libwebrtc built with C++14, do not flip the switch
    #         and keep building with C++17 even on Win32 platform.
    #if ($ScriptPlatform -eq "winuwp") {
        $cmd = "$cmd --cpp17"
    #}
    Invoke-Expression $cmd | Tee-Object -Variable output
    if (Select-String -Pattern "=================== Failed" -InputObject $output -SimpleMatch -CaseSensitive -Quiet)
    {
        Write-Host "Aborting build due to error while building core WebRTC" -ForegroundColor Red
        exit 1
    }
}

function Build-UWPWrappers
{
    param(
        [Parameter(Mandatory)]
        [string]$BuildConfig,
        
        [Parameter(Mandatory)]
        [string]$BuildArch
    )

    # Restore NuGet packages
    # This requires a separate step because Org.WebRtc.Universal uses a packages.config, which is not
    # supported by the /restore option of msbuild.
    nuget restore -NonInteractive "..\..\external\webrtc-uwp-sdk\webrtc\windows\projects\msvc\Org.WebRtc.Universal\packages.config" `
        -SolutionDirectory "..\..\external\webrtc-uwp-sdk\webrtc\windows\solutions" -Verbosity detailed

    # Find MSBuild.exe
    $msbuildTool = "C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
    if (-not (Test-Path $msbuildTool)) {
        $msbuildTool = "C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin\MSBuild.exe"
        if (-not (Test-Path $msbuildTool)) {
            $msbuildTool = "C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe"
            if (-not (Test-Path $msbuildTool)) {
                Write-Error "Cannot find MSBuild.exe from Visual Studio 2019 install path."
                exit 1
            }
        }
    }

    # Compile
    & $msbuildTool `
        /target:Build /property:Configuration=$BuildConfig /property:Platform=$BuildArch `
        "..\..\external\webrtc-uwp-sdk\webrtc\windows\projects\msvc\Org.WebRtc.Universal\Org.WebRtc.vcxproj"
}

# Try to apply a git patch, or check that it is already applied.
# Terminate the script if the patch is not applied already and failed to apply.
function Apply-GitPatch
{
    param(
        [Parameter(Mandatory)]
        [string]$Folder,
        
        [Parameter(Mandatory)]
        [string]$PatchPath
    )
    
    Push-Location $Folder
    git apply $PatchPath
    if ($?)
    {
        # Patch doesn't apply, check if already applied correctly by checking if
        # it can be un-applied in the current state (reverse apply).
        git apply --reverse --check $PatchPath
        if (-not $?)
        {
            Write-Error "Patch $PatchPath not applied, and failed to apply."
            exit 1
        }
    }
    Pop-Location
}


#
# Build
#

# Check Windows SDKs are installed
Test-WindowsSDK

# Build webrtc.lib
Build-CoreWebRTC -BuildConfig $BuildConfig -BuildArch $BuildArch -ScriptPlatform $ScriptPlatform

# Build Org.webrtc.dll/winmd
if ($BuildPlatform -eq "UWP")
{
    Build-UWPWrappers -BuildConfig $BuildConfig -BuildArch $BuildArch
}
