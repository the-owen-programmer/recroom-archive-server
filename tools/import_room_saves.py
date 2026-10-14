#!/usr/bin/env python3
"""Import archived Rec Room room SAVES (the built contents of player rooms) into RecServer: the .room blobs go to
data/cdn/room/, and a catalog entry with the right scene and save goes to data/rooms/ so the room can be entered.

A .room save is protobuf; at top level, field 4 is the Unity scene GUID it was built in (Maker Room, The Lounge, ...) and
field 6 the room's RoomId (field 1 is the save format version). So any save can be turned into an enterable room, even
without other metadata; the RoomId links it to the archived room metadata (tools/import_rooms.py wayback --id).

Sources:
  ripped  A "ripped rooms" collection laid out <creator>/<Room>/Subroom_<Name>/Saves/<time>/<SubRoomDataSaveId>.json + blob
          (e.g. archive.org item afew-good-tacos_rooms_collection), straight from an archive.org RAR/ZIP or a local folder:
            import_room_saves.py ripped --item afew-good-tacos_rooms_collection --file AFewGoodTacos.rar
            import_room_saves.py ripped --dir /path/to/extracted/AFewGoodTacos
          One catalog room per room folder, one sub-room per Subroom_ folder, each using its newest save.
  dump    One of the anonymous "Rec Room CDN room files" zips on archive.org (recroom-cdn-room-files-<n>, ~8,800 saves
          each, filenames only). Reads just the start of each save to get scene + RoomId:
            import_room_saves.py dump --item recroom-cdn-room-files-1788915092376000 --limit 50 [--list]
          --list only prints blob, size, scene, RoomId (a CSV index without downloading the saves).

Stdlib only. Local, offline archival use; credit the original creators and archivists listed in each item.
"""
import argparse
import html
import json
import os
import re
import sys
import time
import urllib.parse

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from import_rooms import DEFAULT_DATA, UA, fetch  # noqa: E402
import rzip  # noqa: E402

GUID = re.compile(rb"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}")
SYNTHETIC_BASE = 9_100_000_000_000_000_000   # rooms whose RoomId is unknown (seed uses 9.0e18+)


def save_header(data):
    """(scene_guid, room_id, version) from a .room save's top-level protobuf fields 4, 6 and 1; None where absent."""
    def varint(i):
        v = s = 0
        while True:
            if i >= len(data):
                raise IndexError
            x = data[i]; i += 1; v |= (x & 0x7F) << s; s += 7
            if x < 0x80:
                return v, i
    scene = room_id = version = None
    i = 0
    try:
        while i < len(data) and (scene is None or room_id is None):
            key, i = varint(i)
            field, wire = key >> 3, key & 7
            if wire == 0:
                v, i = varint(i)
                if field == 1: version = v
                elif field == 6: room_id = v
            elif wire == 2:
                n, i = varint(i)
                if field == 4 and n == 36 and GUID.fullmatch(data[i:i + n]):
                    scene = data[i:i + n].decode()
                i += n
            elif wire == 5: i += 4
            elif wire == 1: i += 8
            else: break
    except IndexError:
        pass   # a prefix ended inside a field: keep what was found
    return scene, room_id, version


def resolve(url):
    """archive.org download URLs redirect to a storage node; range requests must go there."""
    import urllib.request

    class NoRedirect(urllib.request.HTTPRedirectHandler):
        def redirect_request(self, *a, **k):
            return None
    try:
        urllib.request.build_opener(NoRedirect).open(urllib.request.Request(url, headers={"User-Agent": UA}, method="HEAD"), timeout=60)
        return url
    except urllib.error.HTTPError as e:
        return e.headers.get("Location", url)


# ---------------------------------------------------------------------------------------------------------- ripped layout

class RemoteArchive:
    """Files inside an archive.org RAR/ZIP, listed and fetched one by one through archive.org's archive viewer."""
    def __init__(self, item, file):
        self.base = f"https://archive.org/download/{item}/{urllib.parse.quote(file)}"
        page = fetch(self.base + "/")
        if not page:
            sys.exit(f"cannot list {self.base}/")
        links = re.findall(r'<a href="//archive\.org/download/[^"]*?/' + re.escape(urllib.parse.quote(file)) + r'/([^"]+)">', page.decode("utf-8", "replace"))
        self.files = [urllib.parse.unquote(html.unescape(x)) for x in links]

    def read(self, path):
        return fetch(self.base + "/" + urllib.parse.quote(path, safe=""))


class LocalArchive:
    def __init__(self, root):
        self.root = os.path.abspath(root)
        parent = os.path.dirname(self.root)
        self.files = [os.path.relpath(os.path.join(d, f), parent).replace(os.sep, "/")
                      for d, _, fs in os.walk(self.root) for f in fs]
        self._parent = parent

    def read(self, path):
        with open(os.path.join(self._parent, path), "rb") as f:
            return f.read()


def import_ripped(src, data_dir, source_note):
    # group: room folder -> subroom -> save folder -> [files]
    rooms = {}
    images = {}
    for path in src.files:
        p = path.split("/")
        if len(p) == 3 and p[2].lower().endswith((".jpg", ".png")):
            images.setdefault(p[1], p[2])
        if len(p) >= 6 and p[2].startswith("Subroom_") and p[3] == "Saves":
            rooms.setdefault(p[1], {}).setdefault(p[2], {}).setdefault(p[4], []).append(path)
    print(f"{len(rooms)} rooms in archive")
    n_rooms = 0
    for idx, (room_name, subrooms) in enumerate(sorted(rooms.items())):
        print(f"room {room_name}")
        subs, room_id, creator = [], None, None
        for sub_dir, saves in sorted(subrooms.items()):
            # newest save that has both its JSON record and a blob
            for save_time in sorted(saves, reverse=True):
                files = saves[save_time]
                meta_path = next((f for f in files if f.endswith(".json")), None)
                if not meta_path:
                    continue
                meta = json.loads(src.read(meta_path) or b"{}")
                blob_name = meta.get("DataBlob")
                blob_path = next((f for f in files if blob_name and f.endswith("/" + blob_name)), None)
                if not blob_path:
                    continue
                blob = src.read(blob_path)
                if not blob:
                    continue
                scene, rid, version = save_header(blob)
                if not scene:
                    print(f"  {sub_dir}: newest save {blob_name} has no scene id (old format?); trying an older one")
                    continue
                os.makedirs(os.path.join(data_dir, "cdn", "room"), exist_ok=True)
                with open(os.path.join(data_dir, "cdn", "room", blob_name), "wb") as f:
                    f.write(blob)
                room_id = room_id or rid
                creator = creator or meta.get("SavedByAccountId")
                subs.append({"Name": sub_dir[len("Subroom_"):], "UnitySceneId": scene, "DataBlob": blob_name,
                             "DataSavedAt": meta.get("CreatedAt"),
                             "_save": {"SubRoomId": meta.get("SubRoomId"), "SubRoomDataSaveId": meta.get("SubRoomDataSaveId"),
                                       "version": version, "saves_in_archive": len(saves)}})
                print(f"  {sub_dir}: save {save_time} ({len(blob)} bytes, scene {scene}, format {version})")
                break
        if not subs:
            print("  no usable save; skipped")
            continue
        # "Home" first: the client enters the first sub-room by default
        subs.sort(key=lambda s: (s["Name"] != "Home", s["Name"]))
        image = images.get(room_name)
        if image:
            raw = src.read(f"{src.files[0].split('/')[0]}/{room_name}/{image}")
            if raw:
                os.makedirs(os.path.join(data_dir, "img"), exist_ok=True)
                with open(os.path.join(data_dir, "img", image), "wb") as f:
                    f.write(raw)
        room = {
            "RoomId": room_id or SYNTHETIC_BASE + abs(hash(room_name)) % 10**12,
            "Name": room_name,
            "Description": f"Archived room ({source_note}).",
            "ImageName": image or "DefaultRoomImage.jpg",
            "CreatorAccountId": creator or 0,
            "SubRooms": subs,
            "_archive": {"source": source_note, "room_id_from": "save" if room_id else "synthetic"},
        }
        path = os.path.join(data_dir, "rooms", f"{room['RoomId']}.json")
        if os.path.exists(path):   # keep archived metadata (from import_rooms.py) and only add the sub-rooms
            try:
                with open(path, encoding="utf-8") as f:
                    old = json.load(f)
                old["SubRooms"] = subs
                old.setdefault("_archive", {})["saves"] = source_note
                room = old
            except (OSError, json.JSONDecodeError):
                pass
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "w", encoding="utf-8") as f:
            json.dump(room, f, indent=2, ensure_ascii=False)
        n_rooms += 1
        print(f"  -> {room['Name']} ({room['RoomId']}), {len(subs)} sub-room(s)")
    return n_rooms


# ------------------------------------------------------------------------------------------------------- anonymous dumps

def import_dump(item, data_dir, limit, list_only, min_size):
    zips = [f for f in json.loads(fetch(f"https://archive.org/metadata/{item}") or b"{}").get("files", []) if f["name"].endswith(".zip")]
    if not zips:
        sys.exit(f"{item}: no zip")
    url = resolve(f"https://archive.org/download/{item}/{urllib.parse.quote(zips[0]['name'])}")
    entries = [e for e in rzip.entries(url) if e[0].endswith(".room") and e[3] >= min_size]
    print(f"{len(entries)} saves in {item}", file=sys.stderr)
    done = 0
    if list_only:
        print("blob,size,scene,room_id,version")
    for e in entries:
        if done >= limit:
            break
        head = rzip.read_prefix(url, e, 65536)
        scene, rid, version = save_header(head)
        if scene is None and len(head) < e[3]:
            # in big saves fields 4/6 come after the object data: read the whole entry (still one range request)
            head = rzip.read_prefix(url, e, e[2])
            scene, rid, version = save_header(head)
        if list_only:
            print(f"{e[0]},{e[3]},{scene or ''},{rid or ''},{version or ''}")
            done += 1
            continue
        if not scene:
            continue
        out = os.path.join(data_dir, "cdn", "room", e[0])
        os.makedirs(os.path.dirname(out), exist_ok=True)
        if not os.path.exists(out):
            rzip.get(url, e, out)
        room_id = rid or SYNTHETIC_BASE + int(e[0][:12], 36) % 10**12
        path = os.path.join(data_dir, "rooms", f"{room_id}.json")
        sub = {"Name": "Home", "UnitySceneId": scene, "DataBlob": e[0], "_save": {"version": version, "from": item}}
        if os.path.exists(path):
            with open(path, encoding="utf-8") as f:
                room = json.load(f)
            if room.get("SubRooms"):
                done += 1
                continue   # already has sub-rooms (e.g. from a ripped collection); don't overwrite
            room["SubRooms"] = [sub]
        else:
            room = {"RoomId": room_id, "Name": f"Archived{room_id}", "Description": "Archived room save (name unknown "
                    "until its metadata is imported: tools/import_rooms.py wayback --id <RoomId>).",
                    "ImageName": "DefaultRoomImage.jpg", "CreatorAccountId": 0, "SubRooms": [sub],
                    "_archive": {"source": item}}
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "w", encoding="utf-8") as f:
            json.dump(room, f, indent=2)
        done += 1
        print(f"{e[0]} -> room {room_id} scene {scene}", file=sys.stderr)
        time.sleep(0.2)
    return done


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--data", default=os.environ.get("REC_DATA", DEFAULT_DATA), help="RecServer data directory")
    sub = ap.add_subparsers(dest="cmd", required=True)
    r = sub.add_parser("ripped")
    r.add_argument("--item", help="archive.org item id")
    r.add_argument("--file", help="RAR/ZIP file in the item")
    r.add_argument("--dir", help="local extracted collection folder (the <creator> folder)")
    d = sub.add_parser("dump")
    d.add_argument("--item", required=True)
    d.add_argument("--limit", type=int, default=25)
    d.add_argument("--min-size", type=int, default=0, help="skip saves smaller than this many bytes")
    d.add_argument("--list", action="store_true", help="print a CSV index (blob, size, scene, RoomId) instead of importing")
    a = ap.parse_args()
    data_dir = os.path.abspath(a.data)
    if a.cmd == "ripped":
        if a.dir:
            n = import_ripped(LocalArchive(a.dir), data_dir, f"local {os.path.basename(a.dir)}")
        elif a.item and a.file:
            n = import_ripped(RemoteArchive(a.item, a.file), data_dir, f"archive.org {a.item}")
        else:
            sys.exit("ripped needs --dir, or --item and --file")
        print(f"{n} room(s) imported; POST /_debug/rooms/reload or restart RecServer")
    else:
        n = import_dump(a.item, data_dir, a.limit, a.list, a.min_size)
        print(f"{n} save(s) {'listed' if a.list else 'imported'}", file=sys.stderr)


if __name__ == "__main__":
    main()
