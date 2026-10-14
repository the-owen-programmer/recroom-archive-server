import gdb

TARGET = 0x180a49fe0
OUT = "/tmp/claude-1000/-home-owen-Documents-recroom/1dc3b540-7028-41ba-af93-439db1d6e61b/scratchpad/arg_%s.bin"
waiting = {'done': False}

def mapped(addr):
    try:
        gdb.selected_inferior().read_memory(addr, 1)
        return True
    except gdb.MemoryError:
        return False

def dump_key(frame):
    inf = gdb.selected_inferior()
    def reg(name): return int(frame.read_register(name))
    rcx, rdx, r8, r9, rsp = reg('rcx'), reg('rdx'), reg('r8'), reg('r9'), reg('rsp')
    a5 = int.from_bytes(bytes(inf.read_memory(rsp + 0x28, 8)), 'little')
    a6 = int.from_bytes(bytes(inf.read_memory(rsp + 0x30, 8)), 'little')
    print("HIT_OAKMACJKNBK rcx=0x%x rdx=0x%x r8=0x%x r9=0x%x a5=0x%x a6=0x%x" % (rcx, rdx, r8, r9, a5, a6))
    for tag, ptr in [('rcx', rcx), ('rdx', rdx), ('r8', r8), ('r9', r9), ('a5', a5)]:
        if ptr == 0:
            print(tag, "NULL"); continue
        try:
            ln = int.from_bytes(bytes(inf.read_memory(ptr + 0x18, 4)), 'little')
            data = bytes(inf.read_memory(ptr + 0x20, min(max(ln, 0), 512) or 1))
            print(tag, "len=%d" % ln, data.hex())
            open(OUT % tag, 'wb').write(data)
        except gdb.MemoryError as e:
            print(tag, "unreadable", e)

def on_stop(event):
    try:
        frame = gdb.selected_frame()
        pc = int(frame.pc())
    except Exception as e:
        print("no frame:", e)
        return
    if pc == TARGET:
        dump_key(frame)
        gdb.execute("continue")
        return
    if not waiting['done'] and mapped(TARGET):
        waiting['done'] = True
        print("TARGET_NOW_MAPPED")
        for bp in gdb.breakpoints():
            if bp.location in ("mmap", "mprotect"):
                bp.delete()
        gdb.execute("break *0x%x" % TARGET)
    gdb.execute("continue")

gdb.execute("set pagination off")
gdb.execute("set confirm off")
gdb.execute("set breakpoint pending on")
gdb.execute("handle SIGSEGV nostop noprint pass")
gdb.execute("handle SIGILL nostop noprint pass")
gdb.execute("break mmap")
gdb.execute("break mprotect")
gdb.events.stop.connect(on_stop)
print("WAITING_FOR_MAP")
gdb.execute("run")
print("RUN_RETURNED pid=%s" % (gdb.selected_inferior().pid if gdb.selected_inferior().is_valid() else 0))
