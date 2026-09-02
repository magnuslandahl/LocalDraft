# Distribution, code signing and OS warnings

Status: **proposal.** Nothing is configured yet. No signing material exists in
this repository, and none may ever be committed to it.

## The problem

LocalDraft is unsigned, so both desktop platforms warn about it:

- **Windows / SmartScreen:** "Windows skyddade din dator". The user must click
  *Mer information* then *Kör ändå*. Non-technical users read this as "this file
  is dangerous" and stop.
- **macOS / Gatekeeper:** stricter. An unsigned, un-notarized app is blocked
  rather than merely warned about. In practice **macOS cannot ship unsigned.**

Two separate causes: no signature (identity) and no reputation (history).
Signing fixes the first and, over downloads, earns the second.

## macOS: use the existing Apple Developer account

An Apple Developer Program membership already exists, so the yearly 99 USD is a
sunk cost rather than a new one. What is still needed:

1. A **Developer ID Application** certificate, for distribution outside the Mac
   App Store. On an *organization* account only the Account Holder or an Admin
   can create one.
2. Sign every nested binary — the app plus every `.dylib` from LLamaSharp and
   Whisper.net — from the inside out, with the **hardened runtime** enabled.
3. **Notarize** the `.dmg` with `notarytool`, then **staple** the ticket so the
   app also opens offline.
4. Entitlements: microphone input, plus the JIT and unsigned-executable-memory
   entitlements that .NET requires under the hardened runtime.

### One thing to confirm before using it

A Developer ID signature is an identity claim. If the account is an
**organization** account, the signed app is published in that organization's
name, and Gatekeeper shows that name to every user. Confirm that using the
organization's certificate for this project is intended and permitted before
wiring it into CI. If it is not, an **individual** Apple Developer account keeps
the project's identity separate. This is a question about attribution and
responsibility, not a technical blocker.

### Secrets this needs (stored in GitHub, never in the repository)

| Secret | Contents |
| --- | --- |
| `MACOS_CERTIFICATE` | Base64 of the exported Developer ID `.p12` |
| `MACOS_CERTIFICATE_PASSWORD` | Password for that export |
| `MACOS_KEYCHAIN_PASSWORD` | Throwaway password for the temporary CI keychain |
| `APPLE_TEAM_ID` | Team identifier |
| `APPLE_API_KEY_ID`, `APPLE_API_ISSUER_ID`, `APPLE_API_KEY_P8` | App Store Connect API key for `notarytool`, the `.p8` base64-encoded |

The job imports the certificate into a temporary keychain, signs, notarizes,
staples, and deletes the keychain. Nothing is written to the working tree.

Prefer an App Store Connect **API key** over an Apple ID and app-specific
password: it is scoped, revocable, and does not carry a personal login.

## Windows: the Microsoft Store is back on the table

The earlier version of this document rejected the Store. That rejection was
**purely technical and had nothing to do with privacy** — the Store is perfectly
compatible with an app that keeps all data on the user's machine.

The actual objection was this: an MSIX package installs into
`C:\Program Files\WindowsApps`, which is read-only, so the app could not write
its `Data` folder next to the executable the way it does today.

That objection **disappears with work we are doing anyway.** Decision D5 in
[`CROSS_PLATFORM_PLAN.md`](CROSS_PLATFORM_PLAN.md) already replaces the single
"beside the executable" root with a per-platform contained root, because macOS
forces it. The same abstraction gives an MSIX build its proper per-user location,
and the containment rules stay exactly as strict.

| Option | Cost | Removes the warning | Notes |
| --- | --- | --- | --- |
| **Microsoft Store (MSIX)** | one-time developer account fee, low, sometimes none for individuals | **Yes, completely** | Microsoft signs the package. Free auto-update. Requires D5 |
| Azure Trusted Signing | ~10 USD/month | Signature immediately, reputation accrues quickly | Works in GitHub Actions with no hardware token |
| EV certificate + cloud HSM | ~400–700 USD/year | Yes, immediate reputation | Fallback if Trusted Signing validation is refused |
| OV certificate + cloud HSM | ~250–500 USD/year | Signed, reputation still builds slowly | Poor value |
| Win32 app listed in the Store | same account fee | **No** | The Store just runs your installer, which is still unsigned |
| Self-signed certificate | free | No | SmartScreen ignores it |

### What the Store does and does not solve

It solves the warning **for users who install from the Store**. It does nothing
for someone downloading the ZIP or the installer from GitHub Releases — those
stay unsigned unless a certificate is also bought.

A sensible split:

- **Microsoft Store** as the channel for non-technical users. Warning-free,
  auto-updating, no yearly certificate cost.
- **GitHub Releases** for everyone else, with checksums and build provenance.

Other things worth knowing before committing to the Store: the package is
roughly 1.5 GB, so uploads and certification are slow; the microphone needs a
`microphone` device capability in the package manifest; and Store certification
adds review latency to every release. None of these are blockers.

### Recommendation

1. Do **not** buy a Windows certificate yet.
2. Land D5 for macOS, then evaluate MSIX and the Store. If it works, Windows is
   warning-free for the audience that needs it most, at no recurring cost.
3. Only buy Azure Trusted Signing if the GitHub downloads must also be
   warning-free.

## Free measures, worth doing regardless

| Measure | Status | Value |
| --- | --- | --- |
| Publish `SHA256SUMS.txt` | **Done** | Users can verify a download |
| **Build provenance attestations** | Proposed | `actions/attest-build-provenance` cryptographically ties each asset to this repository and workflow, verifiable with `gh attestation verify`. Free, no accounts |
| Pinned, verified inputs | **Done** | Lock files, pinned SDK, SHA-256-verified models |
| `winget` manifest | Proposed | A familiar install channel. Does not remove SmartScreen on its own |

## Keeping signing material out of a public repository

This repository is public. Signing material must never enter it, in any branch,
and must never be reachable from a pull request.

- **Store credentials as GitHub Environment secrets**, not repository secrets,
  and require a reviewer on that environment. Only the tag-triggered release job
  may reference it.
- **Pull requests from forks cannot read secrets at all**, and their
  `GITHUB_TOKEN` is read-only. This is the main protection for a public repo, and
  it is the reason the signing steps must live in the tag-triggered job rather
  than in the pull-request bundle job in `release.yml`.
- **Never commit certificates, keys, provisioning profiles or API keys.**
  `tools/verify-repository.ps1` fails the build when a tracked file has a
  `.pfx`, `.p12`, `.cer`, `.crt`, `.der`, `.pem`, `.key`, `.jks`, `.keystore`,
  `.kdbx`, `.p8`, `.mobileprovision` or `.provisionprofile` extension, or is a
  `.env` or SSH private key. It also fails on credential-shaped file contents.
- **Keep secrets out of logs.** GitHub masks secret values, but base64 blobs
  should still never be echoed. Delete temporary keychains and key files in a
  step that always runs.
- `main` is protected and requires review, so a workflow change that reaches the
  signing environment cannot land unreviewed.
