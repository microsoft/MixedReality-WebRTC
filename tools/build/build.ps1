
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
    ARM64
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
    $cmd = "$cmd -p $ScriptPlatform --cpus $BuildArch -c $BuildConfig --noColor --noWrapper --cpp17"
    Invoke-Expression $cmd
}

function Build-UWPWrappers
{
    param(
        [Parameter(Mandatory)]
        [string]$BuildConfig,
        
        [Parameter(Mandatory)]
        [string]$BuildArch
    )
    & "C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\MSBuild\Current\Bin\MSBuild.exe" `
        /target:Build /maxCpuCount:4 /property:Configuration=$BuildConfig /property:Platform=$BuildArch `
        "..\..\external\webrtc-uwp-sdk\webrtc\windows\projects\msvc\Org.WebRtc.Universal\Org.WebRtc.vcxproj"
}

Test-WindowsSDK
Build-CoreWebRTC -BuildConfig $BuildConfig -BuildArch $BuildArch -ScriptPlatform $ScriptPlatform
if ($BuildPlatform -eq "UWP")
{
    Build-UWPWrappers -BuildConfig $BuildConfig -BuildArch $BuildArch
}
