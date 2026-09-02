# Distribution, code signing and OS warnings

Status: **proposal.** Nothing here is configured yet.

## The problem

LocalDraft is unsigned, so both desktop platforms warn about it:

- **Windows / SmartScreen:** "Windows skyddade din dator". The user must click
  *Mer information* then *Kör ändå*. Non-technical users read this as "this file
  is dangerous" and stop.
- **macOS / Gatekeeper:** stricter. An unsigned, un-notarized app is blocked
  rather than merely warned about, and the old right-click-to-open workaround has
  been progressively tightened. In practice **macOS cannot ship unsigned.**

Two separate causes: no signature (identity), and no reputation (history).
Signing fixes the first and, over downloads, earns the second.

## Windows options

| Option | Cost | Removes the warning | CI-friendly | Verdict |
| --- | --- | --- | --- | --- |
| Stay unsigned | free | No | n/a | Today's state |
| **Azure Trusted Signing** | ~10 USD/month | Signature immediately; reputation accrues quickly under a Microsoft-operated CA | Yes, `azure/trusted-signing-action` | **Recommended** |
| OV certificate + cloud HSM | ~250–500 USD/year | Signed, but reputation still builds slowly | Yes, with an HSM service | Worse value |
| EV certificate + cloud HSM | ~400–700 USD/year | Yes, immediate SmartScreen reputation | Yes, with an HSM service | Fallback if Trusted Signing validation fails |
| MSIX in the Microsoft Store | 19 USD once | Yes | Yes | **Rejected** — see below |
| Self-signed certificate | free | No | Yes | Pointless. SmartScreen ignores it |

**Why Azure Trusted Signing.** It is the only option in that table with a
sensible price for a solo project that also works cleanly in GitHub Actions:
certificates are short-lived and managed for you, so there is no hardware token
to plug into a build machine. Since 2023 the CA/Browser Forum requires private
keys on hardware or an HSM, which is what makes traditional OV and EV
certificates awkward and expensive to automate.

**Why not the Microsoft Store.** MSIX would remove the warning, but it
virtualizes writes and makes the install directory read-only. That directly
breaks the storage model, where everything lives in a writable folder beside the
executable. Choosing MSIX would mean redesigning storage — a large change to
avoid one dialog.

Note that Trusted Signing requires an Azure subscription and identity validation.
Organisation validation asks for three years of verifiable history; individual
validation is available. Confirm eligibility before budgeting for it, and fall
back to an EV certificate if it is refused.

## macOS requirements

Not optional, and needed before the macOS port in
[`CROSS_PLATFORM_PLAN.md`](CROSS_PLATFORM_PLAN.md) can ship at all.

1. **Apple Developer Program**, 99 USD/year.
2. **Developer ID Application** certificate.
3. Sign every nested binary — the app, and every `.dylib` from LLamaSharp and
   Whisper.net — from the inside out, with the **hardened runtime** enabled.
4. **Notarize** the `.dmg` with `notarytool`, then **staple** the ticket so the
   app also opens offline.
5. Entitlements: microphone input, plus the JIT and unsigned-executable-memory
   entitlements that .NET's runtime requires under the hardened runtime.

## Free measures, worth doing regardless

| Measure | Status | Value |
| --- | --- | --- |
| Publish `SHA256SUMS.txt` | **Done** | Users can verify a download |
| **Build provenance attestations** | Proposed | `actions/attest-build-provenance` cryptographically ties each asset to this repository and workflow. Verifiable with `gh attestation verify`. Free, and a natural next step |
| Pinned, verified inputs | **Done** | Lock files, pinned SDK, SHA-256-verified models |
| `winget` manifest | Proposed | `winget install LocalDraft` is a trusted, familiar channel. Does not remove SmartScreen but improves discoverability |
| Clear README guidance | **Done** | Tells users what the warning means |

## Recommended order

1. **Build provenance attestations.** Free, immediate, no accounts.
2. **Apple Developer Program.** Required for macOS anyway, so buy it when the
   port starts, not after.
3. **Azure Trusted Signing** for Windows once eligibility is confirmed.
4. **winget** once a signed build exists.

Approximate ongoing cost: **~120 USD/year for Windows plus 99 USD/year for
Apple**, so roughly **220 USD/year** for both platforms.

## Handling signing secrets

Signing credentials are the most sensitive thing this repository will touch.

- Store them as GitHub **Environment** secrets with required reviewers, not as
  plain repository secrets, so a pull request cannot exfiltrate them.
- Only the tag-triggered release job may access the signing environment. Pull
  request builds must stay unsigned.
- Never commit certificates, provisioning profiles, API keys or notarization
  credentials. `tools/verify-repository.ps1` already fails the build on
  credential-shaped content and on `.pfx`/`.p12` files.
- `main` is protected and requires review, so a workflow change that touches the
  signing environment cannot land unreviewed.
