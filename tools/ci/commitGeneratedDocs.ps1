# Commit a generated documentation to the gh-pages documentation branch
# For the master branch, this commits the documentation at the root of
# the branch. For other branches, this commits it under the verions/ folder.

param(
    [Parameter(Position=0)]
    [string]$SourceBranch
)

# Strip the source branch from refs/heads/ if any.
# In general this is coming from Build.SourceBranch which has it.
# Build.SourceBranchName cannot be used because it strips also the subfolders.
# https://docs.microsoft.com/en-us/azure/devops/pipelines/build/variables?view=azure-devops&tabs=yaml#build-variables
$SourceBranch = ($SourceBranch -Replace "^refs/heads/","")
Write-Host "Source branch: '$SourceBranch'"

# Create some authentication tokens to be able to connect to Azure DevOps to get changes and to GitHub to push changes
Write-Host "Create auth tokens to connect to GitHub and Azure DevOps"
$Authorization = "Basic " + [System.Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes("${env:GITHUB_USER}:${env:GITHUB_PAT}"))
git config --global --add "http.https://github.com/.extraheader" "AUTHORIZATION: $Authorization"
git config --global --add "http.https://microsoft.visualstudio.com.extraheader" "AUTHORIZATION: Bearer $env:SYSTEM_ACCESSTOKEN"

# Set common git configs for new commits
Write-Host "Set commit author to '${env:GITHUB_NAME} <${env:GITHUB_USER}>'"
git config --global user.email ${env:GITHUB_USER}
git config --global user.name ${env:GITHUB_NAME}

# Check that source branch exists
# Note that the Azure DevOps checkout is a specific commit not a branch,
# so the local repository is in detached HEAD. To test if the branch exists,
# use the remote one from the 'origin' remote.
Write-Host "Resolve source branch commit SHA1"
$output = ""
Invoke-Expression "git rev-parse --verify `"refs/remotes/origin/$SourceBranch^{commit}`"" | Tee-Object -Variable output | Out-Null
if (-not $output)
{
    Write-Host "Unknown branch '$SourceBranch'"
    Write-Host "##vso[task.complete result=Failed;]Unknown branch $SourceBranch."
    exit 1
}

# Get info about last change associated with the new generated docs
Write-Host "Get the SHA1 and title of the most recent commit"
$commitSha = git log -1 --pretty=%H
$commitTitle = git log -1 --pretty=%s
Write-Host "${commitSha}: $commitTitle"

# Clean the _docs/ folder to avoid any interaction with a previous build if the agent is not clean
Write-Host "Clean output folder '_docs/' if it exists"
Remove-Item ".\_docs" -Force -Recurse -ErrorAction Ignore

# Compute the source and destination folders
$DestFolder = ".\_docs\versions\$SourceBranch\"
if ($SourceBranch -eq "master")
{
    # The master branch is the default version at the root of the website
    $DestFolder = ".\_docs\"
}
$output = ""
Invoke-Expression "git rev-parse --verify `"refs/remotes/origin/gh-pages^{commit}`"" | Tee-Object -Variable output | Out-Null
if (-not $output)
{
    Write-Host "Missing docs branch 'gh-pages'"
    Write-Host "##vso[task.complete result=Failed;]Missing docs branch 'gh-pages'."
    exit 1
}
Write-Host "Destination folder: $DestFolder"

# Clone the destination branch locally in a temporary folder.
# Note that this creates a second copy of the repository inside itself.
# This will be used to only commit changes to that gh-pages branch which
# contains only generated documentation-related files, and not the code.
Write-Host "Clone the generated docs branch"
git clone https://github.com/Microsoft/MixedReality-WebRTC.git --branch gh-pages "$DestFolder"

# Delete all the files in this folder, so that files deleted in the new version
# of the documentation are effectively deleted in the commit.
Write-Host "Delete currently committed version"
Get-ChildItem "$DestFolder" -Recurse | Remove-Item -Force -Recurse

# Copy the newly-generated version of the docs
Write-Host "Copy new generated version"
Copy-Item ".\build\docs\generated\*" -Destination "$DestFolder" -Force -Recurse

# Move inside the target folder
Set-Location "$DestFolder"

# Check for any change compared to previous version (if any)
Write-Host "Check for changes"
if (git status --short)
{
  # Add everything. Because the current directory is _docs, this is everything from
  # the point of view of the sub-repo inside _docs, so this ignores all changes outside
  # this directory and retain only generated docs changes, which is exactly what we want.
  git add --all
  git commit -m "Generated docs for commit $commitSha ($commitTitle)"
  #git push origin $DestBranch # TEMP -- For now just a dry run (don't push to GitHub)
  Write-Host "Docs changes committed"
}
else
{
  Write-Host "Docs are up to date"
}
