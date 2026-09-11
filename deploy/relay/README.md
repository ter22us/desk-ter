# Releu Desk Ter pentru PC-uri din rețele diferite

Releul permite conexiuni între două PC-uri care nu au o rută directă între ele, de exemplu în două orașe. Ambele PC-uri inițiază conexiuni către server. Fluxul de imagini și comenzi trece prin TLS între capete, în interiorul conexiunilor TLS către releu; aprobarea accesului rămâne pe calculatorul controlat.

Stare: instalarea și verificarea serviciului sunt pregătite în repository. Aceasta nu înseamnă că există deja un server public Desk Ter. Nu trebuie reinstalată aplicația Windows 0.1.2 pentru a introduce configurația unui releu nou.

## Serverul necesar

- VPS/server Ubuntu 24.04 pe x64, cu systemd și acces SSH cu sudo.
- IP public IPv4 stabil, accesibil de la ambele PC-uri. Se poate folosi și un nume DNS care rezolvă către acel IP; domeniul este opțional.
- Un port TCP liber și permis în firewallul sistemului și în regulile furnizorului VPS, implicit `45991`.
- Serverul trebuie să rămână pornit. Traficul video consumă banda serverului; necesarul depinde de rezoluție, monitoare și numărul sesiunilor. Estimare inițială pentru utilizare personală: 2 vCPU și 2 GB RAM, de verificat cu sesiunile reale.

Un cont de găzduire WordPress care permite numai HTTP/PHP nu poate rula acest executabil TCP permanent. Instalatorul de mai jos nu utilizează și nu modifică serviciile WordPress/Nginx/Apache existente.

## Instalare inițială

Actualizează sistemul și instalează dependențele native .NET/cURL pentru Ubuntu 24.04:

```bash
sudo apt-get update
sudo apt-get install -y curl ca-certificates git libicu74 libssl3t64 libstdc++6 libgcc-s1 zlib1g
git clone https://github.com/ter22us/desk-ter.git
cd desk-ter
sudo bash scripts/install-relay-linux.sh IP_PUBLIC_SAU_DNS 45991
```

Înlocuiește `IP_PUBLIC_SAU_DNS` cu adresa reală a serverului. Nu introduce IP-ul local al unuia dintre PC-uri. Scriptul:

1. Verifică sistemul, portul liber și absența unei instalări existente a serviciului.
2. Descarcă binarul Linux x64 0.1.2 din release-ul fix `build-4-1` și verifică SHA-256 înainte de extragere/execuție.
3. Creează utilizatorul de sistem `desk-ter-relay`, fără login, și instalează executabilul deținut de root în `/opt/desk-ter-relay`.
4. Generează identitatea privată ca utilizatorul serviciului, în `/var/lib/desk-ter-relay/relay-state.json`, cu permisiuni `600`, într-un director `700`. Codul secret nu este afișat în jurnalul instalării.
5. Instalează serviciul systemd și îl activează la pornirea sistemului. Procesul de releu rulează fără root, cu restricții de scriere și fără drepturi de escaladare.

Instalatorul verifică pornirea locală, nu firewallul furnizorului sau ruta de la PC-uri. Dacă folosești UFW și acesta este deja activ, regula necesară este:

```bash
sudo ufw allow 45991/tcp comment 'Desk Ter relay'
```

Configurează aceeași permisiune TCP în panoul furnizorului VPS. Instalatorul nu activează, nu resetează și nu dezactivează firewallul serverului. Dacă folosești alt port, înlocuiește-l în toate aceste comenzi.

Pentru o arhivă deja descărcată, scriptul acceptă calea locală și SHA-256 așteptat ca al treilea/al patrulea argument. Hashul trebuie luat dintr-o sursă de încredere pentru release-ul ales, nu calculat dintr-o arhivă necunoscută și apoi considerat dovadă de autenticitate.

## Configurarea PC-urilor

Pe fiecare PC, verifică mai întâi conectivitatea către server în PowerShell:

```powershell
Test-NetConnection -ComputerName IP_PUBLIC_SAU_DNS -Port 45991
```

`TcpTestSucceeded: True` confirmă doar TCP; sesiunea Desk Ter verifică separat certificatele, tokenul și aprobarea.

Pe server, într-un terminal privat, obține codul:

```bash
sudo desk-ter-relay-code
```

Rezultatul `TRR1:...` conține cheia releului. Nu îl pune în GitHub, într-un jurnal public sau într-un tichet public. Comanda nu generează o altă identitate dacă fișierul de stare lipsește.

Pe PC-ul controlat, selectează **Internet — releu propriu**, lipește codul în **Cod de configurare releu**, păstrează aprobarea locală și apasă **Pornește accesul**. Copiază noul cod privat `TRC1:...` pe celălalt PC, în **Lucrează pe alt calculator**, apoi conectează-te și aprobă cererea pe gazdă. Configurația releului este inclusă în codul de conexiune; nu se introduce separat pe client.

## Administrare

```bash
sudo systemctl status desk-ter-relay --no-pager
sudo journalctl -u desk-ter-relay -n 50 --no-pager
sudo systemctl restart desk-ter-relay
sudo systemctl stop desk-ter-relay
```

Configurația adresei și portului este în `/etc/desk-ter-relay/relay.conf`. Fișierul de identitate trebuie păstrat la actualizări/reporniri. Nu-l șterge pentru a reporni serviciul. Certificatul releului existent este valabil un an de la generare; reînnoirea necesită o identitate nouă și redistribuirea codului pe PC-uri. Înaintea unei actualizări se face o copie privată a identității și se planifică întreruperea sesiunilor active.

Pentru oprirea pornirii automate folosește `sudo systemctl disable --now desk-ter-relay`. Instalatorul nu suprascrie un serviciu deja instalat și nu efectuează actualizări automate ale executabilului. Se poate revizui separat o actualizare a versiunii fixate.

## Verificare automată și limite

Jobul `linux-relay` din GitHub Actions rulează pe Ubuntu 24.04. Verifică scripturile, protocolul pe Linux, instalarea binarului publicat, rularea fără root, permisiunile identității, două conexiuni TLS și 200.000 octeți în ambele sensuri. Apoi verifică oprirea și repornirea cu aceeași identitate și repetă conexiunea.

Aceste teste folosesc un runner temporar și adresa loopback. Nu configurează un VPS al utilizatorului și nu confirmă traseul real dintre două orașe. Ultimul pas este conectarea PC-urilor la serverul public configurat, urmată de testul real cu monitoarele și inputul.

Semnătura Windows/SmartScreen este independentă de releu. Activarea acestui serviciu nu schimbă semnătura instalatorului Windows; vezi [semnarea](../../docs/SEMNARE_WINDOWS.md).
