# Like scan_modulus.py but accepts PKCS#1 v1.5 (00 01 ff..) OR PSS-style (ends 0xbc) padding, and runs on all cores.
import base64,sys,time
from multiprocessing import Pool
path=sys.argv[1]
sigs=[base64.b64decode(l.split(b'|',1)[1]) for l in open('builds/2024-08-20/RecRoom_Data/verification.sig','rb').read().splitlines()[:3] if b'|' in l]
sv=[int.from_bytes(s,'big') for s in sigs]
d=open(path,'rb').read()
def ok(n):
    if not all(s<n for s in sv): return None
    ms=[pow(s,65537,n) for s in sv]
    if all((m>>2032)==0x01ff for m in ms): return 'PKCS'
    if all((m&0xff)==0xbc for m in ms): return 'PSS'
def work(a):
    lo,hi=a; out=[]
    for off in range(lo,hi):
        b=d[off:off+256]
        if b[0]&0x80 and b[255]&1:
            r=ok(int.from_bytes(b,'big'))
            if r: out.append(('BE',r,off,b.hex()))
        if b[255]&0x80 and b[0]&1:
            r=ok(int.from_bytes(b,'little'))
            if r: out.append(('LE',r,off,b[::-1].hex()))
    return out
if __name__=='__main__':
    N=len(d)-256; step=200000; t=time.time()
    with Pool() as p:
        for i,res in enumerate(p.imap_unordered(work,[(a,min(a+step,N)) for a in range(0,N,step)])):
            for r in res: print('MATCH',*r,flush=True)
            if i%10==0: print('chunks',i,'of',N//step+1,'%ds'%(time.time()-t),flush=True)
    print('done',flush=True)
