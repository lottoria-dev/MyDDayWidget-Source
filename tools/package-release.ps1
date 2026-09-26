$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$version = '3.1.6'
& (Join-Path $PSScriptRoot 'verify-source.ps1') -Configuration Release
$exe = Join-Path $root 'bin\x64\Release\DDay3.exe'
$artifacts = Join-Path $root 'artifacts'
$target = Join-Path $artifacts "v$version"
$stage = Join-Path $artifacts ('staging-' + [Guid]::NewGuid().ToString('N'))
New-Item $stage -ItemType Directory -Force | Out-Null
try {
    $portable = Join-Path $stage 'portable'
    New-Item $portable -ItemType Directory | Out-Null
    Copy-Item $exe (Join-Path $portable 'DDay3.exe')
    if (Test-Path ($exe + '.config')) { Copy-Item ($exe + '.config') (Join-Path $portable 'DDay3.exe.config') }
    foreach ($name in @('README.md', 'LICENSE.md', 'THIRD_PARTY_NOTICES.md', 'RELEASE_NOTES.md')) {
        Copy-Item (Join-Path $root $name) (Join-Path $portable $name)
    }
    $zipName = "DDay3-v$version-win-x64.zip"
    Compress-Archive -Path (Join-Path $portable '*') -DestinationPath (Join-Path $stage $zipName) -CompressionLevel Optimal
    Copy-Item (Join-Path $portable 'DDay3.exe') (Join-Path $stage 'DDay3.exe')
    $hashLines = foreach ($name in @('DDay3.exe', $zipName)) {
        $hash = (Get-FileHash (Join-Path $stage $name) -Algorithm SHA256).Hash
        "$hash  $name"
    }
    [IO.File]::WriteAllLines((Join-Path $stage 'DDay3_SHA256.txt'), [string[]]$hashLines, [Text.Encoding]::ASCII)
    Remove-Item $portable -Recurse -Force
    # Replace only this script's generated version folder after the full package is ready.
    if (Test-Path $target) { Remove-Item $target -Recurse -Force }
    Move-Item $stage $target
    Write-Host "[OK] Release files: $target"
}
finally {
    if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
}
