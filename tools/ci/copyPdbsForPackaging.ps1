# Copy all PDBs into a single folder for packaging as Universal Package

param(
    [string]$CorePath,
    [string]$WrapperPath,
    [string]$WrapperGluePath,
    [string]$OutputPath
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

# Copy wrapper PDB (Org.WebRtc.pdb)
Write-Host "Copying PDB for Org.WebRtc.dll..."
Copy-Item -Path $(Join-Path $WrapperPath "Org.WebRtc.pdb") -Destination $OutputPath

# Copy wrapper glue PDB (Org.WebRtc.WrapperGlue.pdb)
Write-Host "Copying PDB for Org.WebRtc.WrapperGlue.lib..."
Copy-Item -Path $(Join-Path $WrapperGluePath "Org.WebRtc.WrapperGlue.pdb") -Destination $OutputPath
