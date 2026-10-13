#!/bin/bash
# Run the fake Rec Room backend and the old Windows build together inside a private user+network namespace.
# No root needed. Inside the namespace EVERY host name resolves to 127.0.0.1 (wildcard DNS, names logged), the server owns port 443
# and issues a certificate per requested host from the local test CA, which is appended to the CA bundle Wine reads. Nothing outside the namespace changes.
#
# layout: this repo is expected at <project>/fakeserver, game builds at <project>/builds/...
# usage: run-in-netns.sh [seconds=90] [wineprefix]
#   env: BUILD=builds/2024-08-20  (game directory, relative to the project root)
#        HEADLESS=1  run the game on a private virtual display (Xvfb) so nothing can interact with its window
set -u
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/.." && pwd)"
SECS="${1:-90}"
PFX="${2:-${WINEPREFIX:-$ROOT/work/wineprefix}}"
BUILD="${BUILD:-builds/2024-08-20}"
DOTNET_ROOT="$HERE/.dotnet/root"
mkdir -p "$HERE/run" "$HERE/logs"

[ -x "$DOTNET_ROOT/dotnet" ] || { DOTNET_ROOT="$(dirname "$(readlink -f "$(command -v dotnet)")")"; }
[ -f "$HERE/certs/server.pfx" ] || "$HERE/certs/make-certs.sh" >/dev/null
[ -f "$HERE/RecServer/bin/Debug/net10.0/RecServer.dll" ] || (cd "$HERE/RecServer" && dotnet build -v q)
wineserver -k 2>/dev/null

# files that get bind-mounted over system files inside the namespace
echo "nameserver 127.0.0.1" > "$HERE/run/resolv.conf"   # wildcard DNS (tools/wildcard_dns.py) answers every name with 127.0.0.1
cat /etc/ssl/certs/ca-certificates.crt "$HERE/certs/ca.pem" > "$HERE/run/ca-bundle.crt"

exec unshare -rmn env HERE="$HERE" ROOT="$ROOT" SECS="$SECS" PFX="$PFX" BUILD="$BUILD" HEADLESS="${HEADLESS:-0}" REC_WINEDEBUG="${REC_WINEDEBUG:--all}" REC_LOGLEVEL="${REC_LOGLEVEL:-Information}" DOTNET_ROOT="$DOTNET_ROOT" bash -c '
  ip link set lo up
  mount --bind "$HERE/run/resolv.conf" /etc/resolv.conf
  python3 "$HERE/tools/wildcard_dns.py" "$HERE/logs/dns.log" &
  DNS=$!
  mount --bind "$HERE/run/ca-bundle.crt" /etc/ssl/certs/ca-certificates.crt
  cd "$HERE/RecServer"
  REC_PORT=443 REC_LOGLEVEL="$REC_LOGLEVEL" Logging__LogLevel__Default="${REC_LOGLEVEL:-Information}" "$DOTNET_ROOT/dotnet" bin/Debug/net10.0/RecServer.dll > "$HERE/logs/server.out" 2>&1 &
  SRV=$!
  for i in $(seq 1 30); do (echo > /dev/tcp/127.0.0.1/443) 2>/dev/null && break; sleep 0.5; done
  echo "server up (pid $SRV), starting game for ${SECS}s"
  cd "$ROOT/$BUILD"
  export WINEPREFIX="$PFX" WINEDEBUG="$REC_WINEDEBUG"
  if [ "$HEADLESS" = 1 ]; then Xvfb :99 -screen 0 1280x720x24 >/dev/null 2>&1 & XV=$!; sleep 1; export DISPLAY=:99; unset WAYLAND_DISPLAY; fi
  timeout "$SECS" wine RecRoom.exe > "$HERE/logs/game-wine.out" 2>&1
  echo "game exited ($?)"
  wineserver -k 2>/dev/null
  kill $SRV $DNS $XV 2>/dev/null; wait $SRV 2>/dev/null
'
