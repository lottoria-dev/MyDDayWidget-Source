param([Parameter(Mandatory = $true)][string]$ClientJson)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$path = (Resolve-Path -LiteralPath $ClientJson).Path
if ((Get-Item -LiteralPath $path).Length -gt 65536) { throw 'OAuth file is too large.' }
$inputClient = Get-Content -LiteralPath $path -Encoding UTF8 -Raw | ConvertFrom-Json
if (-not $inputClient.installed -or $inputClient.web -or $inputClient.type -eq 'service_account') {
    throw 'Select a Google OAuth client of type Desktop app (installed).'
}
$clientId = [string]$inputClient.installed.client_id
if ($clientId -notmatch '\A[A-Za-z0-9._-]+\.apps\.googleusercontent\.com\z') { throw 'Invalid Google OAuth client ID.' }
# Copy only the native app identity. User access/refresh tokens and unrelated JSON fields cannot be bundled.
$clean = @{ installed = @{ client_id = $clientId; client_secret = [string]$inputClient.installed.client_secret } }
$directory = Join-Path $root 'Build'
New-Item -Path $directory -ItemType Directory -Force | Out-Null
$destination = Join-Path $directory 'GoogleOAuthClient.json'
[IO.File]::WriteAllText($destination, ($clean | ConvertTo-Json -Depth 3), (New-Object Text.UTF8Encoding($false)))
Write-Host '[OK] Official desktop OAuth identity configured locally. Rebuild Release; do not commit Build/GoogleOAuthClient.json.'
