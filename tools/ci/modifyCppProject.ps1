# Modify the .vcxproj project file to only import the NuGet packages for
# the current build triple, which are the only ones restored.

param(
    [ValidateSet('Win32','UWP')]
    [string]$BuildPlatform,
    [ValidateSet('x86','x64','ARM')]
    [string]$BuildArch,
    [ValidateSet('Debug','Release')]
    [string]$BuildConfig,
    [string]$ProjectFile
)

Write-Host "Modifying .vcxproj for build triple $BuildPlatform-$BuildArch-$BuildConfig..."

$reader = [System.IO.File]::OpenText($ProjectFile)
$content = ""
try {
    for() {
        $line = $reader.ReadLine()
        if ($line -eq $null) { break }
        if ($line -match "Import Project=")
        {
            if ($line -match "packages\\Microsoft\.MixedReality\.WebRTC\.Native\.Core\.(Desktop|UWP|WinRT)\.(x86|x64|ARM)\.(Debug|Release)")
            {
                $linePlatform = $Matches.1 # Desktop|UWP|WinRT
                $lineArch = $Matches.2 # x86|x64|ARM
                $lineConfig = $Matches.3 #Debug|Release
                $matchPlatform = (($linePlatform -eq 'Desktop') -and ($BuildPlatform -eq 'Win32')) -or (($linePlatform -ne 'Desktop') -and ($BuildPlatform -eq 'UWP'))
                $matchArch = ($lineArch -eq $BuildArch)
                $matchConfig = ($lineConfig -eq $BuildConfig)
                if ($matchPlatform -and $matchArch -and $matchConfig)
                {
                    # Copy line for matching build triple as is
                    $content += $line + "`n";
                }
                else
                {
                    # Discard line - mismatching platform or arch or config
                }
            }
            else
            {
                # Copy any other line as is
                $content += $line + "`n";
            }
        }
        elseif ($line -match "Error Condition=")
        {
            if ($line -match "packages\\Microsoft\.MixedReality\.WebRTC\.Native\.Core\.(Desktop|UWP|WinRT)\.(x86|x64|ARM)\.(Debug|Release)")
            {
                $linePlatform = $Matches.1 # Desktop|UWP|WinRT
                $lineArch = $Matches.2 # x86|x64|ARM
                $lineConfig = $Matches.3 #Debug|Release
                $matchPlatform = (($linePlatform -eq 'Desktop') -and ($BuildPlatform -eq 'Win32')) -or (($linePlatform -ne 'Desktop') -and ($BuildPlatform -eq 'UWP'))
                $matchArch = ($lineArch -eq $BuildArch)
                $matchConfig = ($lineConfig -eq $BuildConfig)
                if ($matchPlatform -and $matchArch -and $matchConfig)
                {
                    # Copy line for matching build triple as is
                    $content += $line + "`n";
                }
                else
                {
                    # Discard line - mismatching platform or arch or config
                }
            }
            else
            {
                # Copy any other line as is
                $content += $line + "`n";
            }
        }
        else
        {
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
