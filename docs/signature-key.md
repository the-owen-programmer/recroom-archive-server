# Rec Room 2024-08-20: content-signature public key (recovered 2026-10-13)

**Result:** the RSA public key the client uses for `verification.sig` / `cdn.rec.net/sigs/<guid>` file checks.
- Modulus: 2048-bit, big-endian hex in [rr-signing-modulus.hex](rr-signing-modulus.hex) (starts `c4010a31...`, ends `...4a923`). Exponent 65537.
- **Verified:** all 9,877 signatures in the shipped `RecRoom_Data/verification.sig` decrypt (`sig^e mod n`) to valid PKCS#1 v1.5 padding with a SHA-256 `DigestInfo` (`3031300d060960864801650304020105000420 || hash`).
- **Not yet known:** what exactly is hashed. SHA-256 of the raw file, with/without the path (several orderings/encodings), does not reproduce the digest for `Plugins\Steamworks.NET.txt`. Needs the code in `DFLPDGILBGO.PNGODJMAFFD` (VA `0x180a4a590`) read for how the hashed buffer is built.

## UPDATE: the key IS plain data in global-metadata.dat, so swapping it is a data patch
- In the shipped (and patched) `RecRoom_Data/il2cpp_data/Metadata/global-metadata.dat` the 256 modulus bytes sit unobfuscated at **file offset 18057508** (exactly once; field-default blob). The repaired copy has them at 20446852.
- The earlier static scans "found nothing" because of **a bug in my check**: for `00 01 ff` padding the test must be `m >> 2024 == 0x0001ff`; I used `m >> 2032 == 0x01ff`. The same bug made `dumpkey.py` print "not found". (Fixed lesson: sanity-test any scanner against a known positive first.)
- `tools/patch/swap_signing_key.py <patched build dir>` overwrites those bytes with our own modulus (`certs/content-sign-modulus.hex`; private key `certs/content-sign-private.pem` is gitignored). It refuses on a hardlinked file and writes via temp+rename. **Careful:** `builds/2024-08-20-patched` shares most files with `builds/2024-08-20` by hardlink; an in-place write to a shared file changes the pristine build too (this happened once with the metadata file and was restored byte-for-byte against `builds/2024-08-20.zip`). Only `GameAssembly.dll`, `steam_api64.dll` and now `global-metadata.dat` are real copies in the patched dir.
- Log shows the call shape: `DFLPDGILBGO.PNGODJMAFFD(string name, byte[] data, byte[] sig, byte[] modulus, byte[] exponent)`, reached from `CHCODGBONPB(string[] files, modulus, exponent, out Dictionary<string,byte[]>)` via the `/sigs/` response callback `HIDAPGOBHOF.EFFAGBCKNNF(serverResponse)`; the failing name is `verification.sig`.
## Signature file format (read from the client code, verified on the shipped verification.sig)
`/sigs/<guid>` and `RecRoom_Data/verification.sig` share one format, parsed by `DFLPDGILBGO.CHCODGBONPB(lines, modulus, exponent, out dict)`:
- Text lines (the shipped file uses CRLF). Every line except the last is `name|base64(signature)`.
- The LAST line is `base64(RSA PKCS#1 v1.5 SHA-256 signature)` over the **concatenation of all previous lines, no separator** (UTF-8). Checked against the shipped file with the recovered modulus. It is verified under the name `verification.sig`; a mismatch is the "Error validating file verification.sig" failure.
- `HIDAPGOBHOF.EFFAGBCKNNF` (response callback) then checks only local files whose names appear in the response, via `PNGODJMAFFD(name, data, sig, modulus, exponent)`. How `data` is derived from a file is still unknown (plain SHA-256 of the raw file did not match the shipped per-line signatures).
- Simplest accepted response: one line, base64(sign("")). `RecServer/Controllers/SigsController.cs` does this (plus optional `data/sigs/<guid>.lines`) with `certs/content-sign-private.pem`.
- The old placeholder override `data/overrides/cdn.rec.net/sigs/<guid>.json` (fabricated bytes) was moved to `data/overrides-disabled/` because overrides win over controllers.

## RESULT (2026-10-13): signature check passes
With the patched metadata (our modulus) and SigsController, the client no longer reports "Signatures don't match" and continues past `cdn.rec.net/sigs/`: config/v2, avatar items, rooms lookup for Orientation (room 9000000000000000113). Next blocker is the world layer: `POST match.rec.net/matchmake/none` -> 404, then "Error during initial room join for -1".

## How it was found (what worked / what didn't)
- Static scans of `global-metadata.dat` and `GameAssembly.dll` for a raw 256-byte modulus (both byte orders, PKCS and PSS tests): **nothing**. The data is not stored raw.
- The static constructor of the key class is at VA `0x1808a62f0`: `+0x58 = new byte[0x100]` filled by `InitializeArray` (modulus), `+0x60 = byte[3]{01,00,01}` (exponent). The field data lives in `<PrivateImplementationDetails>` (no field entries in metadata).
- GDB on the Wine process: breakpoints on `mmap`/`mprotect` stayed pending and never fired. Abandoned.
- **Worked:** `tools/re/keydebug/dumpkey.py` (run via `dump-netns.sh`, **not headless**) launches the game as a child, waits for the `/sigs/` request, waits `DUMP_DELAY` (1 s, so the key class's cctor has run), freezes the process and scans its memory for `byte[256]` objects (length `0x100` at +0x18). 337 candidates; one decrypted all signatures. (The script's own PKCS check printed "not found" because of a too-strict multi-signature test, so it was identified offline from `out/cands.txt`.)
- Tools added: `tools/re/find_xrefs.py`, `find_callers.py`, `find_static_stores.py`, `keydebug/scan_modulus2.py`.

## Next
1. Read `PNGODJMAFFD` to learn the signed message format.
2. Generate our own keypair, patch the modulus bytes in memory/at the constructor path (the array is built by `InitializeArray` from `<PrivateImplementationDetails>` data, so the patch point is that data or a hook after the cctor), have the server sign served content.
