# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Utility script to validate that release artifacts are correctly signed.

param (
    # Path to folder that contains signed NuGet packages
    [Parameter(Mandatory=$true)][string]$PackageFolder,
    # Check for .nupkg files in subdirectories
    [bool]$Recurse = $false,
    # Optional temporary directory where to extract the NuGet package content
    [string]$TempFolder = ""
)

if ($TempFolder -eq "") {
    $TempFolder = Join-Path $PackageFolder "extracted_packages"
}

$signtool = 'C:\Program Files (x86)\Windows Kits\10\App Certification Kit\signtool.exe'
$nugetTool = 'nuget' # This should be in %PATH%

$packages = Get-ChildItem "$PackageFolder\*.nupkg" -Recurse:$Recurse
if ($null -eq $packages) {
    Write-Host "No *.nupkg files found"
    exit 1
}

Add-Type -AssemblyName System.IO.Compression.FileSystem

$errors = $false
foreach ($package in $packages) {
    $packageFile = $package.FullName
    Write-Host "Checking package $packageFile..."

    # Check the .nupkg itself
    $outLog = New-Item -ItemType "file" -Path "$TempFolder\out.log" -Force
    $errLog = New-Item -ItemType "file" -Path "$TempFolder\err.log" -Force
    Start-Process $nugetTool -ArgumentList "verify -Signatures $packageFile" -Wait -RedirectStandardOutput $outLog -RedirectStandardError $errLog -WindowStyle Hidden
    if ($null -ne (Get-Content $errLog)) {
        Write-Output "Error while checking signature for $packageFile`n$(Get-Content $errLog)"
        $errors = $true
    } else {
        Get-Content $outLog | Write-Output
        Write-Host -ForegroundColor Green "Successfully checked signature for package $packageFile."
    }
    
    # Extract the .nupkg
    $newDir = Join-Path $TempFolder $package.BaseName
    Write-Host "Extracting package to $newDir..."
    if (Test-Path $newDir) {
        Write-Host "Temporary directory exists - deleting $newDir"
        Remove-Item -Recurse -Force -Path $newDir
    }
    [System.IO.Compression.ZipFile]::ExtractToDirectory($packageFile, $newDir)

    # Check the .nupkg contains a signature
    if (-not (Test-Path "$newDir\.signature.p7s")) {
        Write-Output Get-ChildItem $newDir
        Write-Error "No signature found for $($package.BaseName)"
        $errors = $true
    }

    # Check the content of the .nupkg
    Write-Host "Checking content of package $packageFile..."
    $filesToCheck = Get-ChildItem "$newDir" -Include *.dll,*.winmd -Recurse
    Write-Host "Found $($filesToCheck.Count) files to check for signing."
    foreach ($file in $filesToCheck) {
        Write-Host "Checking file $file..."
        $outLog = New-Item -ItemType "file" -Path "$TempFolder\out.log" -Force
        $errLog = New-Item -ItemType "file" -Path "$TempFolder\err.log" -Force
        Start-Process $signtool -ArgumentList "verify /pa $file" -Wait -RedirectStandardOutput $outLog -RedirectStandardError $errLog -WindowStyle Hidden
        if ($null -ne (Get-Content $errLog)) {
            Write-Error "Error while checking signature for $file`n$(Get-Content $errLog)"
            $errors = $true
        } else {
            Get-Content $outLog | Write-Output
            Write-Host -ForegroundColor Green "Successfully checked signature for file $file."
        }
    }
}

if ($errors) {
    Write-Host "Finished with errors."
    exit 1
} else {
    Write-Host "All tests successful."
    exit 0  # always set the return code, otherwise it is set to the exit code from the command that was ran last
}
