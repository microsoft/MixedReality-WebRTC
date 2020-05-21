# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Copy all PDBs into a single folder for packaging as Universal Package

param(
    [string]$CorePath,
    [string]$WrapperPath,
    [string]$WrapperGluePath,
    [string]$OutputPath,
    [ValidateSet('Debug','Release')]
    [string]$BuildConfig,
    [switch]$WithUwpWrapper
)

# Copied from https://github.com/microsoft/MixedReality-Sharing/blob/master/tools/ci/utils.ps1
function Ensure-Empty($DirectoryPath) {
    mkdir -Force "$DirectoryPath" | out-null
    Remove-Item "$DirectoryPath\*" -Force -Recurse
}

# Ensure the output path exists and is empty
Ensure-Empty $OutputPath

# Copy core PDBs
Write-Host "Copying PDBs for core webrtc.lib..."
Copy-Item -Path $(Join-Path $CorePath "*") -Destination $OutputPath -Include "*.pdb"

if ($WithUwpWrapper)
{
    # Copy wrapper PDB (Org.WebRtc.pdb)
    Write-Host "Copying PDB for Org.WebRtc.dll..."
    Copy-Item -Path $(Join-Path $WrapperPath "Org.WebRtc.pdb") -Destination $OutputPath

    # Copy wrapper glue PDB (Org.WebRtc.WrapperGlue.pdb)
    # In Release there is no PDB; the project does not specify /DEBUG so defaults to Debug-only PDBs.
    if ($BuildConfig -eq "Release") {
        Write-Host "Skipping PDB for Org.WebRtc.WrapperGlue.lib (not generated in Release)"
    } else {
        Write-Host "Copying PDB for Org.WebRtc.WrapperGlue.lib..."
        Copy-Item -Path $(Join-Path $WrapperGluePath "Org.WebRtc.WrapperGlue.pdb") -Destination $OutputPath
    }
}

# List content of output folder
Write-Host "Content of output folder $OutputPath"
foreach ($f in $(Get-ChildItem -Path $OutputPath -Recurse))
{
    Write-Host $f.FullName
}
