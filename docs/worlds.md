# World layer: rooms, matchmaking, archived content

The part of RecServer that comes after login: finding rooms, entering one, and serving its content. The startup chain
(name server, gameconfigs, versioncheck, auth, accounts, settings) is separate work; see `README.md`.
Scope: local, offline, single machine.

**Status: built and smoke-tested with curl, but the client has not reached it yet.** Every path and response shape below is
from the public RecNet API and Internet Archive captures (2023-2026). None has been checked against the 2024-08-20 client's own
classes. When the client gets here, read `logs/requests.jsonl` (lines marked `MISS` are unanswered) and fix shapes, either in
code or with an override file.

## Data directory (`data/`, or `REC_DATA`)
| Path | What | Who writes it |
|---|---|---|
| `rooms/*.json` | archived rooms: one room, a list, or `{"Results":[...]}` in rooms.rec.net shape | `tools/import_rooms.py`, by hand |
| `img/<name>` | images for img.rec.net (thumbnails, profile pictures) | `tools/import_rooms.py --images`, `tools/wayback_mirror.py img.rec.net/` |
| `cdn/<path>` | files for cdn.rec.net at the same path (`cdn/room/<blob>.room`, `cdn/config/...`) | `tools/wayback_mirror.py cdn.rec.net/...` |
| `overrides/<host>/<path>[.<METHOD>].json` | canned response served as-is before any endpoint | by hand |
| `state/` | the local player's cheers, favorites and visited rooms | the server |

`data/` is git-ignored (it is archive content and personal state). Delete `state/` to reset.

**Overrides** are the quickest way to correct a shape: e.g. `data/overrides/rooms.rec.net/rooms/hot.json` replaces
`GET https://rooms.rec.net/rooms/hot`, and `.../rooms/hot.POST.json` replaces only POST. They are re-read on every request.

## Room catalog
`RecServer/Seed/rooms.json` (built-in) plus `data/rooms/*.json`. A later file wins on the same `RoomId`. Unknown fields in
archived JSON are kept and sent back unchanged. Keys that start with `_` (import notes) are stripped before sending.
`POST /_debug/rooms/reload` re-reads the catalog without a restart.

To be **enterable**, a room needs a sub-room with a `UnitySceneId` (the Unity scene GUID in the game build) and, for
player-built rooms, a `DataBlob` (name of the saved room on cdn.rec.net):
```json
"SubRooms": [ { "Name": "Home", "UnitySceneId": "<scene guid>", "DataBlob": "room/<blob>.room" } ]
```
Archived room JSON never has SubRooms (the public API left them out). An imported room without them inherits the seed's
sub-rooms for the same id, and a re-import keeps hand-added ones. Otherwise the server makes an empty "Home" sub-room.

Seed entries (ids chosen to match real rooms where known):
- `RecCenter`, id **170126** (real id from the archive), scene `cbad71af-0831-44d8-b8ef-69edafa841f6`
- `DormRoom` (the local dorm), id 9000000000000000001 (outside the real id range), scene `76d98498-60a1-430c-ab76-b54a29b7a163`

**Both scene GUIDs are UNVERIFIED.** They come from 2018-2020 community servers. The 2024 build's scene GUIDs must be read from
the build (scene list / the client's RRO room table) and replaced here.

## Endpoints
**rooms.rec.net**
- `GET /rooms/{id}`, `GET /rooms?name=` (case-insensitive)
- `GET|POST /rooms/bulk` with `id=`/`name=` repeated
- `GET /rooms/hot`, `GET /rooms/search?query=`: paged `{Results, TotalResults}`, honouring `ExcludeFromLists`/`ExcludeFromSearch`
- `GET /rooms/featured/current`, `GET /rooms/base` (RRO rooms), `GET /rooms/dorm`
- `GET /rooms/createdby/{accountId}`, `/rooms/createdby/me`, `/rooms/ownedby/me`
- `GET /rooms/favoritedby/me`, `/rooms/visitedby/me`
- `GET /rooms/{id}/interactionby/me`; `POST|DELETE /rooms/{id}/cheer` and `/favorite` (persisted)
- `GET /rooms/{id}/subrooms`

**match.rec.net** (login and logout belong to the startup chain)
- `POST /goto/room/{name}` (also `Name.SubRoom`), `/goto/roomid/{id}`, `/goto/dorm`, `/goto/player/{id}`, `/goto/roominstance/{id}`, `/goto/none`
  → `{errorCode, roomInstance}`, where `errorCode` 1 means no such room. Each goto creates a private instance:
  `location` is the sub-room's scene GUID, `dataBlob` is its blob, and `photonRoomId` is `<Room>-<n>-offline`.
- `POST /player/heartbeat`, `GET /player?id=`, `GET /player/{id}`, `GET /roominstance/{id}`

**img.rec.net** `GET /{name}`: serves `data/img/<name>` (also tries `.jpg`/`.png`), otherwise a 1x1 PNG placeholder. It never 404s,
so the client's UI doesn't break. Query parameters such as `width` and `cropSquare` are ignored.

**cdn.rec.net** `GET /{path}`: serves `data/cdn/<path>` byte for byte (range requests supported). Extension-less JSON files go
out as `application/json`. A missing file is a 404, so the gap shows up in the log.

## What the Internet Archive has (checked 2026-10-13)
- `rooms.rec.net/rooms/{id}`: a broad crawl from May-June 2026, just before shutdown, with full room metadata (no sub-rooms).
  `tools/import_rooms.py wayback --id <id> --name <name> [--list file] --images`
- `cdn.rec.net/room/*.room`: **13** saved room blobs (2020-2025). Which room each belongs to is unknown, because the mapping
  lives in the sub-room `DataBlob` field, which the public API never showed.
- `cdn.rec.net/config/`: LoadingScreenTipData, AvatarEffectConfig_v1-3, RRPlusConfig_v1-3, SkuConfig_v1, Test_SkuConfig.
  Also `cdn.rec.net/avatar/*.assetbundle` files.
- `img.rec.net/*`: room thumbnails and other images, fetched per name by `import_rooms.py --images`.

`tools/wayback_mirror.py <host/prefix> [--list]` mirrors any of these into `data/`. It is resumable and keeps the newest capture
per path.

## Open questions (need the client or its dump)
1. The 2024 client's room, sub-room and room-instance class field names (casing included), and the exact rooms/match paths
   and `include` flags it requests.
2. Scene GUIDs of the RRO rooms and dorm in the 2024 build.
3. How a `.room` blob is fetched: the full cdn path, and whether it is compressed or versioned (`UgcVersion`, `PersistenceVersion`).
4. **Photon.** Entering an instance means joining a Photon room. Offline, that needs either a local Photon-compatible server or
   a client patch for Photon offline mode. This is the next big blocker after matchmaking.
