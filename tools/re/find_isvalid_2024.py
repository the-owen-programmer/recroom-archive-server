#!/usr/bin/env python3
"""List every class implementing BouncyCastle ICertificateVerifyer in the 2024-08-20 Rec Room build, with the VA / file
offset of its IsValid method (the RecNet TLS certificate check that rejects the local ns.rec.net certificate).
Needs the repaired metadata (unscramble_metadata_2024.py) and the plain GameAssembly.dll; registration addresses were found
with pe_findreg.py. usage: find_isvalid_2024.py <GameAssembly.dll> <repaired global-metadata.dat> [MetadataRegistration VA] [module array VA]"""
import re, struct, sys
ga, mdp = sys.argv[1], sys.argv[2]
MREG = int(sys.argv[3], 16) if len(sys.argv) > 3 else 0x189dd3ad0
MODARR = int(sys.argv[4], 16) if len(sys.argv) > 4 else 0x18a793820
BASE = 0x180000000
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
S_STR, S_METH, S_TD, S_IMG, S_IFACE = 2, 5, 19, 20, 16
cstr = lambda i: md[OFF(S_STR) + i:md.index(b'\0', OFF(S_STR) + i)].decode('utf8', 'replace')
ntd = SZ(S_TD) // 88
td = lambda i: struct.unpack_from('<16I8H2I', md, OFF(S_TD) + i * 88)
tcount, tptr = q(MREG + 48), q(MREG + 56)
def type_typedef(ti):   # Il2CppType -> typedef index for class/valuetype
    a = q(tptr + 8 * ti); bits = u32(a + 8); code = (bits >> 16) & 0xFF
    return q(a) if code in (17, 18) else None
def tname(i, simple=False):
    t = td(i); n = re.sub(r'`\d+$', '', cstr(t[0]))
    d = type_typedef(t[3]) if t[3] != 0xFFFFFFFF else None
    return (tname(d) + '.' + n) if d is not None and not simple else n
# images -> module names
imgs = [struct.unpack_from('<10I', md, OFF(S_IMG) + i * 40) for i in range(SZ(S_IMG) // 40)]
mods = {}
for k in range(len(imgs)):
    m = q(MODARR + 8 * k)
    mods[bytes(f[off(q(m)):off(q(m)) + 80]).split(b'\0')[0].decode()] = m
target = [i for i in range(ntd) if cstr(td(i)[0]) == 'ICertificateVerifyer']
print('ICertificateVerifyer typedefs:', target)
for ii, im in enumerate(imgs):
    iname = cstr(im[0]); mod = mods.get(iname)
    for t in range(im[2], im[2] + im[3]):
        f_ = td(t); icnt = f_[8 + 8 + 6 - 8 + 0] if False else struct.unpack_from('<H', md, OFF(S_TD) + t * 88 + 64 + 12)[0]
        impl = [type_typedef(struct.unpack_from('<i', md, OFF(S_IFACE) + (f_[13] + k) * 4)[0]) for k in range(icnt)]
        if not any(x in target for x in impl): continue
        print(f'\n{iname}: {tname(t)}  (typedef {t}, name idx {f_[0]})')
        mstart = f_[9]; mcnt = struct.unpack_from('<H', md, OFF(S_TD) + t * 88 + 64)[0]
        for m in range(mstart, mstart + mcnt):
            mm = struct.unpack_from('<6I4H', md, OFF(S_METH) + m * 32)
            nm = cstr(mm[0])
            if nm != 'IsValid': continue
            rid = (mm[5] & 0xFFFFFF) - 1
            if mod is None: print('  no module for', iname); continue
            cnt, ptrs = u32(mod + 8), q(mod + 16)
            va = q(ptrs + 8 * rid) if rid < cnt else None
            if va: print(f'  IsValid  VA {va:#x}  file offset {off(va):#x}  bytes {f[off(va):off(va)+16].hex(" ")}')
            else: print('  IsValid  no pointer, rid', rid, 'count', cnt)
