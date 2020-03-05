# Generate a custom packages.config to restore only the necessary Core
# NuGet packages for the given build triple, to save on disk space

param(
    [ValidateSet('Win32','UWP')]
    [string]$BuildPlatform,
    [ValidateSet('x86','x64','ARM')]
    [string]$BuildArch,
    [ValidateSet('Debug','Release')]
    [string]$BuildConfig,
    [string]$InputFile,
    [string]$OutputFile
)

Write-Host "Generating packages.config for build triple $BuildPlatform-$BuildArch-$BuildConfig..."

$reader = [System.IO.File]::OpenText($InputFile)
$content = ""
try {
    for() {
        $line = $reader.ReadLine()
        if ($line -eq $null) { break }
        if ($line -match "Microsoft\.MixedReality\.WebRTC\.Native\.Core\.(Desktop|UWP|WinRT)\.")
        {
            if ($line -match "Microsoft\.MixedReality\.WebRTC\.Native\.Core\.(Desktop|UWP|WinRT)\.$BuildArch\.$BuildConfig")
            {
                $linePlatform = $Matches.1 # Desktop|UWP|WinRT
                if ((($linePlatform -eq 'Desktop') -and ($BuildPlatform -eq 'Win32')) -or (($linePlatform -ne 'Desktop') -and ($BuildPlatform -eq 'UWP')))
                {
                    # Copy line for matching build triple as is
                    $content += $line + "`n";
                }
                else
                {
                    # Discard line - mismatching platform
                }
            }
            else
            {
                # Discard line - mismatching arch/config
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
Write-Output $content | Set-Content -Path $OutputFile -Encoding UTF8

Write-Host "== $OutputFile ======================================="
Get-Content -Encoding UTF8 -Path "$OutputFile"
Write-Host "=========================================================="

# Write the filename of the new packages.config file to $(PackagesConfigFile) for later use
Write-Host "##vso[task.setvariable variable=PackagesConfigFile]$OutputFile"
