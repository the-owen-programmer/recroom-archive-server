#!/bin/bash
# Run the fake api.rec.net server and a command (default: the Referee launcher under Wine) inside a private
# user+network namespace. No root needed. api.rec.net resolves to loopback only inside the namespace.
# usage: run-in-netns.sh <wineprefix> [seconds]
set -u
HERE="$(cd "$(dirname "$0")" && pwd)"
PFX="${1:?wineprefix}"; SECS="${2:-40}"
GAME="$HERE/../RecRoom"
wineserver -k 2>/dev/null
exec unshare -rmn bash -c '
  ip link set lo up
  printf "127.0.0.1 localhost owen-workstation api.rec.net rec.net\n::1 localhost\n" > "'"$HERE"'/hosts.ns"
  mount --bind "'"$HERE"'/hosts.ns" /etc/hosts
  cd "'"$HERE"'"; rm -f fakeserver.log
  dotnet bin/fakeserver.dll 443 > server.out 2>&1 &
  sleep 2
  cd "'"$GAME"'"
  export WINEPREFIX="'"$PFX"'" WINEDEBUG=-all
  OLDPIN="SxRa6AJck9TOn89oM6jL2x9hjhGrOsPVtvMtKSYaCDo="
  NEWPIN="$(openssl x509 -in "'"$HERE"'/certs/cert.pem" -pubkey -noout | openssl pkey -pubin -outform der | openssl dgst -sha256 -binary | openssl enc -base64)"
  wine "'"$HERE"'/tools/patchpin.exe" "$OLDPIN" "$NEWPIN" '"$SECS"' > "'"$HERE"'/patchpin.out" 2>&1 &
  timeout '"$SECS"' wine Recroom_Release.exe >/dev/null 2>&1
  wineserver -k 2>/dev/null; kill %1 2>/dev/null; true
'
