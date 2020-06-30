# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

param(
    [string]$Target
)

$content = @"
registry=$env:NPM_PUBLISH_REGISTER
_auth=$env:NPM_PUBLISH_AUTH
email=$env:NPM_PUBLISH_EMAIL
always-auth=true
"@
Set-Content -Path "$Target" -Value $content -Encoding UTF8
