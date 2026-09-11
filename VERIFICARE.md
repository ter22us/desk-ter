# Verificarea livrării Ter22 Remote 0.1.2

Data: 11 septembrie 2026.

## Verificat în mediul de pregătire

- Analiză sintactică a tuturor fișierelor C# cu un parser C# separat.
- Parsarea configurațiilor XML ale proiectelor, a manifestului și a soluției.
- Parsarea `global.json` și verificarea referințelor locale ale proiectelor.
- Revizuirea fluxurilor de autentificare, a limitelor mesajelor, a eliberării inputului, a anulării și a rutării.
- Parsarea YAML și verificarea referințelor configurației de compilare GitHub Actions. Acțiunile și hashul Inno Setup provin din publicările oficiale verificate la pregătire.

## Compilare și teste pe Windows

[Prima rulare pe GitHub Actions](https://github.com/ter22us/desk-ter/actions/runs/34575167691), cu .NET SDK 10.0.401, a trecut toate cele 10 teste și a publicat executabilul Windows x64. Testele acoperă protocolul, validarea codurilor, geometria monitoarelor, dimensiunile JPEG, autentificarea TLS, respingerea certificatelor/tokenurilor greșite și transferul prin releu.

Acea rulare s-a oprit la instalator, deoarece Inno Setup nu distribuie traducerea română în instalarea standard. Traducerea este acum inclusă în proiect, cu proveniență și licență, iar configurația folosește fișierul local.

În 0.1.2, suita Core are 15 teste. Cele cinci cazuri noi verifică lista limitată de adrese, conectarea printr-o adresă alternativă fără trimiterea tokenului unui calculator cu alt certificat, eroarea TCP distinctă de aprobare, anularea în negocierea TLS și interoperabilitatea cu un server TLS 1.2.

`tests/Ter22.Windows.Tests` rulează cu mesajeria Windows Forms reală: pornește accesul din fereastra principală, verifică lipsa accesului înaintea aprobării, apasă Refuză/Permite în dialog, primește configurația monitoarelor și decodează primul JPEG capturat prin GDI. Verifică și oprirea accesului cu un dialog de aprobare deschis. Conexiunea este pe loopback, în același runner, nu între PC-urile utilizatorului.

`scripts/test-windows-package.ps1` verifică instalarea în română, hashul executabilului instalat, pornirea/închiderea ferestrei, helperul real de configurare Windows Firewall și dezinstalarea. Regula temporară este inspectată și eliminată de test. Testul firewall necesită un runner cu drepturi administrative; nu schimbă politica implicită a paravanului.

Semnarea cu Azure Artifact Signing este o integrare condiționată, neexecutată până la configurarea identității și resurselor Azure. Modul semnat verifică semnăturile, publisherul și mărcile temporale înainte de publicare. Rezultatul este în `SIGNING-INFO.txt`; o compilare fără configurarea serviciului este marcată explicit nesemnată.

O versiune este publicată în **Releases** numai după trecerea tuturor etapelor. Pentru confirmarea aferentă fiecărei versiuni, verifică `BUILD-INFO.txt`, commitul asociat și jurnalul acelei rulări din Actions. Jurnalele etapelor sunt păstrate și în artefactul `Ter22-Remote-verificare`.

## Ce nu confirmă aceste verificări

- Nu a fost efectuat un test funcțional complet pe două PC-uri Windows 11 fizice.
- Captura efectivă a mai multor ecrane, inputul și comportamentul în rețele reale necesită verificările de mai jos.
- Testul pachetului rulează pe Windows Server 2025 furnizat de GitHub, nu pe fiecare configurație Windows 11 a utilizatorului.
- Fluxul include acum și un job Ubuntu 24.04: instalează binarul de releu 0.1.2 deja publicat în `build-4-1`, pornește serviciul real systemd, verifică TLS între doi clienți și transferul a 200.000 octeți în ambele sensuri, oprește normal serviciul și reconectează după repornire fără schimbarea identității. Acesta este un test pe loopback; accesibilitatea unui VPS din internet trebuie verificată pe serverul țintă. Jobul Windows depinde de succesul acestui job Linux.
- Testele automate nu înlocuiesc un audit de securitate sau măsurarea performanței pe hardware-ul folosit.

Scripturile de compilare și fluxul opresc procesul la o eroare. În mediul local de pregătire, CoreCLR nu a putut porni; compilările executabilelor sunt efectuate pe GitHub Actions.

## Windows 10 Pro

Versiunea 0.1.2 stabilește același prag pentru instalator și executabilul portabil: Windows 10 22H2, build 19045+, x64, sau Windows 11 x64. Nu este introdusă o dependență de API-uri exclusive Windows 11. Captura GDI, inputul Win32 și Windows Forms existente rămân baza aplicației.

Nu declarăm testare efectuată pe Windows 10 Pro: runner-ul disponibil este Windows Server 2025. Trebuie verificate pe Windows 10 Pro instalarea, pornirea, captura, inputul, negocierea TLS 1.2, DPI mixt și monitoarele multiple. Pentru conectarea mixtă se verifică Windows 10 ca gazdă și Windows 11 ca vizualizator, apoi invers.

[Lista oficială a sistemelor pentru .NET 10](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md) și [ciclul de viață Windows 10 Home/Pro](https://learn.microsoft.com/en-us/lifecycle/products/windows-10-home-and-pro) trebuie distinse de pragul tehnic ales de aplicație: Windows 10 Pro 22H2 nu mai este în suport standard Microsoft și nu declarăm suport oficial .NET pentru această ediție ieșită din suport standard.

## Verificări necesare pe Windows 10 Pro 22H2 și Windows 11 x64

| Scenariu | Rezultat așteptat |
|---|---|
| Compilare pe un PC curat cu .NET SDK 10 | Cele 15 teste Core trec; publicarea și Inno Setup se încheie fără erori |
| Test Windows separat, cu desktop deblocat | `dotnet run --project tests/Ter22.Windows.Tests -c Release` verifică aprobarea și prima imagine |
| LAN și VPN active simultan | Clientul găsește o adresă accesibilă și respinge certificatele altor calculatoare |
| TCP blocat sau lipsa rutei | Eroare TCP; nu se pretinde că TLS/aprobarea a început |
| Instalare pe un PC fără SDK/runtime .NET separat | Aplicația pornește; scurtătura și dezinstalarea funcționează |
| Conexiune cu acceptare locală | Desktopul nu este transmis înaintea autorizării |
| Cerere refuzată sau expirată | Conexiunea se închide; gazda poate primi o nouă cerere |
| Cod vechi, modificat sau certificat diferit | Conexiunea este respinsă |
| Doi clienți pentru aceeași gazdă | Un singur client controlează gazda |
| Două monitoare, unul în stânga / deasupra | Cursorul atinge corect colțurile fiecărui monitor |
| Rezoluții diferite și DPI 100%, 150%, 200% | Imagine lizibilă și clicuri corecte după redimensionarea ferestrei |
| Monitor portrait | Imaginea și poziția cursorului corespund desktopului fizic |
| Ferestre separate pentru monitoare | Fiecare fereastră afișează monitorul ales, în aceeași sesiune |
| Deconectarea unui monitor în timpul sesiunii | Lista se actualizează și inputul vechi nu controlează coordonate noi |
| Drag, Shift+clic, Ctrl+C / Ctrl+V pe desktopul remote | Comenzile locale ale desktopului remote funcționează; nu implică transfer clipboard între PC-uri |
| Pierderea focalizării, F12 sau conexiune întreruptă | Se eliberează tastele și butoanele injectate; F12 dezactivează controlul |
| Oprirea accesului | Sesiunea se închide, portul se eliberează și vechiul cod nu mai funcționează |
| Blocarea Windows sau afișarea UAC | Sesiunea nu accesează desktopul securizat; nu se dezactivează protecțiile Windows |
| Releu între două rețele distincte | Conexiunea funcționează fără port de intrare pe PC-uri |
| Întreruperea releului | Sesiunea se închide; gazda reîncearcă înregistrarea după revenirea releului |

Acceptarea pentru utilizare zilnică necesită aceste verificări, măsurarea performanței pe hardware-ul folosit și remedierea oricăror defecte găsite. Nu este declarată echivalență funcțională completă cu AnyDesk.
