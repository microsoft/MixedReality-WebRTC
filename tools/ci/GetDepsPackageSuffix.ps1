
[cmdletbinding()]
param(
    [string]$GitBranch,
    [string]$GitCommitSha1
)

# Parse a SemVer version X.Y or X.Y.Z with optional prerelease/metadata and
# return the (major,minor,patch) triplet with patch=0 if the input did not
# contain any patch version.
# Examples:
#   "2.4.5-pre" => 2, 4, 5
#   "1.2+ds"    => 1, 2, 0
function Get-SemVer([string]$str) {
    $str -match '^(\d+)\.(\d+)(\.(\d+))?' | Out-Null
    if ($matches.Count -lt 3) {
        throw "Failed to parse string for SemVer: $str"
    }
    $major = [int]$matches[1]
    $minor = [int]$matches[2]
    $patch = [int]0
    if ($matches.Count -ge 5) {
        $patch = [int]$matches[4]
    }
    $major, $minor, $patch
}

# Compare some SemVer triplets, return -1/0/1
function Compare-SemVer([int[]]$ver1, [int[]]$ver2) {
    if ($ver1[0] -lt $ver2[0]) {
        -1
    } elseif ($ver1[0] -gt $ver2[0]) {
        1
    } else {
        if ($ver1[1] -lt $ver2[1]) {
            -1
        } elseif ($ver1[1] -gt $ver2[1]) {
            1
        } else {
            if ($ver1[2] -lt $ver2[2]) {
                -1
            } elseif ($ver1[2] -gt $ver2[2]) {
                1
            } else {
                0
            }
        }
    }
}

# Get the "height" of HEAD since a given commit, that is the number of
# commits on the local branch pointed by HEAD since the given commit was
# made, following only the first parent (so, ignoring merges).
function Get-GitHeightSinceCommit($commitHash) {
    [int]$height = & git rev-list --first-parent --count "$commitHash..HEAD"
    return $height
}

# Get the git commit hash (SHA1) of the specified commit-ish reference, which
# can be a branch or tag name, or a delta from another commit (e.g. "HEAD~2").
function Get-GitCommitHash([string]$gitRef) {
    $gitCommitHash = & git rev-parse --short=8 $gitRef
    return $gitCommitHash
}

# Retrieve the latest release by looking at git branches named 'release/X.Y'
# and finding the one with the higher version.
# Return an object:
#   Sha1    = string of git commit SHA1
#   Name    = string of full branch ref, e.g. 'refs/heads/release/X.Y'
#   Version = array of (major, minor) versions as integer
function Get-LatestRelease() {
    # List all branches
    $refs = &git show-ref
    if ($LASTEXITCODE -ne 0) {
        throw "List refs failed with error code $_"
    }
    $refs = $refs | ForEach-Object {
        $kv = $_ -split " "
        [pscustomobject]@{
            Sha1 = $kv[0]
            Name = $kv[1]
        }
    }

    # Truncate to release branches only and parse version from branch name
    $releaseRefsPrefix = "refs/remotes/origin/release/"
    $releases = $refs | Where-Object { $_.Name.StartsWith($releaseRefsPrefix) }
    $releases | ForEach-Object {
        $ver = Get-SemVer $_.Name.Remove(0, $releaseRefsPrefix.Length)
        $_ | Add-Member -NotePropertyName Version -NotePropertyValue $ver
        Write-Host "Found release version: $($ver[0]).$($ver[1])"
    }

    # Find latest version
    $latestVersion = 0, 0
    $latestVersionObj = $null
    $releases | ForEach-Object {
        if ($_.Version[0] -gt $latestVersion[0]) {
            $latestVersion[0] = $_.Version[0]
            $latestVersionObj = $_
        } elseif ($_.Version[0] -eq $latestVersion[0]) {
            if ($_.Version[1] -gt $latestVersion[1]) {
                $latestVersion[1] = $_.Version[1]
                $latestVersionObj = $_
            }
        }
    }
    if (!$latestVersionObj) {
        throw "Cannot find latest version"
    }
    $latestVersionObj
}

# Return the version of the latest published 'libwebrtc' Universal Package from the 'mr-webrtc' feed
# which matches the given major and minor versions, if any.
function Get-LatestPublished([int]$major, [int]$minor) {
    # Use the REST API to query the list of versions
    Write-Verbose "Querying ADO for list of versions of Universal Package 'libwebrtc' from feed 'mr-webrtc'..."
    $response = Invoke-RestMethod `
        -Method Get `
        -Headers @{ Authorization = "Bearer $env:SYSTEM_ACCESSTOKEN" } `
        -Uri "https://feeds.dev.azure.com/aipmr/MixedReality-WebRTC-CI/_apis/packaging/Feeds/mr-webrtc/Packages/7276ea84-464b-42ae-a37c-f2e2c56abcd4/versions?isDeleted=false&api-version=6.1-preview.1"
    Write-Verbose "REST response: $response"
    $allVersions = $response.value
    Write-Verbose "All Versions = $allVersions"
    if (!$allVersions) {
        return ""
    }

    # Filter by major/minor
    $filteredVersions = $allVersions | Where-Object { $_.normalizedVersion.StartsWith("$major.$minor") }
    Write-Verbose "Versions Like $major.$minor = $filteredVersions"
    if (!$filteredVersions) {
        return ""
    }

    # Find latest version
    $latestVersion = -1, 0, 0
    $latestVersionObj = $null
    $filteredVersions | ForEach-Object {
        $ver = Get-SemVer $_.normalizedVersion
        if ($ver[0] -gt $latestVersion[0]) {
            $latestVersion[0] = $ver[0]
            $latestVersionObj = $_
        } elseif ($ver[0] -eq $latestVersion[0]) {
            if ($ver[1] -gt $latestVersion[1]) {
                $latestVersion[1] = $ver[1]
                $latestVersionObj = $_
            } elseif ($ver[1] -eq $latestVersion[1]) {
                if ($ver[2] -gt $latestVersion[2]) {
                    $latestVersion[2] = $ver[2]
                    $latestVersionObj = $_
                }
            }
        }
    }
    if (!$latestVersionObj) {
        throw "Cannot find latest version"
    }

    # Return the "normalized" version, whatever that is
    #$latestVersionObj.normalizedVersion

    # Return the version triplet
    $latestVersion
}



# Get branch name with the refs/heads/ prefix, e.g. 'master' or 'release/<x.y>' or 'user/<aaa>/<bbb>'
$GitBranch = $GitBranch.ToLowerInvariant()
if ($GitBranch.StartsWith("refs/heads/")) {
    $GitBranch = $GitBranch.Remove(0, 11)
}
Write-Host "GitBranch: $GitBranch"

# Get git commit SHA1 truncated to 8 chars
if ($GitCommitSha1.Length -lt 8) {
    throw "Invalid git commit hash $GitCommitSha1 with less than 8 characters"
}
$GitCommitSha1 = $GitCommitSha1.ToLowerInvariant()
if ($GitCommitSha1.Length -gt 8) {
    $GitCommitSha1 = $GitCommitSha1.Remove(8)
}
Write-Host "GitCommitSha1: $GitCommitSha1"

# Compute package version suffix based on source branch
$DepsPackageVersion = switch -Regex ($GitBranch) {
    "^release/" {
        # On release, tag with the same version as the release, and increment patch version.
        # E.g. if latest published is 2.0.3 then new package version is 2.0.4.
        # FIXME - We should use the actual latest patch published (retrieved from git tags)!!!!!
        $rel = $GitBranch -replace '^release/(\d+)\.(\d+)', '$1 $2' -split " "
        $relMajor = [int]$rel[0]
        $relMinor = [int]$rel[1]
        Write-Host "Detected release branch for version $relMajor.$relMinor"
        $latest = Get-LatestPublished $relMajor $relMinor
        if ($latest[0] -ge 0) {
            $relPatch = $latest[2] + 1
        } else {
            # First package for this release
            $relPatch = 0
        }
        "$relMajor.$relMinor.$relPatch+$GitCommitSha1"
    }
    "^master$|^main$" {
        # On main, get latest release and assume main is a minor update from it.
        $release = Get-LatestRelease
        $relMajorLatest = $release.Version[0]
        $relMinorLatest = $release.Version[1]
        $relHashLatest = Get-GitCommitHash "refs/remotes/origin/release/$relMajorLatest.$relMinorLatest"
        if ($false) { # TODO : if(isBreaking)
            $relMajor = $relMajorLatest + 1
            $relMinor = $relMinorLatest
        } else {
            $relMajor = $relMajorLatest
            $relMinor = $relMinorLatest + 1
        }
        $relPatch = 0
        Write-Host "On main branch, assuming minor update: $relMajorLatest.$relMinorLatest.x -> $relMajor.$relMinor.$relPatch"
        # Compute beta suffix using git height
        # Add git commit SHA1 as build metadata (does not participate in versioning)
        $height = Get-GitHeightSinceCommit $relHashLatest
        Write-Host "Commit $GitCommitSha1 is $height commits ahead of origin/release/$relMajorLatest.$relMinorLatest ($relHashLatest)"
        $height = "$height".PadLeft(4, [char]'0')
        $suffix = "beta$height+$GitCommitSha1"
        "$relMajor.$relMinor.$relPatch-$suffix"
    }
    Default {
        # On other branches, same as on main but with alpha tag AND branch name to discriminate
        $release = Get-LatestRelease
        $relMajorLatest = $release.Version[0]
        $relMinorLatest = $release.Version[1]
        $relHashLatest = Get-GitCommitHash "refs/remotes/origin/release/$relMajorLatest.$relMinorLatest"
        if ($false) { # TODO : if(isBreaking)
            $relMajor = $relMajorLatest + 1
            $relMinor = $relMinorLatest
        } else {
            $relMajor = $relMajorLatest
            $relMinor = $relMinorLatest + 1
        }
        $relPatch = 0
        Write-Host "On topic branch $GitBranch, assuming minor update: $relMajorLatest.$relMinorLatest -> $relMajor.$relMinor.$relPatch"
        # Compute beta suffix using git height
        # Add git commit SHA1 as build metadata (does not participate in versioning)
        $height = Get-GitHeightSinceCommit $relHashLatest
        Write-Host "Commit $GitCommitSha1 is $height commits ahead of origin/release/$relMajorLatest.$relMinorLatest ($relHashLatest)"
        $height = "$height".PadLeft(4, [char]'0')
        # Use latest branch item as short branch name.
        $branchStems = $GitBranch -split "/"
        $branchShortName = $branchStems[$branchStems.Count - 1]
        # Include both short branch name AND commit SHA1 in prerelease suffix to avoid any collision
        $suffix = "alpha$height-$branchShortName-$GitCommitSha1"
        "$relMajor.$relMinor.$relPatch-$suffix"
    }
}
Write-Host "DepsPackageVersion: $DepsPackageVersion"

# Save the suffix
Write-Host "##vso[task.setvariable variable=DepsPackageVersion;]$DepsPackageVersion"
