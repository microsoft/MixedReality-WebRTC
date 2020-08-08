# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Modify some package.json NPM config file to store the package version

param(
    [string]$PackageJsonFile,
    [string]$PackageVersion
)

Write-Host "Modifying '$PackageJsonFile' for version '$PackageVersion'..."

# Force absolute path for WriteAllLines(). See link below.
$PackageJsonFile = Resolve-Path $PackageJsonFile

$content = Get-Content -Path $PackageJsonFile -Raw -Encoding UTF8
$content = $content -replace "0.0.1-preview.4294967295", $PackageVersion
# Cannot use Set-Content; it doesn't support UTF8 without BOM, and Unity fails
# to load package.json if it has a BOM.
# https://stackoverflow.com/questions/5596982/using-powershell-to-write-a-file-in-utf-8-without-the-bom
$Utf8NoBomEncoding = New-Object System.Text.UTF8Encoding $False
[System.IO.File]::WriteAllLines($PackageJsonFile, $content, $Utf8NoBomEncoding)

Write-Host "== $PackageJsonFile ======================================="
Get-Content -Path $PackageJsonFile -Raw -Encoding UTF8
Write-Host "==========================================================="
