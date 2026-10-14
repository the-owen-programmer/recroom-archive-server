# Launch the game as our child, wait until it asks cdn.rec.net/sigs/, freeze it, and look through its memory for the RSA modulus
# (a managed byte[256]: length 0x100 at +0x18, data at +0x20) by testing candidates against verification.sig signatures.
import os,sys,time,signal,subprocess,base64,re
ROOT=os.environ['ROOT']; HERE=os.environ['HERE']; OUT=HERE+'/tools/re/keydebug/out'
sigs=[base64.b64decode(l.split(b'|',1)[1]) for l in open(ROOT+'/builds/2024-08-20/RecRoom_Data/verification.sig','rb').read().splitlines()[:6] if b'|' in l]
sv=[int.from_bytes(s,'big') for s in sigs]
def ok(n):
    if not all(s<n for s in sv): return None
    ms=[pow(s,65537,n) for s in sv]
    if all((m>>2032)==0x01ff for m in ms): return 'PKCS'
    if all((m&0xff)==0xbc for m in ms): return 'PSS'
game=subprocess.Popen(['wine','RecRoom.exe'])
print('launched',game.pid,flush=True)
t=time.time()
while time.time()-t<240:
    time.sleep(0.5)
    if b'/sigs/' in open(HERE+'/logs/server.out','rb').read(): break
else: print('never saw sigs request'); game.kill(); sys.exit(1)
time.sleep(float(os.environ.get("DUMP_DELAY","1.0")))
pids=[int(p) for p in os.listdir('/proc') if p.isdigit()]
tgt=[p for p in pids if b'RecRoom.exe' in (open(f'/proc/{p}/cmdline','rb').read() if os.access(f'/proc/{p}/cmdline',os.R_OK) else b'')]
print('candidate pids',tgt,flush=True)
pat=re.compile(rb'\x00\x01\x00\x00\x00\x00\x00\x00',re.S)
found=False
for pid in tgt:
    try: os.kill(pid,signal.SIGSTOP)
    except Exception as e: print('stop',pid,e)
    try:
        maps=open(f'/proc/{pid}/maps').read().splitlines(); mem=open(f'/proc/{pid}/mem','rb',0)
    except Exception as e: print('open',pid,e); continue
    ncand=0
    for ln in maps:
        m=re.match(r'([0-9a-f]+)-([0-9a-f]+) (\S+)',ln)
        a,b,perm=int(m[1],16),int(m[2],16),m[3]
        if 'r' not in perm or 'w' not in perm or b-a>1<<31: continue
        try: mem.seek(a); buf=mem.read(b-a)
        except Exception: continue
        for mm in pat.finditer(buf):
            s=mm.start()+8
            if s+256>len(buf) or mm.start()%8: continue
            blob=buf[s:s+256]
            if blob[0]&0x80 and blob[255]&1:
                ncand+=1; open(OUT+'/cands.txt','a').write(hex(a+s)+' '+blob.hex()+'\n'); r=ok(int.from_bytes(blob,'big'))
                if r: print('FOUND',pid,r,hex(a+s),blob.hex(),flush=True); open(OUT+'/modulus.hex','w').write(blob.hex()); found=True
    print('pid',pid,'candidates tested',ncand,flush=True)
    os.kill(pid,signal.SIGCONT)
print('RESULT','found' if found else 'not found',flush=True)
game.kill()
