# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Compute the name and version of the Universal Package used for PDBs,
# and populate the relevant pipeline variables.

# Compute package name
# Note the restrictions on a Universal Package name (from error message):
#   Universal package names must be one or more lowercase alphanumeric
#   segments separated by a dash, dot or underscore. The package name
#   must be under 256 characters.
if (!$env:BUILDTRIPLE)
{
    $err = "Invalid build triple '$env:BUILDTRIPLE'"
    Write-Error $err
    Write-Host "##vso[task.complete result=Failed;]$err"
    exit 1
}
$PackageName = "mr-webrtc-core_pdbs_$env:BUILDTRIPLE".ToLowerInvariant()
if ($PackageName.Length -ge 256) {
    $err = "Package name too long: '$PackageName'"
    Write-Error $err
    Write-Host "##vso[task.complete result=Failed;]$err"
    exit 1
}
Write-Host "PDB package name : $PackageName"
Write-Host "##vso[task.setvariable variable=MRWebRTC_PdbPackageName]$PackageName"

# Compute version if needed, or check existing version
if ($env:WRITE_VERSION -eq 'true')
{
    # Compute package version based on build variables
    if ($env:MRWEBRTC_PDBPACKAGEVERSION)
    {
        # Normally this should compute $(MRWebRTC_PdbPackageVersion) based on the build's version
        # and optional release tag and build number. But the variable has also been already assigned
        # in the build pipelines variables, which generally indicates a misconfigured pipeline.
        $err = "Pipeline variable MRWebRTC_PdbPackageVersion is already set : '$env:MRWEBRTC_PDBPACKAGEVERSION'."
        Write-Error $err
        Write-Host "##vso[task.complete result=Failed;]$err"
        exit 1
    }
    if (!$env:MRWEBRTCVERSION)
    {
        $err = "Invalid build version '$env:MRWEBRTCVERSION'"
        Write-Error $err
        Write-Host "##vso[task.complete result=Failed;]$err"
        exit 1
    }
    if ($env:MRWEBRTCRELEASETAG)
    {
        $PackageVersion = "$env:MRWEBRTCVERSION-$env:MRWEBRTCRELEASETAG"
    }
    else
    {
        $PackageVersion = "$env:MRWEBRTCVERSION"
    }
    if ($env:MRWEBRTCWITHBUILDNUMBER -eq "true")
    {
        $PackageVersion = "$PackageVersion-$env:BUILD_BUILDNUMBER"
    }
    Write-Host "PDB package version (generated) : $PackageVersion"
    Write-Host "##vso[task.setvariable variable=MRWebRTC_PdbPackageVersion]$PackageVersion"
}
else
{
    # Read and check version from pipeline variables, but do not modify
    if (!$env:MRWEBRTC_PDBPACKAGEVERSION)
    {
        $err = "Invalid PDB package version '$env:MRWEBRTC_PDBPACKAGEVERSION'"
        Write-Error $err
        Write-Host "##vso[task.complete result=Failed;]$err"
        exit 1
    }
    Write-Host "PDB package version (from pipeline) : $env:MRWEBRTC_PDBPACKAGEVERSION"
}
