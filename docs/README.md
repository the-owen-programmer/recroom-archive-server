# RecServer: local fake backend for the 2024-08-20 build

Code: `RecServer/` (ASP.NET, net10.0). Runner: `run-in-netns.sh [seconds] [wineprefix]` (private user+net namespace, wildcard DNS to 127.0.0.1, per-host certificates from a local test CA, port 443). Env: `HEADLESS=1` (Xvfb), `REC_LOGLEVEL=Debug` (TLS alerts), `BUILD=` (game dir). Logs in `logs/` (`requests.jsonl`, `dns.log`, `server.out`, `game-wine.out`); game log is the Wine prefix's `Player.log`. Setup: `wineboot -u` into `work/wineprefix`, then `tools/trust_ca_in_wine.py <prefix> certs/ca.pem`. Needs the ASP.NET runtime in `server/.dotnet/root` or system dotnet.

Scope: local, offline archival only. This server is research scaffolding; the end goal is a mod-loader mod (BepInEx IL2CPP / MelonLoader) that answers the client's requests inside the game process (see 05).

## Status (2026-10-13)
- Swagger crash (two controllers mapped `GET /` on different hosts) fixed with `ResolveConflictingActions`; `docs/openapi.json` is written again at startup.
- Fresh Wine prefix at `work/wineprefix` (the old one was gone), CA trusted in it.
- Answered OK (200/204): Statsig, RudderStack, Unity cloud (config/cdp/perf), `www.google.com/generate_204` (connectivity check).
- **`HEADLESS=1` hangs the game** (main thread blocked right after `MClusterRenderer Create`, before any network step). Run on the real display.
- **`ns.rec.net` is the blocker, and it is the game's own TLS check, not our server.** The client resolves it, completes TCP, then sends a TLS `user_canceled` alert (OpenSSL `0A000442`) right after our certificate; no HTTP request is ever sent. Same CA works for every third-party host, so the client has a RecNet-specific certificate verifier. Game log: `RecNet name server query failed (error code: summer): user_canceled(90)`.
- Where that verifier is (Android build, ARM64): `RecNet.TiltCheck` nested class `IGRFYTHIIHZ` implements `Org.BouncyCastle.Crypto.Tls.ICertificateVerifyer`, `IsValid(Uri, X509CertificateStructure[])` at `0x179b05e0` in `libil2cpp.so`. The game uses BestHTTP with BouncyCastle for RecNet, so it ignores the Windows/Wine certificate store. (First look at the code: a static delegate check, then a loop over the chain calling `0x179afb18` / `0x179af644`; the pinned value is not yet read.) The Windows build has the same class under another obfuscated name.

## Next
1. Neutralise that verifier in the 2024 build. Preferred: load MelonLoader (Rec-Room-Patches already does this for these builds) or BepInEx IL2CPP and patch `IsValid` to return true. Check first that the scrambled-header metadata of the 2024 build works with Il2CppInterop.
2. Then read `requests.jsonl` for the real `ns.rec.net` path and fix `NameServerResponse`, and follow the chain (auth, config, accounts, rooms, Photon) one failure at a time.
3. Port what the server learns into the in-process mod.

## Mod loader attempt: MelonLoader 0.7.3 on the 2024 build (2026-10-13)
- Setup: `builds/2024-08-20-mod/` = hardlinked copy of `builds/2024-08-20/` plus MelonLoader x64 (`version.dll` proxy; run with `WINEDLLOVERRIDES="version=n,b;mscoree,mshtml=d"`). Original build untouched. Scratch: `work/melonloader/`, `work/melon_boot*.out`.
- Result: **works up to IL2CPP init, then fails.** Under Wine, MelonLoader downloads/installs its own .NET 6 runtime, starts, hooks `il2cpp_init` / `il2cpp_runtime_invoke`, loads 0 plugins, then the game shows "failed to initialize il2cpp" and never starts (Unity `Player.log` stays empty). Likely cause: the scrambled `global-metadata.dat` / patched IL2CPP loader of this build (the loader's Cpp2IL/Il2CppInterop step expects standard metadata). Not confirmed.
- Common prerequisite for both routes (loader and static patch of `IsValid`): understand the 2024 metadata layout. `tools/unscramble_metadata.py` is Android-only (hardcoded section order; fails its count check on the 2024 file).

## 2024 metadata layout solved (2026-10-13)
`tools/re/unscramble_metadata_2024.py` rebuilds a standard v29 `global-metadata.dat` header for the 20 Aug 2024 build (29 sections chained from byte 256; section order derived by content; method defs are 32 bytes). Il2CppDumper 6.7.46 accepts the result ("Metadata Version: 29", 49,794 type definitions). Status: **work in progress, dumping not finished** - the dumper cannot auto-find the registration tables in this stripped `GameAssembly.dll`, so `tools/re/pe_findreg.py` finds them by hand: MetadataRegistration at VA `0x189dd3ad0` (types 152,820). CodeRegistration still to locate (the 355 code-gen module pointers follow the typeDefinitionsSizes array).

## IsValid located in the 2024 build (2026-10-13)
`tools/re/find_isvalid_2024.py` (needs the repaired metadata) lists the classes implementing `ICertificateVerifyer` with the address of `IsValid` (from the code-gen module method-pointer tables; MetadataRegistration `0x189dd3ad0`, module array `0x18a793820`; Rec Room's CodeRegistration is in a non-stock layout so stock dumpers fail). Three implementations, all in `RecNet.Runtime.dll`: `AlwaysValidVerifyer` (VA `0x18086c5d0`, already `mov al,1; ret`), `TimeValidVerifyer` (`0x1861732f0`) and the obfuscated nested `PAFJKEDJMAI.LDHLOLGOKJC` (VA `0x186736270`, file offset `0x6735270`), the TiltCheck equivalent. Next: patch the last one to `b0 01 c3` in a copy of GameAssembly.dll and rerun the netns test.
