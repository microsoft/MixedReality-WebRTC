# Commit a generated documentation to its docs/* branch

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
Invoke-Expression "git rev-parse --verify `"refs/remotes/origin/$SourceBranch^{commit}`"" | Tee-Object -Variable output | Write-Host
if (-not $output)
{
    Write-Host "Unknown branch '$SourceBranch'"
    Write-Host "##vso[task.complete result=Failed;]Unknown branch $SourceBranch."
    exit 1
}

# Clean the _docs/ folder to avoid any interaction with a previous build if the agent is not clean
Write-Host "Clean output folder '_docs/' if it exists"
Remove-Item ".\_docs" -Force -Recurse -ErrorAction Ignore

# Create or clone destination branch
$DestBranch = "docs/$SourceBranch"
$output = ""
Invoke-Expression "git rev-parse --verify `"refs/remotes/origin/$DestBranch^{commit}`"" | Tee-Object -Variable output | Write-Host
if ($output)
{
    # Clone the destination branch locally in a temporary folder.
    # Note that this creates a second copy of the repository inside itself.
    # This is fine as long as we stay out of the _docs directory to keep using
    # the outer repository and its config we set above.
    Write-Host "Clone the generated docs branch"
    git clone https://github.com/Microsoft/MixedReality-WebRTC.git --branch $DestBranch ".\_docs"
    
    # Delete all the files in this folder, so that files deleted in the new version
    # of the documentation are effectively deleted in the commit.
    Write-Host "Delete currently committed version"
    Get-ChildItem ".\_docs" -Recurse | Remove-Item -Force -Recurse
}
else
{
    # Create the destination branch and checkout locally in a temporary folder.
    # Note that the orphan branch is created inside the main repository,
    # and unlike the case above there's no inner repository created.
    Write-Host "Creating new destination branch $DestBranch"
    New-Item ".\_docs" -ItemType Directory
    git checkout --orphan "$DestBranch"
}

# Because the _docs folder is now empty, always re-generate the README.md
# This could be skipped if existing, but allows upgrading to a newer template if needed
Write-Host "Generate README.md:"
(Get-Content -Path docs/README.template.md -Encoding UTF8) -Replace '\$branchname',"$SourceBranch" | Set-Content -Path _docs/README.md -Encoding UTF8
Write-Host "--------------------"
Get-Content -Path _docs/README.md -Encoding UTF8 | Write-Host
Write-Host "--------------------"
Write-Host "Commit README.md"
git add _docs/README.md
git commit -m "Add README.md for generated branch $DestBranch"
git log -1 --format=full

# Get info about last change associated with the new generated docs
Write-Host "Get the SHA1 and title of the most recent commit"
$commitSha = git log -1 --pretty=%H
$commitTitle = git log -1 --pretty=%b
Write-Host "${commitSha}: $commitTitle"

# Copy the newly-generated version of the docs
Write-Host "Copy new generated version"
Copy-Item ".\build\docs\generated\*" -Destination ".\_docs" -Force -Recurse

# Check for any change compared to previous version (if any)
Write-Host "Check for changes"
Set-Location ".\_docs"
if (git status --short)
{
  git add --all
  git commit -m "Docs for commit $commitSha ($commitTitle)"
  git push origin $DestBranch
  Write-Host "Docs changes committed"
}
else
{
  Write-Host "Docs are up to date"
}
