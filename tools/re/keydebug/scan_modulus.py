# Slide a 256-byte window over a file; report windows that act as an RSA-2048 modulus (e=65537) for the shipped verification.sig signatures.
import base64,sys,time
path=sys.argv[1]
sigs=[base64.b64decode(l.split(b'|',1)[1]) for l in open('builds/2024-08-20/RecRoom_Data/verification.sig','rb').read().splitlines()[:3] if b'|' in l]
sv=[int.from_bytes(s,'big') for s in sigs]
d=open(path,'rb').read()
N=len(d)-256
def ok(n):
    if not all(s<n for s in sv): return False
    return all((pow(s,65537,n)>>2032)==0x01ff for s in sv)
t=time.time()
for off in range(N):
    if off%2000000==0: print('progress %.1f%% %ds'%(100*off/N,time.time()-t),flush=True)
    b=d[off:off+256]
    if b[0]&0x80 and b[255]&1 and ok(int.from_bytes(b,'big')): print('MATCH BE',hex(off),b.hex(),flush=True)
    if b[255]&0x80 and b[0]&1 and ok(int.from_bytes(b,'little')): print('MATCH LE',hex(off),b[::-1].hex(),flush=True)
print('done',path,flush=True)
