[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),
    [Parameter(Mandatory)] [string]$Version
)

# Collects the downloadable end-user assets into artifacts\release and writes
# SHA-256 checksums so downloads can be verified before installation.

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path $RepositoryRoot).Path
$release = Join-Path $root 'artifacts\release'

if (Test-Path $release) { Remove-Item -Recurse -Force $release }
New-Item -ItemType Directory -Force -Path $release | Out-Null

$sources = [ordered]@{
    (Join-Path $root 'LocalDraft-Portable-win-x64.zip') = "LocalDraft-$Version-portable-win-x64.zip"
    (Join-Path $root 'dist\LocalDraft-Setup-win-x64.exe') = "LocalDraft-$Version-setup-win-x64.exe"
}

foreach ($source in $sources.Keys) {
    if (-not (Test-Path $source)) { throw "Slutanvandarfilen saknas: $source" }
    Copy-Item $source (Join-Path $release $sources[$source]) -Force
}

$lines = Get-ChildItem $release -File | Sort-Object Name | ForEach-Object {
    $hash = (Get-FileHash -Algorithm SHA256 $_.FullName).Hash.ToLowerInvariant()
    "$hash  $($_.Name)"
}
$checksums = Join-Path $release 'SHA256SUMS.txt'
Set-Content -Path $checksums -Value $lines -Encoding ascii

Copy-Item (Join-Path $root 'packaging\LAS-MIG-FORST.txt') $release -Force

Get-ChildItem $release -File | ForEach-Object {
    Write-Host ("{0,-52} {1,8:N0} MB" -f $_.Name, ($_.Length / 1MB))
}
