#!/usr/bin/env python3
"""Find methods by name in the 2024-08-20 Rec Room build: prints class, image, VA, file offset and first bytes.
usage: find_method_2024.py <GameAssembly.dll> <repaired global-metadata.dat> <method name regex> [class regex]
(MetadataRegistration 0x189dd3ad0 / module array 0x18a793820 found with pe_findreg.py)"""
import re, struct, sys
ga, mdp, mre = sys.argv[1], sys.argv[2], re.compile(sys.argv[3])
cre = re.compile(sys.argv[4]) if len(sys.argv) > 4 else None
MREG, MODARR, BASE = 0x189dd3ad0, 0x18a793820, 0x180000000
f = open(ga, 'rb').read(); md = open(mdp, 'rb').read()
pe = struct.unpack_from('<I', f, 0x3c)[0]; nsec = struct.unpack_from('<H', f, pe + 6)[0]; osz = struct.unpack_from('<H', f, pe + 20)[0]
S = []
for i in range(nsec):
    o = pe + 24 + osz + i * 40
    vsz, va, rsz, ro = struct.unpack_from('<IIII', f, o + 8); S.append((va, max(vsz, rsz), ro, rsz))
def off(v):
    r = v - BASE
    for a, sz, ro, rsz in S:
        if a <= r < a + sz and r - a < rsz: return ro + r - a
    raise KeyError(hex(v))
q = lambda v: struct.unpack_from('<Q', f, off(v))[0]
u32 = lambda v: struct.unpack_from('<I', f, off(v))[0]
hdr = struct.unpack_from('<2I62I', md, 0)
OFF = lambda k: hdr[2 + 2 * k]; SZ = lambda k: hdr[3 + 2 * k]
cstr = lambda i: md[OFF(2) + i:md.index(b'\0', OFF(2) + i)].decode('utf8', 'replace')
imgs = [struct.unpack_from('<10I', md, OFF(20) + i * 40) for i in range(SZ(20) // 40)]
mods = {}   # module list order differs from image order: match by module name
for k in range(len(imgs)):
    m = q(MODARR + 8 * k)
    mods[bytes(f[off(q(m)):off(q(m)) + 80]).split(b'\0')[0].decode()] = m
for k, im in enumerate(imgs):
    for t in range(im[2], im[2] + im[3]):
        td = struct.unpack_from('<16I8H2I', md, OFF(19) + t * 88)
        cname = cstr(td[0])
        if cre and not cre.search(cname): continue
        mcnt = struct.unpack_from('<H', md, OFF(19) + t * 88 + 64)[0]
        for m in range(td[9], td[9] + mcnt):
            mm = struct.unpack_from('<6I4H', md, OFF(5) + m * 32)
            nm = cstr(mm[0])
            if not mre.search(nm): continue
            rid = (mm[5] & 0xFFFFFF) - 1
            mod = mods[cstr(im[0])]; cnt, ptrs = u32(mod + 8), q(mod + 16)
            va = q(ptrs + 8 * rid) if rid < cnt else 0
            print(f'{cstr(im[0])}: {cname}.{nm}  params={mm[9]}  VA {va:#x}' + (f'  off {off(va):#x}  {f[off(va):off(va)+12].hex(" ")}' if va else ''))
