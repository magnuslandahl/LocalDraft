# LocalDraft development and release

This guide covers setup, local development, and portable-package creation. It
does not redefine the privacy contract; see [`../PRIVACY.md`](../PRIVACY.md).

## Prerequisites

- Windows 10 22H2 or Windows 11, x64
- .NET 10 SDK
- PowerShell
- Internet access only for developer-time NuGet restore and explicit model or
  native-runtime acquisition
- Sufficient disk space for the models, build output, package folder, and ZIP

The application itself must remain fully offline.

## Clone state and large local assets

The following are required to run the full app but are intentionally ignored by
Git:

- `Models\Whisper\ggml-small-q5_1.bin`
- `Models\Text\Qwen3-1.7B-Q4_K_M.gguf`
- `Native\Whisper\whisper-cli.exe`
- native Whisper DLLs

Fetch them only through the developer tool:

```powershell
.\tools\fetch-models.ps1
```

The script downloads pinned artifacts and verifies their recorded SHA-256
hashes. Do not commit these files and never call the script from runtime code.
See [`../MODEL_EVALUATION.md`](../MODEL_EVALUATION.md) and
[`../DEPENDENCIES.md`](../DEPENDENCIES.md) for revisions, hashes, licenses, and
selection evidence.

## Restore, build, and run

```powershell
dotnet restore .\LokalDiktering.slnx --locked-mode
dotnet build .\LokalDiktering.slnx
dotnet run --project .\src\LokalDiktering.App\LokalDiktering.App.csproj
```

Package lock files are committed and `RestorePackagesWithLockFile` is enabled.
Use locked restore unless deliberately updating dependencies.

The app project copies verified model and native assets into development output
when they exist. This makes a normal Debug run self-contained beside the built
executable. If the assets are absent, compilation can still succeed but startup
model validation and dictation/text processing will report missing components.

Runtime data created by a development run lives below that build output, for
example:

```text
src\LokalDiktering.App\bin\Debug\net10.0-windows\Data\
```

Do not mistake generated `Data` for source data or commit it.

## Testing

Run the smallest relevant test project while iterating:

```powershell
dotnet test .\tests\LokalDiktering.Core.Tests\LokalDiktering.Core.Tests.csproj
dotnet test .\tests\LokalDiktering.Infrastructure.Tests\LokalDiktering.Infrastructure.Tests.csproj
dotnet test .\tests\LokalDiktering.App.Tests\LokalDiktering.App.Tests.csproj
```

Run the solution before a release or broad UI completion:

```powershell
dotnet test .\LokalDiktering.slnx
```

The current baseline is 27 tests: 5 Core, 12 Infrastructure, and 10 App.
Detailed coverage and test-selection guidance are in
[`TESTING.md`](TESTING.md).

## Build the portable distribution

```powershell
.\tools\build-portable.ps1
```

The script:

1. Requires both models and the native Whisper runtime.
2. Restores with `--locked-mode`.
3. Builds Release.
4. Runs all tests unless `-SkipTests` was explicitly supplied.
5. Publishes the WPF app as self-contained `win-x64`.
6. Copies models, native files, licenses, and user-facing documentation.
7. Runs `tools\verify-package.ps1`.
8. Replaces the portable ZIP.

Outputs:

```text
dist\LokalDiktering-Portable-win-x64\
LokalDiktering-Portable-win-x64.zip
```

The ZIP is created in the repository/worktree where the script is run. Do not
assume it appears in another checkout.

## Package contract

The app project publishes:

- `RuntimeIdentifier=win-x64`
- `SelfContained=true`
- `PublishSingleFile=false`
- `PublishTrimmed=false`

Non-single-file and untrimmed output is intentional for WPF, LLamaSharp, NAudio,
and native runtime compatibility.

`tools\verify-package.ps1` rejects a package when:

- required executable, models, manifest, native helper, or user docs are absent
- a model has the wrong size or SHA-256
- dependency metadata contains selected cloud/telemetry packages
- filenames suggest a downloader, updater, telemetry component, or server
- any `.wav` file is present in the distributable
- required native `ggml.dll` or `whisper.dll` is absent

For privacy, storage, model, native-process, or packaging changes, run the
privacy regression tests and package verification against the produced folder.

## Manual release smoke test

From a newly extracted ZIP in a normal writable folder:

1. Start `LokalDiktering.exe`.
2. Confirm the first-run/local-storage explanation.
3. Create and switch among at least three documents, including selecting the
   first item.
4. Type, restart, and verify content persistence.
5. Select a microphone in **Inställningar** and test its level.
6. Start dictation with one click, stop, transcribe, and insert the result.
7. Use **Bearbeta text**, review a result, and apply it.
8. Restore a version.
9. Verify per-document recording actions.
10. Permanently delete a test document after confirmation.
11. Confirm all generated files remain below the extracted app folder.

## Release checklist

- Working tree contains no model binaries, native binaries, `Data`, `dist`, ZIP,
  or build output to commit.
- Dependency and model documentation matches the pinned files.
- Full solution tests pass.
- Portable build and verification succeed.
- The app starts from the actual packaged folder, not only from Debug output.
- Privacy claims in `PRIVACY.md`, `README.md`, and UI still agree.
- `ANVANDARGUIDE.md` matches current Swedish labels and workflows.
- The produced ZIP is copied or attached from the correct worktree path.

## Known build warnings

NAudio 3 may report obsolete API warnings for `WasapiCapture` and
`WaveOutEvent` in a clean build. They are known compatibility warnings, not a
reason to hide other warnings. Do not broadly suppress warnings; migrate only
when the replacement preserves microphone behavior and current tests.

## Repository hygiene

Never commit:

- downloaded models
- native executables or DLLs
- `Data\`
- `bin\`, `obj\`, `dist\`
- the portable ZIP
- user recordings, transcripts, prompts, or assistant results
- raw OBS review media or transcripts

Documentation-only edits do not require a build unless a repository-specific
documentation check exists. Changes that affect behavior still require the
smallest relevant test and, for release-affecting areas, the package checks
above.
