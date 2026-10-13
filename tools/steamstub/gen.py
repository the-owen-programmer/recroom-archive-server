#!/usr/bin/env python3
"""Generate steam_api64_stub.c: a stand-in for Valve's steam_api64.dll for OFFLINE single-player archival use.
It exports every function of the real DLL (exports.txt). Init succeeds, interface accessors return a dummy object, the user
has a fixed fake SteamID and a fake auth ticket (the local server accepts any ticket), everything else returns 0.
usage: gen.py > steam_api64_stub.c ; x86_64-w64-mingw32-gcc -shared -O1 -o steam_api64.dll steam_api64_stub.c -static-libgcc"""
import re
names = [l.strip() for l in open(__file__.rsplit('/', 1)[0] + '/exports.txt') if l.strip()]
SPECIAL = {
    'SteamAPI_Init': 'return 1;', 'SteamAPI_InitSafe': 'return 1;', 'SteamAPI_InitAnonymousUser': 'return 1;',
    'SteamAPI_IsSteamRunning': 'return 1;', 'SteamAPI_RestartAppIfNecessary': 'return 0;',
    'SteamAPI_GetHSteamPipe': 'return 1;', 'SteamAPI_GetHSteamUser': 'return 1;', 'GetHSteamPipe': 'return 1;', 'GetHSteamUser': 'return 1;',
    'SteamAPI_ISteamUser_BLoggedOn': 'return 1;',
    'SteamAPI_ISteamUser_GetSteamID': 'return FAKE_STEAMID;',
    'SteamAPI_ISteamApps_BIsSubscribed': 'return 1;', 'SteamAPI_ISteamApps_BIsSubscribedApp': 'return 1;',
    'SteamAPI_ISteamUtils_GetAppID': 'return APPID;',
    'SteamAPI_ISteamUtils_IsAPICallCompleted': 'return 0;',
    'SteamAPI_ISteamFriends_GetPersonaName': 'return (uint64_t)"Player";',
    'SteamAPI_ISteamFriends_GetFriendPersonaName': 'return (uint64_t)"Player";',
    'SteamAPI_ISteamUtils_GetSteamUILanguage': 'return (uint64_t)"english";',
    'SteamAPI_ISteamApps_GetCurrentGameLanguage': 'return (uint64_t)"english";',
    'SteamAPI_ISteamUtils_GetIPCountry': 'return (uint64_t)"US";',
    'SteamAPI_ISteamUser_GetAuthSessionTicket':
        'unsigned char*t=(unsigned char*)a1; uint32_t*n=(uint32_t*)a3; for(int i=0;i<32;i++) if(t&&i<(int)a2) t[i]=(unsigned char)(0x40+i); if(n) *n=(a2<32?a2:32); return 1;',
}
def body(n):
    if n in SPECIAL: return SPECIAL[n]
    if re.match(r'^SteamAPI_Steam\w+_v\d+$', n) or re.match(r'^SteamAPI_ISteamClient_GetISteam', n) or n.startswith('SteamInternal_') or n in ('SteamClient',) or n.startswith('SteamGameServer_'):
        return 'return (uint64_t)DUMMY;'
    return 'return 0;'
print('#include <stdint.h>\n#define FAKE_STEAMID 76561198000000001ULL\n#define APPID 471710\nstatic char DUMMY[8192];')
for n in names:
    print(f'__declspec(dllexport) uint64_t {n}(uint64_t a0, uint64_t a1, uint64_t a2, uint64_t a3) {{ (void)a0;(void)a1;(void)a2;(void)a3; {body(n)} }}'.replace('a1)','a1)'))
