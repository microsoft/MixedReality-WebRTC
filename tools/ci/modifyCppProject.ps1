# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Modify the .vcxproj project file to only import the NuGet packages for
# the current build triple, which are the only ones restored.

param(
    [ValidateSet('Win32','UWP')]
    [string]$BuildPlatform,
    [ValidateSet('x86','x64','ARM')]
    [string]$BuildArch,
    [ValidateSet('Debug','Release')]
    [string]$BuildConfig,
    # Filename of the .vcxproj file to modify
    [string]$ProjectFile,
    # Remove all reference to Core NuGet packages (the pipeline builds from sources).
    # In that case the build platform/arch/config are ignored.
    [switch]$RemoveAll
)

Write-Host "Modifying .vcxproj for build triple $BuildPlatform-$BuildArch-$BuildConfig..."

$reader = [System.IO.File]::OpenText($ProjectFile)
$content = ""
try {
    for() {
        $line = $reader.ReadLine()
        if ($line -eq $null) { break }
        if ($line -match "Import Project=") {
            if ($line -match "packages\\Microsoft\.MixedReality\.WebRTC\.Native\.Core\.(Desktop|UWP|WinRT)\.(x86|x64|ARM)\.(Debug|Release)") {
                if ($RemoveAll) {
                    # Discard line - The build doesn't use NuGet; remove all NuGet references
                } else {
                    # The build uses NuGet; keep only matching NuGet references
                    $linePlatform = $Matches.1 # Desktop|UWP|WinRT
                    $lineArch = $Matches.2 # x86|x64|ARM
                    $lineConfig = $Matches.3 #Debug|Release
                    $matchPlatform = (($linePlatform -eq 'Desktop') -and ($BuildPlatform -eq 'Win32')) -or (($linePlatform -ne 'Desktop') -and ($BuildPlatform -eq 'UWP'))
                    $matchArch = ($lineArch -eq $BuildArch)
                    $matchConfig = ($lineConfig -eq $BuildConfig)
                    if ($matchPlatform -and $matchArch -and $matchConfig) {
                        # Copy line for matching build triple as is
                        $content += $line + "`n";
                    } else {
                        # Discard line - mismatching platform or arch or config
                    }
                }
            } elseif ($RemoveAll -and ($line -match "packages\\Microsoft\.MixedReality\.WebRTC\.Native\.Core\.UWP")) {
                # Discard line - Remove the Microsoft.MixedReality.WebRTC.Native.Core.UWP package with the WinRT headers
            } else {
                # Copy any other line as is
                $content += $line + "`n";
            }
        } elseif ($line -match "Error Condition=") {
            if ($line -match "packages\\Microsoft\.MixedReality\.WebRTC\.Native\.Core\.(Desktop|UWP|WinRT)\.(x86|x64|ARM)\.(Debug|Release)") {
                if ($RemoveAll) {
                    # Discard line - The build doesn't use NuGet; remove all NuGet references
                } else {
                    # The build uses NuGet; keep only matching NuGet references
                    $linePlatform = $Matches.1 # Desktop|UWP|WinRT
                    $lineArch = $Matches.2 # x86|x64|ARM
                    $lineConfig = $Matches.3 #Debug|Release
                    $matchPlatform = (($linePlatform -eq 'Desktop') -and ($BuildPlatform -eq 'Win32')) -or (($linePlatform -ne 'Desktop') -and ($BuildPlatform -eq 'UWP'))
                    $matchArch = ($lineArch -eq $BuildArch)
                    $matchConfig = ($lineConfig -eq $BuildConfig)
                    if ($matchPlatform -and $matchArch -and $matchConfig) {
                        # Copy line for matching build triple as is
                        $content += $line + "`n";
                    } else {
                        # Discard line - mismatching platform or arch or config
                    }
                }
            } elseif ($RemoveAll -and ($line -match "packages\\Microsoft\.MixedReality\.WebRTC\.Native\.Core\.UWP")) {
                # Discard line - Remove the Microsoft.MixedReality.WebRTC.Native.Core.UWP package with the WinRT headers
            } else {
                # Copy any other line as is
                $content += $line + "`n";
            }
        } else {
            # Copy any other line as is
            $content += $line + "`n";
        }
    }
}
finally {
    $reader.Close()
}
Write-Output $content | Set-Content -Path $ProjectFile -Encoding UTF8

Write-Host "== $ProjectFile ======================================="
Get-Content -Encoding UTF8 -Path "$ProjectFile"
Write-Host "=========================================================="
