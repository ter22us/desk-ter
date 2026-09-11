# Compilare Windows pe GitHub

Configurația `.github/workflows/windows-build.yml` pregătește construirea aplicației și a instalatorului pe un calculator Windows găzduit de GitHub. Nu este necesară instalarea SDK-ului pe calculatorul de pe care descarci rezultatele.

**Stare la livrare:** configurația este pregătită și verificată structural, dar nu a fost rulată. Nu există încă un executabil compilat sau un rezultat de teste trecute. Eventualele erori de compilare trebuie remediate după prima rulare.

## Unde se încarcă proiectul

Destinația cerută este repository-ul **public `ter22us/desk-ter`**. Conținutul directorului `Ter22Remote` trebuie să fie direct la rădăcina repository-ului: `global.json`, `src`, `scripts` și directorul `.github` trebuie să fie alături. Nu încărca directorul exterior ca încă un nivel.

Încărcarea pe ramura `main` sau `build/windows` pornește compilarea automat. Dacă fluxul este pe ramura implicită, se poate porni și din **Actions → Ter22 Remote - Windows EXE → Run workflow**. GitHub Actions trebuie să fie disponibil pentru repository; rularea folosește resursele contului GitHub.

## Ce execută fluxul

1. Pregătește Windows x64 și .NET SDK 10.0.401.
2. Descarcă Inno Setup 6.7.3 din publicarea oficială, verifică hashul SHA-256 și semnătura editorului, apoi instalează compilatorul pe calculatorul temporar de compilare.
3. Rulează cele 10 teste incluse, publică aplicația cu runtime .NET inclus și generează instalatorul. O eroare oprește livrarea executabilelor.
4. Construiește și arhivează releul Linux x64 opțional.
5. Salvează separat `Ter22.Remote.exe` și `Ter22-Remote-Setup-0.1.0.exe`, plus jurnalul și hashurile în Artifacts, timp de 14 zile.
6. Publică aplicația, instalatorul, releul și hashurile în **Releases**, sub eticheta unică `build-N-M`, unde N este numărul rulării și M este numărul încercării. Publicările sunt marcate ca versiuni de dezvoltare (pre-release); nu expiră după 14 zile și nu înlocuiesc compilările anterioare.

După o rulare reușită, deschide **Releases** din repository și descarcă instalatorul din **Assets**. Acesta este un fișier `.exe` public, care nu necesită extragere din ZIP. Alternativ, pagina rulării din **Actions** afișează linkurile în sumar și fișierele în **Artifacts**; această variantă necesită autentificare GitHub pentru descărcare.

Fluxul folosește acțiuni GitHub fixate la commituri și permisiunea `contents: write` pentru publicarea compilărilor în Releases în același repository. Nu necesită introducerea unei chei personale GitHub în surse sau în configurație.

O compilare reușită nu confirmă controlul real al mouse-ului, captura monitoarelor sau instalarea pe PC-ul țintă. Verificările pe două calculatoare Windows sunt descrise în `VERIFICARE.md`. Executabilele rezultate nu au o semnătură digitală de editor a proiectului Ter22 Remote.

## Documentație oficială

- [Calculatoare de compilare găzduite de GitHub](https://docs.github.com/en/actions/reference/runners/github-hosted-runners)
- [Acțiunea oficială setup-dotnet](https://github.com/actions/setup-dotnet)
- [Descărcarea fișierelor cu upload-artifact](https://github.com/actions/upload-artifact)
- [Inno Setup: descărcare](https://jrsoftware.org/isdl.php) și [verificare](https://jrsoftware.org/isdl-verify.php)
