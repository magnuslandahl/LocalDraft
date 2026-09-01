## Vad ändras och varför

<!-- Kort beskrivning av ändringen och bakgrunden. -->

## Verifiering

<!-- Vilka kommandon kördes? Klistra in relevant resultat. -->

- [ ] `dotnet test .\LocalDraft.slnx`
- [ ] `.\tools\verify-repository.ps1`
- [ ] Paketering/modeller/integritet påverkas – i så fall även `.\tools\build-portable.ps1`
      och `.\tools\verify-package.ps1`

## Publikt repo – innan du begär granskning

- [ ] Diffen innehåller inga nycklar, tokens, lösenord eller anslutningssträngar
- [ ] Diffen innehåller inga personuppgifter, användarinnehåll eller maskinspecifika sökvägar
- [ ] Inga modeller, native-binärer, `Data/`, byggutdata eller ZIP-filer är incheckade
- [ ] Berörd dokumentation är uppdaterad (`PRIVACY.md`, `ANVANDARGUIDE.md`, `docs/…`)
