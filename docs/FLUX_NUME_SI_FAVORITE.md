# Desk Ter: conectare prin nume și favorite autorizate

Stare: specificație pentru următoarea versiune, încă neimplementată. Versiunea 0.1.1 modifică ținta Windows și păstrează conectarea prin cod. Compilarea unei versiuni nu înseamnă că serverul de înregistrare a fost instalat.

## Fluxul cerut

Numele ales identifică o instalare Desk Ter, de exemplu `pc-birou` sau `laptop-ter22`. Fiecare calculator are propriul nume unic. Numele rămâne asociat aceleiași identități când se schimbă IP-ul sau când se repornește aplicația.

1. La prima pornire, proprietarul alege numele calculatorului. Aplicația creează o identitate criptografică persistentă și înregistrează numele pe server.
2. Pe celălalt calculator se introduce numai numele destinației și se apasă **Conectează-te**. Aplicația trimite solicitarea prin server.
3. Calculatorul controlat afișează cine cere acces și oferă **Refuză**, **Permite o dată** și **Permite și memorează acest calculator**.
4. După autorizare, calculatorul poate fi salvat în **Favorite**. Dacă gazda a permis și accesul viitor, următoarele conexiuni de pe același dispozitiv nu mai cer aprobare locală.
5. Dacă a fost permisă numai sesiunea curentă, salvarea în Favorite creează o scurtătură. Activarea accesului fără confirmare solicită separat aprobarea gazdei o singură dată.
6. Gazda poate revoca autorizarea din **Dispozitive autorizate**. Revocarea este verificată la fiecare conexiune nouă și poate închide imediat o sesiune activă.

Favoritele nu reprezintă singure o permisiune. Autorizarea se acordă explicit pe calculatorul controlat și este legată de cheia dispozitivului sursă. Copierea numelui, a IP-ului sau a listei de favorite nu permite controlul.

## Interfața

| Zonă | Conținut |
|---|---|
| Acest calculator | Nume unic, stare online/offline, pornire/oprire acces |
| Conectare | Câmp pentru numele destinației și buton Conectează-te |
| Favorite | Calculatoare salvate, stare, conectare și indicator dacă accesul fără confirmare este autorizat |
| Cerere primită | Identitatea solicitantului, permisiune pentru sesiunea curentă sau pentru conexiuni viitoare |
| Dispozitive autorizate | Dispozitive care au primit acces permanent, data acordării și revocare |
| Setări | Pornire după autentificarea Windows, setări de afișare și server |

Adresa serverului poate fi inclusă în distribuția personalizată după alegerea infrastructurii, pentru a evita configurarea ei pe fiecare calculator. Cheile private și secretele nu se includ în executabil sau în repository.

## Infrastructura necesară

| Resursă | Rol |
|---|---|
| VPS Linux disponibil permanent, cu adresă publică stabilă | Găzduiește serviciul de înregistrare, semnalizarea și releul |
| Subdomeniu cu DNS controlat de proprietar | Adresă stabilă pentru aplicații și certificat TLS reînnoit automat |
| Acces administrativ la VPS | Instalarea serviciului și configurarea rețelei, actualizărilor și copiilor de siguranță |
| Spațiu persistent pentru baza de date și backup | Păstrează rezervarea numelor și identitatea dispozitivelor după restartul serverului |
| Trafic de rețea potrivit utilizării | Transportă ecranele când conexiunea trece prin releu; monitoarele multiple măresc volumul |

Pentru uz personal, un punct de plecare estimativ este 2 vCPU, 2 GB RAM și 20 GB SSD, pe un Linux acceptat de .NET 10, de exemplu Ubuntu 24.04 LTS. Dimensionarea finală se face după numărul de sesiuni, monitoare, rezoluție și traficul măsurat. Serverul nu codifică imaginile; aplicația gazdă face captura și codificarea.

Propunerea este un serviciu ASP.NET Core separat, cu HTTPS și WebSocket pe portul 443, bază de date persistentă și management prin systemd. Acest proces și traficul releului necesită acces la servicii de sistem; un cont de găzduire limitat la site-uri WordPress nu oferă de regulă aceste facilități. [Găzduirea ASP.NET Core pe Linux](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/linux-nginx?view=aspnetcore-10.0).

Repository-ul GitHub păstrează sursele și generează executabilele. Nu ține online serverul aplicației.

## Ce se întâmplă cu IP-ul și LAN-ul

Serverul înregistrează prezența calculatorului și observă adresa publică a conexiunii de ieșire. Aplicația detectează interfețele locale și anunță schimbările relevante. Adresele locale nu sunt publicate într-un catalog accesibil oricui.

Într-o rețea locală comună se poate încerca o conexiune directă, verificând identitatea destinației. Pentru rețele diferite sau când încercarea directă eșuează, ambele calculatoare folosesc conexiuni de ieșire către releu. Nu se bazează pe deschiderea automată a porturilor în router.

Cunoașterea IP-ului public nu garantează accesul direct prin NAT/CGNAT. Releul este necesar în situațiile în care routerele împiedică o legătură directă; acesta este și motivul existenței protocoalelor standard de relay precum TURN. Nu afirmăm că releul TCP existent implementează TURN sau ICE. [RFC 8656, introducere și model de releu](https://www.rfc-editor.org/rfc/rfc8656.html).

Traficul desktopului rămâne criptat între calculatoare, inclusiv când trece prin server. Autorizarea dată de gazdă precedă transmiterea capturilor și comenzilor. Sesiunile, încercările de conectare și dimensiunile mesajelor au limite explicite.

## Identitate și autorizare

- Fiecare instalare are un identificator stabil și o pereche de chei. Numele este un alias unic, nu un secret și nu o dovadă de identitate.
- Serverul rezervă numele pentru cheia înregistrată și solicită dovada deținerii cheii la înregistrare, autentificare și schimbări de profil. Numele unui calculator offline nu devine disponibil altui solicitant.
- Numele se normalizează și se validează fără ambiguități de majuscule sau caractere vizual similare. Nu există o listă publică a tuturor calculatoarelor și a adreselor lor.
- Conexiunile dintre calculatoare autentifică ambele identități. Cererile folosesc valori proaspete și expiră, astfel încât o solicitare interceptată să nu poată fi reluată.
- Gazda păstrează autorizările pentru dispozitive. O afirmație a clientului sau un rând din lista sa de favorite nu poate acorda acces permanent.
- Favoritele păstrează identitatea criptografică a destinației. O schimbare de IP nu invalidează autorizarea, iar înlocuirea cheii cere verificare nouă.
- Cheia privată locală este protejată pentru utilizatorul Windows prin DPAPI. Acest mecanism protejează datele stocate folosind contextul Windows al utilizatorului. [Documentația Microsoft pentru protecția datelor](https://learn.microsoft.com/en-us/dotnet/standard/security/how-to-use-data-protection).
- Recuperarea unui nume după pierderea identității necesită o procedură explicită de recuperare; schimbarea cheii invalidează încrederea veche. Nu se permite preluarea unui nume doar pentru că vechiul calculator este offline.
- Jurnalul păstrează rezultatul cererii și identitățile necesare diagnosticului, fără tokenuri, chei private sau capturi de ecran.

## Pornire și versiuni Windows

Țintele sunt Windows 10 Pro 22H2 x64 și Windows 11 x64. Prima etapă a noului flux va permite pornirea după autentificarea Windows și funcționarea în zona de notificări, cu identitatea păstrată între restarturi.

Accesul fără confirmare locală nu înseamnă automat acces la ecranul de autentificare sau la desktopul securizat UAC. Pentru acces înainte de autentificare ar fi necesară o componentă Windows Service și un agent separat în sesiunea utilizatorului, cu IPC autentificat și permisiuni limitate. Serviciile Windows nu pot fi tratate ca aplicații interactive obișnuite în sesiunea utilizatorului. [Modelul serviciilor interactive Windows](https://learn.microsoft.com/en-us/windows/win32/services/interactive-services).

## Ordinea implementării

1. Server de înregistrare și prezență, rezervarea numelor și persistența identităților Windows.
2. Interfața pentru nume și solicitări, plus conexiunea prin releu cu identitățile autentificate.
3. Autorizare memorată pe gazdă, favorite legate de identitatea destinației și revocare.
4. Conexiune LAN directă verificată și revenire la releu la eșec.
5. Pornire după autentificare, interfață în zona de notificări, diagnostic și verificări pe Windows 10 / Windows 11.

## Verificări de acceptare

- Două calculatoare nu pot rezerva același nume; repornirea nu pierde numele sau identitatea.
- Introducerea numelui trimite cererea către calculatorul corect, inclusiv după schimbarea IP-ului.
- Nicio captură și nicio comandă de control nu sunt procesate înaintea autorizării.
- Un favorit fără autorizare permanentă solicită în continuare permisiune.
- După autorizarea permanentă, același dispozitiv se reconectează fără confirmare locală.
- Copierea listei de favorite sau schimbarea cheii nu permite ocolirea autorizării.
- Revocarea oprește accesul memorat; cererile reluate sau expirate sunt respinse.
- Calculatorul offline și întreruperea serverului produc un rezultat clar, fără blocarea interfeței.
- Conectarea prin releu funcționează între rețele diferite fără reguli de intrare pe routerele PC-urilor.
- Captura, inputul, DPI mixt și monitoarele multiple funcționează în ambele direcții Windows 10 Pro ↔ Windows 11.

Înainte de instalarea serverului trebuie stabilite VPS-ul disponibil și numele DNS. Acestea nu sunt configurate în versiunea 0.1.1.
