[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

function Get-VerifiedFile {
    param(
        [Parameter(Mandatory)] [string]$Uri,
        [Parameter(Mandatory)] [string]$Destination,
        [Parameter(Mandatory)] [string]$Sha256
    )

    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Destination) | Out-Null
    if (-not (Test-Path $Destination)) {
        Write-Host "Hamtar $(Split-Path -Leaf $Destination)..."
        Invoke-WebRequest -Uri $Uri -OutFile $Destination -UseBasicParsing
    }

    $actual = (Get-FileHash -Algorithm SHA256 -Path $Destination).Hash.ToLowerInvariant()
    if ($actual -ne $Sha256.ToLowerInvariant()) {
        Remove-Item -Force $Destination
        throw "SHA-256 stammer inte for $Destination. Filen har tagits bort."
    }
}

$whisperModel = Join-Path $RepositoryRoot 'Models\Whisper\ggml-small-q5_1.bin'
$textModel = Join-Path $RepositoryRoot 'Models\Text\Qwen3-1.7B-Q4_K_M.gguf'
$archive = Join-Path $RepositoryRoot 'artifacts\whisper-bin-x64-b4938.zip'

Get-VerifiedFile `
    -Uri 'https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small-q5_1.bin' `
    -Destination $whisperModel `
    -Sha256 'ae85e4a935d7a567bd102fe55afc16bb595bdb618e11b2fc7591bc08120411bb'

Get-VerifiedFile `
    -Uri 'https://huggingface.co/ggml-org/Qwen3-1.7B-GGUF/resolve/main/Qwen3-1.7B-Q4_K_M.gguf' `
    -Destination $textModel `
    -Sha256 'd2387ca2dbfee2ffabce7120d3770dadca0b293052bc2f0e138fdc940d9bc7b5'

foreach ($oldName in @('Qwen3.5-0.8B-Q4_0.gguf', 'Qwen3.5-0.8B-Q8_0.gguf')) {
    $oldTextModel = Join-Path $RepositoryRoot "Models\Text\$oldName"
    if (Test-Path $oldTextModel) {
        Remove-Item -Force $oldTextModel
    }
}

Get-VerifiedFile `
    -Uri 'https://github.com/ggml-org/whisper.cpp/releases/download/b4938/whisper-bin-x64.zip' `
    -Destination $archive `
    -Sha256 'c2a4b60edb11f7e11a9191ffb50929535527d4d91c9903dbe3e554583bbbc63d'

$extractRoot = Join-Path $RepositoryRoot 'artifacts\whisper-bin-x64-b4938'
if (Test-Path $extractRoot) {
    Remove-Item -Recurse -Force $extractRoot
}
Expand-Archive -Path $archive -DestinationPath $extractRoot
$nativeRoot = Join-Path $RepositoryRoot 'Native\Whisper'
if (Test-Path $nativeRoot) {
    Get-ChildItem -Path $nativeRoot -File |
        Where-Object { $_.Name -ne '.gitkeep' } |
        Remove-Item -Force
}
New-Item -ItemType Directory -Force -Path $nativeRoot | Out-Null
Get-ChildItem -Path $extractRoot -Recurse -File |
    Where-Object { $_.Extension -eq '.dll' -or $_.Name -eq 'whisper-cli.exe' } |
    Copy-Item -Destination $nativeRoot -Force

$cli = Join-Path $nativeRoot 'whisper-cli.exe'
if (-not (Test-Path $cli)) {
    throw 'whisper-cli.exe saknas i den verifierade release-zippen.'
}

Write-Host 'Alla modeller och native-filer ar hamtade och verifierade.'
