[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$Version = '0.0.0',
    [string]$PackageRoot,
    [string]$OutputDirectory,
    [switch]$InstallInnoSetupIfMissing
)

# Builds the per-user Windows installer around the verified portable package.
# The installer never requires administrator rights and always installs into a
# writable folder, because LocalDraft keeps all runtime data inside its own
# application directory.

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path $RepositoryRoot).Path
if (-not $PackageRoot) { $PackageRoot = Join-Path $root 'dist\LocalDraft-Portable-win-x64' }
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $root 'dist' }

if (-not (Test-Path (Join-Path $PackageRoot 'LocalDraft.exe'))) {
    throw "Det verifierade paketet saknas i $PackageRoot. Kor tools\build-portable.ps1 forst."
}

function Get-InnoSetupCompiler {
    $command = Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }
    $candidates = @(
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
    )
    foreach ($candidate in $candidates) {
        if ($candidate -and (Test-Path $candidate)) { return $candidate }
    }
    return $null
}

$compiler = Get-InnoSetupCompiler
if (-not $compiler -and $InstallInnoSetupIfMissing) {
    Write-Host 'Inno Setup saknas. Installerar via Chocolatey...'
    choco install innosetup --no-progress --yes
    if ($LASTEXITCODE -ne 0) { throw 'Kunde inte installera Inno Setup.' }
    $compiler = Get-InnoSetupCompiler
}

if (-not $compiler) {
    throw 'Inno Setup 6 (ISCC.exe) hittades inte. Installera Inno Setup eller kor med -InstallInnoSetupIfMissing.'
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$script = Join-Path $root 'packaging\LocalDraft.iss'

# Windows file version metadata only accepts numbers, so strip any prerelease
# suffix such as "-main.42" before handing the value to the compiler.
$numericVersion = ($Version -split '[-+]')[0]
if ($numericVersion -notmatch '^\d+(\.\d+){0,3}$') { $numericVersion = '0.0.0' }

& $compiler "/DAppVersion=$Version" "/DAppNumericVersion=$numericVersion" "/DPackageRoot=$PackageRoot" "/O$OutputDirectory" $script
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup misslyckades.' }

$installer = Join-Path $OutputDirectory 'LocalDraft-Setup-win-x64.exe'
if (-not (Test-Path $installer)) { throw "Installationsprogrammet skapades inte: $installer" }

$size = [Math]::Round((Get-Item $installer).Length / 1GB, 2)
Write-Host "Installationsprogram skapat: $installer ($size GB)"
