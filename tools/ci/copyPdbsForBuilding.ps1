# Copy all PDBs from their packaging folder back to their original location

param(
    [string]$SourcePath,
    [string]$CorePath,
    [string]$WrapperPath,
    [string]$WrapperGluePath,
    [ValidateSet('Debug','Release')]
    [string]$BuildConfig
)

# List content of source folder
Write-Host "Content of source folder $SourcePath"
foreach ($f in $(Get-ChildItem -Path $SourcePath -Recurse))
{
    Write-Host $f.FullName
}

# Move wrapper PDB (Org.WebRtc.pdb)
Write-Host "Moving PDB for Org.WebRtc.dll..."
mkdir -Force $WrapperPath | out-null
Move-Item -Path $(Join-Path $SourcePath "Org.WebRtc.pdb") -Destination $WrapperPath

# Move wrapper glue PDB (Org.WebRtc.WrapperGlue.pdb)
# In Release there is no PDB; the project does not specify /DEBUG so defaults to Debug-only PDBs.
if ($BuildConfig -eq "Release") {
    Write-Host "Skipping PDB for Org.WebRtc.WrapperGlue.lib (not generated in Release)"
} else {
    Write-Host "Moving PDB for Org.WebRtc.WrapperGlue.lib..."
    mkdir -Force $WrapperGluePath | out-null
    Move-Item -Path $(Join-Path $SourcePath "Org.WebRtc.WrapperGlue.pdb") -Destination $WrapperGluePath
}

# Copy core PDBs
Write-Host "Moving PDBs for core webrtc.lib..."
mkdir -Force $CorePath | out-null
Move-Item -Path $(Join-Path $SourcePath "*") -Destination $CorePath -Include "*.pdb"
