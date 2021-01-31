# Create NuGet packages
param (
    [Parameter(Mandatory = $true)]
    [string]$PackageVersion,
    
    [Parameter(Mandatory = $false)]
    [string]$PackageSuffix = "",
    
    [Parameter(Mandatory = $false)]
    [string]$OutputDirectory = "."
)

$rootPath = Join-Path $PSScriptRoot "..\.."

if ($PackageSuffix) {
    # mrwebrtc (Desktop)
    nuget pack $(Join-Path $rootPath "tools\nuget\Desktop\mrwebrtc.nuspec") -BasePath $rootPath -NonInteractive -OutputDirectory $OutputDirectory -Version $PackageVersion -Suffix $PackageSuffix
    # Microsoft.MixedReality.WebRTC (Desktop)
    nuget pack $(Join-Path $rootPath "tools\nuget\Desktop\Microsoft.MixedReality.WebRTC.nuspec") -BasePath $rootPath -NonInteractive -OutputDirectory $OutputDirectory -Version $PackageVersion -Suffix $PackageSuffix
    # mrwebrtc_uwp (UWP)
    nuget pack $(Join-Path $rootPath "tools\nuget\UWP\mrwebrtc_uwp.nuspec") -BasePath $rootPath -NonInteractive -OutputDirectory $OutputDirectory -Version $PackageVersion -Suffix $PackageSuffix
    # Microsoft.MixedReality.WebRTC.UWP (UWP)
    nuget pack $(Join-Path $rootPath "tools\nuget\UWP\Microsoft.MixedReality.WebRTC.UWP.nuspec") -BasePath $rootPath -NonInteractive -OutputDirectory $OutputDirectory -Version $PackageVersion -Suffix $PackageSuffix
} else {
    # mrwebrtc (Desktop)
    nuget pack $(Join-Path $rootPath "tools\nuget\Desktop\mrwebrtc.nuspec") -BasePath $rootPath -NonInteractive -OutputDirectory $OutputDirectory -Version $PackageVersion
    # Microsoft.MixedReality.WebRTC (Desktop)
    nuget pack $(Join-Path $rootPath "tools\nuget\Desktop\Microsoft.MixedReality.WebRTC.nuspec") -BasePath $rootPath -NonInteractive -OutputDirectory $OutputDirectory -Version $PackageVersion
    # mrwebrtc_uwp (UWP)
    nuget pack $(Join-Path $rootPath "tools\nuget\UWP\mrwebrtc_uwp.nuspec") -BasePath $rootPath -NonInteractive -OutputDirectory $OutputDirectory -Version $PackageVersion
    # Microsoft.MixedReality.WebRTC.UWP (UWP)
    nuget pack $(Join-Path $rootPath "tools\nuget\UWP\Microsoft.MixedReality.WebRTC.UWP.nuspec") -BasePath $rootPath -NonInteractive -OutputDirectory $OutputDirectory -Version $PackageVersion
}
