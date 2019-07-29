# Script to generate the part of the NOTICE file relative to the Chormium third-party dependencies
# found in external\webrtc-uwp-sdk\webrtc\xplatform\chromium\third_party.
# This script is not currently used, as WebRTC only imports a small subset of Chromium, found mostly
# in external\webrtc-uwp-sdk\webrtc\xplatform\webrtc\third_party and the submodules.

param (
    [Parameter(Mandatory)][string]$BasePath,
    [string]$ConfigFile
)

function Read-Config {
    param (
        [Parameter(Mandatory)][string]$ConfigFile
    )
    
    if (-not (Test-Path $ConfigFile))
    {
        Write-Host -ForegroundColor Red "ERROR: Config file $ConfigFile not found."
        return
    }

    $config = Get-Content $ConfigFile -Raw -Encoding UTF8 | ConvertFrom-Json

    return $config
}

function Generate-ChromiumThirdPartyNotice {
    param (
        [Parameter(Mandatory)][string]$BasePath,
        [PSCustomObject]$Config
    )

    $ignoreMap = @{}
    if ($Config)
    {
        foreach ($item in $Config.ignore)
        {
            $ignoreMap[$item.path] = $item.reason
        }
    }

    $table = @()
    $keys = @{}

    $components = Get-ChildItem -Path $BasePath -Directory
    foreach ($dir in $components)
    {
        if ($ignoreMap.ContainsKey($dir.ToString()))
        {
            $reason = $ignoreMap[$dir.ToString()]
            Write-Host -ForegroundColor Cyan "Component $dir ignored: $reason"
            continue
        }

        Write-Host -ForegroundColor Cyan "Component - $dir"

        $entry = @{ RelPath = $dir; Path = Join-Path $BasePath $dir }

        # Find the README.chromium file, which is mandatory in any Chromium third-party,
        # and conveniently lists some project properties like the license type and file.
        $readmeFile = Join-Path $BasePath $(Join-Path $dir "README.chromium")
        if (Test-Path $readmeFile)
        {
            $readme = Get-Content -Path $readmeFile -Encoding UTF8
            $fieldRegex = "^([A-Za-z ]+)\: *(.*)$"
            foreach ($line in $readme)
            {
                $result = $line -match $fieldRegex
                $fieldName = $Matches.1
                $fieldValue = $Matches.2
                if ($result)
                {
                    $entry[$fieldName] = $fieldValue
                    $keys[$fieldName] = 1
                    # Write-Host "$fieldName = $fieldValue"
                }
                else
                {
                    break
                }
            }
        }
        else
        {
            Write-Host -ForegroundColor Red "  ERROR: Cannot find README.chromium"
        }
        
        $table += $entry
    }

    $table | ForEach {[PSCustomObject]$_} | Format-Table -AutoSize | Out-String -Width 4096 | Out-File -FilePath NOTICEDATA.Chromium -Encoding UTF8

    Write-Host "`nBuilding NOTICE.Chromium..."

    # Build NOTICE.Chromium file content
    $content = ""
    foreach ($entry in $table)
    {
        $content += "-" * 80
        $content += "`n`nComponent.`n`n    $($entry.Name)`n`nPath.`n`n    $($entry.Path)`n`nOpen Source License/Copyright Notice.`n`n"
        
        Write-Host -NoNewLine -ForegroundColor Cyan "Component - $($entry.RelPath)    "

        # Append license file
        $licenseFile = $entry["License File"]
        $license = $entry["License"]
        if ($licenseFile)
        {
            if ($licenseFile -eq "NOT_SHIPPED")
            {
                Write-Host -NoNewLine -ForegroundColor Cyan "Skipping $($entry.Name) (not shipped)"
                Write-Host ""
                continue
            }

            $licenseFileName = Join-Path $entry.Path $licenseFile
            if (Test-Path $licenseFileName)
            {
                $licenseContent = Get-Content $licenseFileName -Encoding UTF8
                $content += $licenseContent | foreach {"   " + $_ + "`n"}
                Write-Host -NoNewLine -ForegroundColor Green "Added $licenseFile"
            }
            else
            {
                Write-Host -NoNewLine -ForegroundColor Red "License file $licenseFile not found!"
            }
        }
        elseif ($license)
        {
            $content += "    " + $license
            Write-Host -NoNewLine -ForegroundColor Yellow "Using $license as fallback"
        }
        else
        {
            Write-Host -NoNewLine -ForegroundColor Red "  Warning: Missing license for $($entry.Name)"
            $content += "    (unknown)"
        }

        $content += "`n`nAdditional Attribution (if any).`n`n"

        # Append authors file
        $authorsFile = Join-Path $entry.Path "AUTHORS"
        if (Test-Path $authorsFile)
        {
            $authorsContent = Get-Content $authorsFile -Encoding UTF8
            $content += $authorsContent | foreach {"    " + $_ + "`n"}
        }
        else
        {
            $content += "    -"
        }

        $content += "`n`n"

        Write-Host ""
    }

    # Write NOTICE.Chromium to disk
    $content | Out-File -FilePath NOTICE.Chromium -Encoding UTF8 
}

$config = @{}
if ($ConfigFile)
{
    Write-Host "Using config file $ConfigFile."
    $config = Read-Config $ConfigFile
}

Generate-ChromiumThirdPartyNotice -BasePath $BasePath -Config $config
