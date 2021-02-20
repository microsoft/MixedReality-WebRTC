# Integration test for mrwebrtc Desktop library
# - Create a Win32 console project
# - Restore the NuGet package mrwebrtc from disk
# - Add some simple code to start/stop a peer connection
# - Build and run Debug and Release configs
param (
    [Parameter(Mandatory = $true)]
    [string]$PackageVersion,

    [Parameter(Mandatory = $true)]
    [string]$SourceDirectory,

    [Parameter(Mandatory = $true)]
    [string]$TemplateDirectory
)

function New-TemporaryDirectory {
    $parent = [System.IO.Path]::GetTempPath()
    [string] $name = [System.Guid]::NewGuid()
    New-Item -ItemType Directory -Path (Join-Path $parent $name)
}

# Get source package
$packageFile = Join-Path $SourceDirectory "mrwebrtc.$PackageVersion.nupkg" -Resolve
if (!(Test-Path $packageFile)) {
    throw "Cannot find package file (.nupkg) version '$PackageVersion' in source directory '$SourceDirectory'"
}
Write-Host "Source package: $packageFile"

# Create test folder
$testDir = New-TemporaryDirectory
Write-Host "Test folder: $testDir"

Push-Location $testDir

# Copy test app template
Write-Host "Copying C++ project template $TemplateDirectory -> $testDir"
Copy-Item -Path $TemplateDirectory/* -Destination $testDir -Force -Recurse

# Copy package
Write-Host "Copying package $packageFile -> $testDir"
Copy-Item -Path $packageFile -Destination $testDir

# Write nuget.config
$nugetConfigFile = Join-Path $testDir "nuget.config"
Write-Host "NuGet config: $nugetConfigFile"
Write-Output @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
    <packageSources>
        <clear /> <!-- ensure only the sources defined below are used -->
        <add key="local" value="$testDir" />
    </packageSources>
</configuration>
"@ | Set-Content -Path $nugetConfigFile

# Restore packages
nuget restore .\packages.config -PackagesDirectory .\packages -Config .\nuget.config -NonInteractive

# Update the package version in the template project mrwebrtc_integration.vcxproj
$projectTemplate = Get-Content -Raw -Encoding UTF8 -Path .\mrwebrtc_integration.vcxproj
$projectTemplate = $projectTemplate -replace "__PACKAGE_VERSION__","$PackageVersion"
Set-Content -Path .\mrwebrtc_integration.vcxproj -Encoding UTF8 -Value $projectTemplate

# Update the package version in the packages.config
$projectTemplate = Get-Content -Raw -Encoding UTF8 -Path .\packages.config
$projectTemplate = $projectTemplate -replace "__PACKAGE_VERSION__","$PackageVersion"
Set-Content -Path .\packages.config -Encoding UTF8 -Value $projectTemplate

$buildConfigs = "Debug", "Release"
$buildArchs = "x86", "x64"

# Build
foreach ($arch in $buildArchs) {
    foreach ($config in $buildConfigs) {
        $platform = if ($arch -eq "x86") { "Win32" } else { $arch }
        Write-Host "Building ($arch $config)..."
        msbuild -interactive:False -property:"Configuration=$config;Platform=$platform" -t:build
        Write-Host "Building ($arch $config) done.`n"
    }
}

# Run
foreach ($arch in $buildArchs) {
    foreach ($config in $buildConfigs) {
        $platform = if ($arch -eq "x86") { "Win32" } else { $arch }
        Write-Host "Running ($arch $config)..."
        & .\bin\$arch\$config\mrwebrtc_integration.exe
        if ($LASTEXITCODE -ne 0) {
            throw "Integration test ($arch $config) failed with exit code $LASTEXITCODE"
        }
        Write-Host "Running ($arch $config) successful.`n"
    }
}
