# Semnarea aplicației și instalatorului

Stare: integrarea este pregătită, dar contul/certificatul de semnare nu este configurat. Compilarea de dezvoltare rămâne nesemnată până la activare; verifică `SIGNING-INFO.txt` din fiecare Release.

Pentru un publisher verificat în Windows, executabilul aplicației și instalatorul trebuie semnate Authenticode cu un certificat de **code signing** recunoscut public. Certificatul TLS generat de aplicație pentru sesiunea remote și certificatele HTTPS ale unui site nu sunt certificate de semnare a codului.

SmartScreen evaluează și reputația aplicației/certificatului. O semnătură validă nu garantează dispariția imediată a avertismentelor pentru fiecare versiune nouă. Nici schimbarea numelui fișierului, nici publicarea pe GitHub nu oferă această garanție. [Microsoft explică verificările de reputație și semnătură](https://learn.microsoft.com/en-us/windows/security/operating-system-security/virus-and-threat-protection/microsoft-defender-smartscreen/).

## Integrarea pregătită

Fluxul folosește [Azure Artifact Signing](https://learn.microsoft.com/en-us/azure/artifact-signing/overview), un profil **Public Trust**, identitate validată și autentificare GitHub OIDC. Nu se stochează cheia privată în repository. Acțiunile Azure sunt fixate la commituri verificate; contul de compilare primește numai dreptul de semnare pentru profilul ales.

Eligibilitatea trebuie verificată înainte de achiziționarea serviciului. În documentația consultată, organizațiile din UE sunt eligibile pentru Public Trust, iar dezvoltatorii persoane fizice sunt limitați la SUA/Canada. Nu presupunem că orice freelancer din România este eligibil ca persoană fizică. Dacă nu se califică, se alege un furnizor de code signing recunoscut care acceptă identitatea/jurisdicția respectivă și se adaptează integrarea la acel serviciu. [Cerințele oficiale de înregistrare](https://learn.microsoft.com/en-us/azure/artifact-signing/quickstart).

Pentru ruta Azure sunt necesare abonamentul, contul Artifact Signing, validarea identității, profilul Public Trust și o aplicație Entra cu rolul **Artifact Signing Certificate Profile Signer**, limitat la profil. Configurează o credențială federată pentru:

- issuer: `https://token.actions.githubusercontent.com`
- subject: `repo:ter22us/desk-ter:ref:refs/heads/main`
- audience: `api://AzureADTokenExchange`

În GitHub → Settings → Secrets and variables → Actions → Variables se configurează:

| Variabilă | Valoare |
|---|---|
| `TER22_SIGNING_ENABLED` | `true`, numai după configurarea completă |
| `TER22_AZURE_CLIENT_ID` | ID-ul aplicației Entra |
| `TER22_AZURE_TENANT_ID` | ID-ul tenantului |
| `TER22_AZURE_SUBSCRIPTION_ID` | ID-ul abonamentului |
| `TER22_SIGNING_ENDPOINT` | Endpointul regiunii contului Artifact Signing |
| `TER22_SIGNING_ACCOUNT` | Numele contului de semnare |
| `TER22_SIGNING_PROFILE` | Numele profilului Public Trust |
| `TER22_SIGNING_PUBLISHER` | Numele exact al publisherului din certificat, returnat de `GetNameInfo(SimpleName, false)` |

Acestea sunt identificatoare/configurări, nu chei private sau parole. OIDC este limitat la `main`; ramura `build/windows` nu primește dreptul de a semna. Configurarea validării identității se face de către titular în Azure, nu prin publicarea documentelor personale în GitHub/chat.

Ordinea compilării este: teste → publicare EXE → test conexiune Windows → semnare EXE → creare instalator cu EXE deja semnat → semnare instalator → verificare semnături/publisher/mărci temporale → test pachet → hashuri finale → Release. Ambele fișiere folosesc SHA-256 și timestamp RFC 3161. La o eroare în modul semnat, publicarea este oprită; nu există revenire automată la o livrare nesemnată. Implementarea urmează [acțiunea oficială Microsoft](https://github.com/Azure/artifact-signing-action).

Integrarea de semnare nu a putut fi executată fără contul și profilul titularului. `SIGNING-INFO.txt` distinge această situație de o semnare reușită. Dialogul UAC pentru schimbarea firewallului este o solicitare normală de drepturi administrative și poate apărea și pentru executabile semnate.
