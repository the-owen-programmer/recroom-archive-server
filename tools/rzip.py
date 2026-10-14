#!/usr/bin/env python3
"""List or extract single files of a remote ZIP over HTTP range requests (zip64 aware), e.g. one file out of a 5 GB archived
game build without downloading the rest.

  rzip.py https://archive.recagain.site/download/manifest/1664414749991987435 list
  rzip.py <url> get RecRoom_Data/resources.assets resources.assets

The manifest above is the plain 2024-08-20 Windows build. Stdlib only.
"""
import struct, sys, urllib.request, zlib
UA = {"User-Agent": "curl/8.5.0"}

def rng(url, a, b):
    req = urllib.request.Request(url, headers={**UA, "Range": f"bytes={a}-{b}"})
    with urllib.request.urlopen(req, timeout=120) as r:
        total = int(r.headers["Content-Range"].split("/")[1]); return r.read(), total

def entries(url):
    tail, total = rng(url, 0, 0); tail, total = rng(url, total - 65558, total - 1)
    base = total - len(tail); i = tail.rfind(b"PK\x05\x06")
    cd_size, cd_off = struct.unpack("<II", tail[i+12:i+20]); count = struct.unpack("<H", tail[i+10:i+12])[0]
    j = tail.rfind(b"PK\x06\x07")
    if j >= 0:
        z64 = struct.unpack("<Q", tail[j+8:j+16])[0]
        rec, _ = rng(url, z64, z64 + 55)
        count, cd_size, cd_off = struct.unpack("<QQQ", rec[32:56])
    cd, _ = rng(url, cd_off, cd_off + cd_size - 1); p = 0
    for _ in range(count):
        meth, = struct.unpack("<H", cd[p+10:p+12]); csz, usz = struct.unpack("<II", cd[p+20:p+28])
        nl, el, cl = struct.unpack("<HHH", cd[p+28:p+34]); off, = struct.unpack("<I", cd[p+42:p+46])
        name = cd[p+46:p+46+nl].decode("utf-8", "replace"); ex = cd[p+46+nl:p+46+nl+el]; q = 0
        while q < len(ex):
            hid, hl = struct.unpack("<HH", ex[q:q+4]); d = ex[q+4:q+4+hl]
            if hid == 1:
                vals = list(struct.unpack("<" + "Q" * (hl // 8), d[:hl // 8 * 8])); k = 0
                if usz == 0xFFFFFFFF: usz = vals[k]; k += 1
                if csz == 0xFFFFFFFF: csz = vals[k]; k += 1
                if off == 0xFFFFFFFF: off = vals[k]
            q += 4 + hl
        yield name, meth, csz, usz, off; p += 46 + nl + el + cl

def get(url, e, out):
    name, meth, csz, usz, off = e
    h, _ = rng(url, off, off + 29); nl, el = struct.unpack("<HH", h[26:30]); start = off + 30 + nl + el
    with open(out, "wb") as f:
        d = zlib.decompressobj(-15) if meth == 8 else None; pos = start; end = start + csz
        while pos < end:
            chunk, _ = rng(url, pos, min(end, pos + 32 * 2**20) - 1); pos += len(chunk)
            f.write(d.decompress(chunk) if d else chunk)
        if d: f.write(d.flush())

def read_prefix(url, e, nbytes=65536):
    """The first ~nbytes of an entry's uncompressed content, reading only as much of the archive as that needs."""
    name, meth, csz, usz, off = e
    h, _ = rng(url, off, off + 29); nl, el = struct.unpack("<HH", h[26:30]); start = off + 30 + nl + el
    chunk, _ = rng(url, start, start + min(csz, nbytes) - 1)
    if meth != 8:
        return chunk
    try:
        return zlib.decompressobj(-15).decompress(chunk)
    except zlib.error:
        return b""

if __name__ == "__main__":
    url = sys.argv[1]; es = list(entries(url))
    if sys.argv[2] == "list":
        for n, m, c, u, o in es: print(u, n)
    else:
        e = next(x for x in es if x[0] == sys.argv[3]); get(url, e, sys.argv[4]); print("ok", e[3])
