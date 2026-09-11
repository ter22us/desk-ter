# Verificarea livrării Ter22 Remote 0.1

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

Fluxul verifică suplimentar pachetul rezultat prin `scripts/test-windows-package.ps1`: instalare în română într-un director temporar, compararea hashului aplicației instalate, deschiderea și închiderea ferestrei principale, apoi dezinstalare. Acest test nu pornește accesul la distanță.

O versiune este publicată în **Releases** numai după trecerea tuturor etapelor. Pentru confirmarea aferentă fiecărei versiuni, verifică `BUILD-INFO.txt`, commitul asociat și jurnalul acelei rulări din Actions. Jurnalele etapelor sunt păstrate și în artefactul `Ter22-Remote-verificare`.

## Ce nu confirmă aceste verificări

- Nu a fost efectuat un test funcțional complet pe două PC-uri Windows 11 fizice.
- Captura efectivă a mai multor ecrane, inputul și comportamentul în rețele reale necesită verificările de mai jos.
- Testul pachetului rulează pe Windows Server 2025 furnizat de GitHub, nu pe fiecare configurație Windows 11 a utilizatorului.
- Publicarea releului Linux verifică generarea binarului; acel binar trebuie verificat și pe serverul Linux țintă.
- Testele automate nu înlocuiesc un audit de securitate sau măsurarea performanței pe hardware-ul folosit.

Scripturile de compilare și fluxul opresc procesul la o eroare. În mediul local de pregătire, CoreCLR nu a putut porni; compilările executabilelor sunt efectuate pe GitHub Actions.

## Verificări necesare pe două PC-uri Windows 11 x64

| Scenariu | Rezultat așteptat |
|---|---|
| Compilare pe un PC curat cu .NET SDK 10 | Cele 10 teste trec; publicarea și Inno Setup se încheie fără erori |
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
