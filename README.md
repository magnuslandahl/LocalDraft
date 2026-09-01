# LocalDraft

LocalDraft är en portabel WPF-app för svensk diktering, formaterad
textredigering och lokal textbearbetning. Diktering startar med ett klick,
vald mikrofon visas i huvudvyn och text bearbetas som ett granskningsförslag
under **Bearbeta text**. Dokument, WAV-filer, versioner, modeller, temporära
filer och loggar ligger under appmappen. Runtime-koden använder inga
nätverks-API:er och öppnar ingen port.

## Implementation och risker

Implementationens huvuddelar är: (1) isolerad dokument- och
versionslagring, (2) WASAPI-inspelning och offline-Whisper, (3) in-process
LLamaSharp, (4) svensk WPF-klient och (5) tester och verifierad portabel ZIP.

Viktiga risker är native ABI-kompatibilitet mellan LLamaSharp och GGUF-modellen, CPU-prestanda på äldre datorer, mikrofonformat som kräver Media Foundation-resampling samt att Windows 10/11 och faktisk fil-/socketaktivitet måste verifieras på separata testmaskiner. Appen failar tydligt när modeller eller native-filer saknas och gör inga automatiska nedladdningar.

## Bygga och köra som utvecklare

Krav:

- Windows 10 22H2 x64 eller Windows 11 x64
- .NET SDK 10.0.204 eller senare 10.0-patch
- PowerShell 7

```powershell
dotnet restore .\LokalDiktering.slnx --locked-mode
dotnet build .\LokalDiktering.slnx
dotnet test .\LokalDiktering.slnx
.\tools\fetch-models.ps1
dotnet run --project .\src\LokalDiktering.App
```

`fetch-models.ps1` är endast för utvecklare. Den hämtar fastnaglade filer och avbryter vid fel SHA-256. Appen hämtar aldrig modeller.

## Skapa portabel ZIP

```powershell
.\tools\fetch-models.ps1
.\tools\build-portable.ps1
```

Resultatet blir `LokalDiktering-Portable-win-x64.zip`. Paketet är self-contained, folder-baserat, `win-x64`, otrimmat och inte single-file. Slutanvändaren packar upp hela ZIP-filen till en lokal skrivbar mapp och startar `LokalDiktering.exe`.

## Projekt

- `src/LokalDiktering.Core`: domänmodeller, kontrakt, prompt- och textsäkerhet
- `src/LokalDiktering.Infrastructure`: lagring, audio, Whisper, LLamaSharp och lokal loggning
- `src/LokalDiktering.App`: WPF-gränssnitt
- `tests`: enhets-, lagrings-, integritets- och projektkontroller
- `tools`: modellhämtning, publicering och paketverifiering
- `docs`: detaljerad produkt-, implementations-, test- och releasekunskap

## Dokumentation

| Dokument | Innehåll |
| --- | --- |
| [AGENTS.md](AGENTS.md) | Bindande instruktioner och läsordning för AI-agenter |
| [docs/PROJECT_OVERVIEW.md](docs/PROJECT_OVERVIEW.md) | Produkt, aktuellt UX, terminologi och avgränsningar |
| [docs/IMPLEMENTATION_GUIDE.md](docs/IMPLEMENTATION_GUIDE.md) | Kodkarta, DI, lagring, dataflöden och samtidighet |
| [PRIVACY.md](PRIVACY.md) | Auktoritativ integritets- och lagringsmodell |
| [docs/DEVELOPMENT_AND_RELEASE.md](docs/DEVELOPMENT_AND_RELEASE.md) | Utvecklingsmiljö, körning, paketering och release |
| [docs/TESTING.md](docs/TESTING.md) | Testomfattning, kommandon och val av testnivå |
| [ARCHITECTURE.md](ARCHITECTURE.md) | Kort arkitekturöversikt |
| [MODEL_EVALUATION.md](MODEL_EVALUATION.md) | Modellval, mätningar och eval-korpus |
| [DEPENDENCIES.md](DEPENDENCIES.md) | Versions- och licenslåsning |
| [COMPLETION_REPORT.md](COMPLETION_REPORT.md) | Implementerad leveransstatus |
| [ANVANDARGUIDE.md](ANVANDARGUIDE.md) | Slutanvändarguide |
| [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) | Tredjepartslicenser |
| [docs/reviews/2026-08-31-recorded-ux-review.md](docs/reviews/2026-08-31-recorded-ux-review.md) | Sanerad brief från senaste inspelade UX-review |
