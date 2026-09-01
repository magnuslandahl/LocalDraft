# LocalDraft testing guide

The test suite is deliberately small and targeted. It protects deterministic
core behavior, privacy/storage invariants, local media/model integration, and
important WPF wiring.

## Current baseline

| Project | Tests | Focus |
| --- | ---: | --- |
| `LocalDraft.Core.Tests` | 5 | Hashing, chunking, prompt isolation, protected values |
| `LocalDraft.Infrastructure.Tests` | 12 | Containment, storage, recovery, audio/model integration, privacy regression |
| `LocalDraft.App.Tests` | 10 | XAML/source wiring and UX regressions |
| **Total** | **27** | |

## Commands

Target one project while iterating:

```powershell
dotnet test .\tests\LocalDraft.Core.Tests\LocalDraft.Core.Tests.csproj
dotnet test .\tests\LocalDraft.Infrastructure.Tests\LocalDraft.Infrastructure.Tests.csproj
dotnet test .\tests\LocalDraft.App.Tests\LocalDraft.App.Tests.csproj
```

Full baseline:

```powershell
dotnet test .\LocalDraft.slnx
```

Release configuration:

```powershell
dotnet test .\LocalDraft.slnx -c Release
```

## Core coverage

`TextProcessingTests.cs` verifies:

- RTF and plain text contribute to the content hash.
- protected names and exact values are detected when output omits them.
- long text is split without losing content.
- source text is fenced and treated as data rather than instructions.
- custom assistant instructions are represented correctly.

Add Core tests for deterministic logic that does not require WPF, files, audio,
or model inference.

## Infrastructure coverage

### Storage and recovery

`StorageTests.cs` verifies:

- paths cannot escape the allowed root
- RTF/plain text persist atomically without leftover `.tmp` files
- versions deduplicate and restore content
- document deletion removes the complete document directory
- recording deletion removes audio and metadata
- model hashes fail closed on mismatch
- an interrupted WAV can be repaired and recovered

### Privacy

`PrivacyRegressionTests.cs` scans runtime C# sources for selected networking and
telemetry APIs. Extend its forbidden list when a new runtime project or relevant
API surface is introduced. Do not weaken it to accommodate a new dependency;
resolve the boundary violation instead.

### Local integration

`WhisperIntegrationTests.cs` contains four checks:

- the Swedish fixture is PCM 16 kHz, 16-bit, mono
- bundled Whisper transcribes the fixture offline when its assets are present
- bundled Qwen processes Swedish and preserves protected values when present
- Qwen passes the 12-case Swedish acceptance corpus when present

The model-dependent tests return without running when ignored model/native
assets are absent. A release build must fetch the assets before the suite so
those integrations actually execute. The corpus is
`tests\fixtures\swedish-assistant-corpus.json`; measured model results belong in
[`../MODEL_EVALUATION.md`](../MODEL_EVALUATION.md).

`tests\fixtures\svenska-test.wav` is a small repository test fixture used only
for deterministic recovery/conversion tests. Production recordings must never
enter Git.

## App coverage

`ProjectConfigurationTests.cs` inspects XAML and source to protect behaviors
that are costly to automate with a full WPF UI harness. Current regressions
include:

- expected WPF project configuration
- application and main-window startup wiring
- list selection without duplicate loading
- stable three-document selection behavior
- no obsolete document search/filter surface
- per-document row menus and current Swedish action labels
- one-click recording startup and microphone status
- microphone testing in settings rather than the recording dialog
- logarithmic audio-level visualization
- title/editor alignment

These tests are intentionally coupled to control names and source patterns.
When a refactor preserves behavior but changes wiring, update the tests with the
implementation rather than deleting the assertion.

## Test-selection matrix

| Change | Minimum test scope |
| --- | --- |
| Prompt, hash, chunking, protected values | Core |
| Paths, storage, delete, versions, history | Core + Infrastructure |
| Audio capture, recovery, transcription | Infrastructure |
| Model manifest or assistant service | Core + Infrastructure |
| XAML, controls, event wiring, sidebar | App |
| Document selection/save coordination | App + Infrastructure |
| Startup, privacy, runtime dependencies | Infrastructure + App |
| Packaging, models, native files | Full solution + package verification |
| Release or broad UI completion | Full solution |

## Runtime-containment caveat

Tests use isolated directories under `Path.GetTempPath()` for cleanup and
repeatability. That is test-only behavior. Application runtime code must keep
all artifacts below `AppContext.BaseDirectory`.

## Adding tests

- Prefer deterministic, narrowly scoped tests.
- Keep network access out of tests and runtime.
- Do not require developer-installed models unless the test is explicitly an
  opt-in evaluation tool.
- Use sanitized synthetic fixtures.
- Do not log or check in user document text, recordings, transcripts, prompts,
  or assistant output.
- For a reported regression, encode the user-visible failure, not just an
  incidental implementation detail.
