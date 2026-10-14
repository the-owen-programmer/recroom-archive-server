# usage: find_xrefs.py <dll> <target VA hex> [imagebase-section-delta=0x180001000]  -- finds RIP-relative mov/lea references to a VA
import sys, numpy as np
d=np.fromfile(sys.argv[1],dtype=np.uint8); T=int(sys.argv[2],16); base=0x180001000
n=len(d)-8
rex=(d[:n]==0x48)|(d[:n]==0x4c)
op=(d[1:n+1]==0x8b)|(d[1:n+1]==0x8d)
modrm=((d[2:n+2]&0xc7)==0x05)
idx=np.nonzero(rex&op&modrm)[0]
buf=d.tobytes()
for i in idx:
    disp=int.from_bytes(buf[i+3:i+7],'little',signed=True)
    if base+i+7+disp==T: print(hex(base+i), buf[i:i+7].hex())
