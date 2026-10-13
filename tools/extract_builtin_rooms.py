#!/usr/bin/env python3
"""Extract the built-in (Rec Room Original) room table from a game build and write it as a RecServer room seed.

The build's resources.assets holds a room build config (class AGRoomBuildConfig in the 2024-08-20 build) with:
  1. a scene list: display name -> scene GUID ("Rec Center" -> cbad71af-...)
  2. the rooms: room name + room GUID, each with sub-rooms: sub-room name + sub-room GUID -> scene GUID
The scene GUID is what a sub-room's UnitySceneId must be for the client to load that scene.

  extract_builtin_rooms.py resources.assets --json table.json                 # raw table
  extract_builtin_rooms.py resources.assets --seed RecServer/Seed/rooms.json \\
        --ids-from data/rooms                                                  # seed, with real RoomIds where known

Get resources.assets out of an archived build with tools/rzip.py. Real RoomIds come from rooms imported with
tools/import_rooms.py (matched by room name); rooms without a known id get one from a reserved range far above real ids.
Stdlib only; parsing is by string structure (length-prefixed strings), not a full Unity reader.
"""
import argparse
import json
import os
import re
import struct
import sys

GUID = re.compile(rb"\x24\x00\x00\x00([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})")
GUID_TEXT = re.compile(r"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$")
SYNTHETIC_BASE = 9_000_000_000_000_000_000  # outside the range real RecNet room ids used


def string_before(d, i):
    """The printable length-prefixed string that ends just before offset i (Unity aligns strings to 4 bytes)."""
    for back in range(4, 200):
        j = i - back
        if j < 4:
            return None
        n = struct.unpack("<i", d[j - 4:j])[0]
        if 0 < n < 128 and j + n <= i and i - (j + n) < 4:
            s = d[j:j + n]
            if all(32 <= c < 127 for c in s):
                return s.decode()
    return None


def find_table(d):
    """(name, guid) pairs of the first room table: anchored on the first 'subGuid -> sceneGuid' pair, then extended both ways
    while the entries stay close together."""
    rows = []
    for m in GUID.finditer(d):
        rows.append((m.start(), string_before(d, m.start()), m.group(1).decode()))
    anchor = next((k for k in range(len(rows) - 1) if rows[k + 1][1] == rows[k][2]), None)
    if anchor is None:
        sys.exit("no room table found (is this the right resources.assets?)")
    lo = hi = anchor
    while lo > 0 and rows[lo][0] - rows[lo - 1][0] < 400:
        lo -= 1
    while hi + 1 < len(rows) and rows[hi + 1][0] - rows[hi][0] < 400:
        hi += 1
    return rows[lo:hi + 1]


def parse(rows):
    first_sub = next(k for k in range(len(rows) - 1) if rows[k + 1][1] == rows[k][2])
    scenes = [{"Name": n, "SceneGuid": g} for _, n, g in rows[:first_sub - 1] if n]
    rooms, k = [], first_sub - 1
    while k < len(rows):
        _, name, guid = rows[k]
        if k + 1 < len(rows) and rows[k + 1][1] == guid:  # sub-room: (name, subGuid) then (subGuid, sceneGuid)
            if rooms:
                rooms[-1]["SubRooms"].append({"Name": name, "SubRoomGuid": guid, "SceneGuid": rows[k + 1][2]})
            k += 2
            continue
        if not name or GUID_TEXT.match(name):
            k += 1
            continue
        if any(r["Name"] == name and r["SubRooms"] for r in rooms):
            break  # the table is stored twice; stop at the start of the copy
        rooms.append({"Name": name, "RoomGuid": guid, "SubRooms": []})
        k += 1
    return scenes, [r for r in rooms if r["SubRooms"]]


def ids_from(directory):
    ids = {}
    for root, _, files in os.walk(directory):
        for f in files:
            if f.endswith(".json"):
                try:
                    with open(os.path.join(root, f), encoding="utf-8") as fh:
                        doc = json.load(fh)
                except (OSError, json.JSONDecodeError):
                    continue
                for r in doc if isinstance(doc, list) else [doc]:
                    if isinstance(r, dict) and r.get("RoomId") and r.get("Name"):
                        ids[r["Name"].lower()] = r["RoomId"]
    return ids


def seed(rooms, scenes, ids):
    scene_names = {s["SceneGuid"]: s["Name"] for s in scenes}
    out = []
    for n, r in enumerate(rooms):
        room_id = ids.get(r["Name"].lower(), SYNTHETIC_BASE + 100 + n)
        # no SubRoomId: the server derives a stable one from the RoomId
        subs = [{"Name": s["Name"], "UnitySceneId": s["SceneGuid"], "_SubRoomGuid": s["SubRoomGuid"]} for s in r["SubRooms"]]
        out.append({
            "RoomId": room_id,
            "Name": r["Name"],
            "Description": scene_names.get(r["SubRooms"][0]["SceneGuid"], r["Name"]),
            "ImageName": "DefaultRoomImage.jpg",
            "IsRRO": True,
            "IsDorm": r["Name"] == "DormRoom",
            "Accessibility": 0 if r["Name"] in ("DormRoom", "Calibration", "CustomDormRoomTemplate") else 1,
            "MaxPlayers": 4 if r["Name"] == "DormRoom" else 20,
            "SubRooms": subs,
            "Tags": [{"Tag": "rro", "Type": 2}],
            "_RoomGuid": r["RoomGuid"],
            "_IdSource": "archive" if room_id < SYNTHETIC_BASE else "synthetic",
        })
    return out


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("assets", help="resources.assets of the build")
    ap.add_argument("--json", help="write the raw table (scenes + rooms) here")
    ap.add_argument("--seed", help="write a RecServer room seed here")
    ap.add_argument("--ids-from", help="directory of imported room JSON to take real RoomIds from (by name)")
    a = ap.parse_args()

    with open(a.assets, "rb") as f:
        d = f.read()
    scenes, rooms = parse(find_table(d))
    print(f"{len(scenes)} scenes, {len(rooms)} rooms, {sum(len(r['SubRooms']) for r in rooms)} sub-rooms")
    if a.json:
        with open(a.json, "w", encoding="utf-8") as f:
            json.dump({"Scenes": scenes, "Rooms": rooms}, f, indent=2)
    if a.seed:
        ids = ids_from(a.ids_from) if a.ids_from else {}
        s = seed(rooms, scenes, ids)
        with open(a.seed, "w", encoding="utf-8") as f:
            json.dump(s, f, indent=2)
        print(f"seed: {len(s)} rooms, {sum(r['_IdSource'] == 'archive' for r in s)} with real RoomIds -> {a.seed}")


if __name__ == "__main__":
    main()
