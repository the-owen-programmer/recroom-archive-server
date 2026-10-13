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

### Built-in rooms (seed)
`Seed/rooms.json` holds **all 56 built-in (RRO) rooms of the 2024-08-20 build** and their 68 sub-rooms, with **verified scene
GUIDs**. They were extracted from the build's own room build config (`AGRoomBuildConfig` in `RecRoom_Data/resources.assets`)
by `tools/extract_builtin_rooms.py`:
```
python3 tools/rzip.py https://archive.recagain.site/download/manifest/1664414749991987435 get RecRoom_Data/resources.assets /tmp/resources.assets
python3 tools/extract_builtin_rooms.py /tmp/resources.assets --seed RecServer/Seed/rooms.json --ids-from data/rooms
```
The config lists 42 scenes (display name → scene GUID), then each room (name + room GUID) with sub-rooms
(name + sub-room GUID → scene GUID). Examples: DormRoom → `76d98498-…`, RecCenter → `cbad71af-…`, Park → `0a864c86-…`,
Paintball with River/Homestead/Quarry/Clearcut/Spillway/Drive-in, LaserTag with Hangar/CyberJunkCity, StuntRunner with
StuntRunner/TheMainEvent. The room and sub-room GUIDs are kept as `_RoomGuid`/`_SubRoomGuid` notes, which are stripped before
anything is sent to the client.

RoomIds: RecCenter has its real id (170126, from the archive). The others use a reserved range from 9000000000000000100 up,
far above real ids, until their real ids are imported. `tools/import_rooms.py wayback --name Park` brings in the archived
room, which takes over the seed's sub-rooms by name (and its real id, stats and thumbnail).
`DormRoom` is the local dorm (`IsDorm`); `/goto/dorm` sends the player there.

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
- `img.rec.net/*`: **100,000+** archived images (the listing stopped at the tool's default limit). `import_rooms.py --images`
  fetches the ones a room needs, by name.
- All of `cdn.rec.net`: 1,618 archived files. Most are website content (1,136 `wwwcontent`, 198 `video`, 194 `static`),
  plus 41 `img`, the 13 `room` blobs and the 9 `config` files. `config/LoadingScreenTipData` is real loading-screen tip data.

`tools/wayback_mirror.py <host/prefix> [--list]` mirrors any of these into `data/`. It is resumable and keeps the newest capture
per path.

## Open questions (need the client or its dump)
1. The 2024 client's room, sub-room and room-instance class field names (casing included), and the exact rooms/match paths
   and `include` flags it requests.
2. ~~Scene GUIDs of the built-in rooms~~: done, see above. Still open: whether the client wants the room/sub-room GUIDs anywhere.
3. How a `.room` blob is fetched: the full cdn path, and whether it is compressed or versioned (`UgcVersion`, `PersistenceVersion`).
4. ~~Photon~~: a single-player stand-in now runs inside RecServer, see below.

## Photon stand-in (single player)
Entering a room instance means joining a Photon room (the client uses PUN 2 on Photon Realtime). RecServer runs a minimal
**single-player** Photon server in-process (`RecServer/Photon/`), on by default (`REC_PHOTON=0` turns it off):

| UDP port | Role | What it does |
|---|---|---|
| 5058 (+27000) | name server | key exchange, region list (`usw,us,eu`, `REC_PHOTON_REGIONS`), authenticate → master address |
| 5055 (+27001) | master | authenticate, lobby, create/join/join-random → game server address; also echoes region pings |
| 5056 (+27002) | game | create/join → local player is **actor 1 and master client**; RaiseEvent comes back when addressed to all, the master client or actor 1 (nothing is sent for "others"); Set/GetProperties stored and echoed as PropertiesChanged; Leave |

Anything else gets an empty OK and a log line. The log is `logs/photon.log`, one line per operation with its parameters.
Addresses handed out are `REC_PHOTON_HOST` (default `127.0.0.1`) plus the port. The name server host the client dials
(`ns.exitgames.com`) resolves to 127.0.0.1 through the netns wildcard DNS.

What it implements of the wire protocol: Photon reliable UDP (connect/verify, per-channel ordered reliable delivery, acks,
fragments both ways, resends, ping and server-time commands), GpBinaryV18 serialization, and the payload-encryption key exchange
(DH Oakley-768, AES-256-CBC) that the client runs before authenticating. Custom types and other values the server doesn't need
are kept as raw bytes and sent back unchanged. Not implemented: GpBinaryV16, datagram encryption, TCP/WebSocket transports,
Photon Voice and Chat (the game may connect to those too; their requests will show in the log).

Verified with the real Photon client library (PUN 2's Photon3Unity3D 4.1.2.19 and its LoadBalancingClient, in a local test
harness, not committed): fixed-region and region-ping connects, encrypted name-server auth, master → game, actor 1 / master
client, player and room properties, events with object arrays and a 5,000-byte fragmented payload, and "Others" events dropped.
The 2024 game's own copy of the library is newer and untested against this server.

PhotonServerSettings in the 2024 build: Realtime AppId `9372aa8d-d3f4-44a0-986d-419e145a2b83`, Chat/Voice AppId
`e93ae440-f238-4b6c-848f-1df89faf14f5`, UseNameServer, FixedRegion `usw`, UDP. The client may also take AppIds and region
from `match.rec.net/player/connection-info` (boot layer); the stand-in accepts any AppId and region.
