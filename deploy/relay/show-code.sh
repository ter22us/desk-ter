#!/usr/bin/env bash
set -euo pipefail

if (( EUID != 0 )); then
    printf '%s\n' 'Ruleaza cu sudo; codul releului este privat.' >&2
    exit 1
fi
if [[ ! -f /var/lib/desk-ter-relay/relay-state.json ]]; then
    printf '%s\n' 'Identitatea releului lipseste. Verifica instalarea; nu este generata alta identitate aici.' >&2
    exit 1
fi
# Root-owned configuration contains only the validated public host and TCP port.
# shellcheck source=/dev/null
source /etc/desk-ter-relay/relay.conf
cd /var/lib/desk-ter-relay
exec runuser -u desk-ter-relay -- env \
    DOTNET_BUNDLE_EXTRACT_BASE_DIR=/var/lib/desk-ter-relay/.net \
    /opt/desk-ter-relay/Ter22.Relay "$RELAY_PUBLIC_HOST" "$RELAY_PORT" --show-code
