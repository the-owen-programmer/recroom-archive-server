# usage: find_static_stores.py <dll> <classVA hex> -- near each reference to the class pointer, list stores into [reg+disp] (static-field writes)
import sys,subprocess,re
dll=sys.argv[1]; cls=sys.argv[2]
xr=[int(l.split()[0],16) for l in subprocess.check_output(['python3','-I','fakeserver/tools/re/find_xrefs.py',dll,cls]).decode().splitlines()]
import capstone
d=open(dll,'rb').read(); md=capstone.Cs(capstone.CS_ARCH_X86,capstone.CS_MODE_64)
seen=set()
for x in xr:
    off=x-0x180001000
    for i in md.disasm(d[off:off+0x120],x):
        if i.mnemonic=='mov' and re.search(r'\], r',i.op_str) and re.search(r'qword ptr \[r..?\ \+\ 0x(8|10|38|58|60)\]',i.op_str) and i.address not in seen:
            seen.add(i.address); print(hex(x),hex(i.address),i.mnemonic,i.op_str)
