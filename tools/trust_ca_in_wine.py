#!/usr/bin/env python3
"""Make a (throwaway) Wine prefix trust the local test CA (server/certs/ca.pem), in every place a Unity/Mono game may look:
 - Windows cert store roots in the registry (HKCU and HKLM ...\\SystemCertificates\\Root\\Certificates\\<sha1>\\Blob)
 - Mono's own trust store folders (<AppData>/.mono/certs/Trust, <ProgramData>/.mono/certs/Trust)
usage: trust_ca_in_wine.py <wineprefix> <ca.pem>"""
import base64, hashlib, os, re, subprocess, sys, struct
pfx, pem = sys.argv[1], sys.argv[2]
der = base64.b64decode(re.search(r'-----BEGIN CERTIFICATE-----(.*?)-----END CERTIFICATE-----', open(pem).read(), re.S).group(1))
thumb = hashlib.sha1(der).hexdigest().upper()
blob = struct.pack('<III', 32, 1, len(der)) + der          # CERT_CERT_PROP_ID, reserved=1, length, certificate
hexs = ','.join('%02x' % b for b in blob)
reg = 'Windows Registry Editor Version 5.00\n\n'
for root in ('HKEY_CURRENT_USER', 'HKEY_LOCAL_MACHINE'):
    reg += f'[{root}\\Software\\Microsoft\\SystemCertificates\\Root\\Certificates\\{thumb}]\n"Blob"=hex:{hexs}\n\n'
regfile = os.path.join(pfx, 'recserver-ca.reg')
open(regfile, 'w', newline='\r\n').write(reg)
env = dict(os.environ, WINEPREFIX=pfx, WINEDEBUG='-all')
subprocess.run(['wine', 'regedit', '/S', regfile], env=env, check=False, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
user = os.environ.get('USER', 'owen')
for d in (f'drive_c/users/{user}/AppData/Roaming/.mono/certs/Trust', 'drive_c/ProgramData/.mono/certs/Trust'):
    os.makedirs(os.path.join(pfx, d), exist_ok=True)
    open(os.path.join(pfx, d, thumb + '.cer'), 'wb').write(der)
print('trusted', thumb)
