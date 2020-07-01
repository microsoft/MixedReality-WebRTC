# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Modify some package.json NPM config file to store the package version

param(
    [string]$PackageJsonFile,
    [string]$PackageVersion
)

Write-Host "Modifying '$PackageJsonFile' for version '$PackageVersion'..."

$content = Get-Content -Path $PackageJsonFile -Raw -Encoding UTF8
$content = $content -replace "0.0.1-preview.4294967295", $PackageVersion
Set-Content -Path $PackageJsonFile -Value $content -Force -Encoding UTF8

Write-Host "== $PackageJsonFile ======================================="
Get-Content -Path $PackageJsonFile -Raw -Encoding UTF8
Write-Host "==========================================================="
