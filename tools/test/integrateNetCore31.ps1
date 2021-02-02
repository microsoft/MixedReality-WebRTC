# Integration test for C# library Desktop (.NET Core 3.1)
# - Create a .NET Core console project
# - Restore the NuGet package Microsoft.MixedReality.WebRTC from disk
# - Add some simple code to start/stop a peer connection
# - Build and run Debug and Release configs
param (
    [Parameter(Mandatory = $true)]
    [string]$PackageVersion,
    
    [Parameter(Mandatory = $true)]
    [string]$SourceDirectory
)

function New-TemporaryDirectory {
    $parent = [System.IO.Path]::GetTempPath()
    [string] $name = [System.Guid]::NewGuid()
    New-Item -ItemType Directory -Path (Join-Path $parent $name)
}

# Get source package
$packageFile = Join-Path $SourceDirectory "Microsoft.MixedReality.WebRTC.$PackageVersion.nupkg" -Resolve
if (!(Test-Path $packageFile)) {
    throw "Cannot find package file (.nupkg) version '$PackageVersion' in source directory '$SourceDirectory'"
}
Write-Host "Source package: $packageFile"

# Create test folder
$testDir = New-TemporaryDirectory
Write-Host "Test folder: $testDir"

Push-Location $testDir

# Create test app : .NET Core 3.1 with C# 7.2
dotnet new console --language "C#" --name "sigma" --output $testDir --framework "netcoreapp3.1" --langVersion "7.2" --no-restore

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

# List NuGet sources for debugging
dotnet nuget list source --format Detailed

# Add reference to Microsoft.MixedReality.WebRTC (Desktop)
dotnet add package "Microsoft.MixedReality.WebRTC" --version $PackageVersion

# Restore packages
dotnet restore --force --force-evaluate --verbosity detailed --source $testDir

# Add some code that references the package
$programFile = Join-Path $testDir "Program.cs" -Resolve
(Get-Content -Path $programFile) | ForEach-Object {
    switch -regex ($_) {
        '^using System;.*' {
            @"
using System;
using Microsoft.MixedReality.WebRTC;
"@
        }
        'Console\.WriteLine.*' {
            @"
Console.WriteLine("Creating peer connection...");
var pc = new PeerConnection();
Console.WriteLine("Peer connection created.");
pc.Dispose();
Console.WriteLine("Test program terminated.");
"@
        }
        default   { $_ }
    }
} | Set-Content -Path $programFile

# Build
Write-Host "Building (Debug)..."
dotnet build --configuration Debug
Write-Host "Building (Release)..."
dotnet build --configuration Release

# Run
Write-Host "Running (Debug)..."
dotnet run --configuration Debug
Write-Host "Running (Release)..."
dotnet run --configuration Release
