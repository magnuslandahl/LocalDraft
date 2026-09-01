# LocalDraft repository instructions

These instructions apply to the entire repository. LocalDraft handles
privacy-sensitive Swedish documents, recordings, transcripts, and local AI
output. Privacy and storage containment are product requirements, not optional
hardening.

## Start here

Before making a non-trivial change, read the documents that cover that area:

| Document | Purpose |
| --- | --- |
| [`docs/PROJECT_OVERVIEW.md`](docs/PROJECT_OVERVIEW.md) | Product, current UX, terminology, supported environment, non-features |
| [`docs/IMPLEMENTATION_GUIDE.md`](docs/IMPLEMENTATION_GUIDE.md) | Code map, DI, runtime storage tree, document/audio/AI flows, concurrency |
| [`PRIVACY.md`](PRIVACY.md) | Authoritative privacy and storage claims |
| [`docs/DEVELOPMENT_AND_RELEASE.md`](docs/DEVELOPMENT_AND_RELEASE.md) | Setup, build, run, portable packaging, release checklist |
| [`docs/TESTING.md`](docs/TESTING.md) | Current 27-test baseline, coverage, test-selection matrix |
| [`ANVANDARGUIDE.md`](ANVANDARGUIDE.md) | Current Swedish user workflows and labels |
| [`ARCHITECTURE.md`](ARCHITECTURE.md) | Concise architecture overview |
| [`MODEL_EVALUATION.md`](MODEL_EVALUATION.md) | Model selection, hashes, evaluation corpus, measured performance |
| [`DEPENDENCIES.md`](DEPENDENCIES.md) | Pinned SDK, package, native, model, and license inventory |
| [`COMPLETION_REPORT.md`](COMPLETION_REPORT.md) | Implemented-delivery status and verification record |
| [`docs/reviews/2026-08-31-recorded-ux-review.md`](docs/reviews/2026-08-31-recorded-ux-review.md) | Sanitized brief and rationale from the latest recorded review |
| [`UX_REVIEW_ITERATION_2.md`](UX_REVIEW_ITERATION_2.md) | Earlier historical UX review |

When documents disagree, use this priority: this file, `PRIVACY.md`, current
source and tests, detailed implementation/development guides, then historical
review documents.

## Project facts

- User-facing product name: **LocalDraft**.
- Source/assembly name: `LocalDraft`.
- Platform: .NET 10 WPF on Windows x64.
- Distribution: self-contained, portable, non-single-file, untrimmed.
- Runtime AI: bundled Whisper CLI and Qwen through LLamaSharp, both CPU-only.
- Main projects:
  - `LocalDraft.Core`: models, contracts, prompt/text safety.
  - `LocalDraft.Infrastructure`: storage, audio, AI, recovery, settings,
    logs.
  - `LocalDraft.App`: WPF windows, view model, workflow coordination.
- All mutable runtime state is below `AppContext.BaseDirectory\Data`.
- Models and native files are required locally but ignored by Git.

## Public repository safety

- This is a public repository. Treat every committed file and all Git metadata as
  publicly visible.
- Before every commit, inspect `git status --short`, the staged file list, and
  the complete staged diff for secrets or sensitive information. Check for API
  keys, tokens, passwords, private keys, connection strings, internal URLs,
  personal data, user content, machine-specific paths, and accidentally staged
  ignored or generated files.
- Before committing, also verify that the author and committer names and email
  addresses are appropriate for permanent public attribution. Use a GitHub
  noreply address instead of a private or corporate email address.
- Never commit a real secret, even in examples, tests, documentation, fixtures,
  generated output, or Git configuration. Use unmistakably fake placeholders
  where examples require credential-shaped values.
- If sensitive information is found, stop the commit, remove it from the change
  and repository history as appropriate, and tell the user that exposed
  credentials must be revoked or rotated. Do not repeat the sensitive value in
  logs, commit messages, review comments, or chat.
- Push only explicitly reviewed branches and tags to a public remote. Never use
  `git push --all` or `git push --mirror`; local-only refs may contain private
  workspace or checkpoint history that is not intended for publication.

## Workspace policy

- Work directly on `main` in the repository's existing primary/main worktree
  by default. Do not create additional Git worktrees or feature branches for
  normal development, reviews, builds, or releases.
- Do not create or use pull requests for this project. Commit completed,
  verified changes directly to `main` and push `main` to the remote unless the
  user explicitly requests a different workflow.
- If a managed host has already placed the agent in an isolated worktree and
  explicitly prohibits access to the primary checkout, remain in the provided
  worktree. Do not create another worktree or open a pull request; synchronize
  with `origin/main` and integrate directly into `main` when host rules permit.

## Change map

| Change area | Primary files | Minimum validation |
| --- | --- | --- |
| Main layout, document sidebar, labels | `MainWindow.xaml`, `App.xaml` | App tests |
| Selection, autosave, editor workflow | `MainWindow.xaml.cs`, `MainWindowViewModel.cs` | App + Infrastructure tests |
| Storage, versions, deletion, history | `AppPathService.cs`, `DocumentStorage.cs`, `AssistantHistoryService.cs` | Core + Infrastructure tests |
| Recording and microphone UX | `RecordingWindow.xaml(.cs)`, `SettingsWindow.xaml(.cs)`, `AudioServices.cs` | Infrastructure + App tests |
| Transcription or text generation | `AiServices.cs`, `TextProcessing.cs` | Core + Infrastructure tests |
| Startup or recovery | `App.xaml.cs`, `PartialRecordingRecovery.cs` | Infrastructure + App tests |
| Models, dependencies, package | app `.csproj`, `tools/`, manifests and dependency docs | Full tests + package verification |

## Privacy invariants

- Runtime behavior must remain fully offline. Do not add HTTP clients, sockets,
  telemetry, analytics, update checks, remote fonts, web views, cloud speech,
  cloud AI, or runtime downloads.
- All user data and runtime artifacts must remain below
  `AppContext.BaseDirectory` (`AppRoot`). This includes documents, versions,
  WAV files, assistant history, settings, logs, model caches, temporary files,
  crash-related application output, and native-library caches.
- Never fall back to `%TEMP%`, `%APPDATA%`, `%LOCALAPPDATA%`, the user profile,
  Documents, Desktop, registry-backed settings, or another machine-wide
  location. If `AppRoot` is not writable, fail clearly instead of storing
  elsewhere.
- Configure process environment containment before services, native libraries,
  audio devices, or models are initialized. Preserve early initialization in
  `App`.
- Validate every constructed or user-influenced path with the existing
  containment helpers. Preserve reparse-point and symlink defenses for reads,
  writes, moves, recovery, and deletion.
- Keep writes atomic where the repository uses atomic replacement. Do not trade
  containment or durability for convenience.
- Deletion is permanent and must remove the document, versions, recordings,
  partial audio, and assistant history that belong to it. Keep explicit
  confirmation and do not claim forensic secure deletion.
- Logs must not contain titles, document text, transcripts, assistant
  prompts/results, recording contents, microphone names, personal names, child
  process output, or other user-provided content.
- Clipboard access is allowed only after an explicit user action. Preserve the
  explanation that Windows clipboard history or synchronization may retain
  copied text.
- Runtime child processes are limited to bundled local executables required by
  the product. Launch without a shell, use structured arguments and contained
  working/temp paths, and drain redirected output.

## Network boundary

- Network access is allowed only in clearly developer-only acquisition tooling
  such as `tools/fetch-models.ps1`. The application must never invoke it.
- Runtime projects under `src/` must not reference networking or telemetry
  packages or APIs.
- Keep privacy regression tests and package verification current when projects,
  dependencies, native files, or package layout change.
- Do not weaken visible **100 % lokalt** messaging or make broader claims than
  `PRIVACY.md` and `COMPLETION_REPORT.md` support.

## Current UX invariants

- Writing and dictation are the primary workflow. Advanced AI, history,
  recording management, storage detail, and destructive actions use progressive
  disclosure.
- Use current Swedish terminology: **Bearbeta text**,
  **Kopiera dokumenttext**, **Diktera**, and **Inställningar**.
- Dictation starts with one click using the saved microphone. Device selection
  and testing belong in Settings, and the selected device remains visible near
  the Dictate action.
- Document-specific actions belong on each document row, not in an ambiguous
  global overflow menu.
- Do not reintroduce document search without a new demonstrated need.
- Do not rebuild the bound document collection during save or selection; update
  the affected summary in place to avoid flicker and selection races.
- Assistant output is a reviewable proposal. Never apply generated text
  silently.
- Icon-only controls are acceptable only for familiar actions. Give every icon
  control a Swedish tooltip, keyboard focus, and
  `AutomationProperties.Name`.
- Keep explicit text on primary, unfamiliar, privacy-sensitive, and destructive
  actions.
- Preserve keyboard operation, visible focus, high-contrast compatibility, and
  usable layouts at 100%, 125%, and 150% display scaling.

## Change requirements

- Build context from the existing contracts, containment helpers, and tests
  before adding code. Reuse them rather than introducing parallel mechanisms.
- Keep changes surgical but complete across UI, services, tests, and relevant
  documentation.
- Run the smallest relevant tests first, then full solution tests before a
  release or broad UI completion.
- For privacy, storage, deletion, recovery, model, native-process, or packaging
  changes, also run the privacy regression tests and
  `tools/verify-package.ps1` against the produced package.
- Update `PRIVACY.md`, `ANVANDARGUIDE.md`, `ARCHITECTURE.md`,
  `COMPLETION_REPORT.md`, or the detailed docs when behavior or supported claims
  change.
- Never commit downloaded models, native binaries, generated `Data`, build
  output, portable ZIP files, user content, or raw recorded-review artifacts.
