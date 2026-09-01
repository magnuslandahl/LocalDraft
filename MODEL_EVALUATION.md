# Modellutvärdering

## Valda modeller

| Roll | Fil | Storlek | SHA-256 |
|---|---|---:|---|
| Tal | `ggml-small-q5_1.bin` | 190 085 487 byte | `ae85e4a935d7a567bd102fe55afc16bb595bdb618e11b2fc7591bc08120411bb` |
| Text | `Qwen3-1.7B-Q4_K_M.gguf` | 1 282 439 264 byte | `d2387ca2dbfee2ffabce7120d3770dadca0b293052bc2f0e138fdc940d9bc7b5` |

Whisper small q5_1 valdes som flerspråkig CPU-kompromiss med uttryckligt språk `sv`. Qwen3.5 0.8B Q4_0 underkändes eftersom två skyddade värden försvann i 12-fallskorpusen. Q8_0-utgåvan från samma konvertering underkändes eftersom GGUF-filen saknade en tensor som LLamaSharp-backenden krävde. Därför valdes nästa föreslagna fallback, Qwen3 1.7B Q4_K_M, med 8 192 tokens kontext, låg temperatur och `/no_think`.

## Svensk acceptanskorpus

`tests/fixtures/swedish-assistant-corpus.json` innehåller 12 fall med utfyllnadsord, svenska namn, datum, tider, pengar, procent-/måttliknande värden, ärendenummer, rubriker, punktlista, sammanfattning och instruktionsliknande källtext. Qwen3 1.7B Q4_K_M klarade samtliga 12 automatiska fall. Appen gör dessutom en skyddad-token-kontroll och blockerar icke-sammanfattande förslag om ett namn eller exakt värde ändå saknas.

Skyddat referensfall:

> Åsa Lindström, 14 oktober 2026, 09.30, 128 450 kronor, 125 450 kronor, KS-2026-00419 och 3 november.

## CPU-mätning

Mätt 26 augusti 2026 på Windows 11 Enterprise 10.0.26200, AMD Ryzen AI 9 HX PRO 370 (12 kärnor/24 logiska processorer) och 64 GB RAM. GPU-lager var 0 för textmodellen och whisper.cpp-paketet var CPU-bygget.

| Mätvärde | Resultat |
|---|---:|
| Whisper, 61,9 sekunders svensk fixture | 8,91 s |
| Whisper realtidsfaktor | 0,144 |
| Whisper peak working set, benchmark + child process | 652 775 424 byte |
| Textmodell första token | 2,43 s |
| Textmodell total tid, skyddat referensfall | 3,64 s |
| Textmodell hastighet | 9,61 token/s |
| Textmodell peak working set | 2 884 689 920 byte |
| Skyddade tokens i referensfallet | Alla bevarade |

Mätningen är reproducerbar med `dotnet run --project tools/LocalDraft.Benchmark -c Release -- .` och råresultatet skrivs till `artifacts/benchmark-results.json`. Resultaten gäller endast testmaskinen; en 8 GB-laptop förväntas vara långsammare.
