# Ter22 Remote 0.1.2 — aplicație Windows personalizabilă

Proiect C#/.NET propriu pentru controlul calculatoarelor personale, cu interfață Windows Forms, captură de ecran, mouse și tastatură, monitoare multiple și un releu privat opțional. Același executabil Windows permite atât primirea unei conexiuni, cât și controlarea altui calculator. Nu depinde de instalarea AnyDesk sau RustDesk.

**Statutul livrării:** versiune de dezvoltare 0.1. Publicările din Releases sunt generate numai după trecerea testelor automate, compilare și verificarea instalării, pornirii și dezinstalării pe un runner Windows. Rezultatul exact al fiecărei compilări este indicat în `BUILD-INFO.txt` și în jurnalul GitHub Actions. Controlul real la distanță și comportamentul monitoarelor multiple trebuie validate pe două calculatoare Windows; vezi `VERIFICARE.md`.

## 1. Ce include această versiune

| Componentă / funcție | Comportament implementat |
|---|---|
| Aplicație Windows | Interfață în română, pornirea și oprirea explicită a accesului |
| Conexiune directă | IP/DNS și port TCP în LAN sau într-un VPN |
| Conexiune prin internet | Ambii utilizatori se conectează către un releu Linux propriu; nu necesită porturi de intrare pe PC-urile Windows |
| Autorizare | Cod de acces aleator de 256 biți, certificat fixat prin SHA-256, acceptare locală implicită |
| Criptare | TLS 1.2/1.3 între calculatoare; în modul releu există suplimentar TLS către releu |
| Monitoare | Selectarea unui monitor, întregul desktop sau ferestre separate pentru ecrane |
| Geometrie | Coordonate negative, monitoare portrait, dimensiuni diferite și proces Per-Monitor DPI V2 |
| Control | Mouse, butoane, scroll, taste primite de fereastra aplicației |
| Ieșire rapidă | F12 dezactivează controlul; pierderea focalizării eliberează tastele |
| Instalator | Instalare pentru utilizatorul Windows curent, scurtături și dezinstalare |

### Limitele concrete ale versiunii 0.1

- Calculatorul controlat trebuie să fie pornit, cu utilizator autentificat, desktop deblocat și aplicația deschisă. Nu există încă serviciu Windows, acces la ecranul de autentificare, pornire automată sau reconectare automată a clientului după restart.
- Ferestrele UAC / desktopul securizat nu pot fi controlate. Nu se dezactivează protecțiile Windows. Interacțiunea cu aplicații care rulează la un nivel de privilegii superior poate fi respinsă de Windows.
- Codul de conectare este valabil numai cât timp instanța de acces este pornită. O nouă pornire generează un certificat și un cod noi. Nu există încă o listă persistentă de dispozitive autorizate.
- Un singur client poate controla o instanță gazdă. Acest client poate deschide mai multe ferestre pentru monitoarele aceleiași sesiuni.
- Captura folosește GDI/BitBlt și JPEG, cu maximum 2560×1600 pixeli pe cadru transmis și o pauză de 100 ms între cereri. Cadrele sunt cerute pe rând pentru ferestrele deschise; rata efectivă scade cu numărul de monitoare și cu viteza conexiunii. Nu este o implementare video de 60 FPS sau un înlocuitor complet al performanței AnyDesk.
- Suprafața capturată este limitată la 34 megapixeli. Dacă vederea „Toate monitoarele” depășește această limită, folosește ecranele separat. Conținutul protejat sau anumite suprafețe grafice pot apărea negre; HDR și capturile unor aplicații GPU trebuie verificate pe hardware real.
- Alt+Tab, Ctrl+Alt+Delete, unele combinații cu tasta Windows și alte combinații rezervate de sistem nu sunt redirecționate. F12 rămâne local. Pentru tastare predictibilă configurează același aranjament de tastatură pe ambele PC-uri. Nu există încă transmitere Unicode independentă de layout.
- Nu sunt incluse transferul de fișiere, sincronizarea clipboardului, audio, wake-on-LAN, drivere de monitor virtual sau actualizarea automată.

Aceste limite descriu funcțiile absente; nu sunt funcții simulate sau activate parțial prin modificarea setărilor de securitate ale Windows.

## 2. Obținerea executabilului și a instalatorului

Pentru compilare pe un calculator Windows găzduit de GitHub, fără SDK instalat local, vezi `COMPILARE_GITHUB.md`. Compilările sunt gestionate în repository-ul public `ter22us/desk-ter`, iar instalatoarele rezultate apar în Releases.

Țintă de instalare: **Windows 10 Pro 22H2 / Windows 11 x64**. Pentru compilare:

1. Instalează **.NET SDK 10 x64**, versiune stabilă actualizată, de la [Microsoft](https://dotnet.microsoft.com/download/dotnet/10.0). Ai nevoie de SDK, nu doar de runtime. Visual Studio nu este obligatoriu.
2. Instalează **Inno Setup 6**, versiune stabilă actualizată, de la [autorul Inno Setup](https://jrsoftware.org/isdl.php), pentru construirea instalatorului.
3. Extrage întregul proiect într-un director local, de exemplu `C:\Proiecte\Ter22Remote`. Nu compila direct din arhiva ZIP.
4. Deschide `Build-Installer.cmd`. Scriptul rulează testele, publică aplicația și construiește instalatorul. Orice eroare oprește procesul.
5. După finalizare găsești:

```text
artifacts\windows-x64\Ter22.Remote.exe
artifacts\installer\Ter22-Remote-Setup-0.1.2.exe
```

Executabilul aplicației include runtime-ul .NET. Calculatoarele pe care instalezi programul nu au nevoie de SDK sau de instalarea separată a runtime-ului .NET. Publicarea poate produce și fișiere auxiliare; instalatorul include întregul director de publicare.

Pentru Windows 10 Pro este necesară versiunea 22H2, build 19045 sau ulterior, pe 64 de biți; poți verifica versiunea prin `winver`. Versiunile Windows pe 32 de biți nu sunt incluse. Instalatorul și aplicația folosesc același prag minim. Testele automate au loc pe Windows Server 2025; compatibilitatea funcțională trebuie confirmată și pe PC-urile Windows 10 Pro / Windows 11 folosite.

Suportul standard Microsoft pentru Windows 10 Home și Pro s-a încheiat la 14 octombrie 2025. Compatibilitatea tehnică a aplicației nu extinde ciclul de suport al sistemului de operare. [Ciclul de viață Microsoft](https://learn.microsoft.com/en-us/lifecycle/products/windows-10-home-and-pro).

Instalatorul se execută pe fiecare PC pe care vrei să utilizezi aplicația. Instalează în `%LOCALAPPDATA%\Programs\Ter22 Remote`, pentru utilizatorul curent, și oferă dezinstalare din setările Windows. Nu instalează un serviciu și nu pornește ascuns accesul la distanță. Instalatorul rezultat nu este semnat digital cu un certificat de editor.

Pentru utilizatorii PowerShell există și:

```powershell
.\scripts\build-windows.ps1 -Installer
```

Dacă PowerShell blochează scripturile locale, folosește `Build-Installer.cmd`; nu trebuie să modifici permanent politica de execuție. Pentru o cale particularizată a compilatorului Inno:

```powershell
.\scripts\build-windows.ps1 -Installer -InnoCompiler 'D:\Unelte\Inno Setup 6\ISCC.exe'
```

Pentru publicarea aplicației fără instalator, omite `-Installer`. Poți deschide soluția `Ter22Remote.slnx` într-un IDE care acceptă .NET 10 și formatul `.slnx`.

## 3. Prima conexiune în rețeaua locală

Pe calculatorul care va fi controlat:

1. Pornește Ter22 Remote și lasă modul „Direct — LAN / VPN”.
2. Lista adreselor arată interfețele Ethernet, Wi-Fi și VPN active. Codul nou include adresa selectată și până la șapte adrese IPv4 alternative. Clientul încearcă adresele și verifică certificatul înainte de a trimite tokenul. Poți introduce manual un IP/nume DNS; rămâne necesar ca celălalt PC să aibă o rută către cel puțin una dintre adrese.
3. Păstrează portul `45990` sau alege alt port liber. Conexiunile directe folosesc IPv4 în această versiune.
4. Apasă „Permite LAN/VPN în firewall” și aprobă configurarea în dialogul UAC Windows. Regula se referă la acest executabil, TCP și portul ales. Sunt permise surse din subrețeaua locală, `10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16` și `100.64.0.0/10`; regula se aplică și profilului Public deoarece unele VPN-uri sunt clasificate astfel. Nu permite surse Internet arbitrare și nu activează redirecționări pe router. O regulă existentă de blocare poate avea prioritate și este semnalată; aplicația nu o șterge automat.
5. Lasă bifată acceptarea locală și apasă „Pornește accesul”. Codul direct apare după pornirea listenerului. Dacă schimbi portul sau muți executabilul portabil, configurează din nou regula pentru noua combinație cale/port. Regulile create se numesc `Desk Ter LAN-VPN … TCP …` și pot fi eliminate din Windows Defender Firewall → Setări complexe → Reguli de intrare; dezinstalarea per utilizator nu elimină automat reguli administrative.
6. Apasă „Copiază codul privat”. Transferă-l confidențial pe celălalt calculator.

Pe calculatorul de pe care lucrezi:

1. Pornește Ter22 Remote.
2. Lipește codul în zona „Lucrează pe alt calculator”.
3. Apasă „Conectează-te”.
4. Acceptă cererea pe calculatorul controlat în cel mult 30 de secunde.
5. În fereastra desktopului, alege monitorul și fă clic pe imagine pentru a trimite mouse-ul și tastatura.

„Ferestre pentru monitoare” deschide câte o fereastră pentru fiecare monitor fizic care nu este deja afișat separat. Poți muta aceste ferestre pe ecranele tale locale. Numărul de monitoare locale poate fi diferit de cel al calculatorului controlat.

Pentru acces fără acceptarea fiecărei conexiuni, debifează explicit opțiunea de acceptare **înainte să pornești accesul**. Aplicația trebuie să rămână deschisă și desktopul deblocat. Acesta nu este acces înainte de autentificarea în Windows.

Apasă F12 pentru a opri controlul din fereastra vizualizată. Pentru a-l reactiva bifează „Control mouse/tastatură” și fă clic pe imagine. „Deconectare” închide toate ferestrele sesiunii; pe gazdă, „Oprește accesul” invalidează codul și oprește primirea conexiunilor.

Jurnalul arată separat încercările TCP, negocierea TLS și așteptarea aprobării. **Copiază diagnosticul** copiază versiunea, Windows și mesajele tehnice, fără codul privat. Diagnosticul poate conține IP-uri locale; trimite-l numai persoanei care investighează conexiunea. Vezi [diagnosticul detaliat](docs/DIAGNOSTIC_CONEXIUNE.md) și [semnarea instalatorului](docs/SEMNARE_WINDOWS.md).

## 4. Conectarea prin internet

Ai două trasee implementate:

- **Un VPN între calculatoarele tale:** folosești modul direct și adresa IPv4 din VPN. Releul aplicației nu mai este necesar.
- **Un releu propriu:** folosești componenta `Ter22.Relay` pe un server Linux x64 compatibil cu .NET 10, cu adresă accesibilă din internet. Acest server nu a fost creat sau configurat automat.

Pentru a construi releul pe Windows, din directorul proiectului:

```powershell
dotnet publish src/Ter22.Relay/Ter22.Relay.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -o artifacts/relay-linux-x64
```

Transferă conținutul `artifacts/relay-linux-x64` într-un director privat al unui utilizator Linux dedicat. Rulează cu acel utilizator, în directorul respectiv. În exemple, înlocuiești `relay.exemplu.ro` cu propriul nume DNS sau IP public IPv4:

```bash
chmod 700 .
chmod 700 Ter22.Relay
./Ter22.Relay relay.exemplu.ro 45991 --show-code
```

Comanda generează, la prima folosire, `relay-state.json` cu permisiuni `600` și afișează codul privat `TRR1:...`. Copiază codul într-un loc sigur. Include cheia de acces a releului. Fișierul de stare conține cheia privată a certificatului și trebuie păstrat confidențial. Nu îl include în arhivele de distribuție sau într-un repository.

Pornește apoi serviciul de releu:

```bash
./Ter22.Relay relay.exemplu.ro 45991
```

Permite intrarea TCP pe portul `45991` în firewallul serverului și în regulile furnizorului VPS. Serverul trebuie să rămână disponibil; pentru funcționare permanentă rulează comanda prin managerul de servicii al serverului, cu același director de lucru și același utilizator. Poți folosi un port neprivilegiat diferit. Acesta este un releu TLS TCP, nu un server HTTP și nu un serviciu WordPress.

Pe PC-ul controlat:

1. Selectează „Internet — releu propriu”.
2. Lipește codul `TRR1:...` în câmpul de configurare a releului.
3. Pornește accesul și copiază noul cod privat de conexiune `TRC1:...`.
4. Introdu acel cod pe PC-ul de control, în mod obișnuit.

Codul de conexiune include configurația releului; nu trebuie introdusă separat pe client. Ambele PC-uri inițiază conexiuni către releu. Nu trebuie să expui portul de control al fiecărui PC în router. Captura și comenzile sunt protejate de conexiunea TLS interioară, verificată prin certificatul gazdei. Releul vede metadatele de rutare, adresele IP și volumul traficului, fără să dețină cheia privată a conexiunii TLS dintre PC-uri.

Releul acceptă maximum 32 de conexiuni transport simultane și 16 rute gazdă; o pereche activă consumă două conexiuni. Înregistrările care nu primesc un client expiră după 90 de secunde, iar gazda încearcă să se înregistreze din nou. Există o scurtă fereastră de reînregistrare în care conectarea poate necesita o nouă încercare. Certificatul releului este valabil un an; înlocuiește identitatea și reconfigurează codurile înainte de expirare. Nu șterge fișierul de stare pentru o simplă repornire.

## 5. Personalizarea proiectului

| Fișier / modul | Responsabilitate |
|---|---|
| `src/Ter22.Windows/MainForm.cs` | Ecranul principal, pornire acces, conectare și acceptare locală |
| `src/Ter22.Windows/UI/ViewerForm.cs` | Ferestrele monitoarelor și comenzile de vizualizare |
| `src/Ter22.Windows/UI/RemoteCanvas.cs` | Afișarea imaginilor și evenimentele locale de control |
| `src/Ter22.Windows/Desktop/ScreenCapture.cs` | Enumerarea monitoarelor și implementarea `IScreenCapture` |
| `src/Ter22.Windows/Desktop/NativeInput.cs` | Interoperabilitate Windows: captură, cursor, injectare și eliberare input |
| `src/Ter22.Windows/Sessions` | Ciclul de viață al gazdei și clientului, cozi și autorizare |
| `src/Ter22.Core` | Protocol cu dimensiuni limitate, TLS, coduri, geometrie și rutare |
| `src/Ter22.Relay` | Executabilul serverului privat și identitatea sa locală |
| `tests/Ter22.Tests` | Teste fără dependențe de un framework extern de testare |
| `installer/Ter22.Remote.iss` | Nume, versiune, director de instalare, scurtături și dezinstalare |

Poți schimba interfața și comportamentul acestor module. Pentru a înlocui capturarea JPEG cu DXGI și un codec video, păstrezi separarea dintre captură, transport și interfață; modificarea formatului cadrelor necesită și negocierea unei noi versiuni de protocol. Compatibilitatea cu versiuni viitoare nu se presupune: protocolul respinge versiunile necunoscute.

La schimbarea numelui actualizează proprietățile proiectului Windows, textele din interfață, numele din scripturile de compilare și configurația Inno Setup. **Păstrează `AppId` la actualizarea aceleiași aplicații**, pentru ca instalatorul să recunoască instalarea existentă. Păstrează consistente și numele mutexului aplicației și `AppMutex` din instalator.

## 6. Validare și întreținere

Rulează separat testele:

```powershell
dotnet run --project tests/Ter22.Tests/Ter22.Tests.csproj -c Release
```

Testele acoperă mesaje fragmentate/trunchiate, limite de alocare, coduri invalide, geometria monitoarelor, antetul JPEG, certificatul TLS, tokenul de acces și transferul prin releu. Nu înlocuiesc testarea capturii, a inputului, a DPI-ului sau a instalatorului pe Windows. Lista de verificări se află în `VERIFICARE.md`.

Pentru că distribuția include runtime-ul .NET, reconstruiește și reinstalează aplicația după actualizările de securitate relevante ale .NET. Nu există actualizator automat în versiunea 0.1. Păstrează Windows actualizat.

## Referințe tehnice

- [Politica oficială de suport .NET](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core) — alegerea .NET 10 LTS și actualizarea runtime-ului inclus.
- [Publicarea aplicațiilor .NET într-un singur executabil](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview) — împachetarea runtime-ului și dependențelor native.
- [SslStream](https://learn.microsoft.com/en-us/dotnet/api/system.net.security.sslstream?view=net-10.0) — transportul TLS folosit de proiect.
- [SendInput și restricțiile UIPI](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput) — limitele inputului în Windows.
- [Desktop Duplication API](https://learn.microsoft.com/en-us/windows/win32/direct3ddxgi/desktop-dup-api) — opțiune pentru o implementare ulterioară a capturii GPU; nu este motorul capturii din această versiune.
- [Compilatorul Inno Setup](https://jrsoftware.org/ishelp/topic_compilercmdline.htm) — generarea instalatorului.
