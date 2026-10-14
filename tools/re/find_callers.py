# usage: find_callers.py <dll> <target VA hex>... -- E8 rel32 call/jmp sites to each target
import sys,numpy as np
d=np.fromfile(sys.argv[1],dtype=np.uint8); base=0x180001000
n=len(d)-5
idx=np.nonzero((d[:n]==0xe8)|(d[:n]==0xe9))[0]
b=d.tobytes(); T={int(t,16) for t in sys.argv[2:]}
for i in idx:
    t=base+i+5+int.from_bytes(b[i+1:i+5],'little',signed=True)
    if t in T: print(hex(t),'<-',hex(base+i), 'call' if b[i]==0xe8 else 'jmp')
