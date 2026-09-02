# Cross-platform plan: Windows and macOS

Status: **proposal, not yet implemented.** This document records the target
platforms, the five decisions the port depends on, and the order of work.
Nothing here changes behaviour until the individual changes land through their
own pull requests.

## Scope

| Platform | Decision |
| --- | --- |
| Windows 10/11 **x64** | Supported today. Stays the primary platform. |
| macOS **arm64** (Apple Silicon) | Target. |
| macOS **x64** (Intel) | Target. |
| Linux | Explicitly out of scope. |
| Windows **arm64** | Out of scope. The x64 build runs under emulation. |

## What we verified first

These findings drive every decision below. They were checked against the actual
packages and releases, not assumed.

| Question | Finding |
| --- | --- |
| Does the local text model run on macOS? | **Yes.** `LLamaSharp.Backend.Cpu` 0.27.0 ships `osx-arm64` (including `libggml-metal.dylib`) and `osx-x64` natives. Apple Silicon additionally gets Metal acceleration. |
| Does whisper.cpp publish a macOS CLI binary? | **No.** Release `b4938` contains Windows ZIPs, Ubuntu tarballs and an `xcframework` only. There is no macOS `whisper-cli` to bundle. |
| Is there an in-process alternative? | **Yes.** `Whisper.net.Runtime` 1.9.1 ships `macos-arm64` (including `libggml-metal-whisper.dylib`), `macos-x64` and `win-x64` natives. |
| How much UI code must be replaced? | 2 615 hand-written lines of WPF (XAML plus code-behind) across 15 files. |
| How much non-UI code is affected? | Infrastructure 1 083 lines, Core 341 lines, tests 565 lines. |

The models themselves are platform-neutral GGML/GGUF files. `Models/manifest.json`,
the SHA-256 verification and `tools/fetch-models.ps1` need no per-platform work.

## The five decisions

### D1. Replace WPF with Avalonia UI

WPF is Windows-only and cannot be ported. Avalonia 11 is the closest match: XAML,
styles, MVVM and the same mental model, running on Windows and macOS x64/arm64.

Replace WPF rather than maintaining two UIs. Two parallel front ends for a
2 600-line UI would cost more than the port itself.

Consequence: `src/LocalDraft.App` is rewritten. `LocalDraft.Core` and
`LocalDraft.Infrastructure` keep their contracts, which is what makes this
tractable.

### D2. Move the document format from RTF to Markdown

This is the single hardest part of the port and the decision most worth
challenging.

Today the editor is a WPF `RichTextBox` over a `FlowDocument`, persisted as RTF
(`DocumentContent(string Rtf, string PlainText)`). **Avalonia has no rich text
document model** — no `FlowDocument`, no `RichTextBox`, no `TextRange`.

Options considered:

| Option | Verdict |
| --- | --- |
| Build a rich text editor on Avalonia | Rejected. Weeks of work for a document model we do not otherwise need. |
| HTML in a web view | Rejected. The privacy rules forbid web views. |
| `AvaloniaEdit` | Rejected as a rich text surface. It is a code editor. |
| **Markdown as the canonical format** | **Recommended.** |

Markdown fits what the product already does. The toolbar is heading, bold,
italic, bullet list, undo and redo — exactly Markdown's range. The assistant
output is *already* treated as Markdown and converted for display by
`MarkdownFlowDocument.Parse`. Markdown is also plain text, which makes documents
diffable, greppable and trivially portable between the two platforms.

Consequences:

- `DocumentContent` becomes `(string Markdown, string PlainText)`.
- Documents are stored as `.md` instead of `.rtf`.
- A one-time migration converts existing `.rtf` documents and their versions on
  first run. It must be atomic, must keep the original until the conversion is
  verified, and must be covered by tests.
- "Kopiera dokumenttext" can still place RTF on the clipboard by rendering
  Markdown to RTF at copy time, so the Word/Outlook workflow survives.

### D3. Replace the whisper-cli child process with Whisper.net

The current design shells out to a bundled `whisper-cli.exe`. That cannot be
reproduced on macOS from prebuilt binaries, and building whisper.cpp from source
in CI would add a native toolchain plus its own signing and notarization burden.

Move transcription in-process using `Whisper.net`, behind the existing
`ITranscriptionService` contract. This removes a child process instead of adding
a second platform-specific one, and gives Metal-accelerated transcription on
Apple Silicon for free.

Consequences:

- `WhisperCliTranscriptionService` is replaced by an in-process implementation.
- `Native/Whisper/`, the whisper.cpp download in `tools/fetch-models.ps1` and the
  native-file checks in `tools/verify-package.ps1` disappear for the app; the
  model `.bin` file is still fetched and SHA-256 verified.
- Transcription cancellation moves from killing a process to a cancellation
  token. This must keep the current behaviour where a cancelled transcription
  still leaves the WAV file recoverable.
- `AGENTS.md` currently allows bundled child processes. After this change the
  app launches no child processes at all, which is a stronger privacy position
  and should be stated as such.

### D4. Replace WASAPI capture with a cross-platform recorder

`AudioServices.cs` uses NAudio's `WasapiCapture`, `MMDeviceEnumerator` and
`WaveOutEvent`, all Windows-only. The good news is that the project already
abstracts this behind `IAudioRecorder` and `IAudioDeviceProvider`, and NAudio's
*file* handling (`WaveFileReader`/`WaveFileWriter`, used by
`PartialRecordingRecovery`) is portable and can stay.

Only device enumeration, capture and preview playback need a macOS
implementation, either through a cross-platform audio library or a thin
CoreAudio/AVFoundation binding. Pick this in a spike; keep the 16 kHz mono PCM
WAV output contract unchanged so recovery, transcription and stored recordings
are byte-compatible across platforms.

### D5. Make the storage root platform-specific — this changes a privacy invariant

Today every artifact lives under `AppContext.BaseDirectory` (`AppRoot`), and
`PRIVACY.md` promises exactly that. **On macOS this cannot hold.** Apps ship as
signed `.app` bundles in `/Applications`, which is not user-writable, and writing
inside a bundle invalidates its signature and notarization.

Proposal: keep the *principle* and make the *location* explicit per platform.

| Platform | Storage root |
| --- | --- |
| Windows | Unchanged: `Data` beside the executable, so the portable build stays portable. |
| macOS | A single contained root at `~/Library/Application Support/LocalDraft`. |

What must not change: one single root, everything below it, no scattering across
the system, no cloud-synced location, no silent fallback, a clear failure when
the root is not writable, and the existing symlink and reparse-point validation.

This also unlocks Windows packaging options. An MSIX package installs read-only,
which today's "beside the executable" root forbids; once the root is a
per-platform decision, the Microsoft Store becomes viable. See
[`DISTRIBUTION_AND_SIGNING.md`](DISTRIBUTION_AND_SIGNING.md).

This requires an explicit, reviewed amendment to `PRIVACY.md`, `AGENTS.md` and
`ANVANDARGUIDE.md` before any code lands. Treat it as the most sensitive item in
the port: the "copy the app folder to move everything" promise becomes
Windows-only, and the macOS guide must instead tell users where their data lives
and how to back it up or delete it.

## Also required for macOS

- **Microphone permission.** `NSMicrophoneUsageDescription` in `Info.plist`, in
  Swedish, plus handling the case where the user denies the TCC prompt.
- **Clipboard.** Avalonia clipboard with the `public.rtf` type on macOS.
- **App bundle.** `.app` layout, icon set (`.icns`), bundle identifier.
- **Distribution.** `.dmg` per architecture, signed and notarized with the
  existing Apple Developer account. See
  [`DISTRIBUTION_AND_SIGNING.md`](DISTRIBUTION_AND_SIGNING.md). macOS is stricter
  than Windows here: without notarization Gatekeeper effectively blocks the app.
- **Tests.** `ProjectConfigurationTests` asserts WPF XAML strings and must be
  rewritten against the Avalonia markup. The privacy regression test and the
  storage tests stay and should be extended with the new storage-root rules.

## Phases

Each phase is independently mergeable and keeps Windows working throughout.

| Phase | Work | Rough effort |
| --- | --- | --- |
| 0 | Spikes: Avalonia Markdown editor, cross-platform audio capture, Whisper.net on macOS. Confirm D2 and D4 before committing. | 2–4 days |
| 1 | Make Core and Infrastructure platform-neutral: retarget away from `net10.0-windows`, adopt Whisper.net, cross-platform audio, storage-root policy plus the privacy-document amendment. Windows still ships WPF. | 1–2 weeks |
| 2 | Avalonia UI, Markdown editor and the RTF-to-Markdown migration. Windows switches from WPF to Avalonia at the end of this phase. | 2–4 weeks |
| 3 | macOS packaging: `.app`, `.dmg`, signing, notarization, CI matrix, `verify-package` for macOS. | ~1 week |
| 4 | Documentation, user guide, privacy revalidation, manual smoke tests on Intel and Apple Silicon. | 3–5 days |

Realistic total for one developer: **six to eight weeks**. The codebase is small,
which is the main reason this is worth doing rather than living with Windows only.

## CI matrix after the port

| Runner | Target | Produces |
| --- | --- | --- |
| `windows-latest` | `win-x64` | Portable ZIP and installer |
| `macos-14` (arm64) | `osx-arm64` | Signed, notarized `.dmg` |
| `macos-13` (Intel) | `osx-x64` | Signed, notarized `.dmg` |

Ship one `.dmg` per architecture rather than a universal binary. The native
dependencies are per-architecture, and two clearly-named downloads are simpler
than a `lipo` step. Each macOS download is roughly the same 1.5 GB as Windows,
so every asset stays below the 2 GB GitHub release limit.

## Risks

1. **Markdown is a product change, not only a technical one.** Existing documents
   must survive it. If the editor must keep exact RTF fidelity, D2 fails and the
   port becomes far more expensive.
2. **The storage-root change touches the core privacy promise.** It needs sign-off
   before code, not after.
3. **Audio quality and latency on macOS** are unproven until the spike.
4. **Notarization is a hard gate.** It needs working CI secrets before macOS can
   ship at all, and confirmation that the existing Apple Developer identity may
   be used for this project.
5. **Transcription performance on Intel Macs** will be the slowest supported
   configuration and should be measured before promising it.

## Explicit non-goals

- Linux support.
- Windows on ARM as a separate build.
- iOS, Android or any web version.
- Changing the models, the offline guarantee or the Swedish-first scope.
