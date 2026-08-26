[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$PackageRoot
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path $PackageRoot).Path
$required = @(
    'LokalDiktering.exe',
    'Models\manifest.json',
    'Models\Whisper\ggml-small-q5_1.bin',
    'Models\Text\Qwen3-1.7B-Q4_K_M.gguf',
    'Native\Whisper\whisper-cli.exe',
    'README.md',
    'ANVANDARGUIDE.md',
    'PRIVACY.md',
    'THIRD_PARTY_NOTICES.md'
)
foreach ($relative in $required) {
    if (-not (Test-Path (Join-Path $root $relative))) {
        throw "Paketet saknar $relative"
    }
}

$manifest = Get-Content (Join-Path $root 'Models\manifest.json') -Raw | ConvertFrom-Json
foreach ($model in $manifest.models) {
    $path = Join-Path (Join-Path $root 'Models') $model.relativePath
    $file = Get-Item $path
    if ($file.Length -ne [long]$model.size) {
        throw "Fel storlek: $($model.relativePath)"
    }
    $hash = (Get-FileHash -Algorithm SHA256 $path).Hash.ToLowerInvariant()
    if ($hash -ne $model.sha256.ToLowerInvariant()) {
        throw "Fel SHA-256: $($model.relativePath)"
    }
}

$forbiddenPackages = 'ApplicationInsights|OpenTelemetry|Sentry|AWSSDK|Google\.Cloud|Azure\.AI\.OpenAI'
$deps = Get-Content (Join-Path $root 'LokalDiktering.deps.json') -Raw
if ($deps -match $forbiddenPackages) {
    throw 'Paketet innehaller ett forbjudet moln- eller telemetribibliotek.'
}

$forbiddenFiles = Get-ChildItem $root -Recurse -File |
    Where-Object {
        $_.Name -match 'download|updater|telemetry|whisper-server|llama-server' -or
        $_.Extension -eq '.wav'
    }
if ($forbiddenFiles) {
    throw "Paketet innehaller ovantade filer: $($forbiddenFiles.FullName -join ', ')"
}

$requiredNative = @('ggml.dll', 'whisper.dll')
foreach ($name in $requiredNative) {
    if (-not (Get-ChildItem (Join-Path $root 'Native\Whisper') -Filter $name -Recurse -File)) {
        throw "Native-filen $name saknas."
    }
}

Write-Host "Paketet ar verifierat: $root"
