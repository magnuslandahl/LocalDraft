# LocalDraft implementation guide

This is the detailed code and runtime map for LocalDraft. Read
[`PROJECT_OVERVIEW.md`](PROJECT_OVERVIEW.md) first and treat
[`../PRIVACY.md`](../PRIVACY.md) as the authority for privacy claims.

## Solution structure

| Project | Responsibility |
| --- | --- |
| `src/LocalDraft.Core` | Domain records, service contracts, hashing, text chunking, prompt construction, protected-token checks |
| `src/LocalDraft.Infrastructure` | Contained paths, atomic storage, audio, transcription, local AI, recovery, settings, history, logs |
| `src/LocalDraft.App` | WPF composition, dependency injection, windows, view model, editor integration, user workflows |
| `tests/LocalDraft.Core.Tests` | Deterministic text-processing tests |
| `tests/LocalDraft.Infrastructure.Tests` | Storage, privacy, audio, assistant-corpus, and model-manifest tests |
| `tests/LocalDraft.App.Tests` | XAML and source-level UI regression tests |

The architecture intentionally uses a small number of explicit services rather
than a framework-heavy MVVM layer. `MainWindowViewModel` manages document list,
selection, load, save, and version operations. `MainWindow.xaml.cs` coordinates
WPF-specific editor, dialogs, selection ranges, autosave, and long-running
workflows.

## Startup and shutdown

`src/LocalDraft.App/App.xaml.cs` is the composition root.

Startup order is security-sensitive:

1. `AppPathService.ConfigureProcessEnvironment()` redirects `TEMP`, `TMP`,
   `HF_HOME`, `XDG_CACHE_HOME`, `LLAMA_CACHE`, and `GGML_CACHE` below
   `AppRoot\Data`.
2. Swedish culture is selected.
3. The application verifies that `AppRoot` is writable.
4. Dependency injection is configured.
5. Settings and first-run state are loaded.
6. Interrupted recordings are discovered and offered for recovery or deletion.
7. The main window is created.
8. Model-manifest validation runs and reports missing, wrong-size, or
   hash-mismatched models.

Do not move service, native-library, audio, or model initialization before
environment containment.

On shutdown, the main window cancels active work, waits for an autosave, and
asks before discarding changes if the save fails. The app unloads the text
model and disposes the service provider.

## Dependency-injection map

The principal contracts are in `src/LocalDraft.Core/Contracts.cs`.

| Contract | Implementation |
| --- | --- |
| `IAppPathService` | `AppPathService` |
| `IDocumentRepository` | `DocumentRepository` |
| `IVersionService` | `VersionService` |
| `IRecordingRepository` | `RecordingRepository` |
| `IAssistantHistoryService` | `AssistantHistoryService` |
| `ISettingsService` | `SettingsService` |
| `IAudioDeviceService` | `WasapiAudioDeviceService` |
| `IAudioRecorder` | `WasapiAudioRecorder` |
| `ITranscriptionService` | `WhisperCliTranscriptionService` |
| `ITextAssistantService` | `LlamaTextAssistantService` |
| `IPartialRecordingRecovery` | `PartialRecordingRecovery` |
| `ILocalLog` | `LocalLog` |
| `IModelManifestValidator` | `ModelManifestValidator` |

## Runtime directory tree

All mutable runtime paths are descendants of `AppContext.BaseDirectory`.

```text
AppRoot/
  LocalDraft.exe
  Models/
    manifest.json
    Whisper/ggml-small-q5_1.bin
    Text/Qwen3-1.7B-Q4_K_M.gguf
  Native/
    Whisper/whisper-cli.exe
    Whisper/*.dll
  Data/
    Cache/
    Logs/
      app-YYYYMMDD.log
    Settings/
      settings.json
    Temp/
    Documents/
      <document-guid>/
        document.json
        current.rtf
        current.txt
        assistant/
          history.json
        recordings/
          <recording-guid>.json
          <recording-guid>.wav
          <recording-guid>.partial.wav
        versions/
          <version-guid>.json
          <version-guid>.rtf
          <version-guid>.txt
```

Some directories are created lazily. A missing optional directory is not an
error.

## Path containment and atomic writes

`AppPathService` canonicalizes paths and rejects paths outside the permitted
root. It also checks existing path components for reparse points so junctions
and symbolic links cannot redirect reads or writes outside `AppRoot`.

Use the existing path-service methods instead of composing unvalidated paths:

- `GetDocumentDirectory`
- `EnsureContainedPath`
- `AtomicFile.WriteTextAsync`
- `AtomicFile.WriteJsonAsync`

Atomic writes create a sibling temporary file, flush it, and replace or move it
into place. Temporary files remain on the same volume and below `Data`. Do not
replace this with `Path.GetTempFileName`, direct overwrite, AppData, or another
fallback.

Recursive document deletion first rejects reparse points. It deletes the whole
document directory so current content, versions, recordings, partial audio, and
assistant history are removed together.

## Domain and persisted models

The main records are in `src/LocalDraft.Core/Models.cs`.

| Type | Purpose |
| --- | --- |
| `DocumentMetadata` | ID, title, timestamps, content hash |
| `DocumentContent` | RTF plus plain-text representation |
| `LocalDocument` | Metadata and current content |
| `DocumentSummary` | Document-list projection |
| `VersionMetadata` | Snapshot ID, reason, hash, preview, timestamp |
| `RecordingMetadata` | Recording ID, document ID, duration, state, transcript |
| `AppSettings` | Selected microphone and first-run state |
| `AssistantRequest` | Action, source text, optional custom instruction |
| `AssistantResult` | Output, missing protected tokens, timing/token metrics |
| `AssistantHistoryEntry` | Per-document assistant interaction |
| `ModelManifest` | Required model paths, sizes, hashes, revisions, licenses |

JSON uses camelCase through `AtomicFile.JsonOptions`.

## Document lifecycle

### Create and load

`DocumentRepository.CreateAsync` creates a GUID directory, a timestamped
Swedish default title, empty RTF, plain text, and metadata. `ListAsync` reads
metadata summaries and orders them by descending modification time.

`MainWindowViewModel.LoadAsync` populates the list and creates a first document
when none exist. Selection is serialized in `MainWindow` so an in-flight save
cannot race a document switch.

### Save and autosave

`RichTextContent.Read` serializes the WPF `FlowDocument` to RTF and extracts a
plain-text mirror. `DocumentRepository.SaveAsync` updates title, modification
time, and a SHA-256 hash over both forms.

Autosave updates the affected `DocumentSummary` in place. It must not clear and
repopulate the bound collection: doing that previously caused visual flicker,
changed ordering during selection, and made the first item difficult to select.

### Versions

`VersionService.CommitAsync` hashes RTF and plain text and skips a snapshot when
the newest version has the same hash. A version consists of RTF, plain text,
and JSON metadata. Restore loads a snapshot and then saves it through the normal
document path.

Automatic versions use reason strings supplied by the UI. Preserve clear,
Swedish reason text because it appears in the version dialog.

## Recording lifecycle

`WasapiAudioRecorder` captures the chosen WASAPI input through NAudio.

1. The recording window opens with the saved microphone and starts immediately.
2. Capture writes to `<id>.partial.wav` below the current document.
3. The UI receives peak values for a logarithmic level meter.
4. On stop, Media Foundation resamples to PCM 16 kHz, 16-bit, mono.
5. The final file becomes `<id>.wav`.
6. `RecordingMetadata` is atomically written.
7. Transcription runs and the recording remains available if transcription
   fails or is cancelled.

The partial file is the crash-recovery artifact. Do not write capture output to
an operating-system temporary path.

### Interrupted-recording recovery

`PartialRecordingRecovery` scans document trees for `*.partial.wav`. Recovery
repairs RIFF and `data` chunk sizes, resamples the file, moves it to its final
name, and creates metadata. The startup UI asks the user whether to recover or
delete each item.

## Transcription pipeline

`WhisperCliTranscriptionService` launches only the bundled
`Native\Whisper\whisper-cli.exe`.

- `UseShellExecute = false`
- no window
- structured `ArgumentList`
- redirected stdout and stderr are drained
- working/output directories are below `Data\Temp`
- input must be below `Data`
- language is `sv`
- thread count is `max(1, processorCount - 1)`

Cancellation terminates the child process tree. Whisper JSON output is parsed
into a single trimmed transcript and then deleted. Logs contain event IDs and
exception types, not stderr or transcript content.

## Text-processing pipeline

`LlamaTextAssistantService` uses LLamaSharp in-process with:

- `Models\Text\Qwen3-1.7B-Q4_K_M.gguf`
- 8192-token context
- CPU only (`GpuLayerCount = 0`)
- `max(1, processorCount - 1)` threads
- temperature `0.15`
- repeat penalty `1.08`
- 1024 maximum output tokens for summaries, 3072 for other actions

The model is loaded lazily and retained until explicit unload or shutdown.
`SemaphoreSlim` permits one generation operation at a time.

### Prompt and data safety

`AssistantPrompts` keeps source text inside `<SOURCE_TEXT>` tags, labels it as
data rather than instructions, asks for Swedish output, prohibits invented
facts, protects exact values, and requests `/no_think`.

`ParagraphChunker` splits long input at paragraph and sentence boundaries,
normally at 12,000 characters. Non-summary operations extract likely names,
identifiers, dates, measurements, amounts, percentages, and other exact values.
If output omits protected tokens, the service retries the chunk once with the
missing values explicitly listed. The combined result reports anything still
missing.

The UI shows generated text in a preview. The user chooses replacement,
insertion, or cancellation. Assistant history is per document in
`assistant\history.json`.

## Rich text and clipboard

The editor boundary is `RichTextContent` at the end of `MainWindow.xaml.cs`.
It owns RTF load, RTF/plain-text extraction, and explicit clipboard copying.
Clipboard data includes both `DataFormats.Rtf` and `DataFormats.UnicodeText`.

`MarkdownFlowDocument` renders only:

- `#` and `##` headings
- `- ` bullet items
- `**bold**`

Do not assume arbitrary Markdown or HTML support.

## UI composition and concurrency

Important coordination points in `MainWindow.xaml.cs`:

- a dispatcher autosave timer
- a version timer
- `saveLock` to serialize writes
- a document-switch guard and pending selection
- `operationCancellation` for transcription and text processing
- `RunBusyAsync` to disable conflicting controls and expose cancellation
- close-after-save protection

`RecordingWindow.xaml.cs` separately guards automatic startup, retries selected
microphone initialization, and prevents close/start races.

The source-level UI tests intentionally assert some of these wiring details.
Update tests when renaming controls or moving event handlers.

## Settings and diagnostics

`SettingsService` persists settings below `Data\Settings`. The settings window
owns microphone selection and testing plus progressively disclosed local
storage/privacy information.

`LocalLog` writes one JSON object per line to a daily file. Allowed fields are:

- UTC timestamp
- level
- stable event ID
- document ID
- recording ID
- duration
- exception type

Never log titles, text, transcripts, prompts, assistant results, file contents,
microphone names, or redirected process output. Rotation keeps the newest log
files while limiting total log size to 10 MiB.

## Model integrity

`ModelManifestValidator` validates every manifest path for containment and then
checks file existence, exact size, and SHA-256. The package verification script
repeats model hash checks independently.

Model acquisition is developer-only. Runtime code must not invoke
`tools\fetch-models.ps1` or contain download logic.

## Change hotspots

| Change | Start with |
| --- | --- |
| Startup, containment, DI | `App.xaml.cs`, `AppPathService.cs` |
| Document persistence | `DocumentStorage.cs`, `Models.cs`, `Contracts.cs` |
| Document-list behavior | `MainWindowViewModel.cs`, `MainWindow.xaml.cs` |
| Main layout/labels | `MainWindow.xaml`, `App.xaml` |
| Recording UX | `RecordingWindow.xaml(.cs)`, `AudioServices.cs` |
| Microphone settings | `SettingsWindow.xaml(.cs)`, `AudioServices.cs` |
| Transcription | `AiServices.cs` |
| Text prompts/safety | `TextProcessing.cs`, `AiServices.cs` |
| Recovery | `PartialRecordingRecovery.cs`, `App.xaml.cs` |
| Release package | app `.csproj`, `tools/build-portable.ps1`, `tools/verify-package.ps1` |

For test selection and existing coverage, see [`TESTING.md`](TESTING.md).
