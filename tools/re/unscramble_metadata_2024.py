#!/usr/bin/env python3
"""Rebuild a standard IL2CPP metadata v29 header for the 20 Aug 2024 Windows build (Unity 2021.3.29f1).

Same idea as unscramble_metadata.py (Android, v31): the shuffled header is just a bag of (offset, size) words; sections
follow back to back from byte 256, so they are recovered by chaining offset+size. The file order of the 29 sections
differs per build (ORDER below was derived by content: typeDefinitions counts give methods/properties/fields/events/
nested/vtable/interfaces/interfaceOffsets; method parameterCount gives parameters; image/assembly counts give the rest;
default-value tables by index ranges). Method definitions are 32 bytes here (no returnParameterToken: v29).
usage: unscramble_metadata_2024.py <in global-metadata.dat> <out global-metadata.dat>
"""
import struct, sys

src, dst = sys.argv[1], sys.argv[2]
d = open(src, 'rb').read()
N = len(d)
words = [w for w in struct.unpack_from('<64I', d, 0) if w]
S = set(words)
secs, cur = [], 256
while cur < N:
    for z in words:
        if z and (cur + z in S or cur + z == N):
            secs.append((cur, z)); cur += z; break
    else:
        sys.exit(f'chain broke at {cur}')
assert cur == N and len(secs) == 29, (cur, len(secs))

SLOTS = ['stringLiteral', 'stringLiteralData', 'string', 'events', 'properties', 'methods',
         'parameterDefaultValues', 'fieldDefaultValues', 'fieldAndParameterDefaultValueData',
         'fieldMarshaledSizes', 'parameters', 'fields', 'genericParameters',
         'genericParameterConstraints', 'genericContainers', 'nestedTypes', 'interfaces',
         'vtableMethods', 'interfaceOffsets', 'typeDefinitions', 'images', 'assemblies', 'fieldRefs',
         'referencedAssemblies', 'attributeData', 'attributeDataRange',
         'unresolvedIndirectCallParameterTypes', 'unresolvedIndirectCallParameterRanges',
         'windowsRuntimeTypeNames', 'windowsRuntimeStrings', 'exportedTypeDefinitions']
ORDER = ['parameters', 'images', 'assemblies', 'events', 'properties', 'methods',
         'unresolvedIndirectCallParameterRanges', 'exportedTypeDefinitions', 'genericParameters', 'fields',
         'fieldAndParameterDefaultValueData', 'fieldMarshaledSizes', 'nestedTypes', 'genericContainers', 'interfaces',
         'stringLiteralData', 'parameterDefaultValues', 'interfaceOffsets', 'referencedAssemblies',
         'unresolvedIndirectCallParameterTypes', 'attributeData', 'genericParameterConstraints', 'fieldRefs',
         'attributeDataRange', 'fieldDefaultValues', 'vtableMethods', 'string', 'stringLiteral', 'typeDefinitions']
assert len(ORDER) == 29 and set(ORDER) <= set(SLOTS)
table = dict(zip(ORDER, secs))

# sanity checks that fail if the mapping is wrong
td_o, td_z = table['typeDefinitions']
n = td_z // 88
tot = [0] * 8
for i in range(n):
    for j, v in enumerate(struct.unpack_from('<8H', d, td_o + i * 88 + 64)):
        tot[j] += v
chk = {'methods': (tot[0], 32), 'properties': (tot[1], 20), 'fields': (tot[2], 12), 'events': (tot[3], 24),
       'nestedTypes': (tot[4], 4), 'vtableMethods': (tot[5], 4), 'interfaces': (tot[6], 4),
       'interfaceOffsets': (tot[7], 8)}
for name, (cnt, sz) in chk.items():
    assert table[name][1] == cnt * sz, f'{name}: {table[name][1]} != {cnt}*{sz}'
mo, mz = table['methods']
assert table['parameters'][1] == 12 * sum(struct.unpack_from('<H', d, mo + i * 32 + 30)[0] for i in range(mz // 32))
assert table['images'][1] // 40 == table['assemblies'][1] // 64

body, pairs, pos = b'', [], 256
for slot in SLOTS:
    if slot in table:
        o, z = table[slot]
        pairs.append((pos, z)); body += d[o:o + z]; pos += z
        pad = (-pos) % 4
        body += b'\0' * pad; pos += pad
    else:
        pairs.append((pos, 0))
hdr = struct.pack('<Ii', 0xFAB11BAF, 29) + b''.join(struct.pack('<II', o, z) for o, z in pairs)
assert len(hdr) == 256
open(dst, 'wb').write(hdr + body)
print(f'wrote {dst}: {n} type definitions, v29, section counts verified')
