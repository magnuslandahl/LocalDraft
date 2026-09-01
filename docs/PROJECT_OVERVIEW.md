# LocalDraft project overview

This document gives coding agents and maintainers the product context needed
before changing LocalDraft. For implementation details, read
[`IMPLEMENTATION_GUIDE.md`](IMPLEMENTATION_GUIDE.md). The authoritative privacy
contract is [`../PRIVACY.md`](../PRIVACY.md).

## Product

LocalDraft is a portable Windows desktop application for Swedish writing and
dictation. A user can write rich text, record speech, transcribe it locally,
keep document versions, revisit recordings, and use a local language model to
clean up or restructure text.

The primary product promise is that document content, recordings,
transcriptions, prompts, assistant results, settings, logs, caches, and
temporary files remain below the application directory. The app has no runtime
network features. This is part of the product definition, not an optional
deployment mode.

The product name shown to users is **LocalDraft**. The solution, namespaces,
executable, and some historical documents still use `LokalDiktering`.

## Supported environment

- Windows 10 22H2 or Windows 11, x64
- .NET 10 WPF
- Self-contained portable distribution
- CPU-only Whisper transcription and text generation
- Swedish interface and Swedish-first speech transcription
- One local user at a time; no accounts, sync, collaboration, or cloud backup

## Primary user journey

1. Start `LokalDiktering.exe` from a writable extracted folder.
2. Create or select a document in the left document panel.
3. Type and format text in the main editor, or select **Diktera**.
4. Dictation starts immediately with the microphone saved in
   **Inställningar**. The recording is converted and transcribed locally.
5. Insert the transcription into the current document.
6. Optionally select text and use **Bearbeta text** to clean up, summarize,
   structure, improve, or turn it into a list.
7. Review assistant output before replacing or inserting text.
8. Use the three-dot menu on a document row to open recordings, view versions,
   copy document text, or permanently delete the document.

## Current UI model

### Header

- Vector LocalDraft logo and product name
- Visible **100 % lokalt** status
- **Inställningar** entry point

### Document panel

- **Nytt dokument** is the primary sidebar action.
- Documents are ordered by most recently modified.
- There is no document-search field.
- Each document has its own three-dot action menu.
- The panel collapses to a persistent **Dokument** rail.
- In compact layouts it opens as an overlay rather than permanently taking
  editor width.

### Editor

- The document title aligns with the main editor and header controls.
- Rich text is stored as RTF and mirrored as plain text.
- Autosave does not clear or rebuild the document collection, which prevents
  selection flicker and preserves the first item as a selectable target.
- **Kopiera dokumenttext** copies both RTF and Unicode plain text after an
  explicit user action.

### Dictation

- A single click on **Diktera** opens the recording window and starts
  recording.
- The currently selected microphone is shown beside the button.
- The level meter uses logarithmic scaling so normal speech is easier to see.
- Microphone selection and testing live in **Inställningar**, not in the
  recording dialog.

### Text processing

The user-facing term is **Bearbeta text**, not “Textassistent”. Common actions
are shown first; custom instructions, history, and other advanced functions use
progressive disclosure. Generated text is a proposal and is never silently
applied.

## Feature inventory

| Area | Current behavior |
| --- | --- |
| Documents | Create, rename, autosave, select, and permanently delete |
| Rich text | RTF editing, plain-text mirror, rich clipboard copy |
| Versions | Content-hash deduplication, automatic/manual reasons, restore |
| Audio | WASAPI capture, live level, durable partial file, PCM conversion |
| Recovery | Startup detection of interrupted `.partial.wav` recordings |
| Transcription | Bundled `whisper-cli.exe`, Swedish, local CPU processing |
| Text processing | Bundled Qwen model through LLamaSharp, CPU-only |
| Assistant safety | Source isolation, protected-value checks, review preview |
| Settings | Microphone selection/test and local storage/privacy information |
| Diagnostics | Metadata-only local logs with bounded total size |
| Packaging | Self-contained `win-x64` folder and portable ZIP |

## Deliberate non-features

Do not add these without changing the product contract and receiving explicit
direction:

- Cloud AI, cloud speech, telemetry, analytics, crash upload, or update checks
- Runtime downloads or model acquisition
- User accounts, synchronization, collaboration, or network shares
- AppData, registry, user-profile, or machine-wide fallback storage
- Forensic secure-deletion claims
- Automatic clipboard use

## Important constraints and limitations

- `AppRoot` must be writable. The application fails clearly rather than
  redirecting data elsewhere.
- Portable folders inside OneDrive, Dropbox, or similar sync roots trigger a
  warning because another program may copy local data.
- The models and native Whisper files are large and intentionally ignored by
  Git.
- Text generation is serialized and can be slow on low-end CPUs.
- Assistant preview supports a small Markdown subset: headings, bullet lists,
  and bold text.
- Protected-token detection reduces accidental changes to names and exact
  values but does not replace user review.
- Tests may use the operating-system temporary folder inside isolated test
  fixtures. Runtime code may not.

## Documentation authority

When documents overlap, use this order:

1. [`../AGENTS.md`](../AGENTS.md) for repository rules.
2. [`../PRIVACY.md`](../PRIVACY.md) for supported privacy claims.
3. Current source and tests for implemented behavior.
4. [`IMPLEMENTATION_GUIDE.md`](IMPLEMENTATION_GUIDE.md) for the code map and
   runtime flows.
5. [`DEVELOPMENT_AND_RELEASE.md`](DEVELOPMENT_AND_RELEASE.md) and
   [`TESTING.md`](TESTING.md) for contributor procedures.
6. [`../ANVANDARGUIDE.md`](../ANVANDARGUIDE.md) for user-facing workflows.
7. UX review documents for historical rationale, not current behavior.

## Current implementation baseline

The UI and recorded-review changes described here were merged through GitHub
PR #1 on 2026-08-31. Relevant commits include:

- `f41a924` — clearer text-processing UX
- `73414b0` — document-sidebar navigation
- `f3867a6` — recorded UX-review feedback
- `db7b5c2` — document-title alignment

See [`reviews/2026-08-31-recorded-ux-review.md`](reviews/2026-08-31-recorded-ux-review.md)
for the sanitized review brief and implementation mapping.
