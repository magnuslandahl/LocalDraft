[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot)
)

# Public-repository guard. Fails when tracked files carry the retired product
# name, build output, large local assets, credentials or private paths.
# This script is excluded from its own content scans because it necessarily
# contains the patterns it searches for.

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path $RepositoryRoot).Path
$selfPath = 'tools/verify-repository.ps1'
$failures = New-Object System.Collections.Generic.List[string]

Push-Location $root
try {
    $tracked = @(git ls-files)
    if ($LASTEXITCODE -ne 0) { throw 'Could not list tracked files.' }
    if ($tracked.Count -eq 0) { throw 'No tracked files were found.' }

    $scanFiles = @(
        $tracked |
            Where-Object { $_ -notmatch '(?i)\.(ico|svg|png|jpg|jpeg|gif|wav|bin|gguf|dll|exe|zip|pfx)$' } |
            Where-Object { $_ -ne $selfPath } |
            Where-Object { Test-Path -LiteralPath $_ }
    )

    function Add-Failure {
        param([string]$Rule, [string[]]$Items)
        if ($Items -and $Items.Count -gt 0) {
            $failures.Add("$Rule`n  " + (($Items | Select-Object -Unique | Sort-Object) -join "`n  "))
        }
    }

    function Find-Matches {
        param([string[]]$Patterns, [string[]]$Files = $scanFiles)
        if (-not $Files -or $Files.Count -eq 0) { return @() }
        return @(
            $Files |
                Select-String -Pattern $Patterns -List |
                ForEach-Object { (Resolve-Path -Relative -LiteralPath $_.Path) -replace '^\.\\', '' -replace '\\', '/' }
        )
    }

    # 1. The product name must be LocalDraft everywhere.
    Add-Failure 'Tracked paths still use the retired product name:' `
        @($tracked | Where-Object { $_ -match '(?i)lokaldiktering' })
    Add-Failure 'Tracked files still mention the retired product name:' `
        (Find-Matches @('LokalDiktering', 'lokaldiktering', 'Lokal Diktering'))

    # 2. Build output, models and binaries must never be committed.
    Add-Failure 'Build output, models or binaries are tracked by Git:' `
        @($tracked | Where-Object {
            $_ -match '(?i)(^|/)(bin|obj|dist|artifacts)/' -or
            $_ -match '(?i)(^|/)Data/' -or
            $_ -match '(?i)\.(bin|gguf|dll|exe|pdb|zip)$'
        })

    # 3. Signing material and other credentials must live outside the repository.
    Add-Failure 'Signing material or credential files are tracked by Git:' `
        @($tracked | Where-Object {
            $_ -match '(?i)\.(pfx|p12|cer|crt|der|pem|key|jks|keystore|kdbx|p8|mobileprovision|provisionprofile)$' -or
            $_ -match '(?i)(^|/)(\.env(\..+)?|id_rsa|id_dsa|id_ecdsa|id_ed25519)$'
        })

    # 4. High-confidence credential formats must never be committed.
    Add-Failure 'Tracked files contain credential-shaped values:' (Find-Matches @(
        'AKIA[0-9A-Z]{16}',
        'ASIA[0-9A-Z]{16}',
        'gh[pousr]_[A-Za-z0-9]{36}',
        'github_pat_[A-Za-z0-9_]{40,}',
        'AIza[0-9A-Za-z_\-]{35}',
        'xox[baprs]-[A-Za-z0-9\-]{10,}',
        '-----BEGIN [A-Z ]*PRIVATE KEY-----',
        'AccountKey=[A-Za-z0-9+/=]{20,}',
        'SharedAccessKey=[A-Za-z0-9+/=]{10,}',
        'InstrumentationKey=[0-9a-fA-F-]{20,}'
    ))

    # 5. Machine-specific paths and private addresses must not leak.
    Add-Failure 'Tracked files contain machine-specific paths or private addresses:' (Find-Matches @(
        '[A-Za-z]:\\Users\\[^\\/\s"'']+',
        '/home/[a-z0-9_.-]+',
        '[A-Za-z0-9._%+-]+@(gmail|outlook|hotmail|live|apollo)\.[A-Za-z]{2,}'
    ))

    # 6. Runtime projects must stay offline.
    Add-Failure 'Runtime projects reference networking or telemetry APIs:' (Find-Matches `
        -Patterns @(
            'HttpClient', 'WebClient', 'System\.Net\.Sockets', 'HttpWebRequest',
            'ApplicationInsights', 'OpenTelemetry', 'Azure\.AI', 'Google\.Cloud', 'AWSSDK'
        ) `
        -Files @($scanFiles | Where-Object { $_ -match '^src/.+\.(cs|csproj)$' }))

    if ($failures.Count -gt 0) {
        foreach ($failure in $failures) { Write-Host $failure }
        throw "Repository guards failed with $($failures.Count) rule violation(s)."
    }

    Write-Host "Repository guards passed for $($tracked.Count) tracked files."
}
finally {
    Pop-Location
}
