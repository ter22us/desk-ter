# Desk Ter

Aplicație Windows personalizabilă pentru acces la distanță la calculatoarele personale. Executabilul se numește **Ter22 Remote** și include atât primirea conexiunilor, cât și controlarea altui calculator.

Funcțiile implementate includ selectarea monitorului, vederea întregului desktop, ferestre separate pentru mai multe monitoare, controlul mouse-ului și al tastaturii, conexiuni TLS directe prin LAN/VPN și un releu Linux propriu opțional pentru conexiuni prin internet.

**Versiune de dezvoltare 0.1.** Fluxul publică o versiune în Releases numai după trecerea testelor automate, compilare și verificarea instalării, pornirii și dezinstalării pe runner Windows. Verifică jurnalul din Actions pentru fiecare compilare. Aceste verificări nu înlocuiesc testarea unei conexiuni și a monitoarelor pe două PC-uri Windows reale.

## Executabil și instalator

Compilările reușite se publică automat în **Releases**. Din secțiunea **Assets** a compilării dorite descarcă:

- `Ter22-Remote-Setup-0.1.2.exe` pentru instalare pe Windows 10 Pro 22H2 / Windows 11 x64;
- `Ter22.Remote.exe` pentru lansarea aplicației direct;
- `Ter22-Relay-linux-x64.tar.gz` doar dacă vrei să configurezi propriul releu Linux.

Aplicația include runtime-ul .NET; pe calculatoarele țintă nu este necesar SDK-ul. Starea semnăturilor este consemnată pentru fiecare compilare în `SIGNING-INFO.txt`. Semnarea este pregătită în flux, dar necesită configurarea unui serviciu cu identitate verificată; compilările de dezvoltare curente rămân nesemnate. Vezi [semnarea și SmartScreen](docs/SEMNARE_WINDOWS.md).

Versiunea 0.1.2 permite instalarea și lansarea pe Windows 10 Pro 22H2, build 19045+, x64, precum și Windows 11 x64. Acesta este pragul de compatibilitate configurat; verificarea automată rulează pe Windows Server 2025. Testarea funcțională pe un PC Windows 10 Pro rămâne necesară; vezi `VERIFICARE.md`.

## Utilizare și personalizare

În 0.1.2, codul include până la opt adrese IPv4 ale gazdei. Clientul încearcă adresele disponibile și acceptă doar calculatorul al cărui certificat corespunde codului. Diagnosticul separă TCP, TLS și aprobarea locală. Butonul **Permite LAN/VPN în firewall** configurează, cu aprobarea administratorului Windows, o regulă pentru executabilul curent și portul ales. Nu configurează routerul și nu creează un VPN.

Dacă o conexiune nu ajunge la gazdă, urmează [diagnosticul conexiunii](docs/DIAGNOSTIC_CONEXIUNE.md). Două calculatoare din rețele fără rută comună au nevoie de un VPN comun sau de releul propriu.

Pentru calculatoare în locații diferite este pregătită [instalarea releului ca serviciu pe Ubuntu 24.04](deploy/relay/README.md). Instalația păstrează identitatea între reporniri și pornește automat după pornirea serverului. Componenta funcționează cu aplicația Windows 0.1.2; serverul public trebuie furnizat și configurat separat.

Vezi [ghidul în română](GHID_RO.md), [compilarea pe GitHub](COMPILARE_GITHUB.md) și [starea verificărilor](VERIFICARE.md).

Fluxul propus pentru nume unice, conectare automată și favorite autorizate este descris în [planul versiunii următoare](docs/FLUX_NUME_SI_FAVORITE.md). Aceste funcții nu sunt incluse în versiunea 0.1.2.

Calculatorul controlat trebuie să fie pornit, cu utilizator autentificat, desktop deblocat și aplicația deschisă. Versiunea curentă nu oferă serviciu Windows, acces înainte de autentificare, control UAC, transfer de fișiere, clipboard între PC-uri sau audio. Captura folosește JPEG și GDI; performanța nu este echivalentă cu AnyDesk.

Proiectul folosește C#/.NET 10 și Windows Forms. Interfața, protocolul, captura și transportul sunt în module separate în `src`. Comenzile locale de compilare sunt în `Build-Installer.cmd` și `scripts`.
