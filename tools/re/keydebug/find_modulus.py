import re,base64,sys
meta=open('work/meta2024/global-metadata.dat','rb').read()
sigs=[l.split(b'|',1) for l in open('builds/2024-08-20/RecRoom_Data/verification.sig','rb').read().splitlines()[:5] if b'|' in l]
sigv=[int.from_bytes(base64.b64decode(s[1]),'big') for s in sigs]
cands=set(re.findall(rb'[0-9A-Fa-f]{256,}',meta))
print(len(cands),'hex candidates; sig lens',[len(base64.b64decode(s[1])) for s in sigs])
for c in cands:
    print(len(c), c[:24])
    for e in (65537,3,17):
        n=int(c,16)
        for s in sigv:
            if s<n:
                m=pow(s,e,n).to_bytes((n.bit_length()+7)//8,'big')
                if m[:2]==b'\x00\x01' or m[1:3]==b'\x00\x01' if False else m[:2]==b'\x00\x01':
                    print('MATCH e=',e,'n=',c.decode()); 
