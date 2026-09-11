#!/usr/bin/env bash
set -euo pipefail
umask 077

fail() { printf 'Eroare: %s\n' "$*" >&2; exit 1; }
usage() {
    printf '%s\n' 'Utilizare: sudo bash scripts/install-relay-linux.sh IP_PUBLIC_SAU_DNS [PORT] [ARHIVA_LOCALA SHA256]'
    printf '%s\n' 'Instalare initiala pe Ubuntu 24.04 x64 cu systemd. Port implicit: 45991/TCP.'
}
if [[ ${1:-} == --help ]]; then usage; exit 0; fi
if (( $# != 1 && $# != 2 && $# != 4 )); then usage >&2; exit 2; fi
public_host=$1
relay_port=${2:-45991}
[[ $public_host =~ ^[A-Za-z0-9][A-Za-z0-9.-]{0,252}$ ]] || fail 'Foloseste un IPv4/nume DNS, fara protocol, port sau spatii.'
[[ $relay_port =~ ^[0-9]{1,5}$ ]] || fail 'Port TCP invalid.'
relay_port=$((10#$relay_port))
(( relay_port >= 1024 && relay_port <= 65535 )) || fail 'Portul trebuie sa fie intre 1024 si 65535.'
(( EUID == 0 )) || fail 'Instalarea serviciului necesita sudo.'
[[ $(uname -m) == x86_64 ]] || fail 'Pachetul necesita Linux x64.'
# shellcheck source=/dev/null
source /etc/os-release
[[ ${ID:-} == ubuntu && ${VERSION_ID:-} == 24.04 ]] || fail 'Acest instalator a fost pregatit pentru Ubuntu 24.04. Pentru alt sistem adapteaza dependentele si serviciul.'
[[ -d /run/systemd/system ]] || fail 'Este necesar un sistem pornit cu systemd.'
for required in curl tar sha256sum install getent useradd runuser systemctl ss; do
    command -v "$required" >/dev/null || fail "Lipseste comanda $required."
done
source_root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd -P)
[[ -f "$source_root/deploy/relay/desk-ter-relay.service" && -f "$source_root/deploy/relay/show-code.sh" ]] || fail 'Descarca repository-ul complet, inclusiv deploy/relay.'
[[ ! -e /etc/systemd/system/desk-ter-relay.service ]] || fail 'Serviciul este deja instalat. Pentru administrare foloseste systemctl; instalatorul nu suprascrie serviciul existent.'
[[ -z $(ss -H -ltn "sport = :$relay_port") ]] || fail 'Portul ales este deja folosit de alt serviciu.'

stage=$(mktemp -d -t desk-ter-relay.XXXXXXXX)
trap 'rm -rf -- "$stage"' EXIT
if (( $# == 4 )); then
    archive=$(realpath -- "$3")
    expected=$4
    [[ -f $archive ]] || fail 'Arhiva locala nu exista.'
else
    # Pin the existing, published 0.1.2 binary: no moving "latest" or unverified payload.
    archive="$stage/relay.tar.gz"
    expected=aa23fa49276edece4b228e002049e3199c85694842e145a1bb8cc146e14838a6
    curl --fail --location --proto '=https' --proto-redir '=https' --connect-timeout 15 --max-time 180 \
        --output "$archive" \
        'https://github.com/ter22us/desk-ter/releases/download/build-4-1/Ter22-Relay-linux-x64.tar.gz'
fi
[[ $expected =~ ^[a-fA-F0-9]{64}$ ]] || fail 'SHA-256 invalid.'
actual=$(sha256sum -- "$archive")
[[ ${actual%% *} == "${expected,,}" ]] || fail 'SHA-256 nu corespunde. Instalarea a fost oprita.'
# Extract only the published executable into a file we choose, never arbitrary archive paths.
tar -xOzf "$archive" ./Ter22.Relay > "$stage/Ter22.Relay"
[[ -s "$stage/Ter22.Relay" ]] || fail 'Executabilul lipseste din arhiva.'

if getent passwd desk-ter-relay >/dev/null; then
    account=$(getent passwd desk-ter-relay)
    IFS=: read -r _ _ account_uid _ _ account_home account_shell <<< "$account"
    [[ $account_home == /var/lib/desk-ter-relay && $account_shell == /usr/sbin/nologin && $account_uid != 0 ]] \
        || fail 'Exista un alt utilizator desk-ter-relay; verifica-l inainte de instalare.'
else
    useradd --system --user-group --home-dir /var/lib/desk-ter-relay --shell /usr/sbin/nologin desk-ter-relay
fi
install -d -o root -g root -m 0755 /opt/desk-ter-relay /etc/desk-ter-relay
install -d -o desk-ter-relay -g desk-ter-relay -m 0700 /var/lib/desk-ter-relay
install -o root -g root -m 0755 "$stage/Ter22.Relay" /opt/desk-ter-relay/Ter22.Relay
# Also performs the native runtime/certificate preflight. Secrets never go to the installer log.
(
    cd /var/lib/desk-ter-relay
    runuser -u desk-ter-relay -- env DOTNET_BUNDLE_EXTRACT_BASE_DIR=/var/lib/desk-ter-relay/.net \
        /opt/desk-ter-relay/Ter22.Relay "$public_host" "$relay_port" --show-code > /dev/null
)
printf 'RELAY_PUBLIC_HOST=%s\nRELAY_PORT=%s\n' "$public_host" "$relay_port" > "$stage/relay.conf"
install -o root -g root -m 0644 "$stage/relay.conf" /etc/desk-ter-relay/relay.conf
install -o root -g root -m 0755 "$source_root/deploy/relay/show-code.sh" /usr/local/bin/desk-ter-relay-code
install -o root -g root -m 0644 "$source_root/deploy/relay/desk-ter-relay.service" /etc/systemd/system/desk-ter-relay.service
systemctl daemon-reload
systemctl enable --now desk-ter-relay.service
ready=false
for ((attempt=0; attempt<50; attempt++)); do
    if systemctl is-active --quiet desk-ter-relay.service && [[ -n $(ss -H -ltn "sport = :$relay_port") ]]; then
        ready=true
        break
    fi
    sleep 0.1
done
[[ $ready == true ]] || fail 'Serviciul nu asculta pe port. Verifica: journalctl -u desk-ter-relay -n 50 --no-pager'
printf 'Serviciu pornit local pe TCP %s; identitatea privata este pastrata in /var/lib/desk-ter-relay.\n' "$relay_port"
printf '%s\n' 'Codul de configurare se afiseaza doar la cerere: sudo desk-ter-relay-code'
printf 'Permite TCP %s in firewallul serverului si in firewallul furnizorului VPS.\n' "$relay_port"
printf '%s\n' 'Accesibilitatea din internet trebuie verificata separat de pe ambele PC-uri. Instalarea nu modifica firewallul serverului.'
