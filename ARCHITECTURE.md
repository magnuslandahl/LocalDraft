# Arkitektur

## Beslut

- **WPF och .NET 10:** native Windows-UI utan webbläsarskal. Minsta Windows TFM är `windows10.0.19041.0`, vilket täcker Windows 10 22H2 och Windows 11.
- **MVVM med praktisk code-behind:** dokumentlista och status ligger i en testbar view model. RichTextBox-caret, RTF-kommandon och modala desktopflöden ligger nära WPF-vyn eftersom dessa API:er är visuella och positionsbundna.
- **Transparent fillagring:** varje dokument har egen GUID-mapp med `document.json`, `current.rtf`, `current.txt`, `versions` och `recordings`. Atomära temporära filer skapas bredvid målet under `Data`.
- **Meningsfulla versioner:** autosave efter cirka 500 ms och version efter cirka tre sekunders inaktivitet. AI, diktering och återställning skapar namngivna versioner. Identiska innehållshashar dedupliceras.
- **Audio:** NAudio/WASAPI strömmar till `.partial.wav`. Efter stopp konverterar Windows Media Foundation till PCM 16 kHz, 16-bit, mono innan atomisk slutplacering.
- **Whisper:** fastnaglad `whisper-cli.exe` startas utan skal, fönster eller server. Argument skickas med `ArgumentList`; JSON och arbetsfiler ligger i `Data/Temp`. WAV behålls vid fel.
- **Textmodell:** LLamaSharp CPU laddar modellen först när textassistenten används. En semaphore tillåter en tung textoperation i taget. Källtext avgränsas, långa dokument delas vid stycken och skyddade värden kontrolleras före applicering.

## Säkerhetsgränser

Alla dynamiska dokumentvägar normaliseras och måste vara barn till `Data/Documents`. Dokumentradering vägrar reparse points och verifierar att mappen är borta. Modeller verifieras med storlek och SHA-256. Runtime-kod har ingen nätverksklient.

## Kända trade-offs

Media Foundation-resampling ger bäst kompatibilitet med konsumentmikrofoner men är en Windows-komponent. Whisper som separat native-process isolerar ABI-fel bättre än P/Invoke men kräver paketerade DLL:er. RTF är valt för Word-urklipp och lokal transparens; det är inte ett fullständigt Word-dokumentformat.
