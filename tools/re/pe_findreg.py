#!/usr/bin/env python3
"""Locate Il2CppMetadataRegistration / Il2CppCodeRegistration in a stripped x64 Windows GameAssembly.dll (IL2CPP v29).
usage: pe_findreg.py <GameAssembly.dll> <typedef count>
MetadataRegistration: qword == typedef count at +96 (typeDefinitionsSizesCount) with a valid pointer after it.
CodeRegistration: found via the code-gen module list (module->name points at "Assembly-CSharp.dll")."""
import struct, sys
import numpy as np
path, ntd = sys.argv[1], int(sys.argv[2])
f = open(path, 'rb').read()
pe = struct.unpack_from('<I', f, 0x3c)[0]
nsec = struct.unpack_from('<H', f, pe + 6)[0]; osz = struct.unpack_from('<H', f, pe + 20)[0]
base = struct.unpack_from('<Q', f, pe + 24 + 24)[0]
secs = []
for i in range(nsec):
    o = pe + 24 + osz + i * 40
    name = f[o:o + 8].rstrip(b'\0').decode(); vsz, va, rsz, roff = struct.unpack_from('<IIII', f, o + 8)
    secs.append((name, va, vsz, roff, rsz))
print('base', hex(base), [(s[0], hex(s[1]), hex(s[2])) for s in secs])
def va2off(va):
    r = va - base
    for n, a, vsz, ro, rsz in secs:
        if a <= r < a + max(vsz, rsz): return ro + r - a
def inimg(v): return base <= v < base + 0x20000000
def q(va): o = va2off(va); return struct.unpack_from('<Q', f, o)[0]
data = [(s[0], s[1], f[s[3]:s[3] + s[4]]) for s in secs if s[0] in ('.data', '.rdata')]
# metadata registration
for n, a, buf in data:
    arr = np.frombuffer(buf[:len(buf) // 8 * 8], dtype='<u8')
    for idx in np.nonzero(arr == ntd)[0]:
        va = base + a + int(idx) * 8
        try:
            if inimg(q(va + 8)) and q(va - 48) > 1000 and inimg(q(va - 40)):
                print('MetadataRegistration candidate', hex(va - 96), 'types', q(va - 48), 'sizesPtr', hex(q(va + 8)))
        except Exception: pass
# code gen modules
needle = b'Assembly-CSharp.dll\0'
pos = f.find(needle)
print('name string at file off', hex(pos))
nameva = None
for n, a, vsz, ro, rsz in secs:
    if ro <= pos < ro + rsz: nameva = base + a + pos - ro
print('name VA', hex(nameva) if nameva else None)
for n, a, buf in data:
    arr = np.frombuffer(buf[:len(buf) // 8 * 8], dtype='<u8')
    for idx in np.nonzero(arr == nameva)[0]:
        mva = base + a + int(idx) * 8
        print('module struct', hex(mva), 'methodPointerCount', q(mva + 8))
        for n2, a2, b2 in data:
            ar2 = np.frombuffer(b2[:len(b2) // 8 * 8], dtype='<u8')
            for j in np.nonzero(ar2 == mva)[0]:
                lst = base + a2 + int(j) * 8
                print('  listed at', hex(lst))
