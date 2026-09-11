# Verificarea livrării Ter22 Remote 0.1

Data: 11 septembrie 2026.

## Verificat în mediul de pregătire

- Analiză sintactică a tuturor fișierelor C# cu un parser C# separat.
- Parsarea configurațiilor XML ale proiectelor, a manifestului și a soluției.
- Parsarea `global.json` și verificarea referințelor locale ale proiectelor.
- Revizuirea fluxurilor de autentificare, a limitelor mesajelor, a eliberării inputului, a anulării și a rutării.
- Parsarea YAML și verificarea referințelor configurației de compilare GitHub Actions. Acțiunile și hashul Inno Setup provin din publicările oficiale verificate la pregătire.

## Neconfirmat

**Nu există un rezultat de compilare reușită în această livrare.** SDK-ul .NET 10.0.401 a fost descărcat de la Microsoft și verificat folosind hashul SHA-512 din metadatele oficiale, dar pornirea runtime-ului în mediul disponibil a eșuat înainte de compilare cu `Failed to create CoreCLR, HRESULT: 0x8007000E`.

În consecință:

- Cele 10 teste C# incluse nu au fost executate aici și nu sunt raportate drept trecute.
- Executabilul Windows și instalatorul nu au fost construite sau rulate aici.
- Captura, inputul și comportamentul în situații reale de rețea nu au fost validate pe Windows.
- Analiza sintactică nu înlocuiește compilarea, analiza semantică, testele de integrare sau un audit de securitate.

`Build-Installer.cmd` și scriptul PowerShell opresc procesul dacă un test sau compilarea eșuează. Păstrează eroarea completă pentru diagnostic dacă apare o problemă la compilarea pe Windows.

Încercarea suplimentară de compilare locală a întâlnit aceeași eroare de pornire CoreCLR. Transferul compilatorului către un runtime alternativ a fost respins de verificarea automată deoarece scriptul de transfer depășea limita de 64.000 de octeți. Fluxul Windows din GitHub Actions este pregătit, dar nu a fost încă rulat; detalii în `COMPILARE_GITHUB.md`.

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
