#!/usr/bin/env python3
"""Tiny DNS server for the private network namespace: answers every A query with 127.0.0.1 (AAAA/others: empty answer)
and logs each queried name, so the game's traffic always lands on the fake backend and we learn every host it wants.
usage: wildcard_dns.py <logfile>   (binds 127.0.0.1:53 UDP)"""
import socket, struct, sys, time
log = open(sys.argv[1], 'a', buffering=1)
s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
s.bind(('127.0.0.1', 53))
seen = set()
while True:
    data, addr = s.recvfrom(512)
    if len(data) < 12: continue
    tid = data[:2]; i = 12; labels = []
    while i < len(data) and data[i]:
        n = data[i]; labels.append(data[i + 1:i + 1 + n].decode('ascii', 'replace')); i += n + 1
    qend = i + 5
    qtype = struct.unpack('>H', data[i + 1:i + 3])[0] if i + 3 <= len(data) else 0
    name = '.'.join(labels)
    if (name, qtype) not in seen:
        seen.add((name, qtype)); log.write(f"{time.strftime('%H:%M:%S')} type={qtype} {name}\n")
    question = data[12:qend]
    if qtype == 1:
        resp = tid + b'\x81\x80' + b'\x00\x01\x00\x01\x00\x00\x00\x00' + question + b'\xc0\x0c\x00\x01\x00\x01\x00\x00\x00\x3c\x00\x04\x7f\x00\x00\x01'
    else:
        resp = tid + b'\x81\x80' + b'\x00\x01\x00\x00\x00\x00\x00\x00' + question
    s.sendto(resp, addr)
