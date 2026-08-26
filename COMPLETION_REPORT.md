# Slutrapport

## Byggt

En .NET 10/WPF-app med svensk trepanelsvy, RTF-redigering och Word-urklipp, autosave, meningsfull versionshistorik, säker dokument-/inspelningsradering, WASAPI-inspelning till bevarad 16 kHz mono-WAV, offline Whisper-transkribering, in-process LLamaSharp, AI-förhandsvisning och portabel paketering.

## Modeller

- whisper.cpp `b4938` + `ggml-small-q5_1.bin`, SHA-256 enligt `Models/manifest.json`
- LLamaSharp 0.27.0 + `Qwen3-1.7B-Q4_K_M.gguf`, SHA-256 enligt `Models/manifest.json`

## Integritet

Runtime-koden saknar nätverks- och telemetri-API:er, alla appvägar är innanför appmappen, temporära/cache-miljövariabler omdirigeras vid start, loggar sparar inget innehåll och paketverifieraren stoppar moln-SDK:er och felaktiga modeller.

## Verifieringsstatus

- Automatiska tester: 20 godkända, inklusive lokal Whisper-integration, 12-falls svensk textmodellskorpus, sökvägsinneslutning, återställning av partiell WAV och statisk nätverkskontroll.
- Windows 10 22H2: inte verifierat på denna utvecklingsmaskin.
- Windows 11: bygg, lokala AI-integrationer, paketverifiering och start-smoke-test godkända på Windows 11 Enterprise 10.0.26200.
- Offline: Whisper och LLamaSharp-integrationerna kördes lokalt med paketerade modeller; runtime-koden klarade statisk kontroll mot nätverks- och telemetri-API:er.
- Process Monitor- och separat socketobservation: inte körda i denna automatiserade session och kvarstår före formellt produktionsgodkännande.
- Modellkvalitet och CPU-benchmark: redovisade i `MODEL_EVALUATION.md`.

## Kända begränsningar

Första AI-anropet är långsamt eftersom modellen laddas då. Transkribering och lokal textgenerering beror kraftigt på CPU. Modellen kan fortfarande föreslå språkligt olämpliga ändringar; användaren måste granska förhandsvisningen. Appen kan inte garantera hur Windows, antivirus, sidfil, säkerhetskopiering eller lagringshårdvara hanterar data.
