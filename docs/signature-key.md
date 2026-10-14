# Rec Room 2024-08-20: content-signature public key (recovered 2026-10-13)

**Result:** the RSA public key the client uses for `verification.sig` / `cdn.rec.net/sigs/<guid>` file checks.
- Modulus: 2048-bit, big-endian hex in [rr-signing-modulus.hex](rr-signing-modulus.hex) (starts `c4010a31...`, ends `...4a923`). Exponent 65537.
- **Verified:** all 9,877 signatures in the shipped `RecRoom_Data/verification.sig` decrypt (`sig^e mod n`) to valid PKCS#1 v1.5 padding with a SHA-256 `DigestInfo` (`3031300d060960864801650304020105000420 || hash`).
- **Not yet known:** what exactly is hashed. SHA-256 of the raw file, with/without the path (several orderings/encodings), does not reproduce the digest for `Plugins\Steamworks.NET.txt`. Needs the code in `DFLPDGILBGO.PNGODJMAFFD` (VA `0x180a4a590`) read for how the hashed buffer is built.

## How it was found (what worked / what didn't)
- Static scans of `global-metadata.dat` and `GameAssembly.dll` for a raw 256-byte modulus (both byte orders, PKCS and PSS tests): **nothing**. The data is not stored raw.
- The static constructor of the key class is at VA `0x1808a62f0`: `+0x58 = new byte[0x100]` filled by `InitializeArray` (modulus), `+0x60 = byte[3]{01,00,01}` (exponent). The field data lives in `<PrivateImplementationDetails>` (no field entries in metadata).
- GDB on the Wine process: breakpoints on `mmap`/`mprotect` stayed pending and never fired. Abandoned.
- **Worked:** `tools/re/keydebug/dumpkey.py` (run via `dump-netns.sh`, **not headless**) launches the game as a child, waits for the `/sigs/` request, waits `DUMP_DELAY` (1 s, so the key class's cctor has run), freezes the process and scans its memory for `byte[256]` objects (length `0x100` at +0x18). 337 candidates; one decrypted all signatures. (The script's own PKCS check printed "not found" because of a too-strict multi-signature test, so it was identified offline from `out/cands.txt`.)
- Tools added: `tools/re/find_xrefs.py`, `find_callers.py`, `find_static_stores.py`, `keydebug/scan_modulus2.py`.

## Next
1. Read `PNGODJMAFFD` to learn the signed message format.
2. Generate our own keypair, patch the modulus bytes in memory/at the constructor path (the array is built by `InitializeArray` from `<PrivateImplementationDetails>` data, so the patch point is that data or a hook after the cctor), have the server sign served content.
