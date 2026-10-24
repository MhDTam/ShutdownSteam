param([string]$Version = '1.0.0')
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+(-[A-Za-z0-9.-]+)?$') { throw 'Version must be a semantic version such as 1.0.0' }
$root = $PSScriptRoot
& (Join-Path $root 'build.ps1')
$release = Join-Path $root 'release'
New-Item -ItemType Directory -Force -Path $release | Out-Null
$archive = Join-Path $release "ShutdownSteam-$Version-Windows-Portable.zip"
$files = @(
    (Join-Path $root 'portable\ShutdownSteam.exe'),
    (Join-Path $root 'docs\PORTABLE-README.txt'),
    (Join-Path $root 'LICENSE'),
    (Join-Path $root 'THIRD_PARTY_NOTICES.md'),
    (Join-Path $root 'CHANGELOG.md')
)
Compress-Archive -LiteralPath $files -DestinationPath $archive -Force
$checksums = @($archive, (Join-Path $root 'portable\ShutdownSteam.exe')) | ForEach-Object {
    $hash = Get-FileHash -LiteralPath $_ -Algorithm SHA256
    $hash.Hash.ToLowerInvariant() + '  ' + [System.IO.Path]::GetFileName($_)
}
$checksums | Set-Content -LiteralPath (Join-Path $release 'SHA256SUMS.txt') -Encoding Ascii
Get-Item -LiteralPath $archive | Select-Object FullName, Length
