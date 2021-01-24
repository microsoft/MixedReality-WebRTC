
param(
    [string]$GitBranch,
    [string]$GitCommitSha1
)

# Get latest version of Universal Package 'libwebrtc' from 'mr-webrtc' feed
$versions = Invoke-RestMethod -Headers @{ Authorization = "Bearer $env:SYSTEM_ACCESSTOKEN" } -Uri "https://feeds.dev.azure.com/aipmr/MixedReality-WebRTC-CI/_apis/packaging/Feeds/mr-webrtc/Packages/7276ea84-464b-42ae-a37c-f2e2c56abcd4?api-version=6.1-preview.1"
if ([int]$versions.count -le 0) {
    throw "Cannot find any dependency package version. REST query returned:`n$versions"
}
$LatestVersion = ""
foreach ($details in $versions.value) {
    if ($details.isLatest) {
        $LatestVersion = $details.normalizedVersion
        break
    }
}
if (!$LatestVersion) {
    throw "Failed to retrieve versions for package 'libwebrtc'"
}
Write-Host "LatestVersion: $LatestVersion"

# Get branch name with the refs/heads/ prefix, e.g. 'master' or 'release/<x.y>' or 'user/<aaa>/<bbb>'
$GitBranch = $GitBranch.ToLowerInvariant()
if ($GitBranch.StartsWith("refs/heads/")) {
    $GitBranch = $GitBranch.Remove(0, 11)
}
Write-Host "GitBranch: $GitBranch"

# Get git commit SHA1 truncated to 8 chars
$GitCommitSha1 = $GitCommitSha1.ToLowerInvariant().Remove(8)
Write-Host "GitCommitSha1: $GitCommitSha1"

# Compute package version suffix based on source branch
$DepsPackageVersionSuffix = switch -Regex ($GitBranch) {
    "^release/" { "-release-" + $GitCommitSha1 }
    "^master$|^main$" { "-" + $GitCommitSha1 }
    Default { "-alpha-" + $GitCommitSha1 }
}
Write-Host "DepsPackageVersionSuffix: $DepsPackageVersionSuffix"

$DepsPackageVersion = $LatestVersion + $DepsPackageVersionSuffix
Write-Host "DepsPackageVersion: $DepsPackageVersion"

# Save the suffix
Write-Host "##vso[task.setvariable variable=DepsPackageVersion;]$DepsPackageVersion"
