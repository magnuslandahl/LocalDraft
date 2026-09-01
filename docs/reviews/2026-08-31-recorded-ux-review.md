# Recorded UX review — 2026-08-31

This is the sanitized, repository-local brief from a spoken screen review of
LocalDraft. It preserves the actionable product decisions without committing
the raw recording, extracted audio, keyframes, transcript, personal paths, or
other user-provided content.

The review was captured and transcribed locally with OBS Review Recorder. The
local run identifier was `2026-08-31-172525`.

## Transcript-derived requests

The spoken review requested these concrete changes:

1. Move document-specific actions out of a generic sidebar-header menu and put
   them on each document row so their target is obvious.
2. Rename **Kopiera allt** to a clearer action for copying the document text.
3. Make dictation start directly from the main button instead of requiring a
   second start action in the recording dialog.
4. Show which microphone will be used before recording.
5. Make the microphone level feedback useful for ordinary speech.
6. Put microphone selection and testing in settings so the recording dialog is
   focused on the active recording.
7. Make settings easier to understand, with microphone setup prominent and
   storage/privacy detail available without dominating the default view.

A later spoken follow-up asked for the document title to align with the combo
box/header and the main editor rather than retaining excess left padding.

## Visual interpretation

Keyframes supported, but did not independently add to, the spoken requests:

- The old header-level overflow menu appeared visually detached from the
  document it affected.
- Dictation had an avoidable two-step start.
- Microphone state was not visible enough in the main workflow.
- The title field's inherited inner padding made its text start farther right
  than neighboring content.

These are visual inferences from the review and should not be treated as
additional verbatim user requirements.

## Implemented outcome

| Request | Result | Main area |
| --- | --- | --- |
| Targeted document actions | Three-dot menu added to every document row | `MainWindow.xaml(.cs)` |
| Clear copy label | Renamed to **Kopiera dokumenttext** | Main window and user guide |
| One-click dictation | Recording auto-starts with the saved microphone | `RecordingWindow.xaml.cs` |
| Visible microphone | Selected device appears beside **Diktera** | `MainWindow.xaml(.cs)` |
| Better level feedback | Logarithmic level normalization | `AudioLevelMeter.cs` |
| Focused recording dialog | Device selection removed from that dialog | `RecordingWindow.xaml` |
| Better settings | Microphone selection/test first; local-storage detail disclosed progressively | `SettingsWindow.xaml(.cs)` |
| Stable start/close | Retry and race guards added around auto-start | `RecordingWindow.xaml.cs` |
| Title alignment | Inherited text-box padding removed from the title field | `MainWindow.xaml` |

The main recorded-review implementation is commit `f3867a6`. Title alignment is
commit `db7b5c2`. Both were merged through GitHub PR #1.

## Related UX work in the same iteration

The surrounding iteration also:

- renamed the text assistant surface to **Bearbeta text**
- clarified action scope and result review
- embedded navigation controls in the document panel
- added a collapsed **Dokument** rail and compact overlay
- moved **100 % lokalt** beside the LocalDraft title
- removed document search
- introduced a sharp vector LocalDraft logo
- fixed document-list flicker and first-item selection across multiple files

Those changes are documented as current behavior in
[`../PROJECT_OVERVIEW.md`](../PROJECT_OVERVIEW.md).

## Privacy handling

The raw artifacts remain intentionally outside Git because they may contain
spoken user content, screen contents, absolute paths, and other private data.
Future agents should use this sanitized brief for rationale and inspect current
source for behavior. Do not request or commit the raw media merely to
reconstruct this already-documented review.

## Historical context

An earlier keyboard and UX review is preserved in
[`../../UX_REVIEW_ITERATION_2.md`](../../UX_REVIEW_ITERATION_2.md). This review
supersedes it where the interaction changed, especially one-click dictation,
per-document action placement, and current **Bearbeta text** terminology.
