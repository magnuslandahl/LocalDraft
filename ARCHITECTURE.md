# Arkitektur

Detta är den korta arkitekturöversikten. Fullständig kodkarta, DI-tabell,
lagringsträd och dataflöden finns i
[`docs/IMPLEMENTATION_GUIDE.md`](docs/IMPLEMENTATION_GUIDE.md).

## Beslut

- **WPF och .NET 10:** native Windows-UI utan webbläsarskal. Minsta Windows TFM är `windows10.0.19041.0`, vilket täcker Windows 10 22H2 och Windows 11.
- **MVVM med praktisk code-behind:** dokumentlista och status ligger i en testbar view model. RichTextBox-caret, RTF-kommandon och modala desktopflöden ligger nära WPF-vyn eftersom dessa API:er är visuella och positionsbundna.
- **Transparent fillagring:** varje dokument har egen GUID-mapp med
  `document.json`, `current.rtf`, `current.txt`, `assistant`, `versions` och
  `recordings`. Atomära temporära filer skapas bredvid målet under `Data`.
- **Meningsfulla versioner:** autosave efter cirka 500 ms och version efter cirka tre sekunders inaktivitet. AI, diktering och återställning skapar namngivna versioner. Identiska innehållshashar dedupliceras.
- **Audio:** NAudio/WASAPI strömmar till `.partial.wav`. Efter stopp konverterar Windows Media Foundation till PCM 16 kHz, 16-bit, mono innan atomisk slutplacering.
- **Whisper:** fastnaglad `whisper-cli.exe` startas utan skal, fönster eller server. Argument skickas med `ArgumentList`; JSON och arbetsfiler ligger i `Data/Temp`. WAV behålls vid fel.
- **Textmodell:** LLamaSharp CPU laddar modellen först när **Bearbeta text**
  används. En semaphore tillåter en tung textoperation i taget. Källtext
  avgränsas, långa dokument delas vid stycken och skyddade värden kontrolleras
  före applicering.

## Uppstart

`App.xaml.cs` omdirigerar `TEMP`, `TMP`, `HF_HOME`, `XDG_CACHE_HOME`,
`LLAMA_CACHE` och `GGML_CACHE` under `AppRoot\Data` innan DI-tjänster,
ljud, nativekod eller modeller initieras. Därefter verifieras skrivbar
applikationsrot, inställningar läses, avbrutna inspelningar erbjuds för
återställning och modellmanifestet kontrolleras.

## Huvudflöden

- **Dokument:** `MainWindowViewModel` läser och sparar via
  `DocumentRepository`. Autosave uppdaterar dokumentraden på plats i stället för
  att bygga om listan. Dokumentbyten och skrivningar serialiseras för att
  undvika flimmer och race.
- **Diktering:** sparad mikrofon visas i huvudvyn. Inspelningsfönstret startar
  direkt, skriver `.partial.wav`, konverterar till PCM 16 kHz mono och kör
  paketerad `whisper-cli.exe`. Mikrofon väljs och testas under
  **Inställningar**.
- **Bearbeta text:** vald text eller hela dokumentet skickas till den lokala
  Qwen-modellen. Resultatet visas för granskning innan ersättning eller
  infogning. Historik lagras per dokument.
- **Dokumentåtgärder:** varje dokumentrad har egen meny för inspelningar,
  versioner, **Kopiera dokumenttext** och permanent radering.

## Lagringsöversikt

```text
Data/
  Documents/<document-id>/
    document.json
    current.rtf
    current.txt
    assistant/history.json
    recordings/<recording-id>.{json,wav,partial.wav}
    versions/<version-id>.{json,rtf,txt}
  Settings/settings.json
  Logs/app-YYYYMMDD.log
  Temp/
  Cache/
```

## Säkerhetsgränser

Alla dynamiska dokumentvägar normaliseras och måste vara barn till `Data/Documents`. Dokumentradering vägrar reparse points och verifierar att mappen är borta. Modeller verifieras med storlek och SHA-256. Runtime-kod har ingen nätverksklient.

## Kända trade-offs

Media Foundation-resampling ger bäst kompatibilitet med konsumentmikrofoner men är en Windows-komponent. Whisper som separat native-process isolerar ABI-fel bättre än P/Invoke men kräver paketerade DLL:er. RTF är valt för Word-urklipp och lokal transparens; det är inte ett fullständigt Word-dokumentformat.

## Vidare läsning

- [`docs/PROJECT_OVERVIEW.md`](docs/PROJECT_OVERVIEW.md) - produkt och aktuellt beteende
- [`docs/IMPLEMENTATION_GUIDE.md`](docs/IMPLEMENTATION_GUIDE.md) - implementation per tjänst och flöde
- [`docs/TESTING.md`](docs/TESTING.md) - tester och regressionsskydd
- [`docs/DEVELOPMENT_AND_RELEASE.md`](docs/DEVELOPMENT_AND_RELEASE.md) - bygge, paket och release
