[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path $RepositoryRoot).Path
$publish = Join-Path $root 'dist\LocalDraft-Portable-win-x64'
$zip = Join-Path $root 'LocalDraft-Portable-win-x64.zip'

if (-not (Test-Path (Join-Path $root 'Models\Whisper\ggml-small-q5_1.bin')) -or
    -not (Test-Path (Join-Path $root 'Models\Text\Qwen3-1.7B-Q4_K_M.gguf')) -or
    -not (Test-Path (Join-Path $root 'Native\Whisper\whisper-cli.exe'))) {
    throw 'Modeller eller whisper.cpp saknas. Kor tools\fetch-models.ps1 forst.'
}

dotnet restore (Join-Path $root 'LocalDraft.slnx') --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'dotnet restore misslyckades.' }
dotnet build (Join-Path $root 'LocalDraft.slnx') -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'dotnet build misslyckades.' }
if (-not $SkipTests) {
    dotnet test (Join-Path $root 'LocalDraft.slnx') -c Release --no-build
    if ($LASTEXITCODE -ne 0) { throw 'dotnet test misslyckades.' }
}

if (Test-Path $publish) {
    Remove-Item -Recurse -Force $publish
}
dotnet publish (Join-Path $root 'src\LocalDraft.App\LocalDraft.App.csproj') `
    -c Release -r win-x64 --self-contained true --no-build -o $publish
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish misslyckades.' }

Copy-Item (Join-Path $root 'Models') $publish -Recurse -Force
New-Item -ItemType Directory -Force -Path (Join-Path $publish 'Native') | Out-Null
$nativePublish = Join-Path $publish 'Native\Whisper'
New-Item -ItemType Directory -Force -Path $nativePublish | Out-Null
Copy-Item (Join-Path $root 'Native\Whisper\*.dll') $nativePublish -Force
Copy-Item (Join-Path $root 'Native\Whisper\whisper-cli.exe') $nativePublish -Force
Copy-Item (Join-Path $root 'licenses') $publish -Recurse -Force
@('README.md', 'ANVANDARGUIDE.md', 'PRIVACY.md', 'ARCHITECTURE.md',
  'MODEL_EVALUATION.md', 'DEPENDENCIES.md', 'THIRD_PARTY_NOTICES.md') |
    ForEach-Object { Copy-Item (Join-Path $root $_) $publish -Force }

& (Join-Path $root 'tools\verify-package.ps1') -PackageRoot $publish
if (Test-Path $zip) {
    Remove-Item -Force $zip
}
Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $zip -CompressionLevel Optimal
Write-Host "Portable paket skapat: $zip"
