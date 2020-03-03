# Compute the NuGet / Universal package version and populate the
# $(MRWebRTC_PackageVersion) pipeline variable with it.

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
Write-Host "Package name : $PackageName"
Write-Host "##vso[task.setvariable variable=MRWebRTC_PackageName]$PackageName"

# Compute version
if (!$env:MRWEBRTCVERSION)
{
    $err = "Invalid package version '$env:MRWEBRTCVERSION'"
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
Write-Host "Package version : $PackageVersion"
Write-Host "##vso[task.setvariable variable=MRWebRTC_PackageVersion]$PackageVersion"
