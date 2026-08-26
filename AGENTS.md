# Lokal Diktering - repository instructions

These instructions apply to the entire repository.

## Product intent

Lokal Diktering handles privacy-sensitive Swedish documents, recordings, transcripts,
and local AI output. Privacy and storage containment are product requirements, not
optional hardening.

## Privacy invariants

- Runtime behavior must remain fully offline. Do not add HTTP clients, sockets,
  telemetry, analytics, update checks, remote fonts, web views, cloud speech, or
  cloud AI services.
- All user data and runtime artifacts must remain below `AppContext.BaseDirectory`
  (`AppRoot`). This includes documents, versions, WAV files, assistant history,
  settings, logs, model caches, temporary files, crash-related application output,
  and native-library caches.
- Never fall back to `%TEMP%`, `%APPDATA%`, `%LOCALAPPDATA%`, the user profile,
  Documents, Desktop, registry-backed settings, or another machine-wide location.
  If `AppRoot` is not writable, fail clearly instead of storing elsewhere.
- Configure process environment containment before services, native libraries, or
  models are initialized. Preserve the early initialization in `App`.
- Validate every constructed or user-influenced path with the existing containment
  helpers. Preserve reparse-point and symlink defenses for reads, writes, moves,
  recovery, and deletion.
- Keep writes atomic where the repository already uses atomic replacement. Do not
  trade containment or durability for convenience.
- Deletion is permanent and must remove the document, versions, recordings, and
  assistant history that belong to it. Keep explicit confirmation for destructive
  actions and do not claim forensic secure deletion.
- Logs must not contain document text, transcripts, assistant prompts/results,
  recording contents, personal names, or other user-provided content.
- Clipboard access is allowed only after an explicit user action. Preserve the
  privacy explanation that Windows clipboard history or synchronization may retain
  copied text.
- Runtime child processes are limited to bundled, local executables required by the
  product. Launch them without a shell, with structured arguments and contained
  working/temp paths. Drain redirected output to avoid deadlocks.

## Network boundary

- Network access is allowed only in clearly developer-only acquisition tooling such
  as `tools/fetch-models.ps1`. It must never be invoked by the application.
- Runtime projects under `src/` must not reference networking or telemetry packages
  or APIs. Keep the privacy regression tests and package verification checks current
  when project structure or dependencies change.
- Do not weaken the visible "100 % lokalt" messaging or make broader privacy claims
  than `PRIVACY.md` and `COMPLETION_REPORT.md` support.

## UX and accessibility

- The primary workflow is writing or dictating. Use progressive disclosure so
  advanced AI, history, recording management, and destructive actions do not
  dominate the default view.
- Icon-only controls are acceptable only for familiar actions. Give every icon
  control a Swedish tooltip, keyboard focus, and `AutomationProperties.Name`.
- Keep explicit text on primary, unfamiliar, privacy-sensitive, and destructive
  actions.
- Preserve keyboard operation, visible focus, high-contrast compatibility, and
  usable layouts at 100%, 125%, and 150% display scaling.

## Change requirements

- Keep changes surgical and reuse the existing services and containment helpers.
- Run the smallest relevant tests first, then the full solution tests before a
  release or broad UI completion.
- For privacy, storage, deletion, recovery, model, native-process, or packaging
  changes, also run the privacy regression tests and `tools/verify-package.ps1`
  against the produced package.
- Update `PRIVACY.md`, `ANVANDARGUIDE.md`, `ARCHITECTURE.md`, or
  `COMPLETION_REPORT.md` when behavior or supported claims change.
- Never commit downloaded models, native binaries, generated `Data/`, build output,
  or portable ZIP files.
