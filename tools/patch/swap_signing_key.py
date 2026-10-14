#!/usr/bin/env python3
"""Swap the content-signature RSA modulus in the PATCHED build's global-metadata.dat for our own (certs/content-sign-modulus.hex).
Refuses unless the 256 bytes currently there are either Rec Room's original (docs/rr-signing-modulus.hex) or already ours.
usage: swap_signing_key.py <patched build dir>   (never point this at the pristine builds/2024-08-20)"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__)) + '/../..'
orig = bytes.fromhex(open(HERE + '/docs/rr-signing-modulus.hex').read().strip())
ours = bytes.fromhex(open(HERE + '/certs/content-sign-modulus.hex').read().strip())
assert len(orig) == len(ours) == 256
p = sys.argv[1] + '/RecRoom_Data/il2cpp_data/Metadata/global-metadata.dat'
if os.stat(p).st_nlink > 1:
    sys.exit('refusing: %s is hardlinked (%d links) - make a real copy first (cp --remove-destination)' % (p, os.stat(p).st_nlink))
d = bytearray(open(p, 'rb').read())
if d.count(ours) == 1 and d.count(orig) == 0: sys.exit('already patched with our key')
assert d.count(orig) == 1, 'original modulus not found exactly once'
i = d.find(orig); d[i:i+256] = ours
open(p + '.tmp', 'wb').write(d); os.replace(p + '.tmp', p)   # new inode, never writes through a link
print('patched', p, 'at', i)
