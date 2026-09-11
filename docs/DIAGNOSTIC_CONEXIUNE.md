# Conectarea LAN/VPN în 0.1.2

Instalează aceeași versiune pe ambele calculatoare. Pe gazdă, configurează **Permite LAN/VPN în firewall**, pornește accesul și copiază un cod nou. Codul este invalidat la oprirea accesului sau închiderea aplicației. Pe client, lipește codul în partea de jos; nu trebuie să pornești și accesul la propriul calculator.

Noua versiune include în cod adresele IPv4 active, cu cel mult opt destinații. Conexiunile TCP/TLS sunt încercate în paralel; doar un certificat care corespunde codului poate câștiga. Tokenul este trimis numai pe conexiunea selectată și autentificată. Datele monitorului și comenzile de control rămân blocate până la aprobare.

| Mesaj | Interpretare și acțiune |
|---|---|
| `TCP: încerc …` urmat de eroare TCP | Nu s-a stabilit conexiunea la portul gazdei. Verifică IP/rută, accesul pornit și firewallul. |
| Pe gazdă nu apare `Cerere TCP primită` | Pachetele nu ajung la listener; aprobarea locală nu este cauza. |
| `TCP conectat … Verific certificatul TLS` urmat de eroare TLS | Portul răspunde, dar autentificarea calculatorului a eșuat/expirat. Folosește codul actual și verifică jurnalul gazdei. |
| `TLS verificat … aștept aprobarea` | Rețeaua și TLS funcționează. Acceptă dialogul de pe gazdă în 30 secunde. |
| `Accesul a fost refuzat` | Gazda a refuzat cererea; nu este un timeout TCP. |
| `Acces aprobat … monitoare` urmat de eroare | Investighează disponibilitatea desktopului și captura pe gazdă. |

Adresele `192.168.x.x` și `10.x.x.x` sunt locale/private. Faptul că ambele calculatoare au internet nu creează automat o rută între ele. Pentru PC-uri în locații diferite, trebuie ca ambele să fie în același VPN și rutarea/ACL-urile VPN să permită comunicarea, sau să folosească releul propriu. Aplicația nu poate configura automat VPN-ul/routerul dintr-un cod de conectare.

Pe client poți verifica ruta TCP în PowerShell, folosind una dintre adresele afișate pe gazdă:

```powershell
Test-NetConnection -ComputerName ADRESA_GAZDEI -Port 45990
```

Înlocuiește adresa și portul cu valorile reale. `TcpTestSucceeded: True` confirmă numai conexiunea TCP; testul nu verifică certificatul, codul sau aprobarea. `False` poate indica rutare, firewall, o adresă greșită ori acces oprit. ICMP/ping poate fi blocat separat și nu reprezintă un test suficient.

Butonul firewall adaugă numai regula proprie pentru calea executabilului, portul TCP și surse LAN/VPN. Nu dezactivează firewallul, nu elimină reguli de blocare și nu schimbă politicile administrative. O regulă explicită de blocare are prioritate față de o regulă Allow; [documentația Microsoft](https://learn.microsoft.com/en-us/windows/security/operating-system-security/network-security/windows-firewall/rules) explică inclusiv blocările create când notificarea inițială a firewallului a fost refuzată. Dacă este raportată o blocare, administratorul trebuie să verifice regula pentru calea exactă a aplicației în Windows Defender Firewall → Setări complexe → Reguli de intrare.

Pentru investigație, folosește **Copiază diagnosticul** pe ambele PC-uri după aceeași încercare. Jurnalul nu include codul/tokenul, dar include IP-uri locale. Nu publica acel diagnostic automat într-un repository public.
