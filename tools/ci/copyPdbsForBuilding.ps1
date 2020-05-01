# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Copy all PDBs from their packaging folder back next to webrtc.lib
# Note that this is not their original location, but this is where
# the linker currently looks for them.

param(
    [string]$SourcePath,
    [string]$OutputPath
)

# Move all PDBs
Write-Host "Moving PDBs..."
mkdir -Force $OutputPath | out-null
Move-Item -Path $(Join-Path $SourcePath "*") -Destination $OutputPath -Include "*.pdb"

# List content of output folder
Write-Host "Content of output folder $OutputPath"
foreach ($f in $(Get-ChildItem -Path $OutputPath -Recurse))
{
    Write-Host $f.FullName
}
