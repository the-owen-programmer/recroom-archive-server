#!/usr/bin/env python3
"""Mirror files that the Internet Archive captured from a RecNet content host into RecServer's data directory, keeping the
URL path, so the server can hand them back to the game unchanged.

  wayback_mirror.py cdn.rec.net/room/          -> data/cdn/room/<blob>.room   (saved room contents)
  wayback_mirror.py cdn.rec.net/config/        -> data/cdn/config/...         (LoadingScreenTipData, *Config_vN.json)
  wayback_mirror.py img.rec.net/                -> data/img/<name>
  wayback_mirror.py cdn.rec.net/room/ --list    only list what is archived (no download)

Only 200 captures are used, the newest capture of each path wins, query strings are dropped (cdn.rec.net/config/X?a and ?b
are cache-busting copies of X), and files already on disk are skipped, so the command can be re-run to resume.
Stdlib only. Local, offline archival use.
"""
import argparse
import json
import os
import sys
import time
import urllib.parse

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from import_rooms import CDX, DEFAULT_DATA, fetch  # noqa: E402  (shared retrying fetch)

TARGET = {"cdn.rec.net": "cdn", "img.rec.net": "img"}


def captures(prefix, limit):
    """{path: (timestamp, original url)} for every archived 200 under the prefix (CDX paged with resumeKey)."""
    found, resume = {}, None
    while True:
        params = {"url": prefix + "*", "output": "json", "fl": "original,timestamp", "filter": "statuscode:200",
                  "limit": "5000", "showResumeKey": "true"}
        if resume:
            params["resumeKey"] = resume
        raw = fetch(f"{CDX}?{urllib.parse.urlencode(params)}")
        if raw is None:
            print("CDX query failed; partial list", file=sys.stderr)
            break
        rows = json.loads(raw or b"[]")
        resume = None
        if rows and rows[-1] and len(rows) >= 2 and rows[-2] == []:  # [..., [], [resumeKey]]
            resume = rows[-1][0]
            rows = rows[:-2]
        for original, ts in rows[1:]:
            path = urllib.parse.urlsplit(original).path
            if path.endswith("/") or not path:
                continue
            if path not in found or ts > found[path][0]:
                found[path] = (ts, original)
        if not resume or len(found) >= limit:
            break
    return dict(sorted(found.items())[:limit])


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("prefix", help="host plus path prefix, e.g. cdn.rec.net/room/")
    ap.add_argument("--data", default=os.environ.get("REC_DATA", DEFAULT_DATA), help="RecServer data directory")
    ap.add_argument("--list", action="store_true", help="only list archived files")
    ap.add_argument("--limit", type=int, default=100000, help="stop after this many files")
    a = ap.parse_args()

    prefix = a.prefix.split("://", 1)[-1]
    host = prefix.split("/", 1)[0].lower()
    if host not in TARGET:
        sys.exit(f"unknown host {host}; known: {', '.join(TARGET)}")
    root = os.path.join(os.path.abspath(a.data), TARGET[host])

    files = captures(prefix, a.limit)
    print(f"{len(files)} archived file(s) under {prefix}")
    got = skipped = missing = 0
    for path, (ts, original) in files.items():
        dest = os.path.normpath(os.path.join(root, urllib.parse.unquote(path).lstrip("/")))
        if not dest.startswith(root + os.sep):
            continue  # never write outside the data directory
        if a.list:
            print(f"  {ts} {path}")
            continue
        if os.path.exists(dest):
            skipped += 1
            continue
        body = fetch(f"https://web.archive.org/web/{ts}id_/{original}")
        if body is None:
            missing += 1
            continue
        os.makedirs(os.path.dirname(dest), exist_ok=True)
        with open(dest, "wb") as f:
            f.write(body)
        got += 1
        print(f"  {path} ({len(body)} bytes)")
        time.sleep(0.5)  # be gentle with the archive
    if not a.list:
        print(f"downloaded {got}, already present {skipped}, failed {missing} -> {root}")


if __name__ == "__main__":
    main()
