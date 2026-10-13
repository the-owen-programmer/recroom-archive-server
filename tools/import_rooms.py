#!/usr/bin/env python3
"""Import archived Rec Room rooms into RecServer's catalog (data/rooms/<RoomId>.json), plus their thumbnails (data/img/).

Sources:
  wayback   rooms.rec.net captures in the Internet Archive (many rooms were crawled May-June 2026, just before shutdown)
            import_rooms.py wayback --id 1 --id 10000239 --name RecCenter [--list ids.txt] [--images]
  files     JSON saved from rooms.rec.net / apim.rec.net (one room, a list, or {"Results": [...]})
            import_rooms.py files dump1.json dump2.json [--images]

Archived room JSON has no SubRooms (the public API leaves them out), so the server invents one "Home" sub-room without a
scene. To make a room enterable, add "SubRooms": [{"Name": "Home", "UnitySceneId": "<scene guid>", "DataBlob": "<blob>"}]
by hand; an existing file's SubRooms are kept when a room is re-imported. See docs/worlds.md.

Stdlib only. Local, offline archival use.
"""
import argparse
import gzip
import json
import os
import sys
import time
import urllib.error
import urllib.parse
import urllib.request

HERE = os.path.dirname(os.path.abspath(__file__))
DEFAULT_DATA = os.path.join(HERE, "..", "data")
UA = "curl/8.5.0"  # the archive blocks some default library user agents
CDX = "https://web.archive.org/cdx/search/cdx"


def fetch(url, tries=5):
    """GET with exponential backoff; the Wayback Machine drops connections and times out a lot."""
    delay = 2
    for attempt in range(1, tries + 1):
        try:
            req = urllib.request.Request(url, headers={"User-Agent": UA})
            with urllib.request.urlopen(req, timeout=60) as r:
                body = r.read()
            # some captures are stored still gzip-encoded
            return gzip.decompress(body) if body[:2] == b"\x1f\x8b" else body
        except urllib.error.HTTPError as e:
            if e.code == 404:
                return None
            err = f"HTTP {e.code}"
        except (urllib.error.URLError, TimeoutError, ConnectionError, OSError) as e:
            err = str(e)
        if attempt == tries:
            print(f"  giving up on {url}: {err}", file=sys.stderr)
            return None
        time.sleep(delay)
        delay *= 2


def latest_capture(url):
    """Timestamp of the newest 200 capture of exactly this URL, or None."""
    q = urllib.parse.urlencode({"url": url, "output": "json", "fl": "timestamp,statuscode", "filter": "statuscode:200"})
    raw = fetch(f"{CDX}?{q}")
    if not raw:
        return None
    rows = json.loads(raw)[1:]
    return rows[-1][0] if rows else None


def wayback_get(url):
    ts = latest_capture(url)
    if not ts:
        return None, None
    # "id_" returns the original bytes without the archive's toolbar or rewriting
    return fetch(f"https://web.archive.org/web/{ts}id_/{url}"), ts


def rooms_in(doc):
    if isinstance(doc, list):
        return doc
    if isinstance(doc, dict) and isinstance(doc.get("Results"), list):
        return doc["Results"]
    return [doc] if isinstance(doc, dict) else []


def save_room(room, data_dir, source):
    if not isinstance(room, dict) or not room.get("RoomId") or not room.get("Name"):
        print(f"  {source}: not a room, skipped")
        return None
    path = os.path.join(data_dir, "rooms", f"{room['RoomId']}.json")
    if os.path.exists(path):
        try:
            with open(path, encoding="utf-8") as f:
                old = json.load(f)
            if old.get("SubRooms") and not room.get("SubRooms"):
                room["SubRooms"] = old["SubRooms"]  # hand-added scene/blob info survives a re-import
        except (OSError, json.JSONDecodeError):
            pass
    room.setdefault("_archive", {"source": source})
    with open(path, "w", encoding="utf-8") as f:
        json.dump(room, f, indent=2, ensure_ascii=False)
    print(f"  saved {room['Name']} ({room['RoomId']})")
    return room


def save_image(name, data_dir):
    if not name or name.startswith("Default"):
        return
    path = os.path.join(data_dir, "img", os.path.basename(name))
    if os.path.exists(path):
        return
    raw, _ = wayback_get(f"https://img.rec.net/{name}")
    if raw:
        with open(path, "wb") as f:
            f.write(raw)
        print(f"    image {name} ({len(raw)} bytes)")
    else:
        print(f"    image {name}: not archived")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--data", default=os.environ.get("REC_DATA", DEFAULT_DATA), help="RecServer data directory")
    ap.add_argument("--images", action="store_true", help="also fetch each room's thumbnail from the archive")
    sub = ap.add_subparsers(dest="cmd", required=True)
    wb = sub.add_parser("wayback", help="fetch rooms from Internet Archive captures of rooms.rec.net")
    wb.add_argument("--id", action="append", default=[], help="room id (repeatable)")
    wb.add_argument("--name", action="append", default=[], help="room name (repeatable)")
    wb.add_argument("--list", help="file with one room id or name per line")
    fl = sub.add_parser("files", help="import saved JSON files")
    fl.add_argument("paths", nargs="+")
    a = ap.parse_args()

    data_dir = os.path.abspath(a.data)
    for d in ("rooms", "img"):
        os.makedirs(os.path.join(data_dir, d), exist_ok=True)

    saved = []
    if a.cmd == "files":
        for p in a.paths:
            with open(p, encoding="utf-8") as f:
                doc = json.load(f)
            for room in rooms_in(doc):
                saved.append(save_room(room, data_dir, os.path.basename(p)))
    else:
        keys = [("id", i) for i in a.id] + [("name", n) for n in a.name]
        if a.list:
            with open(a.list, encoding="utf-8") as f:
                for line in f:
                    line = line.strip()
                    if line and not line.startswith("#"):
                        keys.append(("id" if line.isdigit() else "name", line))
        for kind, key in keys:
            url = f"https://rooms.rec.net/rooms/{key}" if kind == "id" else f"https://rooms.rec.net/rooms?name={key}"
            print(f"{kind} {key}")
            raw, ts = wayback_get(url)
            if raw is None and kind == "name":  # the archive keeps the URL as it was crawled; names are often lower case
                raw, ts = wayback_get(f"https://rooms.rec.net/rooms?name={key.lower()}")
            if raw is None:
                print("  not archived")
                continue
            try:
                doc = json.loads(raw)
            except (json.JSONDecodeError, UnicodeDecodeError):
                print("  capture is not JSON, skipped")
                continue
            for room in rooms_in(doc):
                saved.append(save_room(room, data_dir, f"wayback {ts} {url}"))
            time.sleep(1)  # be gentle with the archive

    if a.images:
        for room in filter(None, saved):
            save_image(room.get("ImageName"), data_dir)
    n = len([r for r in saved if r])
    print(f"{n} room(s) imported into {os.path.join(data_dir, 'rooms')}; restart RecServer or POST /_debug/rooms/reload")


if __name__ == "__main__":
    main()
