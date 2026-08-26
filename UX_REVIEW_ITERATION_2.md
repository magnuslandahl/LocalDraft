# UX-review – iteration 2

Datum: 2026-08-26

Reviewn genomfördes efter den första UX-implementationen genom att köra appen,
prova första start, bred och kompakt huvudvy, dokumentmeny, textassistent,
dikteringsdialog, inställningar, versioner och inspelningarnas tomläge. Kontrollerna
gjordes även med den aktuella Windows-skalningen på 150 procent.

## Åtgärdslista

1. **Dokumentmenyn öppnas på fel plats.** Den programstyrda kontextmenyn använder
   muspekarens standardplacering och kan visas långt från knappen eller delvis
   utanför fönstret. Den ska alltid förankras direkt under dokumentets menyknapp.

2. **Sparstatus visas två gånger och kan motsäga sig själv.** Redigeraren visar
   statiskt "Sparas automatiskt" samtidigt som den nedre statusraden visar det
   verkliga läget. Endast den levande statusen ska visas, nära dokumentets
   åtgärder, tillsammans med eventuell avbrytknapp.

3. **Sekretessindikatorn försvinner i kompakt läge.** Texten "100 % lokalt" döljs
   för att spara plats, men användaren behöver fortfarande se att appen är lokal.
   Kompakt läge ska behålla en låsikon med tooltip och tillgänglighetsnamn.

4. **Textassistentens primäråtgärd hamnar under scrollkanten.** De tre snabbvalen
   tar onödigt mycket höjd och den tomma instruktionen saknar synlig vägledning.
   Snabbvalen ska bli kompakta och instruktionen ska ha en tydlig platshållare.

5. **Inställningarnas Spara/Avbryt kan hamna under scrollkanten.** Vid 150 procent
   skalning syns inte dialogens primära avslutningsåtgärder utan scrollning. De ska
   ligga i en fast sidfot medan endast innehållet rullar.

6. **Datum visas med engelsk formatering.** Dokumentlistan och versionshistoriken
   visar exempelvis `8/26/2026 11:29 PM` trots svensk UI. WPF-elementens språk ska
   sättas till `sv-SE` innan någon vy skapas.

## Delar utan nya åtgärder

- Dikteringsdialogens enda primära start/slutför-knapp är tydlig.
- Versionsvyn har tydligt val och återställningsåtgärd.
- Inspelningsvyn visar ett begripligt tomläge och döljer irrelevanta åtgärder.
- Den responsiva huvudvyn döljer dokumentlistan utan att minska redigeringsytan.
