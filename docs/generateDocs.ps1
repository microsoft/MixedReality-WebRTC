# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See LICENSE in the project root for license information.

# Script to generate the docs into build/docs/generated/ for local iteration.

param(
    # Serve the generated docs on a temporary web server @ localhost
    # The docs are not completely static, so will not work if not served.
    [switch]$serve = $false
)

# Clear output dir
Write-Host "Clear previous version from build/docs"
Remove-Item -Force -Recurse -ErrorAction Ignore ..\build\docs

# Install DocFX command-line tool
Write-Host "Installing DocFx in build/ using NuGet..."
nuget install docfx.console -o ..\build\
$DocFxDir = Get-ChildItem -Path ..\build | Where-Object {$_.PSIsContainer -eq $true -and $_.Name -match "docfx"} | Select-Object -first 1
$DocFxExe = Join-Path $DocFxDir "tools\docfx.exe"
Write-Host  "DocFx install : $DocFxExe"

# Generate the documentation
Invoke-Expression "$($DocFxDir.FullName)\tools\docfx.exe docfx.json --intermediateFolder ..\build\docs\obj -o ..\build\docs $(if ($serve) {' --serve'} else {''})"
Write-Host "Documentation generated in build/docs/generated."

# Clean-up obj/xdoc folders in source -- See https://github.com/dotnet/docfx/issues/1156
$XdocDirs = Get-ChildItem -Path ..\libs -Recurse | Where-Object {$_.PSIsContainer -eq $true -and $_.Name -eq "xdoc"}
foreach ($Xdoc in $XdocDirs)
{
    if ($Xdoc.Parent -match "obj")
    {
        Write-Host "Deleting $($Xdoc.FullName)"
        Remove-Item -Force -Recurse -ErrorAction Ignore $Xdoc.FullName
    }
}
