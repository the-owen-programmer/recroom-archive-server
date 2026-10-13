#!/bin/bash
# Local CA + server certificate for the fake Rec Room services (*.rec.net).
# Only ever used inside the private network namespace / throwaway Wine prefix.
set -e
cd "$(dirname "$0")"
HOSTS="ns api auth accounts www notifications images img cdn commerce matchmaking match storage chat leaderboard link lists roomcomments clubs rooms platformnotifications moderation datacollection bugreporting discovery playersettings studio gamelogs strings econ stringscdn data cards ai recnetstatus notify"
SAN="DNS:rec.net,DNS:*.rec.net"
for h in $HOSTS; do SAN="$SAN,DNS:$h.rec.net"; done
EXTRA="google.com www.google.com"   # internet connectivity probe (generate_204)
for h in $EXTRA; do SAN="$SAN,DNS:$h"; done
SAN="$SAN,DNS:localhost,IP:127.0.0.1"
openssl req -x509 -newkey rsa:2048 -nodes -keyout ca.key -out ca.pem -days 3650 -subj "/CN=RecServer Local Test CA" 2>/dev/null
openssl req -newkey rsa:2048 -nodes -keyout server.key -out server.csr -subj "/CN=ns.rec.net" 2>/dev/null
printf "subjectAltName=%s\nbasicConstraints=CA:FALSE\nkeyUsage=digitalSignature,keyEncipherment\nextendedKeyUsage=serverAuth\n" "$SAN" > ext.cnf
openssl x509 -req -in server.csr -CA ca.pem -CAkey ca.key -CAcreateserial -out server.pem -days 825 -extfile ext.cnf 2>/dev/null
openssl pkcs12 -export -out server.pfx -inkey server.key -in server.pem -certfile ca.pem -passout pass:rec
rm -f server.csr ext.cnf ca.srl
# names for /etc/hosts inside the namespace (hosts files have no wildcards)
{ echo "rec.net"; for h in $HOSTS; do echo "$h.rec.net"; done; for h in $EXTRA; do echo "$h"; done; } > hosts.txt
ls
