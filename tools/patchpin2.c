// Waits for RefereeClientApp.exe processes and replaces the pinned public-key hash string in their memory.
// usage: patchpin.exe <old44> <new44> [seconds]
#include <windows.h>
#include <tlhelp32.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
static int patch(HANDLE h, const char *oldp, const char *newp, size_t n) {
    MEMORY_BASIC_INFORMATION m; unsigned char *a = 0; int hits = 0;
    while (VirtualQueryEx(h, a, &m, sizeof m) == sizeof m) {
        if (m.State == MEM_COMMIT && !(m.Protect & (PAGE_NOACCESS | PAGE_GUARD)) && m.RegionSize <= 0x4000000) {
            unsigned char *b = malloc(m.RegionSize); SIZE_T got;
            if (ReadProcessMemory(h, m.BaseAddress, b, m.RegionSize, &got) && got >= n) {
                for (SIZE_T i = 0; i + n <= got; i++)
                    if (b[i] == (unsigned char)oldp[0] && !memcmp(b + i, oldp, n)) {
                        void *dst = (char *)m.BaseAddress + i; DWORD op; SIZE_T w;
                        VirtualProtectEx(h, dst, n, PAGE_READWRITE, &op);
                        if (WriteProcessMemory(h, dst, newp, n, &w)) { hits++; printf("  hit type=%lx base=%p off=%zx\n", (unsigned long)m.Type, m.BaseAddress, (size_t)i); fflush(stdout); }
                        VirtualProtectEx(h, dst, n, op, &op);
                    }
            }
            free(b);
        }
        a = (unsigned char *)m.BaseAddress + m.RegionSize;
    }
    return hits;
}
int main(int argc, char **argv) {
    if (argc < 3 || strlen(argv[1]) != strlen(argv[2])) { puts("need two equal-length strings"); return 1; }
    size_t n = strlen(argv[1]); int secs = argc > 3 ? atoi(argv[3]) : 60;
    DWORD done[256]; int ndone = 0; DWORD t0 = GetTickCount();
    while (GetTickCount() - t0 < (DWORD)secs * 1000) {
        HANDLE sn = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0); PROCESSENTRY32 pe = {sizeof pe};
        if (Process32First(sn, &pe)) do if (!_stricmp(pe.szExeFile, "RefereeClientApp.exe")) {
            int seen = 0; for (int i = 0; i < ndone; i++) if (done[i] == pe.th32ProcessID) seen = 1;
            if (seen) continue;
            HANDLE h = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_VM_OPERATION, 0, pe.th32ProcessID);
            if (!h) { static int w; if (!w++) printf("OpenProcess failed %lu for pid %lu\n", GetLastError(), pe.th32ProcessID); continue; }
            static DWORD lastpid, tries; if (lastpid != pe.th32ProcessID) { lastpid = pe.th32ProcessID; tries = 0; printf("saw service pid %lu\n", lastpid); fflush(stdout); }
            int hits = patch(h, argv[1], argv[2], n); tries++;
            if (!hits && tries % 200 == 0) { printf("pid %lu: %lu scans, no match yet\n", lastpid, tries); fflush(stdout); }
            if (hits) { printf("pid %lu: patched %d occurrence(s)\n", pe.th32ProcessID, hits); fflush(stdout);  }
            CloseHandle(h);
        } while (Process32Next(sn, &pe));
        CloseHandle(sn); Sleep(1);
    }
    return 0;
}
