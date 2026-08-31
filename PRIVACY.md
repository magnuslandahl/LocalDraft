# Integritet

LocalDraft är utformad för lokal behandling utan nätverk. Runtime-projekten innehåller inte `HttpClient`, sockets, telemetri, fjärrloggning, uppdateringskontroll eller modelldownload. Whisper körs som en dold lokal process utan server och textmodellen körs i samma process genom LLamaSharp.

## Lagring

`AppRoot` är mappen som innehåller `LokalDiktering.exe`. Alla avsiktliga appskrivningar görs under:

- `Data/Documents` – dokument, RTF, text, versioner och WAV
- `Data/Settings` – lokala inställningar
- `Data/Logs` – begränsade tekniska händelser utan innehåll
- `Data/Temp` och `Data/Cache` – temporära runtime-filer

Vid start sätts arbetsmapp samt `TEMP`, `TMP`, `HF_HOME`, `XDG_CACHE_HOME`, `LLAMA_CACHE` och `GGML_CACHE` till platser under appmappen. Om appmappen inte är skrivbar avslutas appen utan reservlagring i AppData. En varning visas för kända synkroniserade sökvägar, men detekteringen kan inte hitta alla tjänster.

## Loggar

Loggar innehåller tidsstämpel, händelse-ID, opaka dokument-/inspelnings-ID:n, varaktighet och undantagstyp. De innehåller aldrig dokumenttext, transkription, prompt, AI-resultat, ljud, urklippsdata eller namn som extraherats ur dokument. Total loggstorlek begränsas lokalt.

## Urklipp och radering

Data lämnar appen bara när användaren själv kopierar, klipper ut eller klistrar in via Windows urklipp. Windows urklippshistorik och enhetssynkronisering kan behålla kopierad text och kan stängas av i Windows inställningar.

Radering sker direkt och använder inte papperskorgen. Appen gör inte anspråk på forensisk säker radering: SSD-wear-leveling, sidfil, antivirus, säkerhetskopiering och Windows kraschhantering styrs av operativsystemet. Fullständig diskkryptering, till exempel BitLocker, är rätt skydd mot fysisk eller forensisk åtkomst.

## Verifiering

Automatiska tester stoppar införande av vanliga nätverks- och telemetri-API:er samt kontrollerar sökvägsinneslutning och fullständig dokumentradering. Paketverifieraren kontrollerar modeller, native-filer och beroenden. Process Monitor- och socketobservation samt fulla offlineflöden ska köras på de målmaskiner som redovisas i `COMPLETION_REPORT.md`; statiska tester ersätter inte den observationen.
