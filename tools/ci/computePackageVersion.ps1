# Compute the NuGet / Universal package version and populate the
# $(MRWebRTC_PackageVersion) pipeline variable with it.

# Compute the version based on pipeline variables
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

# Set pipeline variables
Write-Host "##vso[task.setvariable variable=MRWebRTC_PackageVersion]$PackageVersion"
