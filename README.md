# LocalDraft

**Skriv och diktera på svenska – helt lokalt på din egen dator.**

[![CI](https://github.com/magnuslandahl/LocalDraft/actions/workflows/ci.yml/badge.svg)](https://github.com/magnuslandahl/LocalDraft/actions/workflows/ci.yml)
[![Bundle and release](https://github.com/magnuslandahl/LocalDraft/actions/workflows/release.yml/badge.svg)](https://github.com/magnuslandahl/LocalDraft/actions/workflows/release.yml)

LocalDraft är ett skrivprogram för Windows där du kan skriva text, diktera med mikrofonen
och låta en lokal AI-modell renskriva, förbättra eller sammanfatta det du skrivit.

Allt sker på din egen dator. Dokument, ljudinspelningar, transkriberingar och AI-resultat
lämnar aldrig datorn, och programmet fungerar helt utan internet. Både talmodellen och
språkmodellen följer med i nedladdningen.

---

## Ladda ner

**[Hämta senaste versionen](https://github.com/magnuslandahl/LocalDraft/releases/latest)**

| Fil | Välj den här om du … |
| --- | --- |
| `LocalDraft-…-setup-win-x64.exe` | vill installera på vanligt sätt och få en genväg på Start-menyn **(rekommenderas)** |
| `LocalDraft-…-portable-win-x64.zip` | vill köra programmet från en egen mapp eller ett USB-minne |
| `SHA256SUMS.txt` | vill kontrollera att nedladdningen är oskadad |

Nedladdningen är stor, cirka 1,5 GB, eftersom AI-modellerna ingår. Du behöver alltså
aldrig ladda ner något mer efteråt.

### Så installerar du

1. Ladda ner `…setup-win-x64.exe` och dubbelklicka på filen.
2. Får du meddelandet **"Windows skyddade din dator"**? Klicka **Mer information** och
   sedan **Kör ändå**. Meddelandet visas eftersom programmet ännu inte är signerat med
   ett kommersiellt certifikat, inte för att något är fel med filen.
3. Följ guiden. Installationen kräver **inga administratörsrättigheter** och installerar
   programmet i din egen användarmapp.
4. Starta **LocalDraft** från Start-menyn.
5. Tillåt mikrofonen när Windows frågar. Den behövs bara när du dikterar.

Använder du ZIP-filen i stället: packa upp **hela** ZIP-filen till en vanlig lokal mapp
där du får spara, till exempel `C:\LocalDraft`, och starta `LocalDraft.exe`. Undvik
OneDrive, Dropbox, Google Drive och nätverksmappar.

### Systemkrav

- Windows 10 (22H2) eller Windows 11, 64-bitars
- Cirka 4 GB ledigt diskutrymme
- Minst 8 GB RAM rekommenderas
- Mikrofon om du vill diktera

Ingen internetanslutning krävs för att använda programmet.

---

## Så använder du LocalDraft

1. Klicka **Nytt dokument** och börja skriva. Allt sparas automatiskt.
2. Klicka **Diktera** för att spela in. Inspelningen startar direkt och transkriberas
   lokalt när du klickar **Klar – transkribera**.
3. Markera text och välj **Bearbeta text** för att renskriva, förbättra, strukturera
   eller sammanfatta. Du får alltid ett förslag att läsa igenom innan något ersätts.
4. Varje dokument har en egen meny **⋯** med versionshistorik, inspelningar, kopiering
   och permanent borttagning.

Hela guiden med tangentbordsgenvägar finns i **[ANVANDARGUIDE.md](ANVANDARGUIDE.md)**.

---

## 100 % lokalt

- Programmet innehåller inga nätverksfunktioner: ingen molntjänst, ingen telemetri,
  ingen uppdateringskontroll och inga nedladdningar under körning.
- Dokument, WAV-filer, versioner, AI-historik, inställningar, loggar och temporära filer
  sparas i undermappen `Data` bredvid programfilen.
- Vill du säkerhetskopiera eller flytta allt: kopiera hela programmappen.
- Vill du ta bort allt: radera hela programmappen.

Den fullständiga och bindande beskrivningen finns i **[PRIVACY.md](PRIVACY.md)**.

### Vad ingår

| Komponent | Roll |
| --- | --- |
| Whisper `small q5_1` | Svensk taltranskribering, körs som lokal process |
| Qwen3 1.7B `Q4_K_M` | Svensk textbearbetning via LLamaSharp, körs i samma process |

Modellerna körs på processorn (CPU) och kräver inget grafikkort. Se
**[MODEL_EVALUATION.md](MODEL_EVALUATION.md)** för mätningar och urval.

---

## Vanliga frågor

**Varför är nedladdningen så stor?**
AI-modellerna ingår så att programmet fungerar direkt och helt utan internet.

**Fungerar det på Mac eller Linux?**
Nej. Programmet är byggt med WPF som bara finns på Windows. På Windows med ARM-processor
fungerar det via Windows inbyggda x64-emulering.

**Varnar Windows för programmet?**
Ja, första gången. Programmet är inte signerat med ett kommersiellt certifikat ännu.
Du kan kontrollera din nedladdning mot `SHA256SUMS.txt` i releasen.

**Skickas något till internet?**
Nej. Om du kopierar text kan däremot Windows egen urklippshistorik eller synkronisering
mellan enheter spara texten. Det stänger du av i Windows inställningar.

---

## För utvecklare

Krav: Windows 10/11 x64, .NET SDK enligt [`global.json`](global.json), PowerShell 7.

```powershell
dotnet restore .\LocalDraft.slnx --locked-mode
dotnet build .\LocalDraft.slnx
dotnet test .\LocalDraft.slnx

.\tools\fetch-models.ps1        # hämtar och verifierar modeller, endast för utvecklare
dotnet run --project .\src\LocalDraft.App
```

Bygg en komplett distribution lokalt:

```powershell
.\tools\fetch-models.ps1
.\tools\build-portable.ps1      # självständigt paket + verifierad ZIP
.\tools\build-installer.ps1     # installationsprogram, kräver Inno Setup 6
```

### Projektstruktur

- `src/LocalDraft.Core` – domänmodeller, kontrakt, prompt- och textsäkerhet
- `src/LocalDraft.Infrastructure` – lagring, ljud, Whisper, LLamaSharp, lokal loggning
- `src/LocalDraft.App` – WPF-gränssnittet
- `tests` – enhets-, lagrings-, integritets- och projektkontroller
- `tools` – modellhämtning, paketering, paket- och repoverifiering
- `packaging` – installationsprogram och texter för slutanvändare
- `docs` – detaljerad produkt-, implementations-, test- och releasekunskap

### Bidra

`main` är skyddad. Alla ändringar går via pull request med grön CI:

```powershell
git switch -c mitt-andringsforslag
# gör ändringen
dotnet test .\LocalDraft.slnx
.\tools\verify-repository.ps1
git push -u origin mitt-andringsforslag
gh pr create --fill
```

Det här är ett **publikt** repo. Kontrollera alltid att ändringen inte innehåller
nycklar, lösenord, personuppgifter, användarinnehåll eller maskinspecifika sökvägar
innan du committar. Se [AGENTS.md](AGENTS.md) och
[docs/DEVELOPMENT_AND_RELEASE.md](docs/DEVELOPMENT_AND_RELEASE.md).

---

## Dokumentation

| Dokument | Innehåll |
| --- | --- |
| [ANVANDARGUIDE.md](ANVANDARGUIDE.md) | Slutanvändarguide |
| [PRIVACY.md](PRIVACY.md) | Auktoritativ integritets- och lagringsmodell |
| [AGENTS.md](AGENTS.md) | Bindande instruktioner och läsordning för AI-agenter |
| [docs/PROJECT_OVERVIEW.md](docs/PROJECT_OVERVIEW.md) | Produkt, aktuellt UX, terminologi och avgränsningar |
| [docs/IMPLEMENTATION_GUIDE.md](docs/IMPLEMENTATION_GUIDE.md) | Kodkarta, DI, lagring, dataflöden och samtidighet |
| [docs/DEVELOPMENT_AND_RELEASE.md](docs/DEVELOPMENT_AND_RELEASE.md) | Utvecklingsmiljö, CI, paketering och release |
| [docs/CROSS_PLATFORM_PLAN.md](docs/CROSS_PLATFORM_PLAN.md) | Plan för macOS-stöd (Apple Silicon och Intel) |
| [docs/DISTRIBUTION_AND_SIGNING.md](docs/DISTRIBUTION_AND_SIGNING.md) | Signering, SmartScreen och Gatekeeper |
| [docs/TESTING.md](docs/TESTING.md) | Testomfattning, kommandon och val av testnivå |
| [ARCHITECTURE.md](ARCHITECTURE.md) | Kort arkitekturöversikt |
| [MODEL_EVALUATION.md](MODEL_EVALUATION.md) | Modellval, mätningar och eval-korpus |
| [DEPENDENCIES.md](DEPENDENCIES.md) | Versions- och licenslåsning |
| [COMPLETION_REPORT.md](COMPLETION_REPORT.md) | Implementerad leveransstatus |
| [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) | Tredjepartslicenser |

## Licens

LocalDrafts egen källkod är licensierad under **Apache License 2.0**, se
[LICENSE](LICENSE). Du får använda, ändra och distribuera koden, även
kommersiellt, så länge du behåller licens- och upphovsrättstexten.

Tredjepartskomponenter och modeller har egna licenser, se
[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) och mappen `licenses/`.
